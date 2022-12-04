﻿// ------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using NuGetPush.Helpers;
using NuGetPush.Models;
using NuGetPush.Processes;
using NuGetPush.WinForms.Enums;
using NuGetPush.WinForms.Extensions;
using NuGetPush.WinForms.Forms;

namespace NuGetPush.WinForms
{
    internal static class Program
    {
        private static Solution? _solution;

        private static MainForm _form;

        [STAThread]
        private static void Main(string[] args)
        {
            PackageSourceStoreProvider.PackageSourceStore = new PackageSourceStore(true);

            _form = new MainForm();

            _form.OpenCloseSolutionButton.Click += async (s, e) => await OnClickOpenCloseSolutionAsync();

            _form.PackAllButton.Click += async (s, e) => await OnClickPackAllAsync();
            _form.PushAllButton.Click += async (s, e) => await OnClickPushAllAsync();
            _form.PackAndPushAllButton.Click += async (s, e) => await OnClickPackAndPushAllAsync();

            _form.ProjectListView.SelectedIndexChanged += (s, e) => UpdateDiagnosticsDisplay();

            _form.ShowDialog();
        }

        private static async Task OnClickOpenCloseSolutionAsync()
        {
            _form.OpenCloseSolutionButton.Enabled = false;

            if (_solution is null)
            {
                await OpenSolutionAsync();
            }
            else
            {
                CloseSolution();
            }

            _form.OpenCloseSolutionButton.Enabled = true;
        }

        internal static async Task OnClickPackSelectedAsync()
        {
            StartWork();

            var selectedProjects = GetSelectedProjects();
            var handledProjects = await PackProjectsAsync(selectedProjects, true);

            await FinishWorkAsync("Projects have been packed.", handledProjects);
        }

        private static async Task OnClickPackAllAsync()
        {
            StartWork();

            var handledProjects = await PackProjectsAsync(_solution.Projects, false);

            await FinishWorkAsync("Projects have been packed.", handledProjects);
        }

        internal static async Task OnClickPushSelectedAsync()
        {
            StartWork();

            var selectedProjects = GetSelectedProjects();
            var handledProjects = await PushProjectsAsync(selectedProjects, selectedProjects.Count == 1);

            await FinishWorkAsync("Projects have been pushed.", handledProjects);
        }

        private static async Task OnClickPushAllAsync()
        {
            StartWork();

            var handledProjects = await PushProjectsAsync(_solution.Projects, false);

            await FinishWorkAsync("Projects have been pushed.", handledProjects);
        }

        internal static async Task OnClickPackAndPushSelectedAsync()
        {
            StartWork();

            var selectedProjects = GetSelectedProjects();
            var handledProjects = await PackProjectsAsync(selectedProjects, true);

            await PushProjectsAsync(handledProjects, selectedProjects.Count == 1);

            await FinishWorkAsync("Projects have been packed and pushed.", handledProjects);
        }

        private static async Task OnClickPackAndPushAllAsync()
        {
            StartWork();

            var handledProjects = await PackProjectsAsync(_solution.Projects, false);

            await PushProjectsAsync(handledProjects, false);

            await FinishWorkAsync("Projects have been packed and pushed.", handledProjects);
        }

        private static List<ClassLibrary> GetSelectedProjects()
        {
            var result = new List<ClassLibrary>();
            for (var i = 0; i < _form.ProjectListView.SelectedIndices.Count; i++)
            {
                var index = _form.ProjectListView.SelectedIndices[i];
                var item = _form.ProjectListView.Items[index];
                var tag = item.GetTag();

                result.Add(tag.ClassLibrary);
            }

            return result;
        }

