// ------------------------------------------------------------------------------
// <copyright file="ListViewItemExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Windows.Forms;

using NuGetPush.WinForms.Enums;
using NuGetPush.WinForms.Extensions;

namespace War3App.MapAdapter.WinForms.Extensions
{
    public static class ListViewItemExtensions
    {
        internal const int StatusColumnIndex = 0;
        internal const int FileNameColumnIndex = 1;
        internal const int ProjectVersionColumnIndex = 2;
        internal const int LocalVersionColumnIndex = 3;
        internal const int NuGetVersionColumnIndex = 4;

        public static ListViewItem Create(ItemTag tag)
        {
            var item = new ListViewItem(new[]
            {
                string.Empty,
                tag.ClassLibrary.Name,
                tag.ClassLibrary.PackageVersion?.ToString() ?? string.Empty,
                string.Empty,
                string.Empty,
            });

            item.Tag = tag;
            tag.ListViewItem = item;

            item.Update(true);
            return item;
        }

        public static ItemTag GetTag(this ListViewItem item)
        {
            return (ItemTag)item.Tag;
        }

        public static void Update(this ListViewItem item, bool recalculateStatus = false)
        {
            var tag = item.GetTag();
            item.Update(recalculateStatus ? tag.ClassLibrary.RecalculateStatus() : tag.Status);
        }

        public static void Update(this ListViewItem item, ProjectStatus status)
        {
            var tag = item.GetTag();
            tag.Status = status;

            item.ImageIndex = (int)tag.Status;
            item.SubItems[StatusColumnIndex].Text = tag.Status.ToString();
            item.SubItems[LocalVersionColumnIndex].Text = tag.ClassLibrary.KnownLatestLocalVersion?.ToString() ?? string.Empty;
            item.SubItems[NuGetVersionColumnIndex].Text = tag.ClassLibrary.KnownLatestNuGetVersion?.ToString() ?? string.Empty;
        }

        public static int CompareTo(this ListViewItem item, ListViewItem other, int column)
        {
            return column switch
            {
                StatusColumnIndex => 0 - item.GetTag().Status.CompareTo(other.GetTag().Status),

                _ => string.IsNullOrWhiteSpace(item.SubItems[column].Text) == string.IsNullOrWhiteSpace(other.SubItems[column].Text)
                    ? string.Compare(item.SubItems[column].Text, other.SubItems[column].Text, StringComparison.InvariantCulture)
                    : string.IsNullOrWhiteSpace(item.SubItems[column].Text) ? 1 : -1,
            };
        }
    }
}