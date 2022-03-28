// ------------------------------------------------------------------------------
// <copyright file="NuGet.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetPush.Processes
{
    public static class NuGet
    {
        private static SourceRepository? _nuGetRepository;

        public static SourceRepository? NuGetRepository => _nuGetRepository;

        public static async Task<NuGetVersion?> GetLatestVersionAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken = default)
        {
            if (_nuGetRepository is null)
            {
                throw new InvalidOperationException("SourceRepository is uninitialized.");
            }

            IEnumerable<IPackageSearchMetadata> response;
            try
            {
                var filter = new SearchFilter(includePrerelease: includePrerelease);
                var search = await _nuGetRepository.GetResourceAsync<PackageSearchResource>(cancellationToken);
                response = await search.SearchAsync($"packageid:{packageId}", filter, skip: 0, take: 20, NullLogger.Instance, cancellationToken);
            }
            catch (FatalProtocolException)
            {
                return null;
            }

            var result = response.SingleOrDefault(result => string.Equals(result.Identity.Id, packageId, StringComparison.Ordinal));

            return result?.Identity.Version;
        }

        public static void InitializeNuGetRepository(string nuGetSource)
        {
            if (_nuGetRepository is not null)
            {
                throw new InvalidOperationException("SourceRepository is already initialized.");
            }

            // https://github.com/NuGet/Samples/blob/master/PackageDownloadsExample/Program.cs

            var source = new PackageSource(nuGetSource);
            var providers = Repository.Provider.GetCoreV3();

            _nuGetRepository = new SourceRepository(source, providers);
        }
    }
}