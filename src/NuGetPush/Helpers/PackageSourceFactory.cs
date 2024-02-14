// ------------------------------------------------------------------------------
// <copyright file="PackageSourceFactory.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

using NuGet.Configuration;

namespace NuGetPush.Helpers
{
    public static class PackageSourceFactory
    {
        public static List<PackageSource> GetPackageSources(string root)
        {
            var nuGetSettings = Settings.LoadDefaultSettings(root);

            return PackageSourceProvider.LoadPackageSources(nuGetSettings)
                .Where(packageSource => packageSource.IsEnabled)
                .ToList();
        }
    }
}