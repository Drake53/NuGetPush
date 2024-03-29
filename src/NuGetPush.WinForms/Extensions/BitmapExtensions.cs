﻿// ------------------------------------------------------------------------------
// <copyright file="BitmapExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Drawing;

namespace NuGetPush.WinForms.Extensions
{
    public static class BitmapExtensions
    {
        public static void SetSolidColor(this Bitmap bitmap, Color color)
        {
            // https://stackoverflow.com/questions/1720160/how-do-i-fill-a-bitmap-with-a-solid-color
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(color);
            }
        }

        public static Bitmap WithSolidColor(this Bitmap bitmap, Color color)
        {
            bitmap.SetSolidColor(color);
            return bitmap;
        }
    }
}