// ------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Versioning;

using NuGetPush.Models;

namespace NuGetPush
{
    public static class Utils
    {
        public static string MakeRelativePath(string absolutePath, string repositoryRoot)
        {
            var fileInfo = new FileInfo(absolutePath);
            var prefixLength = repositoryRoot.Length;

            if (!repositoryRoot.EndsWith('/') && !repositoryRoot.EndsWith('\\'))
            {
                prefixLength++;
            }

            return fileInfo.DirectoryName[prefixLength..];
        }

        public static async Task<NuGetVersion?> GetLatestLocalVersion(ClassLibrary project, string localNuGetFeedDirectory, bool checkBinReleaseFolder = false)
        {
            var localPackages = new List<string>();

            if (checkBinReleaseFolder)
            {
                var binReleaseFolder = Path.Combine(new FileInfo(project.ProjectPath).DirectoryName, "bin", "Release");
                if (Directory.Exists(binReleaseFolder))
                {
                    localPackages.AddRange(Directory.EnumerateFiles(binReleaseFolder, "*.nupkg", SearchOption.TopDirectoryOnly));
                }
            }

            var localFeedFolder = Path.Combine(localNuGetFeedDirectory, project.PackageName.ToLowerInvariant());
            if (Directory.Exists(localFeedFolder))
            {
                localPackages.AddRange(Directory.EnumerateFiles(localFeedFolder, "*.nupkg", SearchOption.TopDirectoryOnly));
            }

            project.KnownLatestLocalVersion = localPackages.Select(localPackage => localPackage.GetNuGetVersion(project.PackageName)).Max();

            if (project.KnownLatestVersion is null || project.KnownLatestLocalVersion > project.KnownLatestVersion)
            {
                project.KnownLatestVersion = project.KnownLatestLocalVersion;
            }

            return project.KnownLatestLocalVersion;
        }

        public static async Task<NuGetVersion?> GetLatestNuGetVersion(ClassLibrary project)
        {
            var latestNuGetVersion = await Processes.NuGet.GetLatestVersionAsync(project.PackageName, true, CancellationToken.None);
            if (latestNuGetVersion is null)
            {
                return project.KnownLatestNuGetVersion;
            }

            project.KnownLatestNuGetVersion = latestNuGetVersion;

            if (project.KnownLatestVersion is null || project.KnownLatestNuGetVersion > project.KnownLatestVersion)
            {
                project.KnownLatestVersion = project.KnownLatestNuGetVersion;
            }

            return project.KnownLatestNuGetVersion;
        }

        private static NuGetVersion GetNuGetVersion(this string filePath, string packageName)
        {
            return NuGetVersion.Parse(filePath.GetVersion(packageName));
        }

        private static string GetVersion(this string filePath, string packageName)
        {
            return new FileInfo(filePath).Name[(packageName.Length + 1)..^6];
        }
    }
}