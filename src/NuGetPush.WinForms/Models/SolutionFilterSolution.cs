// ------------------------------------------------------------------------------
// <copyright file="SolutionFilterSolution.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;

namespace NuGetPush.WinForms.Models
{
    public class SolutionFilterSolution
    {
        public string? Path { get; set; }

        public List<string?>? Projects { get; set; }
    }
}