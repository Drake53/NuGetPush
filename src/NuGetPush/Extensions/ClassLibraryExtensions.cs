// ------------------------------------------------------------------------------
// <copyright file="ClassLibraryExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

using NuGet.Versioning;

using NuGetPush.Helpers;
using NuGetPush.Models;

namespace NuGetPush.Extensions
{
    public static class ClassLibraryExtensions
    {
        /// <summary>
        /// Do not allow the default 1.0.0 version, unless the version has explicitly been set to this value in the project.
        /// Uses <see cref="NuGetVersion.TryParse(string, out NuGetVersion)"/>.
        /// </summary>
        public static bool TryGetExplicitVersion(this ClassLibrary project, [NotNullWhen(true)] out NuGetVersion? nuGetVersion)
        {
            var packageVersion = project.Project.GetProperty("PackageVersion");
            if (!string.Equals(packageVersion.UnevaluatedValue, "$(Version)", StringComparison.Ordinal))
            {
                return NuGetVersion.TryParse(packageVersion.EvaluatedValue, out nuGetVersion);
            }

            var version = project.Project.GetProperty("Version");
            if (!string.Equals(version.UnevaluatedValue, "$(VersionPrefix)", StringComparison.Ordinal))
            {
                return NuGetVersion.TryParse(version.EvaluatedValue, out nuGetVersion);
            }

            var versionPrefix = project.Project.GetProperty("VersionPrefix");
            if (!versionPrefix.IsImported)
            {
                return NuGetVersion.TryParse(versionPrefix.EvaluatedValue, out nuGetVersion);
            }

            if (project.Project.Targets.ContainsKey("SetProjectVersionsFromCentralPackageManagement"))
            {
                try
                {
                    var centralPackageVersions = PackageVersionHelper.GetCentrallyManagedPackageVersions(project.Project);
                    if (centralPackageVersions is not null &&
                        centralPackageVersions.TryGetValue(project.PackageName, out var versionRange))
                    {
                        return NuGetVersion.TryParse(versionRange.OriginalString, out nuGetVersion);
                    }
                }
                catch (InvalidDataException)
                {
                }
            }

            nuGetVersion = null;
            return false;
        }

        /// <summary>
        /// Check if this project must be build before dependees can be built.
        /// </summary>
        /// <param name="classLibrary">The project to check.</param>
        /// <returns><see langword="true"/> if the <paramref name="classLibrary"/>'s current <see cref="ClassLibrary.PackageVersion"/> already exists in one of the package sources.</returns>
        public static bool IsUpToDateAsDependency(this ClassLibrary classLibrary)
        {
            return classLibrary.PackageVersion == classLibrary.KnownLatestLocalVersion
                || classLibrary.PackageVersion == classLibrary.KnownLatestRemoteVersion;
        }

        /// <summary>
        /// Get all projects, including dependencies, which must be built in order to build the given projects.
        /// </summary>
        public static IEnumerable<ClassLibrary> GetProjectsToBuild(this IEnumerable<ClassLibrary> projectsToBuild)
        {
            var result = projectsToBuild.ToHashSet();
            var queue = new Queue<ClassLibrary>(result);

            while (queue.TryDequeue(out var project))
            {
                if (project.Dependencies is null)
                {
                    continue;
                }

                foreach (var dependency in project.Dependencies)
                {
                    if (!result.Contains(dependency) && !dependency.IsUpToDateAsDependency())
                    {
                        result.Add(dependency);
                        queue.Enqueue(dependency);
                    }
                }
            }

            return result;
        }
    }
}