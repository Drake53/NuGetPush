// ------------------------------------------------------------------------------
// <copyright file="RemotePackageSource.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using NuGetPush.Helpers;
using NuGetPush.Processes;

namespace NuGetPush.Models
{
    public class RemotePackageSource : IPackageSource
    {
        private readonly PackageSource _packageSource;
        private readonly ClassLibrary _project;

        internal RemotePackageSource(PackageSource packageSource, ClassLibrary project)
        {
            _packageSource = packageSource;
            _project = project;
        }

        public PackageSource PackageSource => _packageSource;

        public async Task<NuGetVersion?> GetLatestNuGetVersionAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<IPackageSearchMetadata> response;
            try
            {
                var filter = new SearchFilter(includePrerelease: true);
                var packageSearchResource = await PackageSourceStoreProvider.PackageSourceStore.GetPackageSearchResourceAsync(this, cancellationToken);
                response = await packageSearchResource.SearchAsync($"packageid:{_project.PackageName}", filter, skip: 0, take: 20, NullLogger.Instance, cancellationToken);
            }
            catch (FatalProtocolException)
            {
                return null;
            }

            var result = response.SingleOrDefault(result => string.Equals(result.Identity.Id, _project.PackageName, StringComparison.Ordinal));

            return result?.Identity.Version;
        }

        public Task<bool> UploadPackageAsync(CancellationToken cancellationToken = default)
        {
            var packageOutputPath = _project.PackageOutputPath;
            if (!Directory.Exists(packageOutputPath))
            {
                return Task.FromResult(false);
            }

            var fileName = $"{_project.PackageName}.{_project.PackageVersion}";
            var packageFileName = $"{fileName}.nupkg";
            var packageFilePath = Path.Combine(packageOutputPath, packageFileName);

            if (!File.Exists(packageFilePath))
            {
                throw new FileNotFoundException($"Could not find '{packageFileName}'.");
            }

            var symbolsFileName = $"{fileName}.snupkg";
            var symbolsFilePath = Path.Combine(packageOutputPath, symbolsFileName);

            var includeSymbols = bool.TryParse(_project.Project.GetProperty("IncludeSymbols")?.EvaluatedValue, out var b) && b;
            var symbolsFormat = _project.Project.GetProperty("SymbolPackageFormat")?.EvaluatedValue;

            var expectSymbols = includeSymbols && string.Equals(symbolsFormat, "snupkg", StringComparison.OrdinalIgnoreCase);
            if (expectSymbols && !File.Exists(symbolsFilePath))
            {
                throw new FileNotFoundException($"Could not find '{symbolsFileName}'.");
            }

            var apiKey = PackageSourceStoreProvider.PackageSourceStore?.GetOrAddApiKey(this);
            if (string.IsNullOrEmpty(apiKey))
            {
                return Task.FromResult(false);
            }

            return DotNet.PushAsync(packageFilePath, apiKey, _packageSource.Source, cancellationToken);
        }
    }
}