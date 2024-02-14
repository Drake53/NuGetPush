// ------------------------------------------------------------------------------
// <copyright file="RemoteConnectionManager.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Threading.Tasks;
using System.Windows.Forms;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

using NuGetPush.Enums;
using NuGetPush.Helpers;
using NuGetPush.WinForms.Forms;

namespace NuGetPush.WinForms.Helpers
{
    public class RemoteConnectionManager : IRemoteConnectionManager
    {
        private readonly PackageSource _packageSource;
        private readonly FindPackageByIdResource _findPackageByIdResource;

        private string? _apiKey;
        private bool _isApiKeyValid;

        public RemoteConnectionManager(PackageSource packageSource, FindPackageByIdResource findPackageByIdResource)
        {
            _packageSource = packageSource;
            _findPackageByIdResource = findPackageByIdResource;
        }

        public RemoteConnectionState State => RemoteConnectionState.Connected;

        public PackageSource PackageSource => _packageSource;

        public FindPackageByIdResource FindPackageByIdResource => _findPackageByIdResource;

        public Task<string?> TryGetApiKeyAsync()
        {
            if (!_isApiKeyValid)
            {
                using var apiKeyForm = new ApiKeyForm(_packageSource);

                var dialogResult = apiKeyForm.ShowDialog();

                _apiKey = dialogResult == DialogResult.OK ? apiKeyForm.ApiKey : null;
            }

            return Task.FromResult(_apiKey);
        }

        public void SetApiKeyValid(bool isValid)
        {
            _isApiKeyValid = isValid;
        }

        public Task<bool> HandleDeviceLoginAsync(string deviceLoginLine)
        {
            using var deviceLoginForm = new DeviceLoginForm(deviceLoginLine);

            var dialogResult = deviceLoginForm.ShowDialog();

            return Task.FromResult(dialogResult == DialogResult.OK);
        }
    }
}