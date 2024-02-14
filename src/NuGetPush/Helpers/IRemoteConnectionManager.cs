// ------------------------------------------------------------------------------
// <copyright file="IRemoteConnectionManager.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Threading.Tasks;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

using NuGetPush.Enums;

namespace NuGetPush.Helpers
{
    public interface IRemoteConnectionManager
    {
        RemoteConnectionState State { get; }

        PackageSource PackageSource { get; }

        FindPackageByIdResource FindPackageByIdResource { get; }

        Task<string?> TryGetApiKeyAsync();

        void SetApiKeyValid(bool isValid);

        Task<bool> HandleDeviceLoginAsync(string deviceLoginLine);
    }
}