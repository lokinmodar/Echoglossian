// <copyright file="QuestAddonHandlerDependencies.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Delegate used to read a queued quest translation from the shared
///     broker.
/// </summary>
/// <param name="key">Stable translation key.</param>
/// <param name="translatedText">The cached translated text, if any.</param>
/// <returns>True when a cached translation exists.</returns>
internal delegate bool TryGetQueuedTranslationDelegate(
    string key,
    out string translatedText);

/// <summary>
///     Delegate used to queue a quest translation request.
/// </summary>
/// <param name="key">Stable translation key.</param>
/// <param name="resolver">Function that resolves the translated text.</param>
/// <param name="onResolved">Optional callback invoked once the text is cached.</param>
/// <returns>True when the request was queued.</returns>
internal delegate bool QueueTranslationDelegate(
    string key,
    Func<string> resolver,
    Action<string>? onResolved = null);

/// <summary>
///     Delegate used to queue a quest translation batch request.
/// </summary>
/// <param name="key">Stable translation key.</param>
/// <param name="sourceTexts">The source texts that will be translated.</param>
/// <param name="onResolved">Optional callback invoked with the translated batch.</param>
/// <returns>True when the request was queued.</returns>
internal delegate bool QueueTranslationBatchDelegate(
    string key,
    IReadOnlyCollection<string> sourceTexts,
    Action<string[]>? onResolved = null);

/// <summary>
///     Delegate used to remove quest hover tooltips by prefix.
/// </summary>
/// <param name="prefix">The tooltip key prefix to remove.</param>
internal delegate void RemoveHoverTooltipByPrefixDelegate(string prefix);

/// <summary>
///     Delegate used to register a hover tooltip against a live addon window.
/// </summary>
/// <param name="key">Stable key used to refresh the tooltip target.</param>
/// <param name="addon">The live addon window to anchor the tooltip to.</param>
/// <param name="originalText">The original visible text.</param>
/// <param name="translatedText">The translated text.</param>
/// <param name="translatedPayloadReady">
/// Whether the tooltip payload required by the current mode is ready.
/// </param>
/// <param name="swapEnabled">Optional explicit swap override.</param>
/// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
/// <param name="denseHitbox">Whether to use the denser addon hitbox.</param>
internal unsafe delegate void RegisterTranslatedHoverTooltipAddonDelegate(
    string key,
    AtkUnitBase* addon,
    string originalText,
    string translatedText,
    bool translatedPayloadReady = true,
    bool? swapEnabled = null,
    bool forceEnabled = false,
    bool denseHitbox = false);

/// <summary>
///     Delegate used to register a hover tooltip against a live text node.
/// </summary>
/// <param name="key">Stable key used to refresh the tooltip target.</param>
/// <param name="textNode">The text node to anchor the tooltip to.</param>
/// <param name="originalText">The original visible text.</param>
/// <param name="translatedText">The translated text.</param>
/// <param name="translatedPayloadReady">
/// Whether the tooltip payload required by the current mode is ready.
/// </param>
/// <param name="swapEnabled">Optional explicit swap override.</param>
/// <param name="forceEnabled">Whether to register even if tooltips are disabled.</param>
/// <param name="denseHitbox">Whether to use the denser node hitbox.</param>
internal unsafe delegate void RegisterTranslatedHoverTooltipTextNodeDelegate(
    string key,
    AtkTextNode* textNode,
    string originalText,
    string translatedText,
    bool translatedPayloadReady = true,
    bool? swapEnabled = null,
    bool forceEnabled = false,
    bool denseHitbox = false);

/// <summary>
///     Delegate used to register a hover tooltip against a live generic node.
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
/// <param name="denseHitbox">Whether to use the denser node hitbox.</param>
internal unsafe delegate void RegisterTranslatedHoverTooltipResNodeDelegate(
    string key,
    AtkResNode* node,
    string originalText,
    string translatedText,
    bool translatedPayloadReady = true,
    bool? swapEnabled = null,
    bool forceEnabled = false,
    bool denseHitbox = false);

/// <summary>
///     Delegate used to register a hover tooltip from explicit screen bounds.
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
internal delegate void RegisterTranslatedHoverTooltipBoundsDelegate(
    string key,
    Vector2 topLeft,
    Vector2 bottomRight,
    string originalText,
    string translatedText,
    bool translatedPayloadReady = true,
    bool? swapEnabled = null,
    bool forceEnabled = false);

/// <summary>
///     Bundles the quest-specific delegates and services needed by standalone
///     quest handlers.
/// </summary>
internal sealed class QuestAddonHandlerDependencies
{
  /// <summary>Gets or sets the active plugin configuration.</summary>
  public required Config Config { get; init; }

  /// <summary>Gets or sets the shared translation service.</summary>
  public required TranslationService TranslationService { get; init; }

  /// <summary>Gets or sets the full quest lookup delegate.</summary>
  public required Func<QuestPlate, QuestPlate?> FindQuestPlate { get; init; }

  /// <summary>Gets or sets the name-only quest lookup delegate.</summary>
  public required Func<QuestPlate, QuestPlate?> FindQuestPlateByName { get; init; }

  /// <summary>Gets or sets the quest insert delegate.</summary>
  public required Func<QuestPlate, string> InsertQuestPlate { get; init; }

  /// <summary>Gets or sets the quest update delegate.</summary>
  public required Func<QuestPlate, string> UpdateQuestPlate { get; init; }

  /// <summary>Gets or sets the game-version update delegate.</summary>
  public required Action<int, string?> UpdateQuestPlateGameVersion { get; init; }

  /// <summary>Gets or sets the quest text normalization delegate.</summary>
  public required Func<string, string> NormalizeText { get; init; }

    /// <summary>Gets or sets the global translation-state guard.</summary>
    public required Func<bool> DisableTranslationAccordingToState { get; init; }

  /// <summary>Gets or sets the queued translation lookup delegate.</summary>
  public required TryGetQueuedTranslationDelegate TryGetQueuedTranslation { get; init; }

  /// <summary>Gets or sets the queued translation delegate.</summary>
  public required QueueTranslationDelegate QueueTranslation { get; init; }

  /// <summary>Gets or sets the queued batch translation delegate.</summary>
  public required QueueTranslationBatchDelegate QueueTranslationBatch { get; init; }

  /// <summary>Gets or sets the hover tooltip prefix removal delegate.</summary>
  public required RemoveHoverTooltipByPrefixDelegate RemoveHoverTooltipByPrefix { get; init; }

  /// <summary>Gets or sets the addon tooltip registration delegate.</summary>
  public required RegisterTranslatedHoverTooltipAddonDelegate
      RegisterTranslatedHoverTooltipAddon { get; init; }

  /// <summary>Gets or sets the text-node tooltip registration delegate.</summary>
  public required RegisterTranslatedHoverTooltipTextNodeDelegate
      RegisterTranslatedHoverTooltipTextNode { get; init; }

  /// <summary>Gets or sets the generic-node tooltip registration delegate.</summary>
  public required RegisterTranslatedHoverTooltipResNodeDelegate
      RegisterTranslatedHoverTooltipResNode { get; init; }

  /// <summary>Gets or sets the bounds-based tooltip registration delegate.</summary>
  public required RegisterTranslatedHoverTooltipBoundsDelegate
      RegisterTranslatedHoverTooltipBounds { get; init; }
}
