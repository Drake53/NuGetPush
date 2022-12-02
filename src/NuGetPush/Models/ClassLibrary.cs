// ------------------------------------------------------------------------------
// <copyright file="ClassLibrary.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Build.Evaluation;

using NuGet.Versioning;

namespace NuGetPush.Models
{
    public class ClassLibrary
    {
        public ClassLibrary(string name, string projectPath, string repositoryRoot, Project project)
        {
            Name = name;
            ProjectPath = projectPath;
            RelativeProjectPath = Utils.MakeRelativePath(projectPath, repositoryRoot);
            Project = project;

            PackageName = project.Properties.Single(property => property.Name == "PackageId").EvaluatedValue;
            PackageDescription = project.Properties.SingleOrDefault(property => property.Name == "Description")?.EvaluatedValue ?? PackageName;
            if (!project.ItemTypes.Contains("ProjectReference"))
            {
                PackageVersion = NuGetVersion.Parse(project.Properties.Single(property => property.Name == "PackageVersion").EvaluatedValue);
            }
        }

        public string Name { get; init; }

        public string ProjectPath { get; init; }

        public string RelativeProjectPath { get; init; }

        public Project Project { get; init; }

        public string PackageName { get; init; }

        public string PackageDescription { get; init; }

        public NuGetVersion? PackageVersion { get; init; }

        public NuGetVersion? KnownLatestVersion { get; set; }

        public NuGetVersion? KnownLatestLocalVersion { get; set; }

        public NuGetVersion? KnownLatestNuGetVersion { get; set; }

        public HashSet<ClassLibrary> Dependencies { get; private set; }

        public HashSet<ClassLibrary> Dependees { get; private set; }

        public HashSet<TestProject> TestProjects { get; init; } = new();

        public HashSet<TestProject> MisconfiguredTestProjects { get; init; } = new();

        public async Task FindLatestVersionAsync(string localNuGetFeedDirectory)
        {
            if (PackageVersion is null)
            {
                return;
            }

            await Utils.GetLatestLocalVersion(this, localNuGetFeedDirectory);
            await Utils.GetLatestNuGetVersion(this);
        }

        public void FindDependencies(Solution solution)
        {
            Dependencies = new();
            foreach (var packageReference in Project.Items.Where(item => item.ItemType == "PackageReference"))
            {
                var packageName = packageReference.EvaluatedInclude;
                var packageProject = solution.Projects.SingleOrDefault(packageProject => packageProject.PackageName == packageName);
                if (packageProject is not null)
                {
                    var version = packageReference.Metadata.Single(metadata => metadata.Name == "Version");
                    if (!NuGetVersion.TryParse(version.EvaluatedValue, out var packageVersion))
                    {
                        throw new InvalidDataException($"Package version '{version.EvaluatedValue}' ({version.UnevaluatedValue}) is invalid.");
                    }

                    Dependencies.Add(packageProject);
                }
            }
        }

        public void FindDependees(Solution solution)
        {
            Dependees = new();
            foreach (var project in solution.Projects)
            {
                if (project.Dependencies.Contains(this))
                {
                    Dependees.Add(project);
                }
            }
        }

        public override string ToString() => Name;
    }
}