// ------------------------------------------------------------------------------
// <copyright file="ListViewExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Forms;

namespace War3App.MapAdapter.WinForms.Extensions
{
    public static class ListViewExtensions
    {
        public static bool TryGetSelectedItem(this ListView listView, [NotNullWhen(true)] out ListViewItem? item)
        {
            if (listView.SelectedItems.Count == 1)
            {
                item = listView.SelectedItems[0];
                return true;
            }

            item = null;
            return false;
        }

        public static bool TryGetSelectedItemTag(this ListView listView, [NotNullWhen(true)] out ItemTag? tag)
        {
            if (listView.SelectedItems.Count == 1)
            {
                tag = listView.SelectedItems[0].GetTag();
                return true;
            }

            tag = null;
            return false;
        }

        public static bool TryGetSelectedItemTags(this ListView listView, out List<ItemTag> tags)
        {
            tags = new List<ItemTag>();
            foreach (ListViewItem selectedItem in listView.SelectedItems)
            {
                tags.Add(selectedItem.GetTag());
            }

            return tags.Any();
        }
    }
}