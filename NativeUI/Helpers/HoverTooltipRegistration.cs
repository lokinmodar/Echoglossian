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
      bool denseHitbox = false,
      bool useGeneralFont = false)
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
        true,
        useGeneralFont);
  }

  /// <summary>
  /// Registers a hover tooltip for a generic node using its current screen
  /// bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="node">The node to anchor the tooltip to.</param>
  /// <param name="title">Tooltip title.</param>
  /// <param name="body">Tooltip body text.</param>
  private unsafe void RegisterHoverTooltip(
      string key,
      AtkResNode* node,
      string title,
      string body,
      bool forceEnabled = false,
      bool denseHitbox = false,
      bool useGeneralFont = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    if (node == null || !node->IsVisible())
    {
      return;
    }

    var left = node->ScreenX;
    var top = node->ScreenY;
    var right = left + Math.Max(1f, node->Width);
    var bottom = top + Math.Max(1f, node->Height);
    if (denseHitbox)
    {
      left -= 8f;
      right += 8f;
      top -= 4f;
      bottom += 4f;
    }

    this.hoverTooltipManager.Register(
        key,
        new Vector2(left, top),
        new Vector2(right, bottom),
        title,
        body,
        true,
        useGeneralFont);
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
      bool denseHitbox = false,
      bool useGeneralFont = false)
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

    var left = rootNode->ScreenX;
    var top = rootNode->ScreenY;
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
        true,
        useGeneralFont);
  }

  /// <summary>
  /// Registers a hover tooltip using explicit screen bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="topLeft">Top-left screen coordinate.</param>
  /// <param name="bottomRight">Bottom-right screen coordinate.</param>
  /// <param name="title">Tooltip title.</param>
  /// <param name="body">Tooltip body text.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  private void RegisterHoverTooltip(
      string key,
      Vector2 topLeft,
      Vector2 bottomRight,
      string title,
      string body,
      bool forceEnabled = false,
      bool useGeneralFont = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    this.hoverTooltipManager.Register(
        key,
        topLeft,
        bottomRight,
        title,
        body,
        true,
        useGeneralFont);
  }

  /// <summary>
  /// Registers a hover tooltip for a text node using its translated and
  /// original text, swapping the visible content when swap mode is active.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="textNode">The text node to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  private unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkTextNode* textNode,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    if (!translatedPayloadReady)
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    var shouldSwap = swapEnabled ?? this.configuration.SwapTextsUsingImGui;
    var displayText = shouldSwap
        ? originalText
        : translatedText;

    if (string.IsNullOrWhiteSpace(displayText))
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    this.RegisterHoverTooltip(
        key,
        textNode,
        string.Empty,
        displayText,
        forceEnabled,
        denseHitbox,
        useGeneralFont: shouldSwap);
  }

  /// <summary>
  /// Registers a translated hover tooltip using a generic node's screen bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="node">The node to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  /// <param name="swapEnabled">Optional explicit swap override.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  private unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkResNode* node,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    if (!translatedPayloadReady)
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    var shouldSwap = swapEnabled ?? this.configuration.SwapTextsUsingImGui;
    var displayText = shouldSwap
        ? originalText
        : translatedText;

    if (string.IsNullOrWhiteSpace(displayText))
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    this.RegisterHoverTooltip(
        key,
        node,
        string.Empty,
        displayText,
        forceEnabled,
        denseHitbox,
        useGeneralFont: shouldSwap);
  }

  /// <summary>
  /// Registers a hover tooltip for a whole addon window using translated and
  /// original text, swapping the visible content when swap mode is active.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="addon">The live addon window to anchor the tooltip to.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  private unsafe void RegisterTranslatedHoverTooltip(
      string key,
      AtkUnitBase* addon,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false,
      bool denseHitbox = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    if (!translatedPayloadReady)
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    var shouldSwap = swapEnabled ?? this.configuration.SwapTextsUsingImGui;
    var displayText = shouldSwap
        ? originalText
        : translatedText;

    if (string.IsNullOrWhiteSpace(displayText))
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    this.RegisterHoverTooltip(
        key,
        addon,
        string.Empty,
        displayText,
        forceEnabled,
        denseHitbox,
        useGeneralFont: shouldSwap);
  }

  /// <summary>
  /// Registers a translated hover tooltip using explicit screen bounds.
  /// </summary>
  /// <param name="key">Stable key used to refresh the tooltip target.</param>
  /// <param name="topLeft">Top-left screen coordinate.</param>
  /// <param name="bottomRight">Bottom-right screen coordinate.</param>
  /// <param name="originalText">The original visible text.</param>
  /// <param name="translatedText">The translated text.</param>
  /// <param name="translatedPayloadReady">
  /// Whether the tooltip payload required by the current mode is ready.
  /// </param>
  /// <param name="swapEnabled">Optional explicit swap override.</param>
  /// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
  private void RegisterTranslatedHoverTooltip(
      string key,
      Vector2 topLeft,
      Vector2 bottomRight,
      string originalText,
      string translatedText,
      bool translatedPayloadReady = true,
      bool? swapEnabled = null,
      bool forceEnabled = false)
  {
    if (!forceEnabled && !this.configuration.TranslateTooltips)
    {
      return;
    }

    if (!translatedPayloadReady)
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    var shouldSwap = swapEnabled ?? this.configuration.SwapTextsUsingImGui;
    var displayText = shouldSwap
        ? originalText
        : translatedText;

    if (string.IsNullOrWhiteSpace(displayText))
    {
      this.hoverTooltipManager.Remove(key);
      return;
    }

    this.RegisterHoverTooltip(
        key,
        topLeft,
        bottomRight,
        string.Empty,
        displayText,
        forceEnabled,
        useGeneralFont: shouldSwap);
  }

}
