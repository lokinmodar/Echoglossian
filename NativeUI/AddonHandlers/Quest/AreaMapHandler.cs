// <copyright file="AreaMapHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the AreaMap quest addon runtime inside the standalone quest-
///     handler model.
/// </summary>
internal sealed class AreaMapHandler : QuestAddonHandlerBase
{
  private const string AreaMapAddonName = "AreaMap";

  private const string AreaMapHoverPrefix = "AreaMap-";

  private string areaMapHoverOriginalText = string.Empty;

  private string areaMapHoverTranslatedText = string.Empty;

  private readonly Dictionary<string, AreaMapTextCacheEntry> areaMapTextCache = [];

  /// <summary>
  ///     Initializes a new instance of the <see cref="AreaMapHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public AreaMapHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnAreaMapHoverRefreshEvent);
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
    if (!this.Config.TranslateAreaMap)
    {
      return;
    }

    if (!this.TryResolveAreaMapAtkValues(args, out var setupAtkValues))
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

      if (this.TryGetAreaMapCachedText(
              questNameText,
              out var appliedQuestSnapshot))
      {
        this.RememberAreaMapHoverTexts(
            appliedQuestSnapshot.OriginalText,
            appliedQuestSnapshot.TranslatedText);

        if (this.AreaMapWritesNativeTranslation)
        {
          setupAtkValues[142].SetManagedString(
              appliedQuestSnapshot.TranslatedText);
        }

        if (this.AreaMapUsesHoverTooltips)
        {
          var addon = AtkStage.Instance()->RaptureAtkUnitManager
              ->GetAddonByName(AreaMapAddonName);
          this.RegisterTranslatedHoverTooltip(
              $"AreaMap-{(nint)addon:X}-142",
              addon,
              appliedQuestSnapshot.OriginalText,
              appliedQuestSnapshot.TranslatedText,
              swapEnabled: this.AreaMapHoverShowsOriginal,
              forceEnabled: true,
              denseHitbox: true);
        }

        return;
      }

