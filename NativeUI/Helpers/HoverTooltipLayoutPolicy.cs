// <copyright file="HoverTooltipLayoutPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Resolves shared layout values for plugin-managed hover tooltips.
/// </summary>
internal static class HoverTooltipLayoutPolicy
{
  private const float TooltipMinWidth = 240f;
  private const float TooltipViewportWidthFraction = 0.42f;
  private const float AdaptiveTooltipViewportWidthFraction = 0.60f;
  private const float MediumTextWidthMultiplier = 1.15f;
  private const float LongTextWidthMultiplier = 1.30f;
  private const float MaxAdaptiveWidthMultiplier = 1.35f;
  private const float MeasuredHeightToleranceFraction = 0.12f;
  private const int LongTextCharacterThreshold = 320;
  private const int VeryLongTextCharacterThreshold = 720;
  private const int LongTextWordThreshold = 48;
  private const int VeryLongTextWordThreshold = 96;
  private const int LongParagraphCharacterThreshold = 240;
  private const int VeryLongParagraphCharacterThreshold = 420;
  private const int VeryLongParagraphCountThreshold = 8;

  /// <summary>
  ///     Resolves the font scale used for texture-backed hover tooltips.
  /// </summary>
  /// <param name="config">The live plugin configuration.</param>
  /// <returns>The clamped font scale.</returns>
  public static float ResolveTextureFontScale(Config config)
  {
    return Math.Clamp(config.HoverTooltipFontScale, 0.25f, 3.0f);
  }

  /// <summary>
  ///     Resolves the maximum width used for texture-backed hover tooltips.
  /// </summary>
  /// <param name="config">The live plugin configuration.</param>
  /// <param name="viewportWidth">The current main-viewport width.</param>
  /// <returns>The clamped tooltip width.</returns>
  public static float ResolveTextureMaxWidth(
      Config config,
      float viewportWidth)
  {
    var widthCap = Math.Max(TooltipMinWidth, config.HoverTooltipMaxWidth);
    return Math.Clamp(
        viewportWidth * TooltipViewportWidthFraction,
        TooltipMinWidth,
        widthCap);
  }

  /// <summary>
  ///     Resolves the maximum width used for texture-backed hover tooltips,
  ///     including adaptive widening for long text and optional measured
  ///     candidate selection for very long text.
  /// </summary>
  /// <param name="config">The live plugin configuration.</param>
  /// <param name="viewportWidth">The current main-viewport width.</param>
  /// <param name="text">The tooltip text being laid out.</param>
  /// <param name="measureHeightAtWidth">
  /// Optional callback used to measure rendered text height at one candidate
  /// width. Only invoked for very long text.
  /// </param>
  /// <returns>The resolved tooltip width.</returns>
  public static float ResolveTextureMaxWidth(
      Config config,
      float viewportWidth,
      string? text,
      Func<float, float>? measureHeightAtWidth)
  {
    var baseWidth = ResolveTextureMaxWidth(config, viewportWidth);
    var textProfile = AnalyzeText(text);
    if (!textProfile.RequiresAdaptiveWidth)
    {
      return baseWidth;
    }

    var adaptiveCap = ResolveAdaptiveWidthCap(
        config,
        viewportWidth,
        baseWidth);
    var widenedWidth = Math.Min(
        adaptiveCap,
        baseWidth * LongTextWidthMultiplier);
    if (!textProfile.RequiresMeasuredCandidateSelection ||
        measureHeightAtWidth == null)
    {
      return widenedWidth;
    }

    var candidateWidths = BuildCandidateWidths(
        baseWidth,
        adaptiveCap);
    return SelectMeasuredWidth(
        candidateWidths,
        measureHeightAtWidth);
  }

