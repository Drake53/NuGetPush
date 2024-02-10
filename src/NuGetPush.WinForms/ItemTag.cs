// ------------------------------------------------------------------------------
// <copyright file="ItemTag.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Windows.Forms;

using NuGetPush.Models;
using NuGetPush.WinForms.Enums;
using NuGetPush.WinForms.Extensions;

namespace NuGetPush.WinForms
{
    public sealed class ItemTag
    {
        public ItemTag(ClassLibrary classLibrary, int index, string? submoduleName = null)
        {
            ClassLibrary = classLibrary;
            Index = index;
            SubmoduleName = submoduleName ?? string.Empty;
            Status = classLibrary.RecalculateStatus();
        }

        public ClassLibrary ClassLibrary { get; }

        public int Index { get; }

        public string SubmoduleName { get; }

        public ListViewItem ListViewItem { get; set; }

        public ProjectStatus Status { get; set; }
    }
}