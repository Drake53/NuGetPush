// ------------------------------------------------------------------------------
// <copyright file="PackageSourcesForm.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using NuGet.Configuration;

using NuGetPush.WinForms.Extensions;
using NuGetPush.WinForms.Models;

namespace NuGetPush.WinForms.Forms
{
    [DesignerCategory("")]
    public class PackageSourcesForm : Form
    {
        private readonly ComboBox _localPackageSourcesComboBox;
        private readonly ComboBox _remotePackageSourcesComboBox;

        public PackageSourcesForm(List<PackageSource> packageSources)
        {
            Size = new Size(400, 300);
            MinimumSize = new Size(400, 300);
            Text = $"Select package sources";

            _localPackageSourcesComboBox = new ComboBox
            {
                DisplayMember = nameof(PackageSource.Name),
                Dock = DockStyle.Top,
                Text = "Select local package source...",
            };

            _remotePackageSourcesComboBox = new ComboBox
            {
                DisplayMember = nameof(PackageSource.Name),
                Dock = DockStyle.Top,
                Text = "Select remote package source...",
            };

            var localPackageSourcePathLabel = new Label
            {
                Dock = DockStyle.Fill,
            };

            var remotePackageSourceUriLabel = new Label
            {
                Dock = DockStyle.Fill,
            };

            foreach (var packageSource in packageSources)
            {
                if (packageSource.IsLocal)
                {
                    _localPackageSourcesComboBox.Items.Add(packageSource);
                }
                else
                {
                    _remotePackageSourcesComboBox.Items.Add(packageSource);
                }
            }

            _remotePackageSourcesComboBox.Items.Add(NoPackageSource.Instance);

            _localPackageSourcesComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_localPackageSourcesComboBox.SelectedItem is PackageSource packageSource)
                {
                    localPackageSourcePathLabel.Text = packageSource.Source;
                }
                else
                {
                    localPackageSourcePathLabel.Text = string.Empty;
                }
            };

            _remotePackageSourcesComboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_remotePackageSourcesComboBox.SelectedItem is PackageSource packageSource)
                {
                    remotePackageSourceUriLabel.Text = packageSource.Source;
                }
                else if (_remotePackageSourcesComboBox.SelectedItem is NoPackageSource noPackageSource)
                {
                    remotePackageSourceUriLabel.Text = noPackageSource.Description;
                }
                else
                {
                    remotePackageSourceUriLabel.Text = string.Empty;
                }
            };

            var okButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "OK",
            };

            var cancelButton = new Button
            {
                Dock = DockStyle.Bottom,
                Text = "Cancel",
            };

            okButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = okButton;

            var localPackageSourceGroupBox = new GroupBox
            {
                Dock = DockStyle.Top,
                Text = "Local package source",
            };

            localPackageSourceGroupBox.AddControls(localPackageSourcePathLabel, _localPackageSourcesComboBox);

            var remotePackageSourceGroupBox = new GroupBox
            {
                Dock = DockStyle.Top,
                Text = "Remote package source",
            };

            remotePackageSourceGroupBox.AddControls(remotePackageSourceUriLabel, _remotePackageSourcesComboBox);

            this.AddControls(remotePackageSourceGroupBox, localPackageSourceGroupBox, okButton, cancelButton);
        }

        public PackageSource? LocalPackageSource
        {
            get => _localPackageSourcesComboBox.SelectedItem as PackageSource;
            set
            {
                if (value is null)
                {
                    _localPackageSourcesComboBox.SelectedIndex = -1;
                }
                else if (!value.IsLocal)
                {
                    throw new ArgumentException("Package source must be local.", nameof(value));
                }
                else if (!_localPackageSourcesComboBox.Items.Contains(value))
                {
                    throw new ArgumentException("Local package source not found in list.", nameof(value));
                }
                else
                {
                    _localPackageSourcesComboBox.SelectedItem = value;
                }
            }
        }

        public object? RemotePackageSource
        {
            get => _remotePackageSourcesComboBox.SelectedItem;
            set
            {
                if (value is NoPackageSource)
                {
                    _remotePackageSourcesComboBox.SelectedItem = value;
                }
                else if (value is PackageSource packageSource)
                {
                    if (packageSource.IsLocal)
                    {
                        throw new ArgumentException("Package source must be remote.", nameof(value));
                    }
                    else if (!_remotePackageSourcesComboBox.Items.Contains(packageSource))
                    {
                        throw new ArgumentException("Remote package source not found in list.", nameof(value));
                    }
                    else
                    {
                        _remotePackageSourcesComboBox.SelectedItem = value;
                    }
                }
                else
                {
                    _remotePackageSourcesComboBox.SelectedIndex = -1;
                }
            }
        }
    }
}