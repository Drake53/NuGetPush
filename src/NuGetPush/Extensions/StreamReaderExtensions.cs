using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

namespace NuGetPush.Extensions
{
    internal static class StreamReaderExtensions
    {
        private static readonly Regex _deviceLoginRegex = new Regex("To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code ([A-Z\\d]+) to authenticate.", RegexOptions.Compiled);

        public static bool TryReadDeviceLogin(this StreamReader streamReader, [NotNullWhen(true)] out string? deviceLoginLine)
        {
            while (true)
            {
                var line = streamReader.ReadLine();
                if (line is null)
                {
                    deviceLoginLine = null;
                    return false;
                }

                if (_deviceLoginRegex.IsMatch(line))
                {
                    deviceLoginLine = line.Trim();
                    return true;
                }
            }
        }
    }
}