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
        public static async Task<NuGetVersion?> GetLatestNuGetVersionAsync(
            this PackageSource packageSource,
            ClassLibrary classLibrary,
            CancellationToken cancellationToken)
        {
            if (packageSource.IsLocal)
            {
                var packageDirectory = Path.Combine(packageSource.Source, classLibrary.PackageName.ToLowerInvariant());
                if (Directory.Exists(packageDirectory))
                {
                    return Directory.EnumerateFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
                        .Select(filePath => GetNuGetVersionFromFile(classLibrary, filePath))
                        .Max();
                }

                return null;
            }
            else
            {
                var requiresAuthentication = PackageSourceStoreProvider.PackageSourceStore.GetPackageSourceRequiresAuthentication(packageSource, out var credentials);
                if (requiresAuthentication && credentials is null)
                {
                    return null;
                }

                packageSource.Credentials = credentials;

                try
                {
                    var packageByIdResource = await PackageSourceStoreProvider.PackageSourceStore.GetPackageByIdResourceAsync(packageSource, cancellationToken);
                    var packageVersions = await packageByIdResource.GetAllVersionsAsync(classLibrary.PackageName, new SourceCacheContext(), NullLogger.Instance, cancellationToken);
                    return packageVersions.Max();
                }
                catch (FatalProtocolException e)
                {
                    if (e.InnerException is HttpRequestException httpRequestException &&
                        httpRequestException.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        if (PackageSourceStoreProvider.PackageSourceStore.SetPackageSourceRequiresAuthentication(packageSource, true))
                        {
                            return await packageSource.GetLatestNuGetVersionAsync(classLibrary, cancellationToken);
                        }
                    }

                    return null;
                }
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
            }
        }

        private static NuGetVersion GetNuGetVersionFromFile(ClassLibrary classLibrary, string filePath)
        {
            return NuGetVersion.Parse(new FileInfo(filePath).Name[(classLibrary.PackageName.Length + 1)..^6]);
        }
    }
}