// ------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using NuGet.Configuration;

using NuGetPush.Extensions;
using NuGetPush.Helpers;
using NuGetPush.Models;
using NuGetPush.Processes;
using NuGetPush.WinForms.Enums;
using NuGetPush.WinForms.Extensions;
using NuGetPush.WinForms.Forms;
using NuGetPush.WinForms.Models;

namespace NuGetPush.WinForms
{
    internal static class Program
    {
        private static Solution? _solution;

        private static MainForm _form;
        private static CancellationTokenSource? _cancellationTokenSource;

        [STAThread]
        private static void Main(string[] args)
        {
            PackageSourceStoreProvider.PackageSourceStore = new PackageSourceStore(true);

            _form = new MainForm();

            _form.OpenCloseSolutionButton.Click += async (s, e) => await OnClickOpenCloseSolutionAsync();

            _form.PackAllButton.Click += async (s, e) => await OnClickPackAllAsync();
            _form.PushAllButton.Click += async (s, e) => await OnClickPushAllAsync();
            _form.PackAndPushAllButton.Click += async (s, e) => await OnClickPackAndPushAllAsync();
            _form.CancelWorkButton.Click += OnClickCancel;

            _form.ProjectListView.SelectedIndexChanged += (s, e) => UpdateDiagnosticsDisplay();

            _form.ShowDialog();
        }

        private static async Task OnClickOpenCloseSolutionAsync()
        {
            _form.OpenCloseSolutionButton.Enabled = false;

            if (_solution is not null)
            {
                CloseSolution();

                return;
            }

            await OpenSolutionAsync();
        }

