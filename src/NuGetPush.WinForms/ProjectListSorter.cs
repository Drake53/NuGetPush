// ------------------------------------------------------------------------------
// <copyright file="ProjectListSorter.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Collections;
using System.Windows.Forms;

using War3App.MapAdapter.WinForms.Extensions;

namespace War3App.MapAdapter.WinForms
{
    internal sealed class ProjectListSorter : IComparer
    {
        private readonly ListView _projectList;

        public ProjectListSorter(ListView projectList)
        {
            _projectList = projectList;
        }

        public SortOrder SortOrder { get; set; }

        public int SortColumn { get; set; }

        public void Sort(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == SortColumn)
            {
                SortOrder = SortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                SortOrder = SortOrder.Ascending;
                SortColumn = e.Column;
            }

            _projectList.Sort();
        }

        public int Compare(object x, object y)
        {
            if (x is ListViewItem item1 && y is ListViewItem item2)
            {
                return SortOrder switch
                {
                    SortOrder.None => -1,
                    SortOrder.Ascending => item1.CompareTo(item2, SortColumn),
                    SortOrder.Descending => 0 - item1.CompareTo(item2, SortColumn),
                };
            }

            return -1;
        }

        public void Reset()
        {
            SortOrder = SortOrder.None;
            SortColumn = default;
        }
    }
}