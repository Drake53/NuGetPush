// ------------------------------------------------------------------------------
// <copyright file="PackageMetadata.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;

using NuGet.Packaging.Core;

namespace NuGetPush.Models
{
    public class PackageMetadata
    {
        public HashSet<PackageDependency> Dependencies { get; set; }

        public string? Commit { get; set; }
    }
}