  private static float ResolveAdaptiveWidthCap(
      Config config,
      float viewportWidth,
      float baseWidth)
  {
    var configuredCap = Math.Max(TooltipMinWidth, config.HoverTooltipMaxWidth);
    var viewportAdaptiveCap = Math.Max(
        baseWidth,
        viewportWidth * AdaptiveTooltipViewportWidthFraction);
    var configuredAdaptiveCap = Math.Max(
        baseWidth,
        configuredCap * MaxAdaptiveWidthMultiplier);
    return Math.Min(
        viewportAdaptiveCap,
        configuredAdaptiveCap);
  }

  private static IReadOnlyList<float> BuildCandidateWidths(
      float baseWidth,
      float adaptiveCap)
  {
    var candidateWidths = new List<float>(4);
    AddCandidateWidth(candidateWidths, baseWidth);
    AddCandidateWidth(
        candidateWidths,
        Math.Min(adaptiveCap, baseWidth * MediumTextWidthMultiplier));
    AddCandidateWidth(
        candidateWidths,
        Math.Min(adaptiveCap, baseWidth * LongTextWidthMultiplier));
    AddCandidateWidth(candidateWidths, adaptiveCap);
    return candidateWidths;
  }

  private static void AddCandidateWidth(
      ICollection<float> candidateWidths,
      float width)
  {
    var normalizedWidth = (float)Math.Round(
        width,
        3,
        MidpointRounding.AwayFromZero);
    foreach (var existingWidth in candidateWidths)
    {
      if (Math.Abs(existingWidth - normalizedWidth) < 0.01f)
      {
        return;
      }
    }

    candidateWidths.Add(normalizedWidth);
  }

  private static float SelectMeasuredWidth(
      IReadOnlyList<float> candidateWidths,
      Func<float, float> measureHeightAtWidth)
  {
    var measuredCandidates = new List<(float Width, float Height)>(
        candidateWidths.Count);
    foreach (var candidateWidth in candidateWidths)
    {
      var measuredHeight = measureHeightAtWidth(candidateWidth);
      if (float.IsNaN(measuredHeight) || float.IsInfinity(measuredHeight))
      {
        measuredHeight = float.MaxValue;
      }

      measuredCandidates.Add((candidateWidth, measuredHeight));
    }

    var bestMeasuredHeight = measuredCandidates.Min(candidate => candidate.Height);
    var toleratedHeight = bestMeasuredHeight * (1f + MeasuredHeightToleranceFraction);
    return measuredCandidates
        .Where(candidate => candidate.Height <= toleratedHeight)
        .OrderBy(candidate => candidate.Width)
        .First()
        .Width;
  }

  private static HoverTooltipTextProfile AnalyzeText(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return new HoverTooltipTextProfile(
          RequiresAdaptiveWidth: false,
          RequiresMeasuredCandidateSelection: false);
    }

    var normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
    var paragraphs = normalizedText.Split('\n');
    var paragraphCount = paragraphs.Length;
    var longestParagraphLength = 0;
    foreach (var paragraph in paragraphs)
    {
      longestParagraphLength = Math.Max(
          longestParagraphLength,
          paragraph.Trim().Length);
    }

    var words = normalizedText.Split(
        [' ', '\n', '\t'],
        StringSplitOptions.RemoveEmptyEntries);
    var totalCharacters = normalizedText.Length;
    var wordCount = words.Length;
    var requiresAdaptiveWidth =
        totalCharacters >= LongTextCharacterThreshold ||
        wordCount >= LongTextWordThreshold ||
        longestParagraphLength >= LongParagraphCharacterThreshold;
    var requiresMeasuredCandidateSelection =
        totalCharacters >= VeryLongTextCharacterThreshold ||
        wordCount >= VeryLongTextWordThreshold ||
        longestParagraphLength >= VeryLongParagraphCharacterThreshold ||
        paragraphCount >= VeryLongParagraphCountThreshold;
    return new HoverTooltipTextProfile(
        requiresAdaptiveWidth,
        requiresMeasuredCandidateSelection);
  }

  private sealed record HoverTooltipTextProfile(
      bool RequiresAdaptiveWidth,
      bool RequiresMeasuredCandidateSelection);
}
