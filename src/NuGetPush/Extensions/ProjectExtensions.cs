// ------------------------------------------------------------------------------
// <copyright file="ProjectExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Build.Evaluation;

using NuGet.Versioning;

namespace NuGetPush.Extensions
{
    internal static class ProjectExtensions
    {
        /// <summary>
        /// Do not allow the default 1.0.0 version, unless the version has explicitly been set to this value in the project.
        /// Uses <see cref="NuGetVersion.TryParse(string, out NuGetVersion)"/>.
        /// </summary>
        public static bool TryGetExplicitVersion(this Project project, [NotNullWhen(true)] out NuGetVersion? nuGetVersion)
        {
            var packageVersion = project.GetProperty("PackageVersion");
            if (!string.Equals(packageVersion.UnevaluatedValue, "$(Version)", StringComparison.Ordinal))
            {
                return NuGetVersion.TryParse(packageVersion.EvaluatedValue, out nuGetVersion);
            }

            var version = project.GetProperty("Version");
            if (!string.Equals(version.UnevaluatedValue, "$(VersionPrefix)", StringComparison.Ordinal))
            {
                return NuGetVersion.TryParse(version.EvaluatedValue, out nuGetVersion);
            }

            var versionPrefix = project.GetProperty("VersionPrefix");
            if (!versionPrefix.IsImported)
            {
                return NuGetVersion.TryParse(versionPrefix.EvaluatedValue, out nuGetVersion);
            }

            nuGetVersion = null;
            return false;
        }
    }
}