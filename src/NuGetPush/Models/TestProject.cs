// ------------------------------------------------------------------------------
// <copyright file="TestProject.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using Microsoft.Build.Evaluation;

namespace NuGetPush.Models
{
    public class TestProject
    {
        public TestProject(string name, string projectPath, Project project)
        {
            Name = name;
            ProjectPath = projectPath;
            Project = project;
        }

        public string Name { get; }

        public string ProjectPath { get; }

        public Project Project { get; }

        public override string ToString() => Name;
    }
}