// ------------------------------------------------------------------------------
// <copyright file="ItemTag.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Windows.Forms;

using NuGetPush.Models;
using NuGetPush.WinForms.Enums;

namespace War3App.MapAdapter.WinForms
{
    public sealed class ItemTag
    {
        public ItemTag(ClassLibrary classLibrary, string? submoduleName = null)
        {
            ClassLibrary = classLibrary;
            SubmoduleName = submoduleName ?? string.Empty;
        }

        public ClassLibrary ClassLibrary { get; private set; }

        public string SubmoduleName { get; private set; }

        public ListViewItem ListViewItem { get; set; }

        public ProjectStatus Status { get; set; }
    }
}