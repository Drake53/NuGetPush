// ------------------------------------------------------------------------------
// <copyright file="LatestPackageVersionResult.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;

using NuGet.Packaging.Core;
using NuGet.Versioning;

using NuGetPush.Enums;

namespace NuGetPush.Models
{
    public class LatestPackageVersionResult
    {
        private static readonly LatestPackageVersionResult _unauthorizedResult = new(RemotePackageVersionRequestState.Unauthorized);
        private static readonly LatestPackageVersionResult _errorResult = new(RemotePackageVersionRequestState.Error);

        public LatestPackageVersionResult(NuGetVersion? version, HashSet<PackageDependency>? dependencies)
        {
            Version = version;
            State = RemotePackageVersionRequestState.Loaded;
            Dependencies = dependencies;
        }

        private LatestPackageVersionResult(RemotePackageVersionRequestState state)
        {
            Version = null;
            State = state;
            Dependencies = null;
        }

        public static LatestPackageVersionResult UnauthorizedResult => _unauthorizedResult;

        public static LatestPackageVersionResult ErrorResult => _errorResult;

        public NuGetVersion? Version { get; }

        public RemotePackageVersionRequestState State { get; }

        public HashSet<PackageDependency>? Dependencies { get; }
    }
}