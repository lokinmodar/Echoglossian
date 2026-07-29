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

  private static readonly TimeSpan JournalResultRetryInterval =
      TimeSpan.FromSeconds(2);

  private JournalResultHoverState? currentJournalResultHoverState;

  private bool hasPendingJournalResultTranslation;

  private bool ownsJournalResultNativeMutation;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextJournalResultRetryUtc = DateTime.MinValue;

  private bool needsJournalResultHoverRefresh;

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalResultHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalResultHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreSetup, this.OnJournalResultEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnJournalResultPreDrawEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnJournalResultCleanupEvent);
    this.RegisterHandler(
        AddonEvent.PreFinalize,
        this.OnJournalResultCleanupEvent);
  }

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
  ///     Gets whether JournalResult may render a hover tooltip for a payload
  ///     whose translated content is ready.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is ready.
  /// </param>
  /// <returns><c>true</c> when the hover tooltip may be rendered.</returns>
  private bool CanRenderJournalResultHoverTooltip(
      bool translatedPayloadReady) =>
      QuestAddonModeHelpers.CanRenderHoverTooltip(
          this.Config.JournalResultTranslationDisplayMode,
          translatedPayloadReady);

  /// <summary>
  ///     Gets whether translated JournalResult text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool JournalResultShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalResultTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Determines whether the translated JournalResult title is ready for
  ///     native application or tooltip rendering.
  /// </summary>
  /// <param name="translatedQuestName">The translated quest title.</param>
  /// <returns><c>true</c> when the translated title exists.</returns>
  internal static bool IsTranslatedPayloadReady(string? translatedQuestName)
  {
    return !string.IsNullOrWhiteSpace(translatedQuestName);
  }

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

    if (!string.Equals(
            args.AddonName,
            JournalResultAddonName,
            StringComparison.Ordinal))
    {
      return;
    }

    if (!this.Config.TranslateJournalResult ||
        this.DisableTranslationAccordingToState())
    {
      this.ClearJournalResultRuntimeState();
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
      if (!TryReadJournalResultSetupText(
              setupAtkValues,
              out var questNameText))
      {
        return;
      }

      if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
              out var sourceLanguage))
      {
        return;
      }

      var hasCanonicalQuestId =
          QuestPopupIdentity.TryReadJournalResultQuestId(
              setupAtkValues,
              out var questId) &&
          !string.IsNullOrWhiteSpace(questId);
      questId = hasCanonicalQuestId ? questId : string.Empty;

      var foundQuestPlate = this.FindJournalResultQuestPlate(
          sourceLanguage,
          questNameText,
          questId);
      QuestPopupText? foundQuestPopupText = null;
      if (!hasCanonicalQuestId)
      {
        foundQuestPopupText = this.FindQuestPopupText(
            this.CreateQuestPopupText(
                JournalResultAddonName,
                sourceLanguage,
                questNameText,
                string.Empty));
      }

      var cacheKey = hasCanonicalQuestId
          ? $"JournalResult|QuestId:{questId}|{questNameText}"
          : $"JournalResult|{questNameText}";

      if (!this.TryResolveJournalResultTranslation(
              cacheKey,
              questNameText,
              foundQuestPlate,
              foundQuestPopupText,
              out var translatedNameText))
      {
        this.RememberJournalResultHoverState(
            cacheKey,
            sourceLanguage,
            questNameText,
            string.Empty,
            questId);
        this.QueueJournalResultTranslation(
            cacheKey,
            sourceLanguage,
            questNameText,
            questId);
        this.RegisterJournalResultHoverTooltip();
        return;
      }

      if (this.JournalResultShouldRemoveDiacritics)
      {
        translatedNameText = this.NormalizeQuestText(
            translatedNameText ?? string.Empty);
      }

      this.RememberJournalResultHoverState(
          cacheKey,
          sourceLanguage,
          questNameText,
          translatedNameText,
          questId);

      if (this.JournalResultWritesNativeTranslation)
      {
        setupAtkValues[1].SetManagedString(translatedNameText);
        this.ownsJournalResultNativeMutation = true;
      }
      else
      {
        this.ownsJournalResultNativeMutation = false;
      }

      QuestUiTranslationCache.Remember(
          questNameText,
          translatedNameText);

      this.RegisterJournalResultHoverTooltip();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error("UiJournalResultHandler Exception: " + e);
    }
  }

  /// <summary>
  ///     Refreshes JournalResult hover targets after setup and after delayed
  ///     translations settle.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalResultPreDrawEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(
            args.AddonName,
            JournalResultAddonName,
            StringComparison.Ordinal))
    {
      return;
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(JournalResultAddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    if (!this.Config.TranslateJournalResult ||
        this.DisableTranslationAccordingToState())
    {
      this.ClearJournalResultRuntimeState();
      return;
    }

    if (this.currentJournalResultHoverState == null)
    {
      return;
    }

    var shouldRefresh =
        this.needsJournalResultHoverRefresh ||
        this.lastAppliedDisplayMode !=
        this.Config.JournalResultTranslationDisplayMode ||
        (this.hasPendingJournalResultTranslation &&
         DateTime.UtcNow >= this.nextJournalResultRetryUtc);
    if (!shouldRefresh)
    {
      return;
    }

    if (this.hasPendingJournalResultTranslation)
    {
      this.TryRefreshJournalResultPendingTranslation();
    }

    this.RegisterJournalResultHoverTooltip();
  }

  /// <summary>
  ///     Reads the JournalResult setup title safely.
  /// </summary>
  /// <param name="setupAtkValues">The setup value array.</param>
  /// <param name="questNameText">The captured quest title.</param>
  /// <returns><c>true</c> when the title is a readable string.</returns>
  private static unsafe bool TryReadJournalResultSetupText(
      AtkValue* setupAtkValues,
      out string questNameText)
  {
    questNameText = string.Empty;
    if (setupAtkValues == null ||
        setupAtkValues[1].Type != ValueType.String ||
        !setupAtkValues[1].String.HasValue)
    {
      return false;
    }

    questNameText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)setupAtkValues[1].String.Value);
    return !string.IsNullOrWhiteSpace(questNameText);
  }

  /// <summary>
  ///     Resolves the preferred canonical JournalResult quest row lookup
  ///     using quest id when proven and title-only matching otherwise.
  /// </summary>
  /// <param name="sourceLanguage">The captured source language.</param>
  /// <param name="questNameText">The original quest title.</param>
  /// <param name="questId">The optional canonical quest id.</param>
  /// <returns>The preferred canonical quest row, if one exists.</returns>
  private QuestPlate? FindJournalResultQuestPlate(
      SourceClientLanguage sourceLanguage,
      string questNameText,
      string? questId)
  {
    var questPlate = this.CreateQuestPlate(
        sourceLanguage,
        questNameText,
        string.Empty,
        questId);

    return !string.IsNullOrWhiteSpace(questId)
        ? this.FindQuestPlate(questPlate)
        : this.FindQuestPlateByName(questPlate);
  }

  /// <summary>
  ///     Resolves a translated JournalResult title from the session cache,
  ///     persisted quest row, or completed broker result.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="questNameText">The original quest title.</param>
  /// <param name="foundQuestPlate">The matching persisted quest row, if any.</param>
  /// <param name="foundQuestPopupText">
  ///     The matching dedicated popup row, if any.
  /// </param>
  /// <param name="translatedNameText">The translated quest title.</param>
  /// <returns><c>true</c> when a translated title exists.</returns>
  private bool TryResolveJournalResultTranslation(
      string cacheKey,
      string questNameText,
      QuestPlate? foundQuestPlate,
      QuestPopupText? foundQuestPopupText,
      out string translatedNameText)
  {
    translatedNameText = string.Empty;
    if (this.currentJournalResultHoverState is
        {
          TranslatedPayloadReady: true,
        } cachedState &&
        string.Equals(
            cachedState.CacheKey,
            cacheKey,
            StringComparison.Ordinal))
    {
      translatedNameText = cachedState.TranslatedQuestName;
      return true;
    }

    if (QuestUiTranslationCache.TryGetAppliedSnapshot(
            questNameText,
            out var cachedSnapshot) &&
        IsTranslatedPayloadReady(cachedSnapshot.AppliedText))
    {
      translatedNameText = cachedSnapshot.AppliedText;
      return true;
    }

    if (foundQuestPlate != null &&
        IsTranslatedPayloadReady(foundQuestPlate.TranslatedQuestName))
    {
      translatedNameText = foundQuestPlate.TranslatedQuestName ?? string.Empty;
#if DEBUG
      PluginRuntimeLog.Debug(
          $"Name from database: {questNameText} -> {translatedNameText}");
#endif
      return true;
    }

    if (foundQuestPopupText != null &&
        IsTranslatedPayloadReady(foundQuestPopupText.TranslatedTitle))
    {
      translatedNameText = foundQuestPopupText.TranslatedTitle ?? string.Empty;
      return true;
    }

    if (this.TryGetQueuedTranslation(cacheKey, out var cachedTranslatedName) &&
        IsTranslatedPayloadReady(cachedTranslatedName))
    {
      translatedNameText = cachedTranslatedName;
#if DEBUG
      PluginRuntimeLog.Debug(
          $"Name translated: {questNameText} -> {translatedNameText}");
#endif
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Enqueues the JournalResult title translation through the shared quest
  ///     broker.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="sourceLanguage">The captured source language.</param>
  /// <param name="questNameText">The original quest title.</param>
  /// <param name="questId">The optional canonical quest id.</param>
  private void QueueJournalResultTranslation(
      string cacheKey,
      SourceClientLanguage sourceLanguage,
      string questNameText,
      string? questId)
  {
    this.QueueTranslation(
        cacheKey,
        () => this.Translate(questNameText, sourceLanguage),
        translatedNameText =>
        {
          if (!IsTranslatedPayloadReady(translatedNameText))
          {
            return;
          }

          if (!string.IsNullOrWhiteSpace(questId))
          {
            var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                sourceLanguage,
                questNameText,
                string.Empty,
                translatedNameText,
                string.Empty,
                questId);

            var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
            PluginRuntimeLog.Debug(
                $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
            return;
          }

          var translatedQuestPopupText = this.CreateQuestPopupText(
              JournalResultAddonName,
              sourceLanguage,
              questNameText,
              string.Empty,
              translatedNameText,
              string.Empty);
          _ = Task.Run(
              () => this.InsertQuestPopupTextAsync(translatedQuestPopupText));
        });
  }

  /// <summary>
  ///     Attempts to promote a pending JournalResult title from the broker or
  ///     from the persisted quest row.
  /// </summary>
  /// <returns><c>true</c> when a translated title became available.</returns>
  private bool TryRefreshJournalResultPendingTranslation()
  {
    var state = this.currentJournalResultHoverState;
    if (state == null)
    {
      return false;
    }

    var translatedNameText = string.Empty;
    var translatedPayloadReady = false;
    if (this.TryGetQueuedTranslation(
            state.CacheKey,
            out var cachedTranslatedName) &&
        IsTranslatedPayloadReady(cachedTranslatedName))
    {
      translatedNameText = cachedTranslatedName;
      translatedPayloadReady = true;
    }

    if (!translatedPayloadReady)
    {
      var foundQuestPlate = this.FindJournalResultQuestPlate(
          state.SourceLanguage,
          state.OriginalQuestName,
          state.QuestId);
      if (foundQuestPlate != null &&
          IsTranslatedPayloadReady(foundQuestPlate.TranslatedQuestName))
      {
        translatedNameText =
            foundQuestPlate.TranslatedQuestName ?? string.Empty;
        translatedPayloadReady = true;
      }
    }

    if (!translatedPayloadReady &&
        string.IsNullOrWhiteSpace(state.QuestId))
    {
      var foundQuestPopupText = this.FindQuestPopupText(
          this.CreateQuestPopupText(
              JournalResultAddonName,
              state.SourceLanguage,
              state.OriginalQuestName,
              string.Empty));
      if (foundQuestPopupText != null &&
          IsTranslatedPayloadReady(foundQuestPopupText.TranslatedTitle))
      {
        translatedNameText =
            foundQuestPopupText.TranslatedTitle ?? string.Empty;
        translatedPayloadReady = true;
      }
    }

    if (!translatedPayloadReady)
    {
      this.nextJournalResultRetryUtc =
          DateTime.UtcNow + JournalResultRetryInterval;
      return false;
    }

    if (this.JournalResultShouldRemoveDiacritics)
    {
      translatedNameText = this.NormalizeQuestText(translatedNameText);
    }

    QuestUiTranslationCache.Remember(
        state.OriginalQuestName,
        translatedNameText);
    this.RememberJournalResultHoverState(
        state.CacheKey,
        state.SourceLanguage,
        state.OriginalQuestName,
        translatedNameText,
        state.QuestId);
    return true;
  }

  /// <summary>
  ///     Stores the current JournalResult hover payload and retry state.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="sourceLanguage">The captured source language.</param>
  /// <param name="originalQuestName">The original quest title.</param>
  /// <param name="translatedQuestName">The translated quest title.</param>
  /// <param name="questId">The optional canonical quest id.</param>
  private void RememberJournalResultHoverState(
      string cacheKey,
      SourceClientLanguage sourceLanguage,
      string originalQuestName,
      string translatedQuestName,
      string? questId)
  {
    this.currentJournalResultHoverState = new JournalResultHoverState(
        cacheKey,
        sourceLanguage,
        originalQuestName,
        translatedQuestName,
        questId);
    this.hasPendingJournalResultTranslation =
        !this.currentJournalResultHoverState.TranslatedPayloadReady;
    this.nextJournalResultRetryUtc = this.hasPendingJournalResultTranslation
        ? DateTime.UtcNow + JournalResultRetryInterval
        : DateTime.MinValue;
    this.lastAppliedDisplayMode =
        this.Config.JournalResultTranslationDisplayMode;
    this.needsJournalResultHoverRefresh = true;
  }

  /// <summary>
  ///     Registers JournalResult hover tooltip on the title text node when
  ///     possible, falling back to the addon window otherwise.
  /// </summary>
  private unsafe void RegisterJournalResultHoverTooltip()
  {
    this.RemoveHoverTooltipsByPrefix(JournalResultHoverPrefix);

    var state = this.currentJournalResultHoverState;
    if (state == null)
    {
      this.lastAppliedDisplayMode =
          this.Config.JournalResultTranslationDisplayMode;
      this.needsJournalResultHoverRefresh = false;
      return;
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(JournalResultAddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    this.ApplyJournalResultNativeState(addon, state);

    var nativeReadyForSwap =
        !this.JournalResultHoverShowsOriginal ||
        !this.JournalResultWritesNativeTranslation ||
        this.ownsJournalResultNativeMutation;
    var canRenderTooltip = this.CanRenderJournalResultHoverTooltip(
        state.TranslatedPayloadReady && nativeReadyForSwap);
    if (!canRenderTooltip)
    {
      this.lastAppliedDisplayMode =
          this.Config.JournalResultTranslationDisplayMode;
      this.needsJournalResultHoverRefresh = false;
      return;
    }

    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestName,
            state.TranslatedQuestName,
            out var nameNode))
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalResult-QuestName-{(nint)nameNode:X}",
          nameNode,
          state.OriginalQuestName,
          state.TranslatedQuestName,
          translatedPayloadReady: canRenderTooltip,
          swapEnabled: this.JournalResultHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
    }
    else
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalResult-{(nint)addon:X}",
          addon,
          state.OriginalQuestName,
          state.TranslatedQuestName,
          translatedPayloadReady: canRenderTooltip,
          swapEnabled: this.JournalResultHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
    }

    this.lastAppliedDisplayMode =
        this.Config.JournalResultTranslationDisplayMode;
    this.needsJournalResultHoverRefresh = false;
  }

  /// <summary>
  ///     Applies or restores JournalResult native text when the current display
  ///     mode requires this handler-owned mutation.
  /// </summary>
  /// <param name="addon">The visible JournalResult addon.</param>
  /// <param name="state">The current JournalResult hover state.</param>
  private unsafe void ApplyJournalResultNativeState(
      AtkUnitBase* addon,
      JournalResultHoverState state)
  {
    if (!state.TranslatedPayloadReady)
    {
      return;
    }

    if (!this.JournalResultWritesNativeTranslation &&
        !this.ownsJournalResultNativeMutation)
    {
      return;
    }

    var targetQuestName = this.JournalResultWritesNativeTranslation
        ? state.TranslatedQuestName
        : state.OriginalQuestName;
    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestName,
            state.TranslatedQuestName,
            out var nameNode))
    {
      nameNode->SetText(targetQuestName);
      this.ownsJournalResultNativeMutation =
          this.JournalResultWritesNativeTranslation;
    }
  }

  /// <summary>
  ///     Clears JournalResult hover registrations and local runtime state.
  /// </summary>
  private void ClearJournalResultRuntimeState()
  {
    this.currentJournalResultHoverState = null;
    this.hasPendingJournalResultTranslation = false;
    this.ownsJournalResultNativeMutation = false;
    this.lastAppliedDisplayMode = null;
    this.nextJournalResultRetryUtc = DateTime.MinValue;
    this.needsJournalResultHoverRefresh = false;
    this.RemoveHoverTooltipsByPrefix(JournalResultHoverPrefix);
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
      this.ClearJournalResultRuntimeState();
    }
  }

  /// <summary>
  ///     Local JournalResult payload retained between setup and visible draw
  ///     passes.
  /// </summary>
  /// <param name="CacheKey">The stable translation cache key.</param>
  /// <param name="SourceLanguage">The captured source language.</param>
  /// <param name="OriginalQuestName">The original quest title.</param>
  /// <param name="TranslatedQuestName">The translated quest title.</param>
  /// <param name="QuestId">The optional canonical quest id.</param>
  private sealed record JournalResultHoverState(
      string CacheKey,
      SourceClientLanguage SourceLanguage,
      string OriginalQuestName,
      string TranslatedQuestName,
      string? QuestId)
  {
    /// <summary>
    ///     Gets whether the translated JournalResult payload is complete.
    /// </summary>
    public bool TranslatedPayloadReady =>
        IsTranslatedPayloadReady(this.TranslatedQuestName);
  }
}


