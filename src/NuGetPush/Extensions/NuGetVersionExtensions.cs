// ------------------------------------------------------------------------------
// <copyright file="NuGetVersionExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using NuGet.Versioning;

using NuGetPush.Enums;

namespace NuGetPush.Extensions
{
    public static class NuGetVersionExtensions
    {
        public static PackageChangeType GetChangeType(this NuGetVersion currentVersion, NuGetVersion nextVersion)
        {
            if (currentVersion.IsPrerelease || nextVersion.IsPrerelease || currentVersion >= nextVersion)
            {
                return PackageChangeType.None;
            }

            if (nextVersion.Major > currentVersion.Major)
            {
                return PackageChangeType.Major;
            }
            else if (nextVersion.Minor > currentVersion.Minor)
            {
                return PackageChangeType.Minor;
            }
            else
            {
                return PackageChangeType.Patch;
            }
        }

        public static NuGetVersion GetNextVersion(this NuGetVersion version, PackageChangeType changeType)
        {
            return changeType switch
            {
                PackageChangeType.None => version,
                PackageChangeType.Patch => new NuGetVersion(version.Major, version.Minor, version.Patch + 1),
                PackageChangeType.Minor => new NuGetVersion(version.Major, version.Minor + 1, 0),
                PackageChangeType.Major => new NuGetVersion(version.Major + 1, 0, 0),
            };
        }
    }
}