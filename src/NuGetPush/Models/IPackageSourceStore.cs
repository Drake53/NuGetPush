// ------------------------------------------------------------------------------
// <copyright file="IPackageSourceStore.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGetPush.Models
{
    public interface IPackageSourceStore
    {
        bool GetOrAddIsPackageSourceEnabled(IPackageSource packageSource);

        bool GetIsPackageSourceEnabled(IPackageSource packageSource);

        bool GetPackageSourceRequiresAuthentication(RemotePackageSource packageSource, out PackageSourceCredential? credentials);

        bool SetPackageSourceRequiresAuthentication(RemotePackageSource packageSource, bool requiresAuthentication);

        Task<FindPackageByIdResource> GetPackageByIdResourceAsync(RemotePackageSource packageSource, CancellationToken cancellationToken = default);

        string? GetOrAddApiKey(RemotePackageSource packageSource);

        void ResetPackageSourcesEnabled();
    }
}