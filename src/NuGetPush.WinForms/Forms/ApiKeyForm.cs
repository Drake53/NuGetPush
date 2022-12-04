// ------------------------------------------------------------------------------
// <copyright file="ApiKeyForm.cs" company="Drake53">
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
    internal sealed class ApiKeyForm : Form
    {
        private readonly TextBox _apiKeyTextBox;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public ApiKeyForm(RemotePackageSource packageSource)
        {
            Size = new Size(400, 300);
            MinimumSize = new Size(400, 300);
            Text = $"Enter API key for: {packageSource.PackageSource.Name}";

            _apiKeyTextBox = new TextBox
            {
                PlaceholderText = "API key...",
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

            this.AddControls(_apiKeyTextBox, _okButton, _cancelButton);
        }

        public string ApiKey => _apiKeyTextBox.Text;
    }
}