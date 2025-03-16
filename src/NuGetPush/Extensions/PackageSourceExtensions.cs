// ------------------------------------------------------------------------------
// <copyright file="PackageSourceExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using NuGetPush.Helpers;
using NuGetPush.Models;

#if !MOCK_REMOTE
using NuGet.Common;
using NuGetPush.Processes;
#endif

namespace NuGetPush.Extensions
{
    public static class PackageSourceExtensions
    {
        public static NuGetVersion? GetLatestLocalNuGetVersion(
            this PackageSource packageSource,
            ClassLibrary classLibrary,
            out HashSet<PackageDependency>? dependencies)
        {
            if (!packageSource.IsLocal)
            {
                throw new ArgumentException("Package source must be local.", nameof(packageSource));
            }

            var packageDirectory = Path.Combine(packageSource.Source, classLibrary.PackageName.ToLowerInvariant());
            if (Directory.Exists(packageDirectory))
            {
                var result = Directory.EnumerateFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
                    .Select(filePath => GetNuGetVersionFromFile(classLibrary, filePath))
                    .Max();

                var latestVersionNupkgFilePath = Path.Combine(packageDirectory, $"{classLibrary.PackageName}.{result.OriginalVersion}.nupkg");

                dependencies = GetDependenciesFromNupkg(classLibrary, latestVersionNupkgFilePath);
                return result;
            }

            dependencies = null;
            return null;
        }

        public static async Task<LatestPackageVersionResult> GetLatestRemoteNuGetVersionAsync(
            this PackageSource packageSource,
            ClassLibrary classLibrary,
            bool enableCache,
            IRemoteConnectionManager remoteConnectionManager,
            CancellationToken cancellationToken)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("Package source must not be local.", nameof(packageSource));
            }

            try
            {
#if MOCK_REMOTE
                return new LatestPackageVersionResult(enableCache ? null : classLibrary.KnownLatestLocalVersion, null);
#else
                using var sourceCacheContext = new SourceCacheContext();

                if (!enableCache)
                {
                    sourceCacheContext.NoCache = true;
                    sourceCacheContext.RefreshMemoryCache = true;
                }

                var packageVersions = await remoteConnectionManager.FindPackageByIdResource.GetAllVersionsAsync(classLibrary.PackageName, sourceCacheContext, NullLogger.Instance, cancellationToken);
                var latestVersion = packageVersions.Max();

                if (latestVersion is null ||
                    (classLibrary.KnownLatestVersion is not null &&
                     latestVersion <= classLibrary.KnownLatestVersion))
                {
                    return new LatestPackageVersionResult(latestVersion, null);
                }

                var dependencyInfo = await remoteConnectionManager.FindPackageByIdResource.GetDependencyInfoAsync(classLibrary.PackageName, latestVersion, sourceCacheContext, NullLogger.Instance, cancellationToken);
                var dependencies = dependencyInfo.DependencyGroups.SelectMany(group => group.Packages).ToHashSet();

                return new LatestPackageVersionResult(latestVersion, dependencies);
#endif
            }
            catch (Exception exception) when (exception is not TaskCanceledException)
            {
                classLibrary.Diagnostics.Add(exception.Message);

                return LatestPackageVersionResult.ErrorResult;
            }
        }

        public static bool MoveLocalPackage(
            this PackageSource packageSource,
            ClassLibrary classLibrary,
            bool force)
        {
            if (!packageSource.IsLocal)
            {
                throw new ArgumentException("Package source must be local.", nameof(packageSource));
            }

            var packageOutputPath = classLibrary.PackageOutputPath;
            if (!Directory.Exists(packageOutputPath))
            {
                return false;
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

            File.Copy(packageFilePath, Path.Combine(targetFolder, packageFileName), overwrite: force);
            if (expectSymbols)
            {
                File.Copy(symbolsFilePath, Path.Combine(targetFolder, symbolsFileName), overwrite: force);
            }

            return true;
        }

        public static async Task<bool> UploadPackageAsync(
            this PackageSource packageSource,
            ClassLibrary classLibrary,
            IRemoteConnectionManager remoteConnectionManager,
            CancellationToken cancellationToken)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("Package source must not be local.", nameof(packageSource));
            }

#if MOCK_REMOTE
            var apiKey = await remoteConnectionManager.TryGetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return false;
            }

            await Task.Delay(2000, cancellationToken);

            return true;
#else
            var packageOutputPath = classLibrary.PackageOutputPath;
            if (!Directory.Exists(packageOutputPath))
            {
                return false;
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

            var apiKey = await remoteConnectionManager.TryGetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return false;
            }

            return await DotNet.PushAsync(packageFilePath, apiKey, packageSource.Source, remoteConnectionManager.HandleDeviceLoginAsync, cancellationToken);
#endif
        }

        private static NuGetVersion GetNuGetVersionFromFile(ClassLibrary classLibrary, string filePath)
        {
            return NuGetVersion.Parse(new FileInfo(filePath).Name[(classLibrary.PackageName.Length + 1)..^6]);
        }

        private static HashSet<PackageDependency> GetDependenciesFromNupkg(ClassLibrary classLibrary, string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var nuspecEntry = zipArchive.GetEntry($"{classLibrary.PackageName}.nuspec");
            using var nuspecStream = nuspecEntry.Open();
            var nuspecReader = new NuspecReader(nuspecStream);

            return nuspecReader.GetDependencyGroups().SelectMany(group => group.Packages).ToHashSet();
        }

        private static bool IsUnauthorizedException(FatalProtocolException fatalProtocolException)
        {
            return fatalProtocolException.InnerException is HttpRequestException httpRequestException
                && httpRequestException.StatusCode == HttpStatusCode.Unauthorized;
        }
    }
}