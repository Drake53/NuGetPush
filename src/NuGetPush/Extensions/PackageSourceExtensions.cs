// ------------------------------------------------------------------------------
// <copyright file="PackageSourceExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using NuGetPush.Helpers;
using NuGetPush.Models;
using NuGetPush.Processes;

namespace NuGetPush.Extensions
{
    public static class PackageSourceExtensions
    {
        public static NuGetVersion? GetLatestLocalNuGetVersion(
            this PackageSource packageSource,
            ClassLibrary classLibrary)
        {
            if (!packageSource.IsLocal)
            {
                throw new ArgumentException("Package source must be local.", nameof(packageSource));
            }

            var packageDirectory = Path.Combine(packageSource.Source, classLibrary.PackageName.ToLowerInvariant());
            if (Directory.Exists(packageDirectory))
            {
                return Directory.EnumerateFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
                    .Select(filePath => GetNuGetVersionFromFile(classLibrary, filePath))
                    .Max();
            }

            return null;
        }

        public static async Task<LatestPackageVersionResult> GetLatestRemoteNuGetVersionAsync(
            this PackageSource packageSource,
            ClassLibrary classLibrary,
            bool enableCache,
            CancellationToken cancellationToken)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("Package source must not be local.", nameof(packageSource));
            }

            var requiresAuthentication = PackageSourceStoreProvider.PackageSourceStore.GetPackageSourceRequiresAuthentication(packageSource, out var credentials);
            if (requiresAuthentication && credentials is null)
            {
                return LatestPackageVersionResult.UnauthorizedResult;
            }

            packageSource.Credentials = credentials;

            try
            {
#if MOCK_REMOTE
                return new LatestPackageVersionResult(enableCache ? null : classLibrary.KnownLatestLocalVersion);
#else
                using var sourceCacheContext = new SourceCacheContext();

                if (!enableCache)
                {
                    sourceCacheContext.NoCache = true;
                    sourceCacheContext.RefreshMemoryCache = true;
                }

                var packageByIdResource = await PackageSourceStoreProvider.PackageSourceStore.GetPackageByIdResourceAsync(packageSource, cancellationToken);
                var packageVersions = await packageByIdResource.GetAllVersionsAsync(classLibrary.PackageName, sourceCacheContext, NullLogger.Instance, cancellationToken);
                var latestVersion = packageVersions.Max();

                return new LatestPackageVersionResult(latestVersion);
#endif
            }
            catch (FatalProtocolException fatalProtocolException) when (IsUnauthorizedException(fatalProtocolException))
            {
                if (PackageSourceStoreProvider.PackageSourceStore.SetPackageSourceRequiresAuthentication(packageSource, true))
                {
                    return await packageSource.GetLatestRemoteNuGetVersionAsync(classLibrary, enableCache, cancellationToken);
                }

                return LatestPackageVersionResult.UnauthorizedResult;
            }
            catch (Exception exception) when (exception is not TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return LatestPackageVersionResult.ErrorResult;
            }
        }

        public static Task<bool> UploadPackageAsync(
            this PackageSource packageSource,
            ClassLibrary classLibrary,
            Action<string>? deviceLoginCallback,
            CancellationToken cancellationToken)
        {
            if (packageSource.IsLocal)
            {
                var packageOutputPath = classLibrary.PackageOutputPath;
                if (!Directory.Exists(packageOutputPath))
                {
                    return Task.FromResult(false);
                }

                var fileName = $"{classLibrary.PackageName}.{classLibrary.PackageVersion.ToNormalizedString()}";
                var packageFileName = $"{fileName}.nupkg";
                var packageFilePath = Path.Combine(packageOutputPath, packageFileName);

                if (!File.Exists(packageFilePath))
                {
                    throw new FileNotFoundException($"Could not find '{packageFileName}'.");
                }

                var symbolsFileName = $"{fileName}.snupkg";
                var symbolsFilePath = Path.Combine(packageOutputPath, symbolsFileName);

                var includeSymbols = bool.TryParse(classLibrary.Project.GetProperty("IncludeSymbols")?.EvaluatedValue, out var b) && b;
                var symbolsFormat = classLibrary.Project.GetProperty("SymbolPackageFormat")?.EvaluatedValue;

                var expectSymbols = includeSymbols && string.Equals(symbolsFormat, "snupkg", StringComparison.OrdinalIgnoreCase);
                if (expectSymbols && !File.Exists(symbolsFilePath))
                {
                    throw new FileNotFoundException($"Could not find '{symbolsFileName}'.");
                }

                var targetFolder = Path.Combine(packageSource.Source, classLibrary.PackageName.ToLowerInvariant());
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                File.Copy(packageFilePath, Path.Combine(targetFolder, packageFileName), overwrite: false);
                if (expectSymbols)
                {
                    File.Copy(symbolsFilePath, Path.Combine(targetFolder, symbolsFileName), overwrite: false);
                }

                return Task.FromResult(true);
            }
            else
            {
#if MOCK_REMOTE
                return Task.Delay(2000, cancellationToken).ContinueWith(_ => true);
#else
                var packageOutputPath = classLibrary.PackageOutputPath;
                if (!Directory.Exists(packageOutputPath))
                {
                    return Task.FromResult(false);
                }

                var fileName = $"{classLibrary.PackageName}.{classLibrary.PackageVersion.ToNormalizedString()}";
                var packageFileName = $"{fileName}.nupkg";
                var packageFilePath = Path.Combine(packageOutputPath, packageFileName);

                if (!File.Exists(packageFilePath))
                {
                    throw new FileNotFoundException($"Could not find '{packageFileName}'.");
                }

                var symbolsFileName = $"{fileName}.snupkg";
                var symbolsFilePath = Path.Combine(packageOutputPath, symbolsFileName);

                var includeSymbols = bool.TryParse(classLibrary.Project.GetProperty("IncludeSymbols")?.EvaluatedValue, out var b) && b;
                var symbolsFormat = classLibrary.Project.GetProperty("SymbolPackageFormat")?.EvaluatedValue;

                var expectSymbols = includeSymbols && string.Equals(symbolsFormat, "snupkg", StringComparison.OrdinalIgnoreCase);
                if (expectSymbols && !File.Exists(symbolsFilePath))
                {
                    throw new FileNotFoundException($"Could not find '{symbolsFileName}'.");
                }

                if (packageSource.Credentials is not null)
                {
                    return DotNet.PushAsync(packageFilePath, packageSource.Credentials.Password, packageSource.Source, deviceLoginCallback, cancellationToken);
                }

                var apiKey = PackageSourceStoreProvider.PackageSourceStore?.GetOrAddApiKey(packageSource);
                if (string.IsNullOrEmpty(apiKey))
                {
                    return Task.FromResult(false);
                }

                return DotNet.PushAsync(packageFilePath, apiKey, packageSource.Source, deviceLoginCallback, cancellationToken);
#endif
            }
        }

        private static NuGetVersion GetNuGetVersionFromFile(ClassLibrary classLibrary, string filePath)
        {
            return NuGetVersion.Parse(new FileInfo(filePath).Name[(classLibrary.PackageName.Length + 1)..^6]);
        }

        private static bool IsUnauthorizedException(FatalProtocolException fatalProtocolException)
        {
            return fatalProtocolException.InnerException is HttpRequestException httpRequestException
                && httpRequestException.StatusCode == HttpStatusCode.Unauthorized;
        }
    }
}