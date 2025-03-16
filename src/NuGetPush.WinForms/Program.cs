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

using NuGetPush.Enums;
using NuGetPush.Extensions;
using NuGetPush.Helpers;
using NuGetPush.Models;
using NuGetPush.Processes;
using NuGetPush.WinForms.Enums;
using NuGetPush.WinForms.Extensions;
using NuGetPush.WinForms.Forms;
using NuGetPush.WinForms.Helpers;
using NuGetPush.WinForms.Models;

namespace NuGetPush.WinForms
{
    internal static class Program
    {
        private static Solution? _solution;

        private static MainForm _form;
        private static CancellationTokenSource? _workCancellationTokenSource;
        private static CancellationTokenSource? _backgroundWorkCancellationTokenSource;
        private static IRemoteConnectionManager? _remoteConnectionManager;

        [STAThread]
        private static void Main(string[] args)
        {
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
                _backgroundWorkCancellationTokenSource?.Cancel();

                CloseSolution();

                return;
            }

            await OpenSolutionAsync();

            if (_solution?.SelectedRemotePackageSource is not null)
            {
                await LoadRemotePackageVersionsAsync();
            }
        }

        private static async Task LoadRemotePackageVersionsAsync()
        {
            _backgroundWorkCancellationTokenSource = new CancellationTokenSource();
            try
            {
                var cancellationToken = _backgroundWorkCancellationTokenSource.Token;

                _remoteConnectionManager = await RemoteConnectionManagerFactory.TryCreateAsync(_solution.SelectedRemotePackageSource, cancellationToken);
                if (_remoteConnectionManager.State != RemoteConnectionState.Connected)
                {
                    var state = _remoteConnectionManager.State == RemoteConnectionState.Unauthorized
                        ? RemotePackageVersionRequestState.Unauthorized
                        : RemotePackageVersionRequestState.Error;

                    foreach (ListViewItem item in _form.ProjectListView.Items)
                    {
                        var tag = item.GetTag();
                        var project = tag.ClassLibrary;

                        if (project.KnownLatestRemoteVersionState == RemotePackageVersionRequestState.Loading)
                        {
                            project.KnownLatestRemoteVersionState = state;

                            item.Update();
                        }
                    }

                    return;
                }

                var isFirstRun = true;

                var tasks = new List<Task>();

                while (true)
                {
                    foreach (ListViewItem item in _form.ProjectListView.Items)
                    {
                        var tag = item.GetTag();
                        var project = tag.ClassLibrary;

                        if (!isFirstRun && tag.ClassLibrary.KnownLatestRemoteVersionState != RemotePackageVersionRequestState.Indexing)
                        {
                            continue;
                        }

                        tasks.Add(project.FindLatestRemoteVersionAsync(enableCache: isFirstRun, _remoteConnectionManager, cancellationToken)
                            .ContinueWith(task => item.Update(true), TaskContinuationOptions.OnlyOnRanToCompletion));
                    }

                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks);

                        tasks.Clear();

                        var uncommittedChanges = await Git.CheckUncommittedChangesAsync(_solution.RepositoryRoot, cancellationToken);

                        UpdateWorkButtonsEnabled(uncommittedChanges);
                    }

                    isFirstRun = false;

                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                _backgroundWorkCancellationTokenSource?.Dispose();
                _backgroundWorkCancellationTokenSource = null;
            }
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

                await PushProjectsAsync(handledProjects.Where(b => !b.Failed).Select(b => b.Project), selectedProjects.Count == 1, cancellationToken);
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

                await PushProjectsAsync(handledProjects.Where(b => !b.Failed).Select(b => b.Project), false, cancellationToken);
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
            _workCancellationTokenSource?.Cancel();
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

        private static async Task<List<BuildResult>> PackProjectsAsync(IEnumerable<ClassLibrary> projectsToPack, bool force, CancellationToken cancellationToken)
        {
            var result = new List<BuildResult>();

            var uncommittedChanges = await Git.CheckUncommittedChangesAsync(_solution.RepositoryRoot, cancellationToken);

            var requestedProjectsToPack = projectsToPack.ToHashSet();

            var idleProjects = new HashSet<ClassLibrary>();

            foreach (var project in requestedProjectsToPack.GetProjectsToBuild())
            {
                project.CheckDirty(uncommittedChanges);

                if (project.CanPack(requestedProjectsToPack.Contains(project) ? force : false))
                {
                    project.Diagnostics.Clear();

                    idleProjects.Add(project);
                }
            }

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
                    if (project.Dependencies.All(dependency => dependency.IsUpToDateAsDependency()))
                    {
                        packableProjects.Add(project);
                    }
                }

                if (packableProjects.Count == 0)
                {
                    foreach (var project in idleProjects)
                    {
                        result.Add(new BuildResult
                        {
                            Project = project,
                            Failed = true,
                            MissingDependencies = project.Dependencies?.Where(dependency => !dependency.IsUpToDateAsDependency()).ToList(),
                        });
                    }

                    foreach (ListViewItem item in _form.ProjectListView.Items)
                    {
                        var tag = item.GetTag();
                        if (idleProjects.Contains(tag.ClassLibrary))
                        {
                            item.Update(ProjectStatus.DependencyError);
                        }
                    }

                    _form.ProjectListView.Sort();

                    break;
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
                    var moveSucceeded = project.LocalPackageSource.MoveLocalPackage(project, requestedProjectsToPack.Contains(project) ? force : false);
                    if (moveSucceeded)
                    {
                        if (project.KnownLatestLocalVersion is null || project.PackageVersion > project.KnownLatestLocalVersion)
                        {
                            project.KnownLatestLocalVersion = project.PackageVersion;
                        }
                    }

                    result.Add(new BuildResult { Project = project });
                }

