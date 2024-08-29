// ------------------------------------------------------------------------------
// <copyright file="ClassLibrary.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Evaluation;

using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Versioning;

using NuGetPush.Enums;
using NuGetPush.Extensions;
using NuGetPush.Helpers;

namespace NuGetPush.Models
{
    public class ClassLibrary
    {
        public ClassLibrary(string name, string projectPath, Project project, PackageSource localPackageSource, PackageSource? remotePackageSource)
        {
            Name = name;
            ProjectPath = projectPath;
            ProjectDirectory = Utils.NormalizePath(Path.GetDirectoryName(projectPath));
            Project = project;

            PackageName = project.GetPropertyValue("PackageId");
            PackageDescription = project.GetProperty("Description")?.EvaluatedValue ?? PackageName;
            PackageOutputPath = Path.Combine(ProjectDirectory, project.GetPropertyValue("PackageOutputPath"));

            if (project.ItemTypes.Contains("ProjectReference"))
            {
                Diagnostics.Add(string.Join(Environment.NewLine, project.GetItems("ProjectReference").Select(projectReference => $"\"{projectReference.EvaluatedInclude}\"").Prepend("Project references are not allowed:")));
            }
            else if (!this.TryGetExplicitVersion(out var packageVersion))
            {
                Diagnostics.Add("Project is lacking a (Package)Version property.");
            }
            else
            {
                PackageVersion = packageVersion;
            }

            LocalPackageSource = localPackageSource;
            RemotePackageSource = remotePackageSource;

            KnownLatestRemoteVersionState = remotePackageSource is null
                ? RemotePackageVersionRequestState.Offline
                : RemotePackageVersionRequestState.Loading;
        }

        public string Name { get; }

        public string ProjectPath { get; }

        public string ProjectDirectory { get; }

        public Project Project { get; }

        public string PackageName { get; }

        public string PackageDescription { get; }

        public List<string> Diagnostics { get; } = new();

        public List<string> DirtyFiles { get; } = new();

        public string PackageOutputPath { get; }

        public PackageSource LocalPackageSource { get; }

        public PackageSource? RemotePackageSource { get; }

        public NuGetVersion? PackageVersion { get; }

        public NuGetVersion? KnownLatestVersion { get; set; }

        public NuGetVersion? KnownLatestLocalVersion { get; set; }

        public NuGetVersion? KnownLatestRemoteVersion { get; set; }

        public RemotePackageVersionRequestState KnownLatestRemoteVersionState { get; set; }

        public HashSet<PackageDependency>? KnownLatestVersionDependencies { get; set; }

        public HashSet<ClassLibrary>? Dependencies { get; private set; }

        public HashSet<ClassLibrary> Dependees { get; private set; }

        public HashSet<TestProject> TestProjects { get; } = new();

        public HashSet<TestProject> MisconfiguredTestProjects { get; } = new();

        public bool IsDirty { get; set; }

        public void FindLatestLocalVersion()
        {
            if (PackageVersion is null)
            {
                return;
            }

            KnownLatestLocalVersion = LocalPackageSource.GetLatestLocalNuGetVersion(this, out var dependencies);

            if (KnownLatestLocalVersion is not null && (KnownLatestVersion is null || KnownLatestLocalVersion > KnownLatestVersion))
            {
                KnownLatestVersion = KnownLatestLocalVersion;
                KnownLatestVersionDependencies = dependencies;
            }
        }

        public async Task FindLatestRemoteVersionAsync(bool enableCache, IRemoteConnectionManager remoteConnectionManager, CancellationToken cancellationToken)
        {
            if (PackageVersion is null || RemotePackageSource is null)
            {
                return;
            }

            var latestVersionResult = await RemotePackageSource.GetLatestRemoteNuGetVersionAsync(this, enableCache, remoteConnectionManager, cancellationToken);

            KnownLatestRemoteVersion = latestVersionResult.Version;
            KnownLatestRemoteVersionState = latestVersionResult.State;

            if (KnownLatestRemoteVersion is not null && (KnownLatestVersion is null || KnownLatestRemoteVersion > KnownLatestVersion))
            {
                KnownLatestVersion = KnownLatestRemoteVersion;
                KnownLatestVersionDependencies = latestVersionResult.Dependencies;
            }
        }

        public void FindDependencies(Solution solution)
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            Dictionary<string, VersionRange>? centralPackageVersions;
            try
            {
                centralPackageVersions = PackageVersionHelper.GetCentrallyManagedPackageVersions(Project);
            }
            catch (InvalidDataException)
            {
                return;
            }

            var dependencies = new HashSet<ClassLibrary>();
            var diagnostics = new List<string>();

            foreach (var packageReference in Project.GetItems("PackageReference"))
            {
                var packageName = packageReference.EvaluatedInclude;
                var packageProject = solution.Projects.SingleOrDefault(packageProject => packageProject.PackageName == packageName);
                if (packageProject is not null)
                {
                    try
                    {
                        var versionRange = PackageVersionHelper.GetVersionFromPackageReference(packageReference, centralPackageVersions);
                        if (versionRange.Satisfies(packageProject.PackageVersion))
                        {
                            dependencies.Add(packageProject);
                        }
                        else
                        {
                            diagnostics.Add($"Dependency on package \"{packageName}\" version \"{versionRange.OriginalString}\" is not satisfied by project \"{packageProject.Name}\" version \"{packageProject.PackageVersion}\".");
                        }
                    }
                    catch (InvalidDataException e)
                    {
                        diagnostics.Add(e.Message);
                    }
                }
            }

            if (diagnostics.Count == 0)
            {
                Dependencies = dependencies;
            }
            else
            {
                Diagnostics.AddRange(diagnostics);
            }
        }

        public void FindDependees(Solution solution)
        {
            Dependees = new();
            foreach (var project in solution.Projects)
            {
                if (project.Dependencies is not null && project.Dependencies.Contains(this))
                {
                    Dependees.Add(project);
                }
            }
        }

        [MemberNotNullWhen(true, nameof(RemotePackageSource))]
        public bool IsRemotePackageSourceLoaded()
        {
            return RemotePackageSource is not null
                && KnownLatestRemoteVersionState == RemotePackageVersionRequestState.Loaded;
        }

        public override string ToString() => Name;
    }
}