// <copyright file="TextImageRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;

/// <summary>
/// Renders RTL (right-to-left) shaped text into a bitmap using a private font collection.
/// Supports multiline wrapping and right alignment.
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
      this.font = new Font(SystemFonts.DefaultFont.FontFamily, fontSize, style);
      this.fallbackFontUsed = true;
    }
  }

  /// <summary>
  /// Renders RTL-shaped text into a bitmap.
  /// Text will wrap automatically and be right-aligned.
  /// </summary>
  /// <param name="text">The RTL text to render.</param>
  /// <param name="textColor">The color of the text.</param>
  /// <param name="backgroundColor">The background color.</param>
  /// <param name="maxWidth">Optional max width in pixels. If set, will cause line breaks.</param>
  /// <returns>A bitmap containing the shaped RTL text.</returns>
  public Bitmap RenderShapedText(string text, Color textColor, Color backgroundColor, int? maxWidth = null)
  {
    Size size = this.MeasureTextSize(text, maxWidth);
    Bitmap bitmap = new(size.Width, size.Height);
    using Graphics graphics = Graphics.FromImage(bitmap);

    graphics.Clear(backgroundColor);
    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

    using SolidBrush brush = new(textColor);
    using StringFormat format = new(StringFormatFlags.DirectionRightToLeft)
    {
      Alignment = StringAlignment.Far,
      LineAlignment = StringAlignment.Near,
      FormatFlags = StringFormatFlags.DirectionRightToLeft,
      Trimming = StringTrimming.Word,
    };

    RectangleF layoutRect = new(0, 0, size.Width, size.Height);
    graphics.DrawString(text, this.font, brush, layoutRect, format);

    return bitmap;
  }

  /// <summary>
  /// Gets a value indicating whether a fallback font was used due to font load failure.
  /// </summary>
  public bool FallbackFontUsed => this.fallbackFontUsed;

  /// <inheritdoc/>
  public void Dispose()
  {
    this.font.Dispose();
    this.fontCollection.Dispose();
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Measures the size of the given text when rendered with the current font.
  /// </summary>
  /// <param name="text"></param>
  /// <param name="maxWidth"></param>
  /// <returns></returns>
  private Size MeasureTextSize(string text, int? maxWidth = null)
  {
    using Bitmap dummy = new(1, 1);
    using Graphics graphics = Graphics.FromImage(dummy);

    using StringFormat format = new(StringFormatFlags.DirectionRightToLeft)
    {
      Alignment = StringAlignment.Far,
      LineAlignment = StringAlignment.Near,
      Trimming = StringTrimming.Word,
    };

    int layoutWidth = maxWidth ?? int.MaxValue;
    SizeF measured = graphics.MeasureString(text, this.font, layoutWidth, format);

    return new Size((int)Math.Ceiling(measured.Width), (int)Math.Ceiling(measured.Height));
  }
}
