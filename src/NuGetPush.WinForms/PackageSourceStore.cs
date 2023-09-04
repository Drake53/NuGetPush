// ------------------------------------------------------------------------------
// <copyright file="PackageSourceStore.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

using NuGetPush.Models;
using NuGetPush.WinForms.Forms;

namespace NuGetPush.WinForms
{
    public class PackageSourceStore : IPackageSourceStore
    {
        private readonly Dictionary<string, bool> _packageSourcesEnabled;
        private readonly Dictionary<string, bool> _packageSourcesRequiresAuthentication;
        private readonly Dictionary<string, PackageSourceCredential?> _packageSourcesCredentials;
        private readonly Dictionary<string, FindPackageByIdResource> _packageByIdResources;
        private readonly Dictionary<string, string>? _apiKeys;
        private readonly bool _supportsMultiplePackageSources;
        private bool _hasLocalPackageSourceEnabled;
        private bool _hasRemotePackageSourceEnabled;

        public PackageSourceStore(bool storeApiKeys, bool supportsMultiplePackageSources = false)
        {
            _packageSourcesEnabled = new Dictionary<string, bool>(StringComparer.Ordinal);
            _packageSourcesRequiresAuthentication = new Dictionary<string, bool>(StringComparer.Ordinal);
            _packageSourcesCredentials = new Dictionary<string, PackageSourceCredential>(StringComparer.Ordinal);
            _packageByIdResources = new Dictionary<string, FindPackageByIdResource>(StringComparer.Ordinal);
            if (storeApiKeys)
            {
                _apiKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (supportsMultiplePackageSources)
            {
                throw new NotSupportedException();
            }

            _supportsMultiplePackageSources = supportsMultiplePackageSources;
        }

        public bool GetOrAddIsPackageSourceEnabled(IPackageSource packageSource)
        {
            if (_packageSourcesEnabled.TryGetValue(packageSource.PackageSource.Source, out var enabled))
            {
                return enabled;
            }

            var isLocal = packageSource.PackageSource.IsLocal;
            if (isLocal)
            {
                if (!_supportsMultiplePackageSources && _hasLocalPackageSourceEnabled)
                {
                    enabled = false;
                    _packageSourcesEnabled.Add(packageSource.PackageSource.Source, enabled);
                    return enabled;
                }
            }
            else
            {
                if (!_supportsMultiplePackageSources && _hasRemotePackageSourceEnabled)
                {
                    enabled = false;
                    _packageSourcesEnabled.Add(packageSource.PackageSource.Source, enabled);
                    return enabled;
                }
            }

            var dialogResult = MessageBox.Show(
                $"Do you want to use the following package source?\r\nName: {packageSource.PackageSource.Name}\r\nSource: {packageSource.PackageSource.Source}\r\nType: {(isLocal ? "Local" : "Remote")}",
                "Use package source?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            enabled = dialogResult == DialogResult.Yes;
            _packageSourcesEnabled.Add(packageSource.PackageSource.Source, enabled);

            if (enabled)
            {
                if (isLocal)
                {
                    _hasLocalPackageSourceEnabled = true;
                }
                else
                {
                    _hasRemotePackageSourceEnabled = true;
                }
            }

            return enabled;
        }

        public bool GetIsPackageSourceEnabled(IPackageSource packageSource)
        {
            return _packageSourcesEnabled.GetValueOrDefault(packageSource.PackageSource.Source, false);
        }

        public bool GetPackageSourceRequiresAuthentication(PackageSource packageSource, out PackageSourceCredential? credentials)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("PackageSource must be remote.", nameof(packageSource));
            }

            credentials = _packageSourcesCredentials.GetValueOrDefault(packageSource.Source, null);
            return _packageSourcesRequiresAuthentication.GetValueOrDefault(packageSource.Source, false);
        }

        public bool SetPackageSourceRequiresAuthentication(PackageSource packageSource, bool requiresAuthentication)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("PackageSource must be remote.", nameof(packageSource));
            }

            _packageSourcesRequiresAuthentication.Add(packageSource.Source, requiresAuthentication);

            if (!requiresAuthentication)
            {
                return false;
            }

            using var credentialsForm = new CredentialsForm(packageSource);

            var dialogResult = credentialsForm.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                _packageByIdResources.Remove(packageSource.Source);

                _packageSourcesCredentials.Add(packageSource.Source, new PackageSourceCredential(
                    packageSource.Source,
                    credentialsForm.UserName,
                    credentialsForm.AccessToken,
                    true,
                    null));

                return true;
            }

            return false;
        }

        public async Task<FindPackageByIdResource> GetPackageByIdResourceAsync(PackageSource packageSource, CancellationToken cancellationToken = default)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("PackageSource must be remote.", nameof(packageSource));
            }

            if (_packageByIdResources.TryGetValue(packageSource.Source, out var packageByIdResource))
            {
                return packageByIdResource;
            }

            var providers = Repository.Provider.GetCoreV3();
            var sourceRepository = new SourceRepository(packageSource, providers);

            packageByIdResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

            _packageByIdResources.Add(packageSource.Source, packageByIdResource);

            return packageByIdResource;
        }

        public string? GetOrAddApiKey(PackageSource packageSource)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("PackageSource must be remote.", nameof(packageSource));
            }

            if (_apiKeys is not null && _apiKeys.TryGetValue(packageSource.Source, out var apiKey))
            {
                return apiKey;
            }

            using var apiKeyForm = new ApiKeyForm(packageSource);

            var dialogResult = apiKeyForm.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                apiKey = apiKeyForm.ApiKey;

                _apiKeys?.Add(packageSource.Source, apiKey);

                return apiKey;
            }

            return null;
        }

        public void ResetPackageSourcesEnabled()
        {
            _packageSourcesEnabled.Clear();
            _hasLocalPackageSourceEnabled = false;
            _hasRemotePackageSourceEnabled = false;
        }
    }
}