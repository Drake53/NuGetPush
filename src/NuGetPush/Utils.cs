// ------------------------------------------------------------------------------
// <copyright file="Utils.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.IO;

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
    }
}