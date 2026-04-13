// <copyright file="AreaMapHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the AreaMap quest addon runtime inside the standalone quest-
///     handler model.
/// </summary>
internal sealed class AreaMapHandler : QuestAddonHandlerBase
{
  private const string AreaMapAddonName = "AreaMap";

  private const string AreaMapHoverPrefix = "AreaMap-";

  /// <summary>
  ///     Initializes a new instance of the <see cref="AreaMapHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public AreaMapHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnAreaMapCleanupEvent);
    this.RegisterHandler(AddonEvent.PreFinalize, this.OnAreaMapCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the AreaMap family should use hover tooltips.
  /// </summary>
  private bool AreaMapUsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.AreaMapTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the AreaMap family should write translated text into the
  ///     native addon.
  /// </summary>
  private bool AreaMapWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.AreaMapTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the AreaMap family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool AreaMapHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.AreaMapTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the AreaMap family should strip diacritics from
  ///     translated text before it is written to the native UI.
  /// </summary>
  private bool AreaMapShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.AreaMapTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Handles AreaMap refresh and requested-update events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnAreaMapEvent(AddonEvent type, AddonArgs args)
  {
#if DEBUG
    PluginLog.Debug(
  $"AreaMapHandler AddonEvent: {type} {args.AddonName}");
#endif

    if (!this.Config.TranslateAreaMap)
    {
      return;
    }

    if (args is not AddonRefreshArgs setupArgs)
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
      if (setupAtkValues[142].Type != ValueType.String ||
          setupAtkValues[142].String.ToString() == string.Empty)
      {
        return;
      }

      var questNameText = MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)setupAtkValues[142].String.Value);
      if (questNameText == string.Empty)
      {
        return;
      }

      if (QuestUiTranslationCache.TryGetAppliedSnapshot(
              questNameText,
              out _))
      {
        return;
      }

      var questPlate = this.CreateQuestPlate(questNameText, string.Empty);
      var foundQuestPlate = this.FindQuestPlateByName(questPlate);
      var cacheKey = $"AreaMap|{questNameText}";
      if (foundQuestPlate != null)
      {
#if DEBUG
        PluginLog.Debug(
            $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
        if (this.AreaMapShouldRemoveDiacritics)
        {
          foundQuestPlate.TranslatedQuestName = this.NormalizeQuestText(
              foundQuestPlate.TranslatedQuestName ?? string.Empty);
        }

        if (this.AreaMapWritesNativeTranslation)
        {
          setupAtkValues[142].SetManagedString(
              foundQuestPlate.TranslatedQuestName);
        }

        QuestUiTranslationCache.Remember(
            questNameText,
            foundQuestPlate.TranslatedQuestName ?? string.Empty);

        if (this.AreaMapUsesHoverTooltips)
        {
          var addon = AtkStage.Instance()->RaptureAtkUnitManager
              ->GetAddonByName(AreaMapAddonName);
          this.RegisterTranslatedHoverTooltip(
              $"AreaMap-{(nint)addon:X}-142",
              addon,
              questNameText,
              foundQuestPlate.TranslatedQuestName ?? string.Empty,
              swapEnabled: this.AreaMapHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        return;
      }

      if (this.TryGetQueuedTranslation(cacheKey, out var cachedTranslatedName))
      {
        var translatedNameText = cachedTranslatedName;
#if DEBUG
        PluginLog.Debug(
            $"Name translated: {questNameText} -> {translatedNameText}");
#endif
        if (this.AreaMapShouldRemoveDiacritics)
        {
          translatedNameText = this.NormalizeQuestText(translatedNameText);
        }

        if (this.AreaMapWritesNativeTranslation)
        {
          setupAtkValues[142].SetManagedString(translatedNameText);
        }

        QuestUiTranslationCache.Remember(
            questNameText,
            translatedNameText);

        if (this.AreaMapUsesHoverTooltips)
        {
          var addon = AtkStage.Instance()->RaptureAtkUnitManager
              ->GetAddonByName(AreaMapAddonName);
          this.RegisterTranslatedHoverTooltip(
              $"AreaMap-{(nint)addon:X}-142",
              addon,
              questNameText,
              translatedNameText,
              swapEnabled: this.AreaMapHoverShowsOriginal,
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
                string.Empty);

            var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
            PluginLog.Debug(
                $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
          });
    }
    catch (Exception e)
    {
      PluginLog.Error("Exception at AreaMapHandler: " + e);
    }
  }

  /// <summary>
  ///     Clears AreaMap hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnAreaMapCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, AreaMapAddonName, StringComparison.Ordinal))
    {
      this.RemoveHoverTooltipsByPrefix(AreaMapHoverPrefix);
    }
  }
}
