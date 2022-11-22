// ------------------------------------------------------------------------------
// <copyright file="ControlExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Windows.Forms;

namespace NuGetPush.WinForms.Extensions
{
    public static class ControlExtensions
    {
        public static void AddControls(this Control control, params Control[] controls)
        {
            control.Controls.AddRange(controls);
        }
    }
}