// <copyright file="UniscribeTextRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;

public static class UniscribeTextRenderer
{
  public static Bitmap RenderShapedText(string text, Font font, Color textColor, Color backColor)
  {
    var size = MeasureTextSize(text, font);
    Bitmap bmp = new(size.Width, size.Height);
    using var g = Graphics.FromImage(bmp);
    g.Clear(backColor);
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

    var format = new StringFormat(StringFormatFlags.DirectionRightToLeft);
    g.DrawString(text, font, new SolidBrush(textColor), new RectangleF(0, 0, size.Width, size.Height), format);

    return bmp;
  }

  private static Size MeasureTextSize(string text, Font font)
  {
    using Bitmap dummy = new(1, 1);
    using Graphics g = Graphics.FromImage(dummy);
    var sizeF = g.MeasureString(text, font, int.MaxValue, new StringFormat(StringFormatFlags.DirectionRightToLeft));
    return new Size((int)Math.Ceiling(sizeF.Width), (int)Math.Ceiling(sizeF.Height));
  }
}
