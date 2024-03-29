﻿// ------------------------------------------------------------------------------
// <copyright file="RemoteConnectionManagerFactory.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using NuGetPush.Helpers;
using NuGetPush.WinForms.Forms;

namespace NuGetPush.WinForms.Helpers
{
    public static class RemoteConnectionManagerFactory
    {
        public static async Task<IRemoteConnectionManager> TryCreateAsync(PackageSource packageSource, CancellationToken cancellationToken)
        {
            var providers = Repository.Provider.GetCoreV3();

            while (true)
            {
                try
                {
#if MOCK_REMOTE
                    const string MockUsername = "user";

                    if (!string.Equals(packageSource.Credentials?.Username, MockUsername, StringComparison.Ordinal))
                    {
                        await Task.Delay(1000, cancellationToken);

                        throw new FatalProtocolException("Unauthorized", new HttpRequestException(null, null, HttpStatusCode.Unauthorized));
                    }

                    return new RemoteConnectionManager(packageSource, null);
#else
                    var sourceRepository = new SourceRepository(packageSource, providers);
                    var findPackageByIdResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>().WaitAsync(cancellationToken);

                    await TryConnectToRemote(findPackageByIdResource, cancellationToken);

                    return new RemoteConnectionManager(packageSource, findPackageByIdResource);
#endif
                }
                catch (FatalProtocolException fatalProtocolException) when (IsUnauthorizedException(fatalProtocolException))
                {
                    if (!await TrySetCredentialsAsync(packageSource))
                    {
                        return new DisconnectedRemoteConnectionManager(packageSource, unauthorized: true);
                    }
                }
                catch (Exception exception) when (exception is not TaskCanceledException)
                {
                    return new DisconnectedRemoteConnectionManager(packageSource, unauthorized: false);
                }
            }
        }

        /// <exception cref="FatalProtocolException">Thrown when unable to connect to the remote package source, either due to not being authorized or due to some connection issue.</exception>
        private static async Task TryConnectToRemote(FindPackageByIdResource findPackageByIdResource, CancellationToken cancellationToken)
        {
            using var sourceCacheContext = new SourceCacheContext();
            sourceCacheContext.NoCache = true;

            await findPackageByIdResource.DoesPackageExistAsync("Newtonsoft.Json", new NuGetVersion(13, 0, 3), sourceCacheContext, NullLogger.Instance, cancellationToken);
        }

        private static bool IsUnauthorizedException(FatalProtocolException fatalProtocolException)
        {
            return fatalProtocolException.InnerException is HttpRequestException httpRequestException
                && httpRequestException.StatusCode == HttpStatusCode.Unauthorized;
        }

        private static async Task<bool> TrySetCredentialsAsync(PackageSource packageSource)
        {
            using var credentialsForm = new CredentialsForm(packageSource);

            var dialogResult = credentialsForm.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                packageSource.Credentials = new PackageSourceCredential(
                    packageSource.Source,
                    credentialsForm.UserName,
                    credentialsForm.AccessToken,
                    true,
                    null);

                return true;
            }

            return false;
        }
    }
}