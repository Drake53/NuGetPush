// ------------------------------------------------------------------------------
// <copyright file="BuildResult.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;

using NuGetPush.Models;

namespace NuGetPush.WinForms.Models
{
    public class BuildResult
    {
        public ClassLibrary Project { get; set; }

        public bool Failed { get; set; }

        public List<ClassLibrary>? MissingDependencies { get; set; }
    }
}