// <copyright file="TextImageRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;

/// <summary>
/// Defines the bounded raster allocation contract for generated text textures.
/// </summary>
internal static class TextRasterLimits
{
  /// <summary>
  /// The maximum width or height of a generated text texture.
  /// </summary>
  public const int MaximumDimension = 2048;

  /// <summary>
  /// The maximum pixel area of a generated text texture.
  /// </summary>
  public const int MaximumArea = 2_097_152;

  /// <summary>
  /// The maximum byte budget for one cached text texture.
  /// </summary>
  public const long MaximumTextureBytes = 48L * 1024L * 1024L;

  /// <summary>
  /// The maximum source character count accepted for one measured layout.
  /// This bounds paragraph splitting and text-element analysis before raster
  /// limits can reject an oversized texture.
  /// </summary>
  public const int MaximumLayoutCharacters = 32_768;

  /// <summary>
  /// Clamps one wrapping request to the supported raster dimension.
  /// </summary>
  /// <param name="requestedWidth">The requested wrapping width.</param>
  /// <returns>The bounded effective wrapping width.</returns>
  public static int ClampWrapWidth(int? requestedWidth)
  {
    return Math.Clamp(
        requestedWidth.GetValueOrDefault(MaximumDimension),
        1,
        MaximumDimension);
  }

  /// <summary>
  /// Gets whether a measured layout can be rasterized within the approved
  /// dimension and area limits.
  /// </summary>
  /// <param name="size">The measured raster size.</param>
  /// <returns>Whether the size is safe to rasterize.</returns>
  public static bool IsWithinLimits(Size size)
  {
    return size.Width > 0 &&
        size.Height > 0 &&
        size.Width <= MaximumDimension &&
        size.Height <= MaximumDimension &&
        (long)size.Width * size.Height <= MaximumArea;
  }
}

/// <summary>
/// Renders shaped text into a bitmap using a private font collection.
/// Supports multiline wrapping and policy-selected text direction.
/// </summary>
public sealed class TextImageRenderer : IDisposable
{
  private readonly PrivateFontCollection fontCollection = new();
  private readonly Font font;
  private readonly bool fallbackFontUsed;
  private readonly float lineHeightScale;
  private readonly bool rightToLeft;

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
    : this(fontPath, fontSize, style, lineHeightScale, rightToLeft: true)
  {
  }