                foreach (var project in projectBuildsFailed)
                {
                    result.Add(new BuildResult { Project = project, Failed = true });
                }

                foreach (ListViewItem item in _form.ProjectListView.Items)
                {
                    var tag = item.GetTag();
                    if (packableProjects.Contains(tag.ClassLibrary))
                    {
                        item.Update(projectBuildsFailed.Contains(tag.ClassLibrary) ? ProjectStatus.PackError : ProjectStatus.Packed);
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

            var uploadedProjects = new HashSet<ClassLibrary>();

            foreach (var project in result)
            {
                var uploadSucceeded = project.RemotePackageSource is not null && await project.RemotePackageSource.UploadPackageAsync(project, _remoteConnectionManager, cancellationToken);

                _remoteConnectionManager.SetApiKeyValid(uploadSucceeded);

                if (uploadSucceeded)
                {
                    project.KnownLatestRemoteVersionState = RemotePackageVersionRequestState.Indexing;

                    uploadedProjects.Add(project);
                }
                else
                {
                    break;
                }
            }

            foreach (ListViewItem item in _form.ProjectListView.Items)
            {
                var tag = item.GetTag();

                if (result.Contains(tag.ClassLibrary))
                {
                    item.Update(uploadedProjects.Contains(tag.ClassLibrary) ? ProjectStatus.Pushed : ProjectStatus.PushError);
                }
            }

            _form.ProjectListView.Sort();

            return result;
        }

        private static void UpdateDiagnosticsDisplay()
        {
            _form.DiagnosticsDisplay.Text = _form.ProjectListView.TryGetSelectedItemTag(out var tag)
                ? string.Join(Environment.NewLine + Environment.NewLine, tag.ClassLibrary.DirtyFiles.Select(path => $"Uncommitted file: {path}").Concat(tag.ClassLibrary.Diagnostics).Append($"Description:{Environment.NewLine}{tag.ClassLibrary.PackageDescription}"))
                : $"{_form.ProjectListView.SelectedItems.Count} projects selected.";
        }

        private static CancellationToken StartWork()
        {
            _workCancellationTokenSource = new CancellationTokenSource();

            _form.ProjectListView.DisableContextMenuButtons();
            _form.OpenCloseSolutionButton.Enabled = false;
            _form.PackAllButton.Enabled = false;
            _form.PushAllButton.Enabled = false;
            _form.PackAndPushAllButton.Enabled = false;
            _form.CancelWorkButton.Enabled = true;

            return _workCancellationTokenSource.Token;
        }

        private static async Task FinishWorkAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                if (_workCancellationTokenSource?.IsCancellationRequested == true)
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
                _workCancellationTokenSource?.Dispose();
                _workCancellationTokenSource = null;
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
                    HashSet<string>? solutionFilterProjects = null;
                    if (string.Equals(fileInfo.Extension, ".slnf", StringComparison.OrdinalIgnoreCase))
                    {
                        using var solutionFilterFileStream = fileInfo.OpenRead();

                        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
                        {
                            PropertyNameCaseInsensitive = true,
                        };

                        var solutionFilter = JsonSerializer.Deserialize<SolutionFilterFile>(solutionFilterFileStream, options);

                        solutionFileInfo = new FileInfo(Path.Combine(fileInfo.DirectoryName, solutionFilter.Solution.Path));
                        solutionFilterProjects = solutionFilter.Solution.Projects.Select(path => Path.GetFullPath(path, fileInfo.DirectoryName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    }

                    var repositoryRoot = await Git.GetRepositoryRootAsync(solutionFileInfo.DirectoryName, cancellationToken);

                    var uncommittedChanges = await Git.CheckUncommittedChangesAsync(repositoryRoot, cancellationToken);

                    _solution = new Solution(solutionFileInfo, repositoryRoot);

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

                    var solutionFilePath = Utils.NormalizePath(solutionFileInfo.FullName);
                    var solutionPackageSources = packageSources.FirstOrDefault(s => string.Equals(Utils.NormalizePath(s.SolutionPath), solutionFilePath, StringComparison.OrdinalIgnoreCase));

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

                    await DotNet.SetMsBuildExePathAsync(cancellationToken);

                    _solution.ParseSolutionProjects(solutionFilterProjects, true);

                    foreach (var project in _solution.Projects)
                    {
                        project.CheckDirty(uncommittedChanges);
                    }

                    _form.ProjectListView.LoadSolution(_solution);

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
                    _workCancellationTokenSource?.Dispose();
                    _workCancellationTokenSource = null;
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
                project.CheckDirty(uncommittedChanges);

                var canPush = project.CanPush(false);

                if (project.CanPack(force: false))
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
        }
    }
}