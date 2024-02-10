// ------------------------------------------------------------------------------
// <copyright file="ProjectListView.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using NuGetPush.Models;
using NuGetPush.Processes;
using NuGetPush.WinForms.Enums;
using NuGetPush.WinForms.Extensions;

namespace NuGetPush.WinForms.Controls
{
    [DesignerCategory("")]
    internal sealed class ProjectListView : ListView
    {
        private readonly ProjectListSorter _projectListSorter;
        private readonly ToolStripButton _packContextButton;
        private readonly ToolStripButton _pushContextButton;
        private readonly ToolStripButton _packAndPushContextButton;

        private Solution? _solution;

        public ProjectListView()
        {
            Dock = DockStyle.Fill;

            _projectListSorter = new ProjectListSorter(this);

            View = View.Details;
            Columns.AddRange(new[]
            {
                new ColumnHeader { Text = "Status", Width = 102 },
                new ColumnHeader { Text = "FileName", Width = 300 },
                new ColumnHeader { Text = "Version", Width = 60 },
                new ColumnHeader { Text = "(Local)", Width = 64 },
                new ColumnHeader { Text = "(Remote)", Width = 94 },
            });

            FullRowSelect = true;
            MultiSelect = true;

            HeaderStyle = ColumnHeaderStyle.Clickable;

            SmallImageList = new ImageList();
            var statusColors = new Dictionary<ProjectStatus, Color>
            {
                { ProjectStatus.Packed, Color.Yellow },
                { ProjectStatus.PackError, Color.Red },
                { ProjectStatus.ParseError, Color.Maroon },
                { ProjectStatus.Pending, Color.LightSkyBlue },
                { ProjectStatus.Pushed, Color.LimeGreen },
                { ProjectStatus.PushError, Color.IndianRed },
                { ProjectStatus.TestFailed, Color.DarkViolet },
                { ProjectStatus.UpToDate, Color.ForestGreen },
                { ProjectStatus.Working, Color.DarkViolet },
                { ProjectStatus.NotReady, Color.DarkSlateGray },
                { ProjectStatus.Idle, Color.Violet },
                { ProjectStatus.DependencyError, Color.Orange },
                { ProjectStatus.ReadyToPush, Color.LightBlue },
                { ProjectStatus.Outdated, Color.SlateGray },
                { ProjectStatus.ReadyToPack, Color.SkyBlue },
                { ProjectStatus.Misconfigured, Color.DimGray },
            };

            foreach (var status in Enum.GetValues(typeof(ProjectStatus)))
            {
                SmallImageList.Images.Add(new Bitmap(16, 16).WithSolidColor(statusColors[(ProjectStatus)status]));
            }

            _packContextButton = new ToolStripButton("Pack");
            _packContextButton.Enabled = false;
            _packContextButton.Click += async (s, e) => await Program.OnClickPackSelectedAsync();

            _pushContextButton = new ToolStripButton("Push");
            _pushContextButton.Enabled = false;
            _pushContextButton.Click += async (s, e) => await Program.OnClickPushSelectedAsync();

            _packAndPushContextButton = new ToolStripButton("Pack && push");
            _packAndPushContextButton.Enabled = false;
            _packAndPushContextButton.Click += async (s, e) => await Program.OnClickPackAndPushSelectedAsync();

            var projectListContextMenu = new ContextMenuStrip
            {
            };

            projectListContextMenu.Items.AddRange(new[]
            {
                _packContextButton,
                _pushContextButton,
                _packAndPushContextButton,
            });

            ContextMenuStrip = projectListContextMenu;

            SelectedIndexChanged += async (s, e) => await UpdateContextMenuAsync();
        }

        public void LoadSolution(Solution solution)
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            if (_solution is not null)
            {
                throw new InvalidOperationException();
            }

            _solution = solution;

            ListViewItemSorter = _projectListSorter;
            ColumnClick += _projectListSorter.Sort;

            var index = 0;
            foreach (var project in _solution.Projects.OrderBy(project => project.Name))
            {
                var tag = new ItemTag(project, index++);
                Items.Add(ListViewItemExtensions.Create(tag));
            }
        }

        public void UnloadSolution()
        {
            if (_solution is null)
            {
                return;
            }

            _solution = null;

            ColumnClick -= _projectListSorter.Sort;
            ListViewItemSorter = null;
            _projectListSorter.Reset();

            Items.Clear();
        }

        public void DisableContextMenuButtons()
        {
            _packContextButton.Enabled = false;
            _pushContextButton.Enabled = false;
            _packAndPushContextButton.Enabled = false;
        }

        internal void UpdateContextMenu(HashSet<string> uncommittedChanges)
        {
            DisableContextMenuButtons();

            if (_solution is null)
            {
                throw new InvalidOperationException();
            }

            if (this.TryGetSelectedItemTags(out var tags))
            {
                foreach (var tag in tags)
                {
                    var project = tag.ClassLibrary;
                    var canPack = project.CanPack(uncommittedChanges, true);
                    var canPush = project.CanPush(tags.Count == 1);

                    _packContextButton.Enabled |= canPack;
                    _pushContextButton.Enabled |= canPush && project.KnownLatestLocalVersion is not null;

                    if (canPack && canPush)
                    {
                        _packAndPushContextButton.Enabled = true;
                        break;
                    }
                }
            }
        }

        private async Task UpdateContextMenuAsync()
        {
            var uncommittedChanges = await Git.CheckUncommittedChangesAsync(_solution.RepositoryRoot, CancellationToken.None);

            UpdateContextMenu(uncommittedChanges);
        }
    }
}