        private static async Task<HashSet<ClassLibrary>> PackProjectsAsync(IEnumerable<ClassLibrary> projectsToPack, bool force)
        {
            var result = new HashSet<ClassLibrary>();

            var uncommittedChanges = await Git.CheckUncommittedChangesAsync(_solution.RepositoryRoot);

            foreach (var project in projectsToPack)
            {
                if (project.CanPack(uncommittedChanges, force))
                {
                    result.Add(project);
                }
            }

            var idleProjects = new HashSet<ClassLibrary>(result);
            foreach (ListViewItem item in _form.ProjectListView.Items)
            {
                var tag = item.GetTag();
                if (idleProjects.Contains(tag.ClassLibrary))
                {
                    item.Update(ProjectStatus.Idle);
                }
            }

            while (idleProjects.Any())
            {
                var packableProjects = new HashSet<ClassLibrary>();
                foreach (var project in idleProjects)
                {
                    if (project.Dependencies.All(dependency => !idleProjects.Contains(dependency) || dependency.PackageVersion == dependency.KnownLatestLocalVersion || dependency.PackageVersion == dependency.KnownLatestNuGetVersion))
                    {
                        packableProjects.Add(project);
                    }
                }

                idleProjects.ExceptWith(packableProjects);
                foreach (ListViewItem item in _form.ProjectListView.Items)
                {
                    var tag = item.GetTag();
                    if (packableProjects.Contains(tag.ClassLibrary))
                    {
                        item.Update(ProjectStatus.Working);
                    }
                }

                var packProjectsResult = await Utils.PackProjectsAsync(packableProjects);
                var projectBuildsSucceeded = packProjectsResult.Succeeded;
                var projectBuildsFailed = packProjectsResult.Failed;

                foreach (var project in projectBuildsSucceeded)
                {
                    var anyUploadSucceeded = false;
                    foreach (var packageSource in project.PackageSources)
                    {
                        if (packageSource is LocalPackageSource localPackageSource)
                        {
                            if (!PackageSourceStoreProvider.PackageSourceStore.GetIsPackageSourceEnabled(packageSource))
                            {
                                continue;
                            }

                            var uploadSucceeded = await localPackageSource.UploadPackageAsync();
                            if (uploadSucceeded)
                            {
                                anyUploadSucceeded = true;
                            }
                        }
                    }

                    if (anyUploadSucceeded)
                    {
                        if (project.KnownLatestLocalVersion is null || project.PackageVersion > project.KnownLatestLocalVersion)
                        {
                            project.KnownLatestLocalVersion = project.PackageVersion;
                        }
                    }
                }

                var projectBuildsDependenciesError = new HashSet<ClassLibrary>();
                foreach (var project in projectBuildsFailed)
                {
                    void AddDependeesToDependencyErrorProjectsList(ClassLibrary classLibrary)
                    {
                        foreach (var dependee in classLibrary.Dependees)
                        {
                            idleProjects.Remove(dependee);
                            projectBuildsDependenciesError.Add(dependee);

                            AddDependeesToDependencyErrorProjectsList(dependee);
                        }
                    }

                    AddDependeesToDependencyErrorProjectsList(project);
                }

                foreach (ListViewItem item in _form.ProjectListView.Items)
                {
                    var tag = item.GetTag();
                    if (packableProjects.Contains(tag.ClassLibrary))
                    {
                        item.Update(projectBuildsFailed.Contains(tag.ClassLibrary) ? ProjectStatus.PackError : ProjectStatus.Packed);
                    }
                    else if (projectBuildsDependenciesError.Contains(tag.ClassLibrary))
                    {
                        item.Update(ProjectStatus.DependencyError);
                    }
                }
            }

            return result;
        }

        /// <param name="force">
        /// If <see langword="true"/>, projects that don't exist yet on the remote package source can be pushed as well.
        /// </param>
        private static async Task<HashSet<ClassLibrary>> PushProjectsAsync(IEnumerable<ClassLibrary> projectsToPush, bool force)
        {
            var result = projectsToPush.Where(project => project.CanPush(force)).ToHashSet();

            foreach (ListViewItem item in _form.ProjectListView.Items)
            {
                var tag = item.GetTag();
                if (result.Contains(tag.ClassLibrary))
                {
                    item.Update(ProjectStatus.Working);
                }
            }

            var testFailed = new HashSet<ClassLibrary>();
            var uploadFailed = new HashSet<ClassLibrary>();

            foreach (var project in result)
            {
                var anyUploadSucceeded = false;
                foreach (var packageSource in project.PackageSources)
                {
                    if (packageSource is RemotePackageSource remotePackageSource)
                    {
                        if (!PackageSourceStoreProvider.PackageSourceStore.GetIsPackageSourceEnabled(packageSource))
                        {
                            continue;
                        }

                        var uploadSucceeded = await remotePackageSource.UploadPackageAsync();
                        if (uploadSucceeded)
                        {
                            anyUploadSucceeded = true;
                        }
                    }
                }

                if (anyUploadSucceeded)
                {
                    if (project.KnownLatestNuGetVersion is null || project.PackageVersion > project.KnownLatestNuGetVersion)
                    {
                        project.KnownLatestNuGetVersion = project.PackageVersion;
                    }
                }
                else
                {
                    uploadFailed.Add(project);
                }
            }

            foreach (ListViewItem item in _form.ProjectListView.Items)
            {
                var tag = item.GetTag();

                if (result.Contains(tag.ClassLibrary))
                {
                    item.Update(testFailed.Contains(tag.ClassLibrary) ? ProjectStatus.TestFailed : uploadFailed.Contains(tag.ClassLibrary) ? ProjectStatus.PushError : ProjectStatus.Pushed);
                }
            }

            return result;
        }

