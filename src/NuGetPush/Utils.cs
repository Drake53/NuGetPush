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