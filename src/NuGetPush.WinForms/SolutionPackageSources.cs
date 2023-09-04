// ------------------------------------------------------------------------------
// <copyright file="SolutionPackageSources.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

namespace NuGetPush.WinForms
{
    public class SolutionPackageSources
    {
        public string SolutionPath { get; set; }

        public string? LocalPackageSource { get; set; }

        public string? RemotePackageSource { get; set; }
    }
}