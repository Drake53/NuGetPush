// ------------------------------------------------------------------------------
// <copyright file="DotNetSdk.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace NuGetPush.Models
{
    public class DotNetSdk
    {
        private static readonly Regex _regex = new Regex("([0-9]+.[0-9]+.[0-9]+) \\[(.*)\\]", RegexOptions.Compiled);

        private DotNetSdk(string input)
        {
            var match = _regex.Match(input);
            if (match.Success)
            {
                SdkVersion = Version.Parse(match.Groups[1].Value);
                MSBuildPath = Path.Combine(match.Groups[2].Value, match.Groups[1].Value, "MSBuild.dll");
            }
        }

        public Version SdkVersion { get; }

        public string MSBuildPath { get; }

        public static DotNetSdk Parse(string input)
        {
            return new DotNetSdk(input);
        }
    }
}