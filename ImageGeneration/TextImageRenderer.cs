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
  private readonly float lineHeightScale;

  /// <summary>
  /// Initializes a new instance of the <see cref="TextImageRenderer"/> class.
  /// </summary>
  /// <param name="fontPath">The path to the TTF font file to use.</param>
  /// <param name="fontSize">The font size to use.</param>
  /// <param name="style">The font style to apply (bold, italic, etc.).</param>
  /// <param name="lineHeightScale">
  /// The relative line-height scale used for multiline layout.
  /// </param>
  public TextImageRenderer(
      string fontPath,
      float fontSize,
      FontStyle style = FontStyle.Regular,
      float lineHeightScale = 1.0f)
  {
    this.lineHeightScale = Math.Clamp(lineHeightScale, 0.8f, 1.2f);
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
    using Bitmap dummy = new(1, 1);
    using Graphics measuringGraphics = Graphics.FromImage(dummy);
    measuringGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
    using StringFormat format = CreateStringFormat();
    var layout = this.BuildTextLayout(
        measuringGraphics,
        format,
        text,
        maxWidth);
    Bitmap bitmap = new(layout.Width, layout.Height);
    using Graphics graphics = Graphics.FromImage(bitmap);

    graphics.Clear(backgroundColor);
    graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

    using SolidBrush brush = new(textColor);
    foreach (var line in layout.Lines)
    {
      if (line.Text.Length == 0)
      {
        continue;
      }

      RectangleF layoutRect = new(
          0f,
          line.Top,
          layout.Width,
          line.Height);
      graphics.DrawString(line.Text, this.font, brush, layoutRect, format);
    }

    return bitmap;
  }

  /// <summary>
  /// Measures the bitmap size required to render the provided shaped text.
  /// </summary>
  /// <param name="text">The RTL text to measure.</param>
  /// <param name="maxWidth">Optional max width in pixels used for wrapping.</param>
  /// <returns>The measured bitmap size.</returns>
  public Size MeasureShapedText(string text, int? maxWidth = null)
  {
    using Bitmap dummy = new(1, 1);
    using Graphics measuringGraphics = Graphics.FromImage(dummy);
    measuringGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
    using StringFormat format = CreateStringFormat();
    var layout = this.BuildTextLayout(
        measuringGraphics,
        format,
        text,
        maxWidth);
    return new Size(layout.Width, layout.Height);
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
  /// Builds the wrapped line layout used for both measuring and drawing.
  /// </summary>
  /// <param name="graphics">The measurement graphics context.</param>
  /// <param name="format">The string format used for measurement.</param>
  /// <param name="text">The text to layout.</param>
  /// <param name="maxWidth">The optional wrap width.</param>
  /// <returns>The resolved text layout.</returns>
  private TextLayout BuildTextLayout(
      Graphics graphics,
      StringFormat format,
      string text,
      int? maxWidth)
  {
    var resolvedLines = this.ResolveWrappedLines(
        graphics,
        format,
        text,
        maxWidth);
    var baseLineHeight = this.font.GetHeight(graphics);
    var lineAdvance = Math.Max(1f, baseLineHeight * this.lineHeightScale);
    var lines = new List<TextLayoutLine>(resolvedLines.Count);
    var maxMeasuredWidth = 1f;
    var currentTop = 0f;

    foreach (var resolvedLine in resolvedLines)
    {
      var measuredSize = resolvedLine.Length == 0
          ? new SizeF(0f, baseLineHeight)
          : graphics.MeasureString(
              resolvedLine,
              this.font,
              int.MaxValue,
              format);
      var measuredWidth = Math.Max(1f, measuredSize.Width);
      var measuredHeight = Math.Max(baseLineHeight, measuredSize.Height);
      lines.Add(
          new TextLayoutLine(
              resolvedLine,
              currentTop,
              measuredHeight));
      maxMeasuredWidth = Math.Max(maxMeasuredWidth, measuredWidth);
      currentTop += lineAdvance;
    }

    var totalHeight = lines.Count == 0
        ? (int)Math.Ceiling(baseLineHeight)
        : (int)Math.Ceiling(lines[^1].Top + lines[^1].Height);
    var totalWidth = Math.Max(1, (int)Math.Ceiling(maxMeasuredWidth));
    return new TextLayout(
        totalWidth,
        Math.Max(1, totalHeight),
        lines);
  }

  /// <summary>
  /// Resolves wrapped text lines for manual multiline layout.
  /// </summary>
  /// <param name="graphics">The measurement graphics context.</param>
  /// <param name="format">The string format used for measurement.</param>
  /// <param name="text">The text to wrap.</param>
  /// <param name="maxWidth">The optional wrap width.</param>
  /// <returns>The ordered line collection.</returns>
  private List<string> ResolveWrappedLines(
      Graphics graphics,
      StringFormat format,
      string text,
      int? maxWidth)
  {
    var normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
    var paragraphs = normalizedText.Split('\n');
    var lines = new List<string>();

    foreach (var paragraph in paragraphs)
    {
      if (!maxWidth.HasValue || maxWidth.Value <= 0)
      {
        lines.Add(paragraph);
        continue;
      }

      if (string.IsNullOrWhiteSpace(paragraph))
      {
        lines.Add(string.Empty);
        continue;
      }

      var words = paragraph.Split(
          ' ',
          StringSplitOptions.RemoveEmptyEntries);
      var currentLine = string.Empty;

      foreach (var word in words)
      {
        var candidate = string.IsNullOrEmpty(currentLine)
            ? word
            : $"{currentLine} {word}";
        var candidateWidth = graphics.MeasureString(
            candidate,
            this.font,
            int.MaxValue,
            format).Width;

        if (!string.IsNullOrEmpty(currentLine) &&
            candidateWidth > maxWidth.Value)
        {
          lines.Add(currentLine);
          currentLine = word;
          continue;
        }

        currentLine = candidate;
      }

      lines.Add(currentLine);
    }

    return lines.Count == 0
        ? [string.Empty]
        : lines;
  }

  /// <summary>
  /// Creates the string format used for manual RTL line measurement and draw.
  /// </summary>
  /// <returns>The configured string format.</returns>
  private static StringFormat CreateStringFormat()
  {
    return new StringFormat(StringFormatFlags.DirectionRightToLeft)
    {
      Alignment = StringAlignment.Far,
      LineAlignment = StringAlignment.Near,
      FormatFlags =
          StringFormatFlags.DirectionRightToLeft |
          StringFormatFlags.NoWrap,
      Trimming = StringTrimming.Word,
    };
  }

  private sealed record TextLayout(
      int Width,
      int Height,
      IReadOnlyList<TextLayoutLine> Lines);

  private sealed record TextLayoutLine(
      string Text,
      float Top,
      float Height);
}
