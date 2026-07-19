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

  private const int AreaMapQuestValueIndex = 142;

  private static readonly TimeSpan AreaMapRetryInterval =
      TimeSpan.FromSeconds(2);

  private readonly Dictionary<string, AreaMapTextCacheEntry> areaMapTextCache = [];

  private string areaMapHoverOriginalText = string.Empty;

  private string areaMapHoverTranslatedText = string.Empty;

  private bool hasPendingAreaMapTranslation;

  private bool needsAreaMapApplicationRefresh = true;

  private bool ownsAreaMapNativeMutation;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextAreaMapRetryUtc = DateTime.MinValue;

  /// <summary>
  ///     Initializes a new instance of the <see cref="AreaMapHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public AreaMapHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreRefresh, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnAreaMapEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnAreaMapPreDrawEvent);
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
  ///     Gets whether translated AreaMap text should be normalized before being
  ///     written to the native UI.
  /// </summary>
  private bool AreaMapShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.AreaMapTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Gets whether AreaMap may render a hover tooltip for a payload whose
  ///     translated content is ready.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is ready.
  /// </param>
  /// <returns><c>true</c> when the hover tooltip may be rendered.</returns>
  private bool CanRenderAreaMapHoverTooltip(bool translatedPayloadReady) =>
      QuestAddonModeHelpers.CanRenderHoverTooltip(
          this.Config.AreaMapTranslationDisplayMode,
          translatedPayloadReady);

  /// <summary>
  ///     Determines whether translated AreaMap quest text is ready for native
  ///     application or tooltip rendering.
  /// </summary>
  /// <param name="translatedQuestText">The translated AreaMap quest text.</param>
  /// <returns><c>true</c> when the translated text exists.</returns>
  internal static bool IsTranslatedPayloadReady(string? translatedQuestText)
  {
    return !string.IsNullOrWhiteSpace(translatedQuestText);
  }

  /// <summary>
  ///     Handles AreaMap refresh and requested-update events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnAreaMapEvent(AddonEvent type, AddonArgs args)
  {
    this.ProcessAreaMap(args, queueMissingTranslation: true);
  }

  /// <summary>
  ///     Refreshes AreaMap application and hover targets after delayed
  ///     translations settle.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnAreaMapPreDrawEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.TryResolveAreaMapAtkValues(args, out var atkValues))
    {
      return;
    }

    if (!this.Config.TranslateAreaMap ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreAreaMapOriginal(atkValues);
      this.ClearAreaMapRuntimeState(removeHoverTooltips: true);
      return;
    }

    var shouldRefresh =
        this.needsAreaMapApplicationRefresh ||
        this.lastAppliedDisplayMode != this.Config.AreaMapTranslationDisplayMode ||
        (this.hasPendingAreaMapTranslation &&
         DateTime.UtcNow >= this.nextAreaMapRetryUtc);
    if (shouldRefresh)
    {
      this.TryRefreshAreaMapPendingTranslation();
      this.ProcessAreaMap(args, queueMissingTranslation: true);
      return;
    }

    this.ApplyAreaMapPresentation(atkValues);
  }

  /// <summary>
  ///     Processes the visible AreaMap quest row by resolving cached or
  ///     persisted translations, applying the selected display mode, and
  ///     optionally queueing missing background translations.
  /// </summary>
  /// <param name="args">The addon lifecycle arguments, if available.</param>
  /// <param name="queueMissingTranslation">
  ///     Whether missing text should be sent to the shared translation broker.
  /// </param>
  private unsafe void ProcessAreaMap(
      AddonArgs? args,
      bool queueMissingTranslation)
  {
    if (!this.TryResolveAreaMapAtkValues(args, out var atkValues))
    {
      return;
    }

    if (!this.Config.TranslateAreaMap ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreAreaMapOriginal(atkValues);
      this.ClearAreaMapRuntimeState(removeHoverTooltips: true);
      return;
    }

    if (!TryReadAreaMapQuestText(atkValues, out var visibleQuestText))
    {
      return;
    }

    if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage))
    {
      return;
    }

    var originalQuestText = this.ResolveOriginalAreaMapText(visibleQuestText);
    if (this.TryResolveAreaMapTranslation(
            sourceLanguage,
            originalQuestText,
            visibleQuestText,
            out var translatedQuestText))
    {
      this.RememberAreaMapHoverTexts(originalQuestText, translatedQuestText);
      this.RememberAreaMapCachedText(originalQuestText, translatedQuestText);
      this.hasPendingAreaMapTranslation = false;
      this.nextAreaMapRetryUtc = DateTime.MinValue;
      this.ApplyAreaMapPresentation(atkValues);
      return;
    }

    this.RememberAreaMapHoverTexts(originalQuestText, string.Empty);
    this.hasPendingAreaMapTranslation = true;
    this.nextAreaMapRetryUtc = DateTime.UtcNow + AreaMapRetryInterval;
    if (queueMissingTranslation)
    {
      this.QueueAreaMapTranslation(sourceLanguage, originalQuestText);
    }

    this.ApplyAreaMapPresentation(atkValues);
  }

  /// <summary>
  ///     Resolves the live AreaMap ATK value array for refresh, requested-
  ///     update, and draw events.
  /// </summary>
  /// <param name="args">The lifecycle arguments for the current event.</param>
  /// <param name="atkValues">The resolved ATK value pointer.</param>
  /// <returns>True when a usable ATK value array was found.</returns>
  private unsafe bool TryResolveAreaMapAtkValues(
      AddonArgs? args,
      out AtkValue* atkValues)
  {
    atkValues = null;

    if (args != null &&
        !string.Equals(args.AddonName, AreaMapAddonName, StringComparison.Ordinal))
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
  ///     Reads the AreaMap quest text from the live ATK value payload.
  /// </summary>
  /// <param name="atkValues">The AreaMap ATK values.</param>
  /// <param name="questText">The resolved quest text.</param>
  /// <returns><c>true</c> when a readable quest string exists.</returns>
  private static unsafe bool TryReadAreaMapQuestText(
      AtkValue* atkValues,
      out string questText)
  {
    questText = string.Empty;
    if (atkValues == null ||
        atkValues[AreaMapQuestValueIndex].Type is not
            (ValueType.String or
             ValueType.String8 or
             ValueType.ManagedString) ||
        !atkValues[AreaMapQuestValueIndex].String.HasValue)
    {
      return false;
    }

    questText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)atkValues[AreaMapQuestValueIndex].String.Value);
    return !string.IsNullOrWhiteSpace(questText);
  }

  /// <summary>
  ///     Resolves a translated AreaMap quest text from local state, the DB, or
  ///     the shared translation queue.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="originalQuestText">The original AreaMap quest text.</param>
  /// <param name="visibleQuestText">The current visible AreaMap text.</param>
  /// <param name="translatedQuestText">The resolved translated text.</param>
  /// <returns><c>true</c> when a translated quest text exists.</returns>
  private bool TryResolveAreaMapTranslation(
      SourceClientLanguage sourceLanguage,
      string originalQuestText,
      string visibleQuestText,
      out string translatedQuestText)
  {
    translatedQuestText = string.Empty;
    if (this.TryGetAreaMapCachedText(
            originalQuestText,
            out var cachedAreaMapText) &&
        IsTranslatedPayloadReady(cachedAreaMapText.TranslatedText))
    {
      translatedQuestText = cachedAreaMapText.TranslatedText;
      return true;
    }

    var questPlate = this.CreateQuestPlate(
        sourceLanguage,
        originalQuestText,
        string.Empty);
    var foundQuestPlate = this.FindQuestPlateByName(questPlate);
    if (foundQuestPlate == null &&
        !string.Equals(
            originalQuestText,
            visibleQuestText,
            StringComparison.Ordinal))
    {
      questPlate = this.CreateQuestPlate(
          sourceLanguage,
          visibleQuestText,
          string.Empty);
      foundQuestPlate = this.FindQuestPlateByName(questPlate);
    }

    if (foundQuestPlate != null &&
        IsTranslatedPayloadReady(foundQuestPlate.TranslatedQuestName))
    {
      translatedQuestText = foundQuestPlate.TranslatedQuestName ?? string.Empty;
      return true;
    }

    if (this.TryGetQueuedTranslation(
            BuildAreaMapCacheKey(originalQuestText),
            out var queuedAreaMapTranslation) &&
        IsTranslatedPayloadReady(queuedAreaMapTranslation))
    {
      translatedQuestText = queuedAreaMapTranslation;
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Attempts to promote a pending AreaMap payload from the broker cache.
  /// </summary>
  /// <returns><c>true</c> when translated text became available.</returns>
  private bool TryRefreshAreaMapPendingTranslation()
  {
    if (!this.hasPendingAreaMapTranslation ||
        string.IsNullOrWhiteSpace(this.areaMapHoverOriginalText))
    {
      return false;
    }

    if (!this.TryGetQueuedTranslation(
            BuildAreaMapCacheKey(this.areaMapHoverOriginalText),
            out var translatedQuestText) ||
        !IsTranslatedPayloadReady(translatedQuestText))
    {
      this.nextAreaMapRetryUtc = DateTime.UtcNow + AreaMapRetryInterval;
      return false;
    }

    this.RememberAreaMapHoverTexts(
        this.areaMapHoverOriginalText,
        translatedQuestText);
    this.RememberAreaMapCachedText(
        this.areaMapHoverOriginalText,
        translatedQuestText);
    this.hasPendingAreaMapTranslation = false;
    this.nextAreaMapRetryUtc = DateTime.MinValue;
    return true;
  }

  /// <summary>
  ///     Enqueues one AreaMap quest text through the shared quest broker.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source identity.</param>
  /// <param name="questText">The original AreaMap quest text.</param>
  private void QueueAreaMapTranslation(
      SourceClientLanguage sourceLanguage,
      string questText)
  {
    if (string.IsNullOrWhiteSpace(questText))
    {
      return;
    }

    this.QueueTranslation(
        BuildAreaMapCacheKey(questText),
        () => this.Translate(questText, sourceLanguage),
        translatedQuestText =>
        {
          if (!IsTranslatedPayloadReady(translatedQuestText))
          {
            return;
          }

          var translatedQuestPlate = this.CreateTranslatedQuestPlate(
              sourceLanguage,
              questText,
              string.Empty,
              translatedQuestText,
              string.Empty);

          var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          PluginRuntimeLog.Debug(
              $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
        });
  }

  /// <summary>
  ///     Applies the current AreaMap presentation mode to native text and hover
  ///     tooltip state.
  /// </summary>
  /// <param name="atkValues">The live AreaMap ATK values.</param>
  private unsafe void ApplyAreaMapPresentation(AtkValue* atkValues)
  {
    var translatedPayloadReady = IsTranslatedPayloadReady(
        this.areaMapHoverTranslatedText);
    if (this.AreaMapWritesNativeTranslation && translatedPayloadReady)
    {
      atkValues[AreaMapQuestValueIndex].SetManagedString(
          this.GetAreaMapTranslatedDisplayText(this.areaMapHoverTranslatedText));
      this.ownsAreaMapNativeMutation = true;
    }
    else
    {
      this.RestoreAreaMapOriginal(atkValues);
    }

    if (this.AreaMapUsesHoverTooltips)
    {
      this.RegisterAreaMapHoverTooltip(translatedPayloadReady);
    }
    else
    {
      this.RemoveHoverTooltipsByPrefix(AreaMapHoverPrefix);
    }

    this.lastAppliedDisplayMode = this.Config.AreaMapTranslationDisplayMode;
    this.needsAreaMapApplicationRefresh = false;
  }

  /// <summary>
  ///     Restores the original AreaMap native text when this handler previously
  ///     wrote a native translation.
  /// </summary>
  /// <param name="atkValues">The live AreaMap ATK values.</param>
  private unsafe void RestoreAreaMapOriginal(AtkValue* atkValues)
  {
    if (!this.ownsAreaMapNativeMutation ||
        string.IsNullOrWhiteSpace(this.areaMapHoverOriginalText))
    {
      return;
    }

    atkValues[AreaMapQuestValueIndex].SetManagedString(
        this.areaMapHoverOriginalText);
    this.ownsAreaMapNativeMutation = false;
  }

  /// <summary>
  ///     Registers or suppresses the AreaMap hover tooltip for the current
  ///     resolved text pair.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated text required by the tooltip exists.
  /// </param>
  private unsafe void RegisterAreaMapHoverTooltip(bool translatedPayloadReady)
  {
    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(AreaMapAddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    this.RegisterTranslatedHoverTooltip(
        $"AreaMap-{(nint)addon:X}-{AreaMapQuestValueIndex}",
        addon,
        this.areaMapHoverOriginalText,
        this.areaMapHoverTranslatedText,
        translatedPayloadReady: this.CanRenderAreaMapHoverTooltip(
            translatedPayloadReady),
        swapEnabled: this.AreaMapHoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
  }

  /// <summary>
  ///     Resolves the original AreaMap text even if the addon currently shows
  ///     a translated value written by a previous native mode.
  /// </summary>
  /// <param name="visibleText">The current visible AreaMap text.</param>
  /// <returns>The original source text backing the current AreaMap row.</returns>
  private string ResolveOriginalAreaMapText(string visibleText)
  {
    return QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
        visibleText,
        this.areaMapHoverOriginalText,
        this.GetAreaMapTranslatedDisplayText(this.areaMapHoverTranslatedText));
  }

  /// <summary>
  ///     Normalizes translated AreaMap text before it is written into the
  ///     native UI.
  /// </summary>
  /// <param name="translatedText">The translated text.</param>
  /// <returns>The translated text as it should be displayed natively.</returns>
  private string GetAreaMapTranslatedDisplayText(string translatedText)
  {
    if (!this.AreaMapShouldRemoveDiacritics)
    {
      return translatedText;
    }

    return this.NormalizeQuestText(translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Remembers the latest AreaMap hover text pair so the tooltip can be
  ///     refreshed on draw without recomputing translations.
  /// </summary>
  /// <param name="originalText">The current original AreaMap quest text.</param>
  /// <param name="translatedText">The current translated AreaMap quest text.</param>
  private void RememberAreaMapHoverTexts(
      string originalText,
      string translatedText)
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
      this.ClearAreaMapRuntimeState(removeHoverTooltips: true);
    }
  }

  /// <summary>
  ///     Clears AreaMap runtime state.
  /// </summary>
  /// <param name="removeHoverTooltips">
  ///     Whether registered hover targets should also be removed.
  /// </param>
  private void ClearAreaMapRuntimeState(bool removeHoverTooltips)
  {
    this.areaMapHoverOriginalText = string.Empty;
    this.areaMapHoverTranslatedText = string.Empty;
    this.hasPendingAreaMapTranslation = false;
    this.needsAreaMapApplicationRefresh = true;
    this.ownsAreaMapNativeMutation = false;
    this.lastAppliedDisplayMode = null;
    this.nextAreaMapRetryUtc = DateTime.MinValue;
    if (removeHoverTooltips)
    {
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
    if (this.areaMapTextCache.TryGetValue(
            originalText,
            out var foundCachedText))
    {
      cachedText = foundCachedText;
      return true;
    }

    cachedText = null!;
    return false;
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
    this.areaMapTextCache[originalText ?? string.Empty] =
        new AreaMapTextCacheEntry(
            originalText ?? string.Empty,
            translatedText ?? string.Empty);
  }

  /// <summary>
  ///     Builds the shared broker cache key for one AreaMap quest text.
  /// </summary>
  /// <param name="questText">The original AreaMap quest text.</param>
  /// <returns>The stable cache key.</returns>
  private static string BuildAreaMapCacheKey(string questText)
  {
    return $"AreaMap|{questText}";
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
