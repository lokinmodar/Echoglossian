// <copyright file="JournalResultHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the JournalResult quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class JournalResultHandler : QuestAddonHandlerBase
{
  private const string JournalResultAddonName = "JournalResult";

  private const string JournalResultHoverPrefix = "JournalResult-";

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalResultHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalResultHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreSetup, this.OnJournalResultEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnJournalResultCleanupEvent);
    this.RegisterHandler(
        AddonEvent.PreFinalize,
        this.OnJournalResultCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the JournalResult family should use hover tooltips.
  /// </summary>
  private bool JournalResultUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.JournalResultTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the JournalResult family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool JournalResultWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.JournalResultTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the JournalResult family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalResultHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.JournalResultTranslationDisplayMode);

  /// <summary>
  ///     Gets whether translated JournalResult text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool JournalResultShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalResultTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Handles JournalResult setup events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalResultEvent(AddonEvent type, AddonArgs args)
  {
#if DEBUG
    PluginRuntimeLog.Debug($"JournalResultHandler AddonEvent: {type} {args.AddonName}");
#endif

    if (!this.Config.TranslateJournalResult)
    {
      return;
    }

    if (args is not AddonSetupArgs setupArgs)
    {
      return;
    }

    var setupAtkValues = (AtkValue*)setupArgs.AtkValues;
    if (setupAtkValues == null)
    {
      return;
    }

    try
    {
      if (setupAtkValues[1].Type != ValueType.String ||
          !setupAtkValues[1].String.HasValue)
      {
        return;
      }

      var questNameText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)setupAtkValues[1].String.Value);
      if (questNameText == string.Empty)
      {
        return;
      }

      if (QuestUiTranslationCache.TryGetAppliedSnapshot(
              questNameText,
              out var cachedSnapshot))
      {
        if (this.JournalResultUsesHoverTooltips)
        {
          var addon = AtkStage.Instance()->RaptureAtkUnitManager
              ->GetAddonByName(JournalResultAddonName);
          this.RegisterTranslatedHoverTooltip(
              $"JournalResult-{(nint)addon:X}",
              addon,
              questNameText,
              cachedSnapshot.AppliedText,
              swapEnabled: this.JournalResultHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        return;
      }

      var questPlate = this.CreateQuestPlate(questNameText, string.Empty);
      var foundQuestPlate = this.FindQuestPlateByName(questPlate);
      var cacheKey = $"JournalResult|{questNameText}";
      if (foundQuestPlate != null)
      {
#if DEBUG
        PluginRuntimeLog.Debug(
            $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
        var translatedNameText = foundQuestPlate.TranslatedQuestName;
        if (this.JournalResultShouldRemoveDiacritics)
        {
          translatedNameText = this.NormalizeQuestText(
              translatedNameText ?? string.Empty);
        }

        if (this.JournalResultWritesNativeTranslation)
        {
          setupAtkValues[1].SetManagedString(translatedNameText);
        }

        QuestUiTranslationCache.Remember(
            questNameText,
            translatedNameText);

        if (this.JournalResultUsesHoverTooltips)
        {
          var addon = AtkStage.Instance()->RaptureAtkUnitManager
              ->GetAddonByName(JournalResultAddonName);
          this.RegisterTranslatedHoverTooltip(
              $"JournalResult-{(nint)addon:X}",
              addon,
              questNameText,
              translatedNameText,
              swapEnabled: this.JournalResultHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        return;
      }

      if (this.TryGetQueuedTranslation(cacheKey, out var cachedTranslatedName))
      {
        var translatedNameText = cachedTranslatedName;
#if DEBUG
        PluginRuntimeLog.Debug(
            $"Name translated: {questNameText} -> {translatedNameText}");
#endif
        if (this.JournalResultShouldRemoveDiacritics)
        {
          translatedNameText = this.NormalizeQuestText(
              translatedNameText ?? string.Empty);
        }

        if (this.JournalResultWritesNativeTranslation)
        {
          setupAtkValues[1].SetManagedString(translatedNameText);
        }

        QuestUiTranslationCache.Remember(
            questNameText,
            translatedNameText);

        if (this.JournalResultUsesHoverTooltips)
        {
          var addon = AtkStage.Instance()->RaptureAtkUnitManager
              ->GetAddonByName(JournalResultAddonName);
          this.RegisterTranslatedHoverTooltip(
              $"JournalResult-{(nint)addon:X}",
              addon,
              questNameText,
              translatedNameText,
              swapEnabled: this.JournalResultHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        return;
      }

      this.QueueTranslation(
          cacheKey,
          () => this.Translate(questNameText),
          translatedNameText =>
          {
            var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                questNameText,
                string.Empty,
                translatedNameText,
                string.Empty,
                string.Empty);

            var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
            PluginRuntimeLog.Debug(
                $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
          });
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error("UiJournalResultHandler Exception: " + e.StackTrace);
    }
  }

  /// <summary>
  ///     Clears JournalResult hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnJournalResultCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, JournalResultAddonName, StringComparison.Ordinal))
    {
      this.RemoveHoverTooltipsByPrefix(JournalResultHoverPrefix);
    }
  }
}


