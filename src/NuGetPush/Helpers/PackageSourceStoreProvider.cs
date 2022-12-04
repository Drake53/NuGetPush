// ------------------------------------------------------------------------------
// <copyright file="PackageSourceStoreProvider.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using NuGetPush.Models;

namespace NuGetPush.Helpers
{
    public static class PackageSourceStoreProvider
    {
        [DisallowNull]
        public static IPackageSourceStore? PackageSourceStore { get; set; }
    }
}