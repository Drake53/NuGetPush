// ------------------------------------------------------------------------------
// <copyright file="CredentialsForm.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using NuGetPush.Models;
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

        public CredentialsForm(RemotePackageSource packageSource)
        {
            Size = new Size(400, 300);
            MinimumSize = new Size(400, 300);
            Text = $"Enter credentials for: {packageSource.PackageSource.Name}";

            _userNameTextBox = new TextBox
            {
                PlaceholderText = "User name...",
                Dock = DockStyle.Top,
            };

            _accessTokenTextBox = new TextBox
            {
                PlaceholderText = "Access token...",
                PasswordChar = '*',
                Dock = DockStyle.Top,
            };

            _okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Bottom,
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Dock = DockStyle.Bottom,
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