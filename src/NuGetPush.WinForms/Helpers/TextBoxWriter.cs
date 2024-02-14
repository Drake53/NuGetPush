// ------------------------------------------------------------------------------
// <copyright file="TextBoxWriter.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NuGetPush.WinForms.Helpers
{
    public class TextBoxWriter : TextWriter
    {
        private readonly TextBoxBase _textBox;
        private readonly Encoding _encoding;

        public TextBoxWriter(
            TextBoxBase textBox,
            Encoding? encoding = null)
        {
            _textBox = textBox;
            _encoding = encoding ?? Encoding.Default;
        }

        public override Encoding Encoding => _encoding;

        public override void Write(string? value)
        {
            void AppendString()
            {
                _textBox.AppendText(value);
            }

            if (value is not null)
            {
                if (_textBox.InvokeRequired)
                {
                    _textBox.Invoke(AppendString);
                }
                else
                {
                    AppendString();
                }
            }
        }

        public override void Write(char value)
        {
            void AppendChar()
            {
                _textBox.AppendText(value.ToString());
            }

            if (_textBox.InvokeRequired)
            {
                _textBox.Invoke(AppendChar);
            }
            else
            {
                AppendChar();
            }
        }
    }
}