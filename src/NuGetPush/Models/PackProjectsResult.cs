// ------------------------------------------------------------------------------
// <copyright file="PackProjectsResult.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;

namespace NuGetPush.Models
{
    public class PackProjectsResult
    {
        public List<ClassLibrary> Succeeded { get; set; }

        public List<ClassLibrary> Failed { get; set; }
    }
}