  /// <summary>
  /// Initializes a direction-aware text renderer.
  /// </summary>
  /// <param name="fontPath">The path to the TTF font file to use.</param>
  /// <param name="fontSize">The font size to use.</param>
  /// <param name="style">The font style to apply.</param>
  /// <param name="lineHeightScale">The relative multiline line height.</param>
  /// <param name="rightToLeft">
  /// Whether text uses right-to-left direction and far alignment.
  /// </param>
  internal TextImageRenderer(
      string fontPath,
      float fontSize,
      FontStyle style,
      float lineHeightScale,
      bool rightToLeft)
  {
    this.lineHeightScale = Math.Clamp(lineHeightScale, 0.8f, 1.2f);
    this.rightToLeft = rightToLeft;
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
  /// Renders shaped text into a bitmap using the configured direction.
  /// Text will wrap automatically and align to its logical leading edge.
  /// </summary>
  /// <param name="text">The shaped text to render.</param>
  /// <param name="textColor">The color of the text.</param>
  /// <param name="backgroundColor">The background color.</param>
  /// <param name="maxWidth">Optional max width in pixels. If set, will cause line breaks.</param>
  /// <returns>A bitmap containing the shaped RTL text.</returns>
  public Bitmap RenderShapedText(string text, Color textColor, Color backgroundColor, int? maxWidth = null)
  {
    var layout = this.CreateTextLayout(text, maxWidth);
    return this.RenderTextLayout(layout, textColor, backgroundColor);
  }

  /// <summary>
  /// Renders a previously measured text layout without repeating layout work.
  /// </summary>
  /// <param name="layout">The measured text layout to rasterize.</param>
  /// <param name="textColor">The color of the text.</param>
  /// <param name="backgroundColor">The background color.</param>
  /// <returns>A bitmap containing the shaped text.</returns>
  internal Bitmap RenderTextLayout(
      TextRasterLayout layout,
      Color textColor,
      Color backgroundColor)
  {
    var measuredSize = new Size(layout.Width, layout.Height);
    if (!TextRasterLimits.IsWithinLimits(measuredSize))
    {
      throw new InvalidOperationException(
          "Text layout exceeds the bounded raster allocation limits.");
    }

    Bitmap bitmap = new(layout.Width, layout.Height);
    using Graphics graphics = Graphics.FromImage(bitmap);
    using StringFormat format = CreateStringFormat(this.rightToLeft);

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
  /// <param name="text">The shaped text to measure.</param>
  /// <param name="maxWidth">Optional max width in pixels used for wrapping.</param>
  /// <returns>The measured bitmap size.</returns>
  public Size MeasureShapedText(string text, int? maxWidth = null)
  {
    var layout = this.CreateTextLayout(text, maxWidth);
    return new Size(layout.Width, layout.Height);
  }

  /// <summary>
  /// Creates the bounded text layout shared by measurement and rasterization.
  /// </summary>
  /// <param name="text">The shaped text to layout.</param>
  /// <param name="maxWidth">Optional max width in pixels used for wrapping.</param>
  /// <returns>The measured text layout.</returns>
  internal TextRasterLayout CreateTextLayout(string text, int? maxWidth = null)
  {
    if (text.Length > TextRasterLimits.MaximumLayoutCharacters)
    {
      throw new InvalidOperationException(
          "Text exceeds the bounded layout character limit.");
    }

    using Bitmap dummy = new(1, 1);
    using Graphics measuringGraphics = Graphics.FromImage(dummy);
    measuringGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
    using StringFormat format = CreateStringFormat(this.rightToLeft);
    var layout = this.BuildTextLayout(
        measuringGraphics,
        format,
        text,
        maxWidth);
    return layout;
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
  private TextRasterLayout BuildTextLayout(
      Graphics graphics,
      StringFormat format,
      string text,
      int? maxWidth)
  {
    var effectiveMaxWidth = TextRasterLimits.ClampWrapWidth(maxWidth);
    var baseLineHeight = this.font.GetHeight(graphics);
    var lineAdvance = Math.Max(1f, baseLineHeight * this.lineHeightScale);
    var maximumLineCount = CalculateMaximumLineCount(
        baseLineHeight,
        lineAdvance);
    var resolvedLines = this.ResolveWrappedLines(
        graphics,
        format,
        text,
        effectiveMaxWidth,
        maximumLineCount);
    var lines = new List<TextRasterLayoutLine>(resolvedLines.Count);
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
      var nextWidth = Math.Max(
          1,
          (int)Math.Ceiling(Math.Max(maxMeasuredWidth, measuredWidth)));
      var nextHeight = Math.Max(
          1,
          (int)Math.Ceiling(currentTop + measuredHeight));
      if (!TextRasterLimits.IsWithinLimits(new Size(nextWidth, nextHeight)))
      {
        throw new InvalidOperationException(
            "Text layout exceeds the bounded raster allocation limits.");
      }

      lines.Add(
          new TextRasterLayoutLine(
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
    return new TextRasterLayout(
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
  /// <param name="maxWidth">The bounded wrap width.</param>
  /// <returns>The ordered line collection.</returns>
  private List<string> ResolveWrappedLines(
      Graphics graphics,
      StringFormat format,
      string text,
      int maxWidth,
      int maximumLineCount)
  {
    var normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
    var paragraphs = normalizedText.Split('\n');
    var lines = new List<string>();

    foreach (var paragraph in paragraphs)
    {
      if (string.IsNullOrWhiteSpace(paragraph))
      {
        AddResolvedLine(lines, string.Empty, maximumLineCount);
        continue;
      }

      var words = paragraph.Split(
          ' ',
          StringSplitOptions.RemoveEmptyEntries);
      var currentLine = string.Empty;

      foreach (var word in words)
      {
        foreach (var segment in this.SplitOverwideWord(
                     graphics,
                     format,
                     word,
                     maxWidth))
        {
          var candidate = string.IsNullOrEmpty(currentLine)
              ? segment
              : $"{currentLine} {segment}";
          var candidateWidth = graphics.MeasureString(
              candidate,
              this.font,
              int.MaxValue,
              format).Width;

          if (!string.IsNullOrEmpty(currentLine) &&
              candidateWidth > maxWidth)
          {
            AddResolvedLine(lines, currentLine, maximumLineCount);
            currentLine = segment;
            continue;
          }

          currentLine = candidate;
        }
      }

      AddResolvedLine(lines, currentLine, maximumLineCount);
    }

    return lines.Count == 0
        ? [string.Empty]
        : lines;
  }

  /// <summary>
  ///     Adds one wrapped line while enforcing the maximum height-compatible
  ///     layout count before the complete source is materialized in memory.
  /// </summary>
  /// <param name="lines">The accumulated resolved lines.</param>
  /// <param name="line">The resolved line to add.</param>
  /// <param name="maximumLineCount">The height-compatible line count limit.</param>
  private static void AddResolvedLine(
      List<string> lines,
      string line,
      int maximumLineCount)
  {
    if (lines.Count >= maximumLineCount)
    {
      throw new InvalidOperationException(
          "Text layout exceeds the bounded raster allocation limits.");
    }

    lines.Add(line);
  }

  /// <summary>
  ///     Calculates the number of lines that can fit within the raster height
  ///     limit without constructing a texture layout that cannot be drawn.
  /// </summary>
  /// <param name="baseLineHeight">The measured line glyph height.</param>
  /// <param name="lineAdvance">The configured distance between line origins.</param>
  /// <returns>The maximum number of drawable lines.</returns>
  private static int CalculateMaximumLineCount(
      float baseLineHeight,
      float lineAdvance)
  {
    if (baseLineHeight > TextRasterLimits.MaximumDimension)
    {
      return 0;
    }

    return Math.Max(
        1,
        1 + (int)Math.Floor(
            (TextRasterLimits.MaximumDimension - baseLineHeight) /
            lineAdvance));
  }

  /// <summary>
  /// Splits an unbroken word at text-element boundaries when it cannot fit the
  /// requested raster width.
  /// </summary>
  /// <param name="graphics">The graphics context used for measurement.</param>
  /// <param name="format">The string format used for measurement.</param>
  /// <param name="word">The word to split.</param>
  /// <param name="maxWidth">The bounded maximum line width.</param>
  /// <returns>The bounded word segments.</returns>
  private IEnumerable<string> SplitOverwideWord(
      Graphics graphics,
      StringFormat format,
      string word,
      int maxWidth)
  {
    if (graphics.MeasureString(word, this.font, int.MaxValue, format).Width <=
        maxWidth)
    {
      yield return word;
      yield break;
    }

    var textElementStarts = StringInfo.ParseCombiningCharacters(word);
    var startElementIndex = 0;
    while (startElementIndex < textElementStarts.Length)
    {
      var low = startElementIndex + 1;
      var high = textElementStarts.Length;
      var bestEndElementIndex = startElementIndex;
      while (low <= high)
      {
        var candidateEndElementIndex = low + ((high - low) / 2);
        var candidate = this.GetTextElementSegment(
            word,
            textElementStarts,
            startElementIndex,
            candidateEndElementIndex);
        if (graphics.MeasureString(candidate, this.font, int.MaxValue, format).Width <=
            maxWidth)
        {
          bestEndElementIndex = candidateEndElementIndex;
          low = candidateEndElementIndex + 1;
        }
        else
        {
          high = candidateEndElementIndex - 1;
        }
      }

      if (bestEndElementIndex == startElementIndex)
      {
        bestEndElementIndex++;
      }

      yield return this.GetTextElementSegment(
          word,
          textElementStarts,
          startElementIndex,
          bestEndElementIndex);
      startElementIndex = bestEndElementIndex;
    }
  }

  /// <summary>
  /// Gets one segment that starts and ends on text-element boundaries.
  /// </summary>
  /// <param name="word">The source word.</param>
  /// <param name="textElementStarts">The text-element start offsets.</param>
  /// <param name="startElementIndex">The inclusive start element index.</param>
  /// <param name="endElementIndex">The exclusive end element index.</param>
  /// <returns>The requested text-element segment.</returns>
  private string GetTextElementSegment(
      string word,
      IReadOnlyList<int> textElementStarts,
      int startElementIndex,
      int endElementIndex)
  {
    var startCharacterIndex = textElementStarts[startElementIndex];
    var endCharacterIndex = endElementIndex < textElementStarts.Count
        ? textElementStarts[endElementIndex]
        : word.Length;
    return word.Substring(
        startCharacterIndex,
        endCharacterIndex - startCharacterIndex);
  }

  /// <summary>
  /// Creates the string format used for manual line measurement and draw.
  /// </summary>
  /// <param name="rightToLeft">
  /// Whether text uses right-to-left direction and far alignment.
  /// </param>
  /// <returns>The configured string format.</returns>
  internal static StringFormat CreateStringFormat(bool rightToLeft)
  {
    var formatFlags = StringFormatFlags.NoWrap;
    if (rightToLeft)
    {
      formatFlags |= StringFormatFlags.DirectionRightToLeft;
    }

    return new StringFormat(formatFlags)
    {
      Alignment = rightToLeft
          ? StringAlignment.Far
          : StringAlignment.Near,
      LineAlignment = StringAlignment.Near,
      FormatFlags = formatFlags,
      Trimming = StringTrimming.Word,
    };
  }

  /// <summary>
  /// Represents a measured text layout that can be rasterized without
  /// remeasuring its wrapped lines.
  /// </summary>
  /// <param name="Width">The measured layout width.</param>
  /// <param name="Height">The measured layout height.</param>
  /// <param name="Lines">The ordered measured layout lines.</param>
  internal sealed record TextRasterLayout(
      int Width,
      int Height,
      IReadOnlyList<TextRasterLayoutLine> Lines);

  /// <summary>
  /// Represents one measured line in a text raster layout.
  /// </summary>
  /// <param name="Text">The line text.</param>
  /// <param name="Top">The line's top coordinate.</param>
  /// <param name="Height">The measured line height.</param>
  internal sealed record TextRasterLayoutLine(
      string Text,
      float Top,
      float Height);
}
