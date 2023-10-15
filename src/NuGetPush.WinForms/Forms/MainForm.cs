// ------------------------------------------------------------------------------
// <copyright file="MainForm.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using NuGetPush.WinForms.Controls;
using NuGetPush.WinForms.Extensions;

namespace NuGetPush.WinForms.Forms
{
    [DesignerCategory("")]
    internal sealed class MainForm : Form
    {
        private const string Title = "NuGet Push v0.1.0";

        private readonly TextBox _solutionInput;
        private readonly Button _solutionInputBrowseButton;
        private readonly Button _openCloseSolutionButton;
        private readonly FileSystemWatcher _watcher;

        private readonly Button _packAllButton;
        private readonly Button _pushAllButton;
        private readonly Button _packAndPushAllButton;

        private readonly ProjectListView _projectListView;

        private readonly TextBox _diagnosticsDisplay;

        public MainForm()
        {
            _watcher = new FileSystemWatcher();
            _watcher.Created += OnWatchedFileEvent;
            _watcher.Renamed += OnWatchedFileEvent;
            _watcher.Deleted += OnWatchedFileEvent;

            Size = new Size(1280, 720);
            MinimumSize = new Size(400, 300);
            Text = Title;

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
            };

            _solutionInput = new TextBox
            {
                PlaceholderText = "Input solution...",
            };

            _openCloseSolutionButton = new Button
            {
                Text = "Open solution",
                Enabled = false,
            };

            _diagnosticsDisplay = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
            };

            _packAllButton = new Button
            {
                Text = "Pack all",
            };

            _packAllButton.Size = _packAllButton.PreferredSize;
            _packAllButton.Enabled = false;

            _pushAllButton = new Button
            {
                Text = "Push all",
            };

            _pushAllButton.Size = _pushAllButton.PreferredSize;
            _pushAllButton.Enabled = false;

            _packAndPushAllButton = new Button
            {
                Text = "Pack && push all",
            };

            _packAndPushAllButton.Size = _packAndPushAllButton.PreferredSize;
            _packAndPushAllButton.Enabled = false;

            _solutionInput.TextChanged += OnSolutionInputTextChanged;

            _solutionInputBrowseButton = new Button
            {
                Text = "Browse",
            };

            _solutionInputBrowseButton.Size = _solutionInputBrowseButton.PreferredSize;
            _solutionInputBrowseButton.Click += (s, e) =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    CheckFileExists = false,
                };
                openFileDialog.Filter = string.Join('|', new[]
                {
                    "Solution or Solution Filter|*.sln;*.slnf",
                    "Solution|*.sln",
                    "Solution Filter|*.slnf",
                    "All files|*.*",
                });
                var openFileDialogResult = openFileDialog.ShowDialog();
                if (openFileDialogResult == DialogResult.OK)
                {
                    _solutionInput.Text = openFileDialog.FileName;
                }
            };

            _projectListView = new ProjectListView();

            _openCloseSolutionButton.Size = _openCloseSolutionButton.PreferredSize;

            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                Width = 640,
            };

            var inputSolutionFlowLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
            };

            var buttonsFlowLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
            };

            inputSolutionFlowLayout.AddControls(_solutionInput, _solutionInputBrowseButton, _openCloseSolutionButton);
            buttonsFlowLayout.AddControls(_packAllButton, _pushAllButton, _packAndPushAllButton);
            flowLayout.AddControls(inputSolutionFlowLayout, buttonsFlowLayout);

            splitContainer.Panel1.AddControls(_diagnosticsDisplay, flowLayout);
            splitContainer.Panel2.AddControls(_projectListView);
            this.AddControls(splitContainer);

            splitContainer.Panel1.SizeChanged += (s, e) =>
            {
                var width = splitContainer.Panel1.Width;
                _solutionInput.Width = (width > 360 ? 360 : width) - 10;

                inputSolutionFlowLayout.Size = inputSolutionFlowLayout.GetPreferredSize(new Size(width, 0));
                buttonsFlowLayout.Size = buttonsFlowLayout.GetPreferredSize(new Size(width, 0));
                flowLayout.Height
                    = inputSolutionFlowLayout.Margin.Top + inputSolutionFlowLayout.Height + inputSolutionFlowLayout.Margin.Bottom
                    + buttonsFlowLayout.Margin.Top + buttonsFlowLayout.Height + buttonsFlowLayout.Margin.Bottom;
            };

            splitContainer.SplitterDistance = 640 - splitContainer.SplitterWidth;
            splitContainer.Panel1MinSize = 200;
        }

        public TextBox SolutionInputTextBox => _solutionInput;

        public Button SolutionInputBrowseButton => _solutionInputBrowseButton;

        public Button OpenCloseSolutionButton => _openCloseSolutionButton;

        public Button PackAllButton => _packAllButton;

        public Button PushAllButton => _pushAllButton;

        public Button PackAndPushAllButton => _packAndPushAllButton;

        public ProjectListView ProjectListView => _projectListView;

        public TextBox DiagnosticsDisplay => _diagnosticsDisplay;

        private void OnWatchedFileEvent(object sender, EventArgs e)
        {
            SetOpenSolutionButtonEnabled(File.Exists(_solutionInput.Text));
        }

        private void SetOpenSolutionButtonEnabled(bool enabled)
        {
            if (_openCloseSolutionButton.InvokeRequired)
            {
                _openCloseSolutionButton.Invoke(new Action(() => _openCloseSolutionButton.Enabled = enabled));
            }
            else
            {
                _openCloseSolutionButton.Enabled = enabled;
            }
        }

        private void OnSolutionInputTextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_solutionInput.Text))
            {
                _watcher.EnableRaisingEvents = false;
                _openCloseSolutionButton.Enabled = false;
            }
            else
            {
                var fileInfo = new FileInfo(_solutionInput.Text);
                _watcher.Path = fileInfo.DirectoryName;
                _watcher.Filter = fileInfo.Name;
                _watcher.EnableRaisingEvents = true;

                SetOpenSolutionButtonEnabled(fileInfo.Exists);
            }
        }
    }
}