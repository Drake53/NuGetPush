// ------------------------------------------------------------------------------
// <copyright file="IPackageSourceStore.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGetPush.Models
{
    public interface IPackageSourceStore
    {
        [Obsolete]
        bool GetOrAddIsPackageSourceEnabled(IPackageSource packageSource);

        [Obsolete]
        bool GetIsPackageSourceEnabled(IPackageSource packageSource);

        [Obsolete]
        bool GetPackageSourceRequiresAuthentication(PackageSource packageSource, out PackageSourceCredential? credentials);

        [Obsolete]
        bool SetPackageSourceRequiresAuthentication(PackageSource packageSource, bool requiresAuthentication);

        Task<FindPackageByIdResource> GetPackageByIdResourceAsync(PackageSource packageSource, CancellationToken cancellationToken);

        string? GetOrAddApiKey(PackageSource packageSource);

        [Obsolete]
        void ResetPackageSourcesEnabled();
    }
}