        internal static async Task OnClickPackSelectedAsync()
        {
            var cancellationToken = StartWork();
            try
            {
                var selectedProjects = GetSelectedProjects();
                var handledProjects = await PackProjectsAsync(selectedProjects, true, cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                await FinishWorkAsync("Projects have been packed.", cancellationToken);
            }
        }

        private static async Task OnClickPackAllAsync()
        {
            var cancellationToken = StartWork();
            try
            {
                var handledProjects = await PackProjectsAsync(_solution.Projects, false, cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                await FinishWorkAsync("Projects have been packed.", cancellationToken);
            }
        }

        internal static async Task OnClickPushSelectedAsync()
        {
            var cancellationToken = StartWork();
            try
            {
                var selectedProjects = GetSelectedProjects();
                var handledProjects = await PushProjectsAsync(selectedProjects, selectedProjects.Count == 1, cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                await FinishWorkAsync("Projects have been pushed.", cancellationToken);
            }
        }

        private static async Task OnClickPushAllAsync()
        {
            var cancellationToken = StartWork();
            try
            {
                var handledProjects = await PushProjectsAsync(_solution.Projects, false, cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                await FinishWorkAsync("Projects have been pushed.", cancellationToken);
            }
        }

        internal static async Task OnClickPackAndPushSelectedAsync()
        {
            var cancellationToken = StartWork();
            try
            {
                var selectedProjects = GetSelectedProjects();
                var handledProjects = await PackProjectsAsync(selectedProjects, true, cancellationToken);

                await PushProjectsAsync(handledProjects, selectedProjects.Count == 1, cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                await FinishWorkAsync("Projects have been packed and pushed.", cancellationToken);
            }
        }

        private static async Task OnClickPackAndPushAllAsync()
        {
            var cancellationToken = StartWork();
            try
            {
                var handledProjects = await PackProjectsAsync(_solution.Projects, false, cancellationToken);

                await PushProjectsAsync(handledProjects, false, cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                await FinishWorkAsync("Projects have been packed and pushed.", cancellationToken);
            }
        }

        private static void OnClickCancel(object? sender, EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
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

        private static async Task<HashSet<ClassLibrary>> PackProjectsAsync(IEnumerable<ClassLibrary> projectsToPack, bool force, CancellationToken cancellationToken)
        {
            var result = new HashSet<ClassLibrary>();

            var uncommittedChanges = await Git.CheckUncommittedChangesAsync(_solution.RepositoryRoot, cancellationToken);

            foreach (var project in projectsToPack)
            {
                if (project.CanPack(uncommittedChanges, force))
                {
                    project.Diagnostics.Clear();

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
                    if (project.Dependencies.All(dependency => !idleProjects.Contains(dependency) || dependency.PackageVersion == dependency.KnownLatestLocalVersion || dependency.PackageVersion == dependency.KnownLatestRemoteVersion))
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

                _form.ProjectListView.Sort();

                var packProjectsResult = await Utils.PackProjectsAsync(packableProjects, cancellationToken);
                var projectBuildsSucceeded = packProjectsResult.Succeeded;
                var projectBuildsFailed = packProjectsResult.Failed;

                foreach (var project in projectBuildsSucceeded)
                {
                    var uploadSucceeded = await project.LocalPackageSource.UploadPackageAsync(project, null, cancellationToken);
                    if (uploadSucceeded)
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

                _form.ProjectListView.Sort();
            }

            return result;
        }

        /// <param name="force">
        /// If <see langword="true"/>, projects that don't exist yet on the remote package feed can be pushed as well.
        /// </param>
        private static async Task<HashSet<ClassLibrary>> PushProjectsAsync(IEnumerable<ClassLibrary> projectsToPush, bool force, CancellationToken cancellationToken)
        {
            var result = new HashSet<ClassLibrary>();

            foreach (var project in projectsToPush)
            {
                if (project.CanPush(force))
                {
                    project.Diagnostics.Clear();

                    result.Add(project);
                }
            }

            foreach (ListViewItem item in _form.ProjectListView.Items)
            {
                var tag = item.GetTag();
                if (result.Contains(tag.ClassLibrary))
                {
                    item.Update(ProjectStatus.Working);
                }
            }

            _form.ProjectListView.Sort();

            var testFailed = new HashSet<ClassLibrary>();
            var uploadFailed = new HashSet<ClassLibrary>();

            foreach (var project in result)
            {
                var uploadSucceeded = project.RemotePackageSource is not null && await project.RemotePackageSource.UploadPackageAsync(project, HandleDeviceLogin, cancellationToken);
                if (uploadSucceeded)
                {
                    if (project.KnownLatestRemoteVersion is null || project.PackageVersion > project.KnownLatestRemoteVersion)
                    {
                        project.KnownLatestRemoteVersion = project.PackageVersion;
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

            _form.ProjectListView.Sort();

            return result;
        }

        private static void HandleDeviceLogin(string deviceLoginLine)
        {
            using var deviceLoginForm = new DeviceLoginForm(deviceLoginLine);

            var dialogResult = deviceLoginForm.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                _cancellationTokenSource?.Cancel();
            }
        }

        private static void UpdateDiagnosticsDisplay()
        {
            _form.DiagnosticsDisplay.Text = _form.ProjectListView.TryGetSelectedItemTag(out var tag)
                ? string.Join(Environment.NewLine + Environment.NewLine, tag.ClassLibrary.Diagnostics.Append($"Description:{Environment.NewLine}{tag.ClassLibrary.PackageDescription}"))
                : $"{_form.ProjectListView.SelectedItems.Count} projects selected.";
        }

        private static CancellationToken StartWork()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _form.ProjectListView.DisableContextMenuButtons();
            _form.OpenCloseSolutionButton.Enabled = false;
            _form.PackAllButton.Enabled = false;
            _form.PushAllButton.Enabled = false;
            _form.PackAndPushAllButton.Enabled = false;
            _form.CancelWorkButton.Enabled = true;

            return _cancellationTokenSource.Token;
        }

        private static async Task FinishWorkAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                if (_cancellationTokenSource?.IsCancellationRequested == true)
                {
                    MessageBox.Show("Canceled", string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                foreach (ListViewItem item in _form.ProjectListView.Items)
                {
                    item.Update(true);
                }

                _form.ProjectListView.Sort();

                var uncommittedChanges = await Git.CheckUncommittedChangesAsync(_solution.RepositoryRoot, cancellationToken);

                _form.ProjectListView.UpdateContextMenu(uncommittedChanges);
                UpdateWorkButtonsEnabled(uncommittedChanges);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private static async Task OpenSolutionAsync()
        {
            var fileInfo = new FileInfo(_form.SolutionInputTextBox.Text);
            if (fileInfo.Exists)
            {
                _form.SolutionInputTextBox.Enabled = false;
                _form.SolutionInputBrowseButton.Enabled = false;
                _form.OpenCloseSolutionButton.Text = "Close solution";

                var cancellationToken = StartWork();
                try
                {
                    FileInfo solutionFileInfo = fileInfo;
                    List<string>? solutionFilterProjects = null;
                    if (string.Equals(fileInfo.Extension, ".slnf", StringComparison.OrdinalIgnoreCase))
                    {
                        using var solutionFilterFileStream = fileInfo.OpenRead();

                        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
                        {
                            PropertyNameCaseInsensitive = true,
                        };

                        var solutionFilter = JsonSerializer.Deserialize<SolutionFilterFile>(solutionFilterFileStream, options);

                        solutionFileInfo = new FileInfo(Path.Combine(fileInfo.DirectoryName, solutionFilter.Solution.Path));
                        solutionFilterProjects = solutionFilter.Solution.Projects.Select(path => Path.GetFullPath(path, fileInfo.DirectoryName)).ToList();
                    }

                    var repositoryRoot = await Git.GetRepositoryRootAsync(solutionFileInfo.DirectoryName, cancellationToken);

                    var uncommittedChanges = await Git.CheckUncommittedChangesAsync(repositoryRoot, cancellationToken);

                    _solution = new Solution(repositoryRoot, solutionFileInfo.FullName);

                    using var packageSourcesForm = new PackageSourcesForm(_solution.PackageSources);

                    const string JsonFileName = "packagesources.json";
                    var jsonFileInfo = new FileInfo(JsonFileName);

                    List<SolutionPackageSources>? packageSources = null;
                    if (jsonFileInfo.Exists)
                    {
                        using var jsonFileStream = jsonFileInfo.OpenRead();
                        try
                        {
                            packageSources = JsonSerializer.Deserialize<List<SolutionPackageSources>>(jsonFileStream);
                        }
                        catch
                        {
                        }
                    }

                    packageSources ??= new List<SolutionPackageSources>();

                    var solutionFilePath = Path.GetFullPath(solutionFileInfo.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var solutionPackageSources = packageSources.FirstOrDefault(s => string.Equals(Path.GetFullPath(s.SolutionPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), solutionFilePath, StringComparison.OrdinalIgnoreCase));

                    if (solutionPackageSources is not null)
                    {
                        if (!string.IsNullOrEmpty(solutionPackageSources.LocalPackageSource))
                        {
                            packageSourcesForm.LocalPackageSource = _solution.PackageSources.FirstOrDefault(p => p.IsLocal && string.Equals(p.Source, solutionPackageSources.LocalPackageSource, StringComparison.OrdinalIgnoreCase));
                        }

                        if (!string.IsNullOrEmpty(solutionPackageSources.RemotePackageSource))
                        {
                            packageSourcesForm.RemotePackageSource = _solution.PackageSources.FirstOrDefault(p => !p.IsLocal && string.Equals(p.Source, solutionPackageSources.RemotePackageSource, StringComparison.OrdinalIgnoreCase));
                        }
                        else if (!string.IsNullOrEmpty(solutionPackageSources.LocalPackageSource))
                        {
                            packageSourcesForm.RemotePackageSource = NoPackageSource.Instance;
                        }
                    }
                    else
                    {
                        solutionPackageSources = new SolutionPackageSources
                        {
                            SolutionPath = solutionFilePath,
                        };

                        packageSources.Add(solutionPackageSources);
                    }

                    var dialogResult = packageSourcesForm.ShowDialog();
                    if (dialogResult != DialogResult.OK)
                    {
                        CloseSolution();

                        return;
                    }

                    if (packageSourcesForm.LocalPackageSource is null ||
                        packageSourcesForm.RemotePackageSource is null)
                    {
                        MessageBox.Show("You must select a local and a remote package source to use this program.", "No package sources selected", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        CloseSolution();

                        return;
                    }

                    _solution.SelectedLocalPackageSource = packageSourcesForm.LocalPackageSource;
                    _solution.SelectedRemotePackageSource = packageSourcesForm.RemotePackageSource as PackageSource;

                    solutionPackageSources.LocalPackageSource = _solution.SelectedLocalPackageSource.Source;
                    solutionPackageSources.RemotePackageSource = _solution.SelectedRemotePackageSource?.Source;

                    using (var jsonFileStream = jsonFileInfo.Create())
                    {
                        JsonSerializer.Serialize(jsonFileStream, packageSources);
                    }

                    await _solution.ParseSolutionProjectsAsync(solutionFilterProjects, true, cancellationToken);

                    _form.ProjectListView.LoadSolution(_solution);

                    foreach (ListViewItem item in _form.ProjectListView.Items)
                    {
                        var project = item.GetTag().ClassLibrary;

                        await project.FindLatestRemoteVersionAsync(true, cancellationToken);

                        item.Update(true);
                    }

                    UpdateWorkButtonsEnabled(uncommittedChanges);
                }
                catch (TaskCanceledException)
                {
                    CloseSolution();
                }
                catch (Exception e)
                {
                    MessageBox.Show($"{e.GetType().FullName}: {e.Message}", "Failed to open solution", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    CloseSolution();
                }
                finally
                {
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        private static void UpdateWorkButtonsEnabled(HashSet<string> uncommittedChanges)
        {
            var anyCanBePacked = false;
            var anyCanBePushed = false;
            var anyCanBePackedAndPushed = false;

            foreach (ListViewItem item in _form.ProjectListView.Items)
            {
                var project = item.GetTag().ClassLibrary;
                var canPush = project.CanPush(false);

                if (project.CanPack(uncommittedChanges, false))
                {
                    anyCanBePacked = true;
                    if (canPush)
                    {
                        anyCanBePackedAndPushed = true;
                    }
                }

                if (canPush && project.KnownLatestLocalVersion is not null)
                {
                    anyCanBePushed = true;
                }
            }

            _form.OpenCloseSolutionButton.Enabled = true;
            _form.PackAllButton.Enabled = anyCanBePacked;
            _form.PushAllButton.Enabled = anyCanBePushed;
            _form.PackAndPushAllButton.Enabled = anyCanBePackedAndPushed;
            _form.CancelWorkButton.Enabled = false;
        }

        private static void CloseSolution()
        {
            _solution = null;

            _form.SolutionInputTextBox.Enabled = true;
            _form.SolutionInputBrowseButton.Enabled = true;

            _form.OpenCloseSolutionButton.Text = "Open solution";
            _form.OpenCloseSolutionButton.Enabled = true;
            _form.PackAllButton.Enabled = false;
            _form.PushAllButton.Enabled = false;
            _form.PackAndPushAllButton.Enabled = false;
            _form.CancelWorkButton.Enabled = false;

            _form.ProjectListView.UnloadSolution();

            _form.DiagnosticsDisplay.Text = string.Empty;

            PackageSourceStoreProvider.PackageSourceStore.ResetPackageSourcesEnabled();
        }
    }
}