// <copyright file="TextImageRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License.
// </copyright>

namespace Echoglossian.ImageGeneration;

/// <summary>
/// Renders RTL or styled LTR text into a bitmap using a private font collection.
/// </summary>
public sealed class TextImageRenderer : IDisposable
{
  private readonly PrivateFontCollection fontCollection = new();
  private readonly Font font;
  private readonly bool fallbackFontUsed;

  /// <summary>
  /// Initializes a new instance of the <see cref="TextImageRenderer"/> class.
  /// </summary>
  /// <param name="fontPath">The path to the TTF font file to use.</param>
  /// <param name="fontSize">The font size to use.</param>
  /// <param name="style">The font style to apply (bold, italic, etc.).</param>
  public TextImageRenderer(string fontPath, float fontSize, FontStyle style = FontStyle.Regular)
  {
    try
    {
      this.fontCollection.AddFontFile(fontPath);
      this.font = new Font(this.fontCollection.Families[0], fontSize, style);
      this.fallbackFontUsed = false;
    }
    catch (Exception)
    {
      // Fall back to default UI font
      this.font = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, style);
      this.fallbackFontUsed = true;
    }
  }

  /// <summary>
  /// Renders shaped or LTR text into a bitmap.
  /// </summary>
  /// <param name="text">The text to render.</param>
  /// <param name="textColor">The color of the text.</param>
  /// <param name="backgroundColor">The background color.</param>
  /// <returns>A bitmap image containing the shaped text.</returns>
  public Bitmap RenderShapedText(string text, Color textColor, Color backgroundColor)
  {
    Size size = this.MeasureTextSize(text);
    Bitmap bitmap = new(size.Width, size.Height);
    using Graphics graphics = Graphics.FromImage(bitmap);

    graphics.Clear(backgroundColor);
    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

    using SolidBrush brush = new(textColor);
    using StringFormat format = new(StringFormatFlags.DirectionRightToLeft);

    graphics.DrawString(text, this.font, brush, new RectangleF(0, 0, size.Width, size.Height), format);
    return bitmap;
  }

  /// <summary>
  /// Gets a value indicating whether fallback font was used.
  /// </summary>
  public bool FallbackFontUsed => this.fallbackFontUsed;

  /// <inheritdoc/>
  public void Dispose()
  {
    this.font.Dispose();
    this.fontCollection.Dispose();
    GC.SuppressFinalize(this);
  }

  private Size MeasureTextSize(string text)
  {
    using Bitmap dummy = new(1, 1);
    using Graphics graphics = Graphics.FromImage(dummy);
    using StringFormat format = new(StringFormatFlags.DirectionRightToLeft);

    SizeF sizeF = graphics.MeasureString(text, this.font, int.MaxValue, format);
    return new Size((int)Math.Ceiling(sizeF.Width), (int)Math.Ceiling(sizeF.Height));
  }
}
