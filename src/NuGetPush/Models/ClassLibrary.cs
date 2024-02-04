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
        }

        public string Name { get; init; }

        public string ProjectPath { get; init; }

        public string ProjectDirectory { get; init; }

        public string RelativeProjectPath { get; init; }

        public Project Project { get; init; }

        public string PackageName { get; init; }

        public string PackageDescription { get; init; }

        public List<string> Diagnostics { get; } = new();

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

        public override string ToString() => Name;
    }
}