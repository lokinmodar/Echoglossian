// <copyright file="QuestTranslationModeHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

public partial class Echoglossian
{
  /// <summary>
  ///     Gets whether a quest-like display mode should show hover tooltips.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <returns><c>true</c> when hover tooltips should be rendered.</returns>
  private static bool QuestModeUsesHoverTooltips(
      JournalTranslationDisplayMode displayMode,
      bool overlayOnlyLanguage)
  {
    return TranslationDisplayModeHelper.UsesHoverTooltips(
        displayMode,
        overlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether a quest-like display mode should write translated text
  ///     into the native addon.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <returns><c>true</c> when the native addon should receive translated text.</returns>
  private static bool QuestModeWritesNativeTranslation(
      JournalTranslationDisplayMode displayMode,
      bool overlayOnlyLanguage)
  {
    return TranslationDisplayModeHelper.WritesNativeTranslation(
        displayMode,
        overlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether a quest-like display mode should show the original text
  ///     in hover tooltips.
  /// </summary>
  /// <param name="displayMode">The configured display mode.</param>
  /// <returns><c>true</c> when hover tooltips should show the original text.</returns>
  private static bool QuestModeShowsOriginalTooltips(
      JournalTranslationDisplayMode displayMode,
      bool overlayOnlyLanguage)
  {
    return TranslationDisplayModeHelper.ShowsOriginalTooltips(
        displayMode,
        overlayOnlyLanguage);
  }

  /// <summary>
  ///     Gets whether the Journal family should write translated text into the
  ///     native addon.
  /// </summary>
  private bool JournalWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.JournalTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the Journal family should use hover tooltips.
  /// </summary>
  private bool JournalUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.JournalTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the Journal family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.JournalTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalDetail family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool JournalDetailWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.JournalDetailTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalDetail family should use hover tooltips.
  /// </summary>
  private bool JournalDetailUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.JournalDetailTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalDetail family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalDetailHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.JournalDetailTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the ToDoList family should use hover tooltips.
  /// </summary>
  private bool ToDoListUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.ToDoListTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the ToDoList family should write translated text into the
  ///     native addon.
  /// </summary>
  private bool ToDoListWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.ToDoListTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the ToDoList family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool ToDoListHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.ToDoListTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the ScenarioTree family should use hover tooltips.
  /// </summary>
  private bool ScenarioTreeUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.ScenarioTreeTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the ScenarioTree family should write translated text into
  ///     the native addon.
  /// </summary>
  private bool ScenarioTreeWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.ScenarioTreeTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the ScenarioTree family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool ScenarioTreeHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.ScenarioTreeTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the RecommendList family should use hover tooltips.
  /// </summary>
  private bool RecommendListUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.RecommendListTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the RecommendList family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool RecommendListWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.RecommendListTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the RecommendList family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool RecommendListHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.RecommendListTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the AreaMap family should use hover tooltips.
  /// </summary>
  private bool AreaMapUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.AreaMapTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the AreaMap family should write translated text into the
  ///     native addon.
  /// </summary>
  private bool AreaMapWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.AreaMapTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the AreaMap family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool AreaMapHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.AreaMapTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalAccept family should use hover tooltips.
  /// </summary>
  private bool JournalAcceptUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.JournalAcceptTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalAccept family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool JournalAcceptWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.JournalAcceptTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalAccept family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalAcceptHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.JournalAcceptTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalResult family should use hover tooltips.
  /// </summary>
  private bool JournalResultUsesHoverTooltips =>
      QuestModeUsesHoverTooltips(
          this.configuration.JournalResultTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalResult family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool JournalResultWritesNativeTranslation =>
      QuestModeWritesNativeTranslation(
          this.configuration.JournalResultTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the JournalResult family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalResultHoverShowsOriginal =>
      QuestModeShowsOriginalTooltips(
          this.configuration.JournalResultTranslationDisplayMode,
          this.configuration.OverlayOnlyLanguage);

  /// <summary>
  ///     Gets whether the Journal family should strip diacritics from translated
  ///     text before use. Diacritics removal is only meaningful when translated
  ///     text is written directly into native addon nodes, because our own
  ///     overlays and tooltips always use fonts that support every diacritic.
  /// </summary>
  private bool JournalShouldRemoveDiacritics =>
      this.JournalWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether the JournalDetail family should strip diacritics from
  ///     translated text before use.
  /// </summary>
  private bool JournalDetailShouldRemoveDiacritics =>
      this.JournalDetailWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether the JournalAccept family should strip diacritics.
  ///     Only active when writing to native addon nodes.
  /// </summary>
  private bool JournalAcceptShouldRemoveDiacritics =>
      this.JournalAcceptWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether the JournalResult family should strip diacritics.
  ///     Only active when writing to native addon nodes.
  /// </summary>
  private bool JournalResultShouldRemoveDiacritics =>
      this.JournalResultWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether the ScenarioTree family should strip diacritics.
  ///     Only active when writing to native addon nodes.
  /// </summary>
  private bool ScenarioTreeShouldRemoveDiacritics =>
      this.ScenarioTreeWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether the ToDoList family should strip diacritics.
  ///     Only active when writing to native addon nodes.
  /// </summary>
  private bool ToDoListShouldRemoveDiacritics =>
      this.ToDoListWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether the RecommendList family should strip diacritics.
  ///     Only active when writing to native addon nodes.
  /// </summary>
  private bool RecommendListShouldRemoveDiacritics =>
      this.RecommendListWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether the AreaMap family should strip diacritics.
  ///     Only active when writing to native addon nodes.
  /// </summary>
  private bool AreaMapShouldRemoveDiacritics =>
      this.AreaMapWritesNativeTranslation &&
      this.configuration.RemoveDiacriticsWhenUsingReplacementQuest;

  /// <summary>
  ///     Gets whether any quest family is currently configured to render hover
  ///     tooltips.
  /// </summary>
  private bool ShouldDrawHoverTooltips =>
      this.configuration.TranslateTooltips ||
      (this.configuration.TranslateJournal && this.JournalUsesHoverTooltips) ||
      (this.configuration.TranslateJournalDetail &&
       this.JournalDetailUsesHoverTooltips) ||
      (this.configuration.TranslateToDoList && this.ToDoListUsesHoverTooltips) ||
      (this.configuration.TranslateScenarioTree &&
       this.ScenarioTreeUsesHoverTooltips) ||
      (this.configuration.TranslateRecommendList &&
       this.RecommendListUsesHoverTooltips) ||
      (this.configuration.TranslateAreaMap && this.AreaMapUsesHoverTooltips) ||
      (this.configuration.TranslateJournalAccept &&
       this.JournalAcceptUsesHoverTooltips) ||
      (this.configuration.TranslateJournalResult &&
       this.JournalResultUsesHoverTooltips);
}
