﻿// ------------------------------------------------------------------------------
// <copyright file="DeviceLoginForm.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using NuGetPush.WinForms.Extensions;

namespace NuGetPush.WinForms.Forms
{
    [DesignerCategory("")]
    internal sealed class DeviceLoginForm : Form
    {
        private readonly RichTextBox _deviceLoginTextBox;
        private readonly Button _okButton;

        public DeviceLoginForm(string deviceLoginLine)
        {
            Size = new Size(400, 300);
            MinimumSize = new Size(400, 300);
            Text = "Authentication required";

            _deviceLoginTextBox = new RichTextBox
            {
                Text = $"{deviceLoginLine}{Environment.NewLine}When you are done, click OK to close this window.",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = true,
            };

            _okButton = new Button
            {
                Text = "OK",
                Dock = DockStyle.Bottom,
            };

            _okButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            this.AddControls(_deviceLoginTextBox, _okButton);
        }
    }
}