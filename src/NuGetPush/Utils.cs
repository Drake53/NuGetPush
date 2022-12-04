// ------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using NuGet.Versioning;

using NuGetPush.Models;
using NuGetPush.Processes;

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

        public static async Task<PackProjectsResult> PackProjectsAsync(IEnumerable<ClassLibrary> projects)
        {
            var succeeded = new List<ClassLibrary>();
            var failed = new List<ClassLibrary>();

            foreach (var project in projects)
            {
                if (await DotNet.PackAsync(project))
                {
                    succeeded.Add(project);
                }
                else
                {
                    failed.Add(project);
                }
            }

            return new PackProjectsResult
            {
                Succeeded = succeeded,
                Failed = failed,
            };
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