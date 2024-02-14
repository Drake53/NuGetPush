// ------------------------------------------------------------------------------
// <copyright file="DisconnectedRemoteConnectionManager.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

using NuGetPush.Enums;
using NuGetPush.Helpers;

namespace NuGetPush.WinForms.Helpers
{
    public class DisconnectedRemoteConnectionManager : IRemoteConnectionManager
    {
        private readonly PackageSource _packageSource;
        private readonly RemoteConnectionState _state;

        public DisconnectedRemoteConnectionManager(PackageSource packageSource, bool unauthorized)
        {
            _packageSource = packageSource;
            _state = unauthorized
                ? RemoteConnectionState.Unauthorized
                : RemoteConnectionState.Error;
        }

        public RemoteConnectionState State => _state;

        public PackageSource PackageSource => _packageSource;

        public FindPackageByIdResource FindPackageByIdResource => throw new NotSupportedException();

        public Task<string?> TryGetApiKeyAsync()
        {
            throw new NotSupportedException();
        }

        public void SetApiKeyValid(bool isValid)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HandleDeviceLoginAsync(string deviceLoginLine)
        {
            throw new NotSupportedException();
        }
    }
}