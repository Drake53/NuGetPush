// ------------------------------------------------------------------------------
// <copyright file="ClassLibrary.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Build.Evaluation;

using NuGet.Configuration;
using NuGet.Versioning;

using NuGetPush.Extensions;
using NuGetPush.Helpers;

namespace NuGetPush.Models
{
    public class ClassLibrary
    {
        public ClassLibrary(string name, string projectPath, string repositoryRoot, Project project, PackageSource localPackageSource, PackageSource? remotePackageSource)
        {
            Name = name;
            ProjectPath = projectPath;
            ProjectDirectory = new FileInfo(projectPath).DirectoryName;
            RelativeProjectPath = Utils.MakeRelativePath(projectPath, repositoryRoot);
            Project = project;

            PackageName = project.GetPropertyValue("PackageId");
            PackageDescription = project.GetProperty("Description")?.EvaluatedValue ?? PackageName;
            PackageOutputPath = Path.Combine(ProjectDirectory, project.GetPropertyValue("PackageOutputPath"));
            if (!project.ItemTypes.Contains("ProjectReference") && this.TryGetExplicitVersion(out var packageVersion))
            {
                PackageVersion = packageVersion;
            }

            LocalPackageSource = localPackageSource;
            RemotePackageSource = remotePackageSource;
        }

        public string Name { get; init; }

        public string ProjectPath { get; init; }

        public string ProjectDirectory { get; init; }

        public string RelativeProjectPath { get; init; }

        public Project Project { get; init; }

        public string PackageName { get; init; }

        public string PackageDescription { get; init; }

        public string PackageOutputPath { get; init; }

        public PackageSource LocalPackageSource { get; }

        public PackageSource? RemotePackageSource { get; }

        public NuGetVersion? PackageVersion { get; init; }

        public NuGetVersion? KnownLatestVersion { get; set; }

        public NuGetVersion? KnownLatestLocalVersion { get; set; }

        public NuGetVersion? KnownLatestNuGetVersion { get; set; }

        public HashSet<ClassLibrary>? Dependencies { get; private set; }

        public HashSet<ClassLibrary> Dependees { get; private set; }

        public HashSet<TestProject> TestProjects { get; init; } = new();

        public HashSet<TestProject> MisconfiguredTestProjects { get; init; } = new();

        public async Task FindLatestVersionAsync()
        {
            if (PackageVersion is null)
            {
                return;
            }

            foreach (var packageSource in new[] { LocalPackageSource, RemotePackageSource })
            {
                if (packageSource is null)
                {
                    continue;
                }

                var latestVersionFromSource = await packageSource.GetLatestNuGetVersionAsync(this);
                if (packageSource.IsLocal)
                {
                    KnownLatestLocalVersion = latestVersionFromSource;
                }
                else
                {
                    KnownLatestNuGetVersion = latestVersionFromSource;
                }

                if (KnownLatestVersion is null || latestVersionFromSource > KnownLatestVersion)
                {
                    KnownLatestVersion = latestVersionFromSource;
                }
            }
        }

        public void FindDependencies(Solution solution)
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            Dictionary<string, NuGetVersion>? centralPackageVersions;
            try
            {
                centralPackageVersions = PackageVersionHelper.GetCentrallyManagedPackageVersions(Project);
            }
            catch (InvalidDataException)
            {
                return;
            }

            var dependencies = new HashSet<ClassLibrary>();
            foreach (var packageReference in Project.GetItems("PackageReference"))
            {
                var packageName = packageReference.EvaluatedInclude;
                var packageProject = solution.Projects.SingleOrDefault(packageProject => packageProject.PackageName == packageName);
                if (packageProject is not null)
                {
                    try
                    {
                        _ = PackageVersionHelper.GetNuGetVersionFromPackageReference(packageReference, centralPackageVersions);

                        dependencies.Add(packageProject);
                    }
                    catch (InvalidDataException)
                    {
                        return;
                    }
                }
            }

            Dependencies = dependencies;
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

        public override string ToString() => Name;
    }
}