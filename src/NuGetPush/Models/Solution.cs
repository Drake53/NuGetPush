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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
#if NET8_0_OR_GREATER
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
#endif

using NuGet.Configuration;
using NuGet.Versioning;

using NuGetPush.Helpers;

namespace NuGetPush.Models
{
    public class Solution
    {
        private readonly string _solutionFilePath;
        private readonly string _solutionDirectoryName;

        private bool _projectsLoaded;

        public Solution(FileInfo solutionFileInfo, string? repositoryRoot)
        {
            _solutionFilePath = solutionFileInfo.FullName;
            _solutionDirectoryName = solutionFileInfo.DirectoryName;

            Name = Path.GetFileNameWithoutExtension(_solutionFilePath);
            RepositoryRoot = repositoryRoot;
            PackageSources = PackageSourceFactory.GetPackageSources(_solutionDirectoryName);
            Projects = new();
            TestProjects = new();
            InvalidProjects = new();
        }

        public string Name { get; }

        public string? RepositoryRoot { get; }

        public List<PackageSource> PackageSources { get; }

        public PackageSource? SelectedLocalPackageSource { get; set; }

        public PackageSource? SelectedRemotePackageSource { get; set; }

        public List<ClassLibrary> Projects { get; }

        public List<TestProject> TestProjects { get; }

        public List<string> InvalidProjects { get; }

        public override string ToString() => Name;

        public async Task ParseSolutionProjectsAsync(HashSet<string>? solutionFilterProjects, bool checkDependencies, CancellationToken cancellationToken)
        {
            if (_projectsLoaded)
            {
                throw new InvalidOperationException("Projects have already been loaded.");
            }

            if (SelectedLocalPackageSource is null)
            {
                throw new InvalidOperationException("Local package source is required to load projects.");
            }

            using var projectCollection = new ProjectCollection();
            var projectOptions = new ProjectOptions
            {
                EvaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared),
                GlobalProperties = new Dictionary<string, string> { { "Configuration", "Release" }, { "IsPublishBuild", "true" } },
                LoadSettings = ProjectLoadSettings.Default,
                ProjectCollection = projectCollection,
            };

#if NET8_0_OR_GREATER
            var solutionSerializer = SolutionSerializers.GetSerializerByMoniker(_solutionFilePath);
            if (solutionSerializer is null)
            {
                throw new InvalidOperationException($"No known serializer for '{Path.GetExtension(_solutionFilePath)}' solution file format.");
            }

            var solutionFile = await solutionSerializer.OpenAsync(_solutionFilePath, cancellationToken);

            foreach (var projectInSolution in solutionFile.SolutionProjects)
            {
                var projectName = projectInSolution.ActualDisplayName;
                var projectAbsolutePath = Path.GetFullPath(projectInSolution.FilePath, _solutionDirectoryName);
#else
            var solutionFile = SolutionFile.Parse(_solutionFilePath);

            foreach (var projectInSolution in solutionFile.ProjectsInOrder)
            {
                if (projectInSolution.ProjectType == SolutionProjectType.SolutionFolder)
                {
                    continue;
                }

                var projectName = projectInSolution.ProjectName;
                var projectAbsolutePath = projectInSolution.AbsolutePath;
#endif

                if (solutionFilterProjects is not null &&
                    !solutionFilterProjects.Contains(projectAbsolutePath))
                {
                    continue;
                }

                Project project;
                try
                {
                    project = Project.FromFile(projectAbsolutePath, projectOptions);
                }
                catch
                {
                    InvalidProjects.Add(projectAbsolutePath);
                    continue;
                }

                if (project.Properties.Any(property => property.Name == "OutputType" && property.EvaluatedValue == "Library") &&
                    project.Properties.Any(property => property.Name == "IsPackable" && property.EvaluatedValue == "true"))
                {
                    Projects.Add(new ClassLibrary(projectName, projectAbsolutePath, project, SelectedLocalPackageSource, SelectedRemotePackageSource));
                }
                else if (project.Items.Any(item => item.ItemType == "PackageReference"
                    && (item.EvaluatedInclude == "MSTest.TestFramework"
                    || item.EvaluatedInclude == "NUnit"
                    || item.EvaluatedInclude == "xunit")))
                {
                    TestProjects.Add(new TestProject(projectName, projectAbsolutePath, project));
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

            _projectsLoaded = true;
        }
    }
}