// ------------------------------------------------------------------------------
// <copyright file="LocalPackageSource.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Configuration;
using NuGet.Versioning;

namespace NuGetPush.Models
{
    public class LocalPackageSource : IPackageSource
    {
        private readonly PackageSource _packageSource;
        private readonly ClassLibrary _project;

        internal LocalPackageSource(PackageSource packageSource, ClassLibrary project)
        {
            _packageSource = packageSource;
            _project = project;
        }

        public PackageSource PackageSource => _packageSource;

        public Task<NuGetVersion?> GetLatestNuGetVersionAsync(CancellationToken cancellationToken)
        {
            var packageDirectory = Path.Combine(_packageSource.Source, _project.PackageName.ToLowerInvariant());
            if (Directory.Exists(packageDirectory))
            {
                return Task.FromResult(Directory.EnumerateFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
                    .Select(GetNuGetVersionFromFile)
                    .Max());
            }

            return Task.FromResult<NuGetVersion?>(null);
        }

        public Task<bool> UploadPackageAsync(Action<string>? deviceLoginCallback, CancellationToken cancellationToken)
        {
            var packageOutputPath = _project.PackageOutputPath;
            if (!Directory.Exists(packageOutputPath))
            {
                return Task.FromResult(false);
            }

            var fileName = $"{_project.PackageName}.{_project.PackageVersion.ToNormalizedString()}";
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

            var targetFolder = Path.Combine(_packageSource.Source, _project.PackageName.ToLowerInvariant());
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

        private NuGetVersion GetNuGetVersionFromFile(string filePath)
        {
            return NuGetVersion.Parse(new FileInfo(filePath).Name[(_project.PackageName.Length + 1)..^6]);
        }
    }
}