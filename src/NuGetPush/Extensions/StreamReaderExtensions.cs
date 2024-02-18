// ------------------------------------------------------------------------------
// <copyright file="StreamReaderExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetPush.Extensions
{
    internal static class StreamReaderExtensions
    {
        private static readonly Regex _deviceLoginRegex = new Regex("To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code ([A-Z\\d]+) to authenticate.", RegexOptions.Compiled);

        public static async Task<string?> TryReadDeviceLoginAsync(this StreamReader streamReader, CancellationToken cancellationToken)
        {
            while (true)
            {
                var line = await streamReader.ReadLineAsync().WaitAsync(cancellationToken);
                if (line is null)
                {
                    return null;
                }

                if (_deviceLoginRegex.IsMatch(line))
                {
                    return line.Trim();
                }
            }
        }
    }
}