      var questPlate = this.CreateQuestPlate(questNameText, string.Empty);
      var foundQuestPlate = this.FindQuestPlateByName(questPlate);
      var cacheKey = $"AreaMap|{questNameText}";
      if (foundQuestPlate != null)
      {
#if DEBUG
        PluginRuntimeLog.Debug(
            $"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
#endif
        if (this.AreaMapShouldRemoveDiacritics)
        {
          foundQuestPlate.TranslatedQuestName = this.NormalizeQuestText(
              foundQuestPlate.TranslatedQuestName ?? string.Empty);
        }

        this.RememberAreaMapHoverTexts(
            questNameText,
            foundQuestPlate.TranslatedQuestName ?? string.Empty);
        this.RememberAreaMapCachedText(
            questNameText,
            foundQuestPlate.TranslatedQuestName ?? string.Empty);

        if (this.AreaMapWritesNativeTranslation)
        {
          setupAtkValues[142].SetManagedString(
              foundQuestPlate.TranslatedQuestName);
        }

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
        PluginRuntimeLog.Debug(
            $"Name translated: {questNameText} -> {translatedNameText}");
#endif
        if (this.AreaMapShouldRemoveDiacritics)
        {
          translatedNameText = this.NormalizeQuestText(translatedNameText);
        }

        this.RememberAreaMapHoverTexts(
            questNameText,
            translatedNameText);
        this.RememberAreaMapCachedText(
            questNameText,
            translatedNameText);

        if (this.AreaMapWritesNativeTranslation)
        {
          setupAtkValues[142].SetManagedString(translatedNameText);
        }

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

      this.RememberAreaMapHoverTexts(questNameText, questNameText);

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
            PluginRuntimeLog.Debug(
                $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
          });
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error("Exception at AreaMapHandler: " + e);
    }
  }

  /// <summary>
  ///     Resolves the live AreaMap ATK value array for refresh and requested-
  ///     update events.
  /// </summary>
  /// <param name="args">The lifecycle arguments for the current event.</param>
  /// <param name="atkValues">The resolved ATK value pointer.</param>
  /// <returns>True when a usable ATK value array was found.</returns>
  private unsafe bool TryResolveAreaMapAtkValues(
      AddonArgs args,
      out AtkValue* atkValues)
  {
    atkValues = null;

    if (!string.Equals(args.AddonName, AreaMapAddonName, StringComparison.Ordinal))
    {
      return false;
    }

    if (args is AddonRefreshArgs refreshArgs)
    {
      atkValues = (AtkValue*)refreshArgs.AtkValues;
      return atkValues != null;
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(AreaMapAddonName);
    if (addon == null || !addon->IsVisible || addon->AtkValues == null)
    {
      return false;
    }

    atkValues = addon->AtkValues;
    return true;
  }

  /// <summary>
  ///     Refreshes the AreaMap hover target every draw using the most recently
  ///     resolved text pair without queueing new translations.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnAreaMapHoverRefreshEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.Config.TranslateAreaMap || !this.AreaMapUsesHoverTooltips)
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(this.areaMapHoverOriginalText) &&
        string.IsNullOrWhiteSpace(this.areaMapHoverTranslatedText))
    {
      return;
    }

    if (this.TryGetAreaMapCachedText(
            this.areaMapHoverOriginalText,
            out var cachedAreaMapText))
    {
      this.areaMapHoverOriginalText = cachedAreaMapText.OriginalText;
      this.areaMapHoverTranslatedText = cachedAreaMapText.TranslatedText;
    }
    else
    {
      var cacheKey = $"AreaMap|{this.areaMapHoverOriginalText}";
      if (this.TryGetQueuedTranslation(cacheKey, out var queuedAreaMapTranslation))
      {
        var translatedAreaMapText = queuedAreaMapTranslation;
        if (this.AreaMapShouldRemoveDiacritics)
        {
          translatedAreaMapText = this.NormalizeQuestText(
              translatedAreaMapText ?? string.Empty);
        }

        this.RememberAreaMapCachedText(
            this.areaMapHoverOriginalText,
            translatedAreaMapText);
        this.areaMapHoverTranslatedText = translatedAreaMapText;
      }
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(AreaMapAddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    this.RegisterTranslatedHoverTooltip(
        $"AreaMap-{(nint)addon:X}-142",
        addon,
        this.areaMapHoverOriginalText,
        this.areaMapHoverTranslatedText,
        swapEnabled: this.AreaMapHoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
  }

  /// <summary>
  ///     Remembers the latest AreaMap hover text pair so the tooltip can be
  ///     refreshed on draw without recomputing translations.
  /// </summary>
  /// <param name="originalText">The current original AreaMap quest text.</param>
  /// <param name="translatedText">The current translated AreaMap quest text.</param>
  private void RememberAreaMapHoverTexts(string originalText, string translatedText)
  {
    this.areaMapHoverOriginalText = originalText ?? string.Empty;
    this.areaMapHoverTranslatedText = translatedText ?? string.Empty;
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
      this.areaMapHoverOriginalText = string.Empty;
      this.areaMapHoverTranslatedText = string.Empty;
      this.RemoveHoverTooltipsByPrefix(AreaMapHoverPrefix);
    }
  }

  /// <summary>
  ///     Attempts to read the handler-local AreaMap translated-text cache.
  /// </summary>
  /// <param name="originalText">The original AreaMap quest text.</param>
  /// <param name="cachedText">The cached original/translated pair.</param>
  /// <returns>True when the local cache contains a value.</returns>
  private bool TryGetAreaMapCachedText(
      string originalText,
      out AreaMapTextCacheEntry cachedText)
  {
    return this.areaMapTextCache.TryGetValue(originalText, out cachedText);
  }

  /// <summary>
  ///     Remembers the latest translated AreaMap text pair in the handler-local
  ///     runtime cache.
  /// </summary>
  /// <param name="originalText">The original AreaMap quest text.</param>
  /// <param name="translatedText">The translated AreaMap quest text.</param>
  private void RememberAreaMapCachedText(
      string originalText,
      string translatedText)
  {
    this.areaMapTextCache[originalText ?? string.Empty] = new AreaMapTextCacheEntry(
        originalText ?? string.Empty,
        translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Captures the handler-local AreaMap text-cache payload.
  /// </summary>
  /// <param name="OriginalText">The original AreaMap quest text.</param>
  /// <param name="TranslatedText">The translated AreaMap quest text.</param>
  private sealed record AreaMapTextCacheEntry(
      string OriginalText,
      string TranslatedText);
}


