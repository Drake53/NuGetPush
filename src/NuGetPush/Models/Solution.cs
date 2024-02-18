// ------------------------------------------------------------------------------
// <copyright file="Solution.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;

using NuGet.Configuration;
using NuGet.Versioning;

using NuGetPush.Helpers;

namespace NuGetPush.Models
{
    public class Solution
    {
        private readonly string _solutionFileName;

        public Solution(string? repositoryRoot, string fileName)
        {
            RepositoryRoot = repositoryRoot;

            var solutionFileInfo = new FileInfo(fileName);
            Name = solutionFileInfo.Name[..^4];

            _solutionFileName = solutionFileInfo.FullName;

            PackageSources = PackageSourceFactory.GetPackageSources(solutionFileInfo.DirectoryName);
        }

        public string Name { get; }

        public string? RepositoryRoot { get; }

        public List<PackageSource> PackageSources { get; }

        public PackageSource? SelectedLocalPackageSource { get; set; }

        public PackageSource? SelectedRemotePackageSource { get; set; }

        public List<ClassLibrary> Projects { get; private set; }

        public List<TestProject> TestProjects { get; private set; }

        public List<string> InvalidProjects { get; private set; }

        public override string ToString() => Name;

        public void ParseSolutionProjects(List<string>? solutionFilterProjects, bool checkDependencies)
        {
            if (Projects is not null || TestProjects is not null)
            {
                throw new InvalidOperationException();
            }

            if (SelectedLocalPackageSource is null)
            {
                throw new InvalidOperationException();
            }

            Projects = new();
            TestProjects = new();
            InvalidProjects = new();

            var solutionFile = SolutionFile.Parse(_solutionFileName);

            using var projectCollection = new ProjectCollection();
            var projectOptions = new ProjectOptions
            {
                EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared),
                GlobalProperties = new Dictionary<string, string> { { "Configuration", "Release" }, { "IsPublishBuild", "true" } },
                LoadSettings = ProjectLoadSettings.Default,
                ProjectCollection = projectCollection,
            };

            foreach (var projectInSolution in solutionFile.ProjectsInOrder)
            {
                if (projectInSolution.ProjectType == SolutionProjectType.SolutionFolder)
                {
                    continue;
                }

                if (solutionFilterProjects is not null)
                {
                    var isProjectInSolutionFilter = false;
                    foreach (var solutionFilterProject in solutionFilterProjects)
                    {
                        if (string.Equals(solutionFilterProject, projectInSolution.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                        {
                            isProjectInSolutionFilter = true;
                            break;
                        }
                    }

                    if (!isProjectInSolutionFilter)
                    {
                        continue;
                    }
                }

                Project project;
                try
                {
                    project = Project.FromFile(projectInSolution.AbsolutePath, projectOptions);
                }
                catch
                {
                    InvalidProjects.Add(projectInSolution.AbsolutePath);
                    continue;
                }

                if (project.Properties.Any(property => property.Name == "OutputType" && property.EvaluatedValue == "Library") &&
                    project.Properties.Any(property => property.Name == "IsPackable" && property.EvaluatedValue == "true"))
                {
                    Projects.Add(new ClassLibrary(projectInSolution.ProjectName, projectInSolution.AbsolutePath, RepositoryRoot, project, SelectedLocalPackageSource, SelectedRemotePackageSource));
                }
                else if (project.Items.Any(item => item.ItemType == "PackageReference"
                    && (item.EvaluatedInclude == "MSTest.TestFramework"
                    || item.EvaluatedInclude == "NUnit"
                    || item.EvaluatedInclude == "xunit")))
                {
                    TestProjects.Add(new TestProject(projectInSolution.ProjectName, projectInSolution.AbsolutePath, RepositoryRoot, project));
                }
            }

            foreach (var project in Projects)
            {
                project.FindLatestLocalVersion();
            }

            if (checkDependencies)
            {
                foreach (var project in Projects)
                {
                    project.FindDependencies(this);
                }

                foreach (var project in Projects)
                {
                    project.FindDependees(this);
                }

                foreach (var testProject in TestProjects)
                {
                    Dictionary<string, VersionRange>? centralPackageVersions = null;
                    InvalidDataException? exception = null;

                    try
                    {
                        centralPackageVersions = PackageVersionHelper.GetCentrallyManagedPackageVersions(testProject.Project);
                    }
                    catch (InvalidDataException e)
                    {
                        exception = e;
                    }

                    foreach (var packageReference in testProject.Project.Items.Where(item => item.ItemType == "PackageReference"))
                    {
                        var packageName = packageReference.EvaluatedInclude;
                        var packageProject = Projects.SingleOrDefault(packageProject => packageProject.PackageName == packageName);
                        if (packageProject is not null)
                        {
                            if (exception is not null)
                            {
                                packageProject.Diagnostics.Add(exception.Message);

                                packageProject.MisconfiguredTestProjects.Add(testProject);
                            }
                            else
                            {
                                try
                                {
                                    var versionRange = PackageVersionHelper.GetVersionFromPackageReference(packageReference, centralPackageVersions);
                                    if (!versionRange.Satisfies(packageProject.PackageVersion))
                                    {
                                        packageProject.Diagnostics.Add($"Test project \"{testProject.Name}\" depends on version \"{versionRange.OriginalString}\".");

                                        packageProject.MisconfiguredTestProjects.Add(testProject);
                                    }
                                    else
                                    {
                                        packageProject.TestProjects.Add(testProject);
                                    }
                                }
                                catch (InvalidDataException e)
                                {
                                    packageProject.Diagnostics.Add(e.Message);

                                    packageProject.MisconfiguredTestProjects.Add(testProject);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}