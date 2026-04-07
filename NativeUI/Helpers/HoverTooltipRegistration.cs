// <copyright file="HoverTooltipRegistration.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian;

public partial class Echoglossian
{
  /// <summary>
  /// Registers a hover tooltip for a text node using its current screen bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="textNode">The text node to anchor the tooltip to.</param>
  /// <param name="title">Tooltip title.</param>
  /// <param name="body">Tooltip body text.</param>
  private unsafe void RegisterHoverTooltip(
      string key,
      AtkTextNode* textNode,
      string title,
      string body,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    if (textNode == null || !textNode->IsVisible())
    {
      return;
    }

    var left = textNode->ScreenX;
    var top = textNode->ScreenY;
    var right = left + Math.Max(1f, textNode->GetWidth());
    var bottom = top + Math.Max(1f, textNode->GetHeight());

    var widthPadding = denseHitbox
        ? Math.Clamp(textNode->GetWidth() * 0.15f, 18f, 40f)
        : Math.Clamp(textNode->GetWidth() * 0.08f, 12f, 24f);
    var heightPadding = denseHitbox
        ? Math.Clamp(textNode->GetHeight() * 0.45f, 10f, 22f)
        : Math.Clamp(textNode->GetHeight() * 0.3f, 8f, 14f);
    left -= widthPadding;
    top -= heightPadding;
    right += widthPadding;
    bottom += heightPadding;

    this.hoverTooltipManager.Register(
        key,
        new Vector2(left, top),
        new Vector2(right, bottom),
        title,
        body,
        true);
  }

  /// <summary>
  /// Registers a hover tooltip for a whole addon window using its root node.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="addon">The live addon window to anchor the tooltip to.</param>
  /// <param name="title">Tooltip title.</param>
  /// <param name="body">Tooltip body text.</param>
  private unsafe void RegisterHoverTooltip(
      string key,
      AtkUnitBase* addon,
      string title,
      string body,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    if (addon == null || !addon->IsVisible || addon->UldManager.RootNode == null)
    {
      return;
    }

    var rootNode = addon->UldManager.RootNode;
    if (!rootNode->IsVisible())
    {
      return;
    }

    var left = rootNode->X;
    var top = rootNode->Y;
    var right = left + Math.Max(1f, rootNode->Width * addon->Scale);
    var bottom = top + Math.Max(1f, rootNode->Height * addon->Scale);
    if (denseHitbox)
    {
      left -= 24f;
      right += 24f;
      top -= 16f;
      bottom += 16f;
    }

    this.hoverTooltipManager.Register(
        key,
        new Vector2(left, top),
        new Vector2(right, bottom),
        title,
        body,
        true);
  }

  /// <summary>
  /// Registers a hover tooltip for a text node using its translated and
  /// original text, swapping the visible content when swap mode is active.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="textNode">The text node to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  private unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkTextNode* textNode,
      string originalText,
      string translatedText,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    var shouldSwap = swapEnabled ?? this.configuration.SwapTextsUsingImGui;
    var displayText = shouldSwap
        ? originalText
        : translatedText;
    if (string.IsNullOrWhiteSpace(displayText))
    {
      displayText = shouldSwap
          ? translatedText
          : originalText;
    }

    if (string.IsNullOrWhiteSpace(displayText))
    {
      return;
    }

    this.RegisterHoverTooltip(
        key,
        textNode,
        string.Empty,
        displayText,
        forceEnabled,
        denseHitbox);
  }

  /// <summary>
  /// Registers a hover tooltip for a whole addon window using translated and
  /// original text, swapping the visible content when swap mode is active.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="addon">The live addon window to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  private unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkUnitBase* addon,
      string originalText,
      string translatedText,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    var shouldSwap = swapEnabled ?? this.configuration.SwapTextsUsingImGui;
    var displayText = shouldSwap
        ? originalText
        : translatedText;
    if (string.IsNullOrWhiteSpace(displayText))
    {
      displayText = shouldSwap
          ? translatedText
          : originalText;
    }

    if (string.IsNullOrWhiteSpace(displayText))
    {
      return;
    }

    this.RegisterHoverTooltip(
        key,
        addon,
        string.Empty,
        displayText,
        forceEnabled,
        denseHitbox);
  }

  private bool JournalUsesNativeTranslation =>
      this.configuration.JournalTranslationDisplayMode !=
      JournalTranslationDisplayMode.TooltipTranslation;

  private bool JournalWritesNativeTranslation =>
      this.JournalUsesNativeTranslation;

  private bool JournalUsesHoverTooltips =>
      this.configuration.JournalTranslationDisplayMode !=
      JournalTranslationDisplayMode.NativeUiTranslation;

  private bool JournalHoverShowsOriginal =>
      this.configuration.JournalTranslationDisplayMode ==
      JournalTranslationDisplayMode
          .NativeUiTranslationWithOriginalTooltips;
}
