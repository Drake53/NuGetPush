// ------------------------------------------------------------------------------
// <copyright file="PackageChangeType.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

namespace NuGetPush.Enums
{
    /// <summary>SemVer 2.0 change type.</summary>
    public enum PackageChangeType
    {
        /// <summary>No changes.</summary>
        None = 0,

        /// <summary>Bugfixes and/or non-functional changes.</summary>
        Patch = 1,

        /// <summary>Non-breaking changes.</summary>
        Minor = 2,

        /// <summary>Breaking changes.</summary>
        Major = 3,
    }
}