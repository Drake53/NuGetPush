// ------------------------------------------------------------------------------
// <copyright file="PackageSourceFactory.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using NuGet.Configuration;

using NuGetPush.Models;

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

        [Obsolete]
        public static List<IPackageSource> GetPackageSources(ClassLibrary project)
        {
            var nuGetSettings = Settings.LoadDefaultSettings(project.ProjectDirectory);

            return PackageSourceProvider.LoadPackageSources(nuGetSettings)
                .Where(packageSource => packageSource.IsEnabled)
                .Select(packageSource => GetPackageSource(packageSource, project))
                .ToList();
        }

        [Obsolete]
        private static IPackageSource GetPackageSource(PackageSource packageSource, ClassLibrary project)
        {
            return packageSource.IsLocal
                ? new LocalPackageSource(packageSource, project)
                : new RemotePackageSource(packageSource, project);
        }
    }
}