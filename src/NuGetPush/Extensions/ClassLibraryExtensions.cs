// ------------------------------------------------------------------------------
// <copyright file="ClassLibraryExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using NuGet.Versioning;

using NuGetPush.Helpers;
using NuGetPush.Models;

namespace NuGetPush.Extensions
{
    internal static class ClassLibraryExtensions
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
                    if (centralPackageVersions is not null)
                    {
                        return centralPackageVersions.TryGetValue(project.PackageName, out nuGetVersion);
                    }
                }
                catch (InvalidDataException)
                {
                }
            }

            nuGetVersion = null;
            return false;
        }
    }
}