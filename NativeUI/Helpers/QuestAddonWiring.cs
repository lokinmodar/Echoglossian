// <copyright file="QuestAddonWiring.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

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
      InsertQuestPlate = this.InsertQuestPlate,
      UpdateQuestPlate = this.UpdateQuestPlate,
      UpdateQuestPlateGameVersion = this.UpdateQuestPlateGameVersion,
      NormalizeText = text => this.RemoveDiacritics(
          text,
          this.SpecialCharsSupportedByGameFont),
      DisableTranslationAccordingToState = this.DisableTranslationAccordingToState,
      TryGetQueuedTranslation = this.TryGetQueuedTranslation,
      QueueTranslation = this.QueueTranslation,
      QueueTranslationBatch = this.QueueTranslationBatch,
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
    };
  }
}
