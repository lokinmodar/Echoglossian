// <copyright file="QuestAddonWiring.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Persistence;

namespace Echoglossian;

public partial class Echoglossian
{
  /// <summary>
  ///     Builds the shared dependency bundle for standalone quest handlers.
  /// </summary>
  /// <returns>The reusable quest-handler dependency bundle.</returns>
  private unsafe QuestAddonHandlerDependencies CreateQuestAddonHandlerDependencies()
  {
    return new QuestAddonHandlerDependencies
    {
      Config = this.configuration,
      TranslationService = TranslationService,
      FindQuestPlate = this.FindQuestPlate,
      FindQuestPlateByName = this.FindQuestPlateByName,
      FindQuestPlateCacheFirst = this.FindQuestPlateCacheFirst,
      FindQuestPopupText = this.FindQuestPopupText,
      InsertQuestPlate = this.InsertQuestPlate,
      InsertQuestPopupTextAsync = this.InsertQuestPopupTextData,
      UpdateQuestPlate = this.UpdateQuestPlate,
      UpdateQuestPlateGameVersion = this.UpdateQuestPlateGameVersion,
      NormalizeText = text => this.RemoveDiacritics(
          text,
          this.SpecialCharsSupportedByGameFont),
      DisableTranslationAccordingToState = this.DisableTranslationAccordingToState,
      TryGetQueuedTranslation = this.TryGetQueuedTranslation,
      QueueTranslation = this.QueueTranslation,
      QueueTranslationBatch = this.QueueTranslationBatch,
      RequestAcceptedQuestPrefetch = this.RequestAcceptedQuestPrefetch,
      RemoveHoverTooltipByPrefix =
          prefix => this.hoverTooltipManager.RemoveByPrefix(prefix),
      RegisterTranslatedHoverTooltipAddon =
          (key, addon, originalText, translatedText, translatedPayloadReady, swapEnabled, forceEnabled, denseHitbox) =>
              this.RegisterTranslatedHoverTooltip(
                  key,
                  addon,
                  originalText,
                  translatedText,
                  translatedPayloadReady,
                  swapEnabled,
                  forceEnabled,
                  denseHitbox),
      RegisterTranslatedHoverTooltipTextNode =
          (key, textNode, originalText, translatedText, translatedPayloadReady, swapEnabled, forceEnabled, denseHitbox) =>
              this.RegisterTranslatedHoverTooltip(
                  key,
                  textNode,
                  originalText,
                  translatedText,
                  translatedPayloadReady,
                  swapEnabled,
                  forceEnabled,
                  denseHitbox),
      RegisterTranslatedHoverTooltipResNode =
          (key, node, originalText, translatedText, translatedPayloadReady, swapEnabled, forceEnabled, denseHitbox) =>
              this.RegisterTranslatedHoverTooltip(
                  key,
                  node,
                  originalText,
                  translatedText,
                  translatedPayloadReady,
                  swapEnabled,
                  forceEnabled,
                  denseHitbox),
      RegisterTranslatedHoverTooltipBounds =
          (key, topLeft, bottomRight, originalText, translatedText, translatedPayloadReady, swapEnabled, forceEnabled) =>
              this.RegisterTranslatedHoverTooltip(
                  key,
                  topLeft,
                  bottomRight,
                  originalText,
                  translatedText,
                  translatedPayloadReady,
                  swapEnabled,
                  forceEnabled),
      RegisterTranslatedHoverTooltipTextNodeBounds =
          (key, topLeft, bottomRight, textNode, originalText, translatedText, translatedPayloadReady, swapEnabled, forceEnabled) =>
              this.RegisterTranslatedHoverTooltip(
                  key,
                  topLeft,
                  bottomRight,
                  textNode,
                  originalText,
                  translatedText,
                  translatedPayloadReady,
                  swapEnabled,
                  forceEnabled),
      LogPopupBodyHoverGeometryDecision =
          (key, preferredHoverNodeKind, preferredTopLeft, preferredBottomRight, explicitBoundsBuilt, explicitTopLeft, explicitBottomRight, finalAnchorKind) =>
              this.hoverTooltipManager.LogBodyGeometryDecision(
                  key,
                  preferredHoverNodeKind,
                  preferredTopLeft,
                  preferredBottomRight,
                  explicitBoundsBuilt,
                  explicitTopLeft,
                  explicitBottomRight,
                  finalAnchorKind),
    };
  }

  /// <summary>
  ///     Resolves a committed QuestPlate cache projection and admits exactly
  ///     one shared interactive read for a cache miss without blocking an
  ///     addon callback.
  /// </summary>
  /// <param name="questPlate">The managed QuestPlate lookup payload.</param>
  /// <returns>A committed cloned projection when present; otherwise null.</returns>
  private QuestPlate? FindQuestPlateCacheFirst(
      QuestPlate questPlate,
      SourceClientLanguage sourceLanguage)
  {
    ArgumentNullException.ThrowIfNull(questPlate);
    var targetLanguage = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
        this.configuration.Lang);
    if (string.IsNullOrWhiteSpace(targetLanguage))
    {
      return null;
    }

    var scope = new TranslationReuseScope(
        sourceLanguage.PersistenceCode,
        targetLanguage,
        questPlate.TranslationEngine ?? this.configuration.ChosenTransEngine,
        this.configuration.TranslateAlreadyTranslatedTexts);

    if (this.questPlateRuntimeCache.TryGet(questPlate, scope, out var cached))
    {
      return cached;
    }

    var writer = this.questPlatePersistenceWriter;
    if (writer is null)
    {
      return null;
    }

    _ = writer.TryFind(
        questPlate,
        scope,
        PersistencePriority.Interactive,
        out _);
    return this.questPlateRuntimeCache.TryGet(questPlate, scope, out cached)
        ? cached
        : null;
  }
}
