// ------------------------------------------------------------------------------
// <copyright file="CredentialsForm.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using NuGet.Configuration;

using NuGetPush.WinForms.Extensions;

namespace NuGetPush.WinForms.Forms
{
    [DesignerCategory("")]
    internal sealed class CredentialsForm : Form
    {
        private readonly TextBox _userNameTextBox;
        private readonly TextBox _accessTokenTextBox;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public CredentialsForm(PackageSource packageSource)
        {
            if (packageSource.IsLocal)
            {
                throw new ArgumentException("PackageSource must be remote.", nameof(packageSource));
            }

            Size = new Size(400, 300);
            MinimumSize = new Size(400, 300);
            Text = $"Enter credentials for: {packageSource.Name}";

            _userNameTextBox = new TextBox
            {
                PlaceholderText = "User name...",
                Dock = DockStyle.Top,
                TabIndex = 0,
            };

            _accessTokenTextBox = new TextBox
            {
                PlaceholderText = "Access token...",
                PasswordChar = '*',
                Dock = DockStyle.Top,
                TabIndex = 1,
            };

            _okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Bottom,
                TabIndex = 2,
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Dock = DockStyle.Bottom,
                TabIndex = 3,
            };

            _okButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            _cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            this.AddControls(_accessTokenTextBox, _userNameTextBox, _okButton, _cancelButton);
        }

        public string UserName => _userNameTextBox.Text;

        public string AccessToken => _accessTokenTextBox.Text;
    }
}