        private static void UpdateDiagnosticsDisplay()
        {
            _form.DiagnosticsDisplay.Text = _form.ProjectListView.TryGetSelectedItemTag(out var tag)
                ? tag.ClassLibrary.PackageDescription
                : $"{_form.ProjectListView.SelectedItems.Count} projects selected.";
        }

        private static void StartWork()
        {
            _form.ProjectListView.DisableContextMenuButtons();
            _form.PackAllButton.Enabled = false;
            _form.PushAllButton.Enabled = false;
            _form.PackAndPushAllButton.Enabled = false;
        }

        private static async Task FinishWorkAsync(string message, HashSet<ClassLibrary> handledProjects)
        {
            MessageBox.Show(message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);

            foreach (ListViewItem item in _form.ProjectListView.Items)
            {
                var tag = item.GetTag();
                if (handledProjects.Contains(tag.ClassLibrary))
                {
                    item.Update(true);
                }
            }

            await _form.ProjectListView.UpdateContextMenuAsync();
        }

        private static async Task OpenSolutionAsync()
        {
            var fileInfo = new FileInfo(_form.SolutionInputTextBox.Text);
            if (fileInfo.Exists)
            {
                _form.SolutionInputTextBox.Enabled = false;
                _form.SolutionInputBrowseButton.Enabled = false;
                _form.OpenCloseSolutionButton.Text = "Close solution";

                var repositoryRoot = await Git.GetRepositoryRootAsync(fileInfo.DirectoryName);

                var uncommittedChanges = await Git.CheckUncommittedChangesAsync(repositoryRoot);

                _solution = new Solution(repositoryRoot, fileInfo.FullName);
                await _solution.ParseSolutionProjectsAsync(null, true);

                var anyCanBePacked = false;
                var anyCanBePushed = false;
                var anyCanBePackedAndPushed = false;

                foreach (var project in _solution.Projects.OrderBy(project => project.Name))
                {
                    await project.FindLatestVersionAsync();

                    var tag = new ItemTag(project);
                    _form.ProjectListView.Items.Add(ListViewItemExtensions.Create(tag));

                    var canPush = tag.ClassLibrary.CanPush(false);

                    if (project.CanPack(uncommittedChanges, false))
                    {
                        anyCanBePacked = true;
                        if (canPush)
                        {
                            anyCanBePackedAndPushed = true;
                        }
                    }

                    if (canPush)
                    {
                        anyCanBePushed = true;
                    }
                }

                _form.ProjectListView.LoadSolution(_solution);

                _form.PackAllButton.Enabled = anyCanBePacked;
                _form.PushAllButton.Enabled = anyCanBePushed;
                _form.PackAndPushAllButton.Enabled = anyCanBePackedAndPushed;
            }
        }

        private static void CloseSolution()
        {
            _solution = null;

            _form.SolutionInputTextBox.Enabled = true;
            _form.SolutionInputBrowseButton.Enabled = true;

            _form.OpenCloseSolutionButton.Text = "Open solution";
            _form.PackAllButton.Enabled = false;
            _form.PushAllButton.Enabled = false;
            _form.PackAndPushAllButton.Enabled = false;

            _form.ProjectListView.UnloadSolution();

            _form.DiagnosticsDisplay.Text = string.Empty;

            PackageSourceStoreProvider.PackageSourceStore.ResetPackageSourcesEnabled();
        }
    }
}