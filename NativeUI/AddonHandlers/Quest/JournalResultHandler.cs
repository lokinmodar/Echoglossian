// <copyright file="JournalResultHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.Helpers;
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

  private const uint JournalResultDescriptionTextId = 543;

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
      this.RestoreJournalResultOriginals();
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
              out var questNameText,
              out var questNamePayload))
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

      var storedMessagePayload = ResolveJournalResultStoredMessage(
          foundQuestPlate?.OriginalQuestMessage,
          foundQuestPlate?.TranslatedQuestMessage,
          foundQuestPopupText?.OriginalBody,
          foundQuestPopupText?.TranslatedBody);

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
            storedMessagePayload[0],
            storedMessagePayload[1],
            questId,
            questNamePayload,
            null);
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
          storedMessagePayload[0],
          storedMessagePayload[1],
          questId,
          questNamePayload,
          null);

      if (this.JournalResultWritesNativeTranslation)
      {
        SetJournalResultValue(
            &setupAtkValues[1],
            this.currentJournalResultHoverState?.TranslatedQuestNamePayload,
            translatedNameText);
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
      this.RestoreJournalResultOriginals();
      this.ClearJournalResultRuntimeState();
      return;
    }

    if (this.currentJournalResultHoverState == null)
    {
      return;
    }

    this.TryCaptureJournalResultVisiblePayloads(addon);

    if (!this.JournalResultWritesNativeTranslation &&
        this.ownsJournalResultNativeMutation)
    {
      this.RestoreJournalResultOriginals();
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
      out string questNameText,
      out byte[]? questNamePayload)
  {
    questNameText = string.Empty;
    questNamePayload = null;
    if (setupAtkValues == null ||
        setupAtkValues[1].Type != ValueType.String ||
        !setupAtkValues[1].String.HasValue)
    {
      return false;
    }

    questNameText = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)setupAtkValues[1].String.Value);
    try
    {
      questNamePayload = MemoryHelper.ReadSeStringNullTerminated(
              (nint)setupAtkValues[1].String.Value)
          .Encode();
    }
    catch
    {
      questNamePayload = null;
    }

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

    if (!string.IsNullOrWhiteSpace(questId))
    {
      var foundQuestPlate = this.FindQuestPlate(questPlate);
      if (foundQuestPlate != null)
      {
        return foundQuestPlate;
      }
    }

    return this.FindQuestPlateByName(questPlate);
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
    QuestPlate? foundQuestPlate = null;
    QuestPopupText? foundQuestPopupText = null;
    foundQuestPlate = this.FindJournalResultQuestPlate(
        state.SourceLanguage,
        state.OriginalQuestName,
        state.QuestId);
    if (foundQuestPlate != null &&
        IsTranslatedPayloadReady(foundQuestPlate.TranslatedQuestName))
    {
      translatedNameText = foundQuestPlate.TranslatedQuestName ?? string.Empty;
      translatedPayloadReady = true;
    }

    if (!translatedPayloadReady &&
        this.TryGetQueuedTranslation(
            state.CacheKey,
            out var cachedTranslatedName) &&
        IsTranslatedPayloadReady(cachedTranslatedName))
    {
      translatedNameText = cachedTranslatedName;
      translatedPayloadReady = true;
    }

    if (!translatedPayloadReady &&
        string.IsNullOrWhiteSpace(state.QuestId))
    {
      foundQuestPopupText = this.FindQuestPopupText(
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

    var storedMessagePayload = ResolveJournalResultStoredMessage(
        foundQuestPlate?.OriginalQuestMessage,
        foundQuestPlate?.TranslatedQuestMessage,
        foundQuestPopupText?.OriginalBody,
        foundQuestPopupText?.TranslatedBody);

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
        storedMessagePayload[0],
        storedMessagePayload[1],
        state.QuestId,
        state.OriginalQuestNamePayload,
        state.OriginalQuestMessagePayload);
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
      string originalQuestMessage,
      string translatedQuestMessage,
      string? questId,
      byte[]? originalQuestNamePayload,
      byte[]? originalQuestMessagePayload)
  {
    var retainedOriginalQuestNamePayload =
        ReadableSeStringPayloadHelper.RetainMatchingPayload(
            originalQuestNamePayload,
            originalQuestName);
    var retainedOriginalQuestMessagePayload =
        ReadableSeStringPayloadHelper.RetainMatchingPayload(
            originalQuestMessagePayload,
            originalQuestMessage);
    var translatedQuestNamePayload =
        ReadableSeStringPayloadHelper.ProjectReadablePayloadBytes(
            retainedOriginalQuestNamePayload,
            originalQuestName,
            translatedQuestName);
    var translatedQuestMessagePayload =
        ReadableSeStringPayloadHelper.ProjectReadablePayloadBytes(
            retainedOriginalQuestMessagePayload,
            originalQuestMessage,
            translatedQuestMessage);
    this.currentJournalResultHoverState = new JournalResultHoverState(
        cacheKey,
        sourceLanguage,
        originalQuestName,
        translatedQuestName,
        originalQuestMessage,
        translatedQuestMessage,
        questId,
        retainedOriginalQuestNamePayload,
        translatedQuestNamePayload,
        retainedOriginalQuestMessagePayload,
        translatedQuestMessagePayload);
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

    var registeredName = false;
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
      registeredName = true;
    }

    var registeredMessage = false;
    if (!string.IsNullOrWhiteSpace(state.OriginalQuestMessage) &&
        IsTranslatedPayloadReady(state.TranslatedQuestMessage) &&
        this.TryFindJournalResultMessageNode(
            addon,
            state,
            out var messageNode))
    {
      AtkResNode* preferredHoverNode = null;
      this.TryResolveJournalResultPreferredHoverNode(
          addon,
          messageNode,
          out preferredHoverNode);
      var messageHoverKey = $"JournalResult-QuestBody-{(nint)messageNode:X}";
      if (TryBuildPopupBodyHoverBounds(
              messageNode,
              preferredHoverNode,
              out var messageTopLeft,
              out var messageBottomRight))
      {
        this.RegisterTranslatedHoverTooltip(
            messageHoverKey,
            messageTopLeft,
            messageBottomRight,
            messageNode,
            state.OriginalQuestMessage,
            state.TranslatedQuestMessage,
            canRenderTooltip,
            this.JournalResultHoverShowsOriginal,
            true);
      }
      else
      {
        this.RegisterTranslatedHoverTooltip(
            messageHoverKey,
            messageNode,
            state.OriginalQuestMessage,
            state.TranslatedQuestMessage,
            translatedPayloadReady: canRenderTooltip,
            swapEnabled: this.JournalResultHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
      }

      registeredMessage = true;
    }

    if (!registeredName && !registeredMessage)
    {
      var originalAddonPayload = state.OriginalQuestName;
      var translatedAddonPayload = state.TranslatedQuestName;
      if (!string.IsNullOrWhiteSpace(state.OriginalQuestMessage) &&
          IsTranslatedPayloadReady(state.TranslatedQuestMessage))
      {
        originalAddonPayload =
            $"{state.OriginalQuestName}\n{state.OriginalQuestMessage}";
        translatedAddonPayload =
            $"{state.TranslatedQuestName}\n{state.TranslatedQuestMessage}";
      }

      this.RegisterTranslatedHoverTooltip(
          $"JournalResult-{(nint)addon:X}",
          addon,
          originalAddonPayload,
          translatedAddonPayload,
          translatedPayloadReady: canRenderTooltip,
          swapEnabled: this.JournalResultHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
    }
    else if (!registeredMessage &&
             !string.IsNullOrWhiteSpace(state.OriginalQuestMessage) &&
             IsTranslatedPayloadReady(state.TranslatedQuestMessage))
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalResult-QuestBody-{(nint)addon:X}",
          addon,
          state.OriginalQuestMessage,
          state.TranslatedQuestMessage,
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
  ///     Captures the current visible JournalResult title and body payloads
  ///     before handler-owned native mutation rewrites the live nodes.
  /// </summary>
  /// <param name="addon">The visible JournalResult addon.</param>
  /// <returns>
  ///     <c>true</c> when at least one newly captured payload was retained.
  /// </returns>
  private unsafe bool TryCaptureJournalResultVisiblePayloads(AtkUnitBase* addon)
  {
    var state = this.currentJournalResultHoverState;
    if (state == null || addon == null || !addon->IsVisible)
    {
      return false;
    }

    var updatedOriginalQuestNamePayload =
        state.OriginalQuestNamePayload;
    if (updatedOriginalQuestNamePayload == null &&
        this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestName,
            state.TranslatedQuestName,
            out var nameNode))
    {
      updatedOriginalQuestNamePayload =
          ReadableSeStringPayloadHelper.TryCaptureMatchingPayload(
              nameNode,
              state.OriginalQuestName);
    }

    var updatedOriginalQuestMessagePayload =
        state.OriginalQuestMessagePayload;
    if (!string.IsNullOrWhiteSpace(state.OriginalQuestMessage) &&
        updatedOriginalQuestMessagePayload == null &&
        this.TryFindJournalResultMessageNode(
            addon,
            state,
            out var messageNode))
    {
      updatedOriginalQuestMessagePayload =
          ReadableSeStringPayloadHelper.TryCaptureMatchingPayload(
              messageNode,
              state.OriginalQuestMessage);
    }

    if (ReferenceEquals(
            updatedOriginalQuestNamePayload,
            state.OriginalQuestNamePayload) &&
        ReferenceEquals(
            updatedOriginalQuestMessagePayload,
            state.OriginalQuestMessagePayload))
    {
      return false;
    }

    this.RememberJournalResultHoverState(
        state.CacheKey,
        state.SourceLanguage,
        state.OriginalQuestName,
        state.TranslatedQuestName,
        state.OriginalQuestMessage,
        state.TranslatedQuestMessage,
        state.QuestId,
        updatedOriginalQuestNamePayload,
        updatedOriginalQuestMessagePayload);
    return true;
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
    var targetQuestMessage = this.JournalResultWritesNativeTranslation
        ? state.TranslatedQuestMessage
        : state.OriginalQuestMessage;
    var targetQuestNamePayload = this.JournalResultWritesNativeTranslation
        ? state.TranslatedQuestNamePayload
        : state.OriginalQuestNamePayload;
    var targetQuestMessagePayload = this.JournalResultWritesNativeTranslation
        ? state.TranslatedQuestMessagePayload
        : state.OriginalQuestMessagePayload;
    var appliedName = false;
    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestName,
            state.TranslatedQuestName,
            out var nameNode))
    {
      SetJournalResultTextNode(
          nameNode,
          targetQuestNamePayload,
          targetQuestName);
      appliedName = true;
    }

    var appliedMessage = string.IsNullOrWhiteSpace(state.OriginalQuestMessage);
    if (!appliedMessage &&
        IsTranslatedPayloadReady(state.TranslatedQuestMessage) &&
        this.TryFindJournalResultMessageNode(
            addon,
            state,
            out var messageNode))
    {
      SetJournalResultTextNode(
          messageNode,
          targetQuestMessagePayload,
          targetQuestMessage);
      appliedMessage = true;
    }

    if (appliedName && appliedMessage)
    {
      this.ownsJournalResultNativeMutation =
          this.JournalResultWritesNativeTranslation;
    }
  }

  /// <summary>
  ///     Restores the original JournalResult title when this handler owns the
  ///     visible native mutation.
  /// </summary>
  private unsafe void RestoreJournalResultOriginals()
  {
    if (!this.ownsJournalResultNativeMutation)
    {
      return;
    }

    var state = this.currentJournalResultHoverState;
    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(JournalResultAddonName);
    if (addon == null || state == null)
    {
      this.ownsJournalResultNativeMutation = false;
      return;
    }

    if (addon->AtkValues != null && addon->AtkValuesCount > 1)
    {
      SetJournalResultValue(
          &addon->AtkValues[1],
          state.OriginalQuestNamePayload,
          state.OriginalQuestName);
    }

    if (this.TryFindReadableTextNodeByText(
        addon,
        state.OriginalQuestName,
        state.TranslatedQuestName,
        out var nameNode))
    {
      SetJournalResultTextNode(
          nameNode,
          state.OriginalQuestNamePayload,
          state.OriginalQuestName);
    }

    if (!string.IsNullOrWhiteSpace(state.OriginalQuestMessage) &&
        this.TryFindJournalResultMessageNode(
            addon,
            state,
            out var messageNode))
    {
      SetJournalResultTextNode(
          messageNode,
          state.OriginalQuestMessagePayload,
          state.OriginalQuestMessage);
    }

    this.ownsJournalResultNativeMutation = false;
  }

  /// <summary>
  ///     Finds the visible JournalResult body node, falling back to the shared
  ///     popup-section structural resolver when readable-node scans miss the
  ///     empty runtime body node.
  /// </summary>
  /// <param name="addon">The live JournalResult addon instance.</param>
  /// <param name="state">The current JournalResult hover state.</param>
  /// <param name="messageNode">The resolved body node, if any.</param>
  /// <returns><c>true</c> when the body node was found.</returns>
  private unsafe bool TryFindJournalResultMessageNode(
      AtkUnitBase* addon,
      JournalResultHoverState state,
      out AtkTextNode* messageNode)
  {
    messageNode = null;
    if (addon == null || string.IsNullOrWhiteSpace(state.OriginalQuestMessage))
    {
      return false;
    }

    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestMessage,
            state.TranslatedQuestMessage,
            out messageNode))
    {
      return true;
    }

    return TryFindPopupSectionBodyTextNodeByHeadingTextId(
        addon,
        JournalResultDescriptionTextId,
        out messageNode);
  }

  /// <summary>
  ///     Resolves the structural body node only when the heading-based popup
  ///     resolver identifies the same live text node selected for payload work.
  /// </summary>
  /// <param name="addon">The live JournalResult addon instance.</param>
  /// <param name="messageNode">The live body text node.</param>
  /// <param name="preferredHoverNode">
  ///     The matching structural body node used only for tooltip geometry.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a matching structural body node was
  ///     resolved; otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool TryResolveJournalResultPreferredHoverNode(
      AtkUnitBase* addon,
      AtkTextNode* messageNode,
      out AtkResNode* preferredHoverNode)
  {
    preferredHoverNode = null;
    return messageNode != null &&
           TryFindPopupSectionBodyHoverNodeByHeadingTextId(
               addon,
               JournalResultDescriptionTextId,
               messageNode,
               out preferredHoverNode);
  }

  /// <summary>
  ///     Writes a JournalResult title payload back into an addon value while
  ///     falling back to plain text when no rich payload is available.
  /// </summary>
  /// <param name="atkValue">The addon value to write.</param>
  /// <param name="payload">The optional rich payload bytes.</param>
  /// <param name="fallbackText">The plain-text fallback.</param>
  private static unsafe void SetJournalResultValue(
      AtkValue* atkValue,
      byte[]? payload,
      string fallbackText)
  {
    if (atkValue == null)
    {
      return;
    }

    if (payload is { Length: > 0 })
    {
      atkValue->SetManagedString(payload);
      return;
    }

    atkValue->SetManagedString(fallbackText);
  }

  /// <summary>
  ///     Writes one JournalResult text payload back into the live text node
  ///     while falling back to plain text when no rich payload is available.
  /// </summary>
  /// <param name="textNode">The live text node to mutate.</param>
  /// <param name="payload">The optional rich payload bytes.</param>
  /// <param name="fallbackText">The plain-text fallback.</param>
  private static unsafe void SetJournalResultTextNode(
      AtkTextNode* textNode,
      byte[]? payload,
      string fallbackText)
  {
    if (textNode == null)
    {
      return;
    }

    if (payload is { Length: > 0 })
    {
      textNode->SetText(payload);
      return;
    }

    textNode->SetText(fallbackText);
  }

  /// <summary>
  ///     Resolves the best stored JournalResult body payload without forcing a
  ///     body path when only title data is trustworthy.
  /// </summary>
  /// <param name="canonicalOriginalBody">The canonical original body text.</param>
  /// <param name="canonicalTranslatedBody">
  ///     The canonical translated body text.
  /// </param>
  /// <param name="popupOriginalBody">The popup-scoped original body text.</param>
  /// <param name="popupTranslatedBody">
  ///     The popup-scoped translated body text.
  /// </param>
  /// <returns>
  ///     A two-item payload with original body at index 0 and translated body
  ///     at index 1, or empty strings when no complete body is available.
  /// </returns>
  private static string[] ResolveJournalResultStoredMessage(
      string? canonicalOriginalBody,
      string? canonicalTranslatedBody,
      string? popupOriginalBody,
      string? popupTranslatedBody)
  {
    if (!string.IsNullOrWhiteSpace(canonicalOriginalBody) &&
        IsTranslatedPayloadReady(canonicalTranslatedBody))
    {
      return [canonicalOriginalBody, canonicalTranslatedBody ?? string.Empty];
    }

    if (!string.IsNullOrWhiteSpace(popupOriginalBody) &&
        IsTranslatedPayloadReady(popupTranslatedBody))
    {
      return [popupOriginalBody, popupTranslatedBody ?? string.Empty];
    }

    return [string.Empty, string.Empty];
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
  private unsafe void OnJournalResultCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, JournalResultAddonName, StringComparison.Ordinal))
    {
      this.RestoreJournalResultOriginals();
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
  /// <param name="OriginalQuestMessage">The optional original quest body.</param>
  /// <param name="TranslatedQuestMessage">
  ///     The optional translated quest body.
  /// </param>
  /// <param name="QuestId">The optional canonical quest id.</param>
  /// <param name="OriginalQuestNamePayload">
  ///     The captured original quest title payload bytes, if available.
  /// </param>
  /// <param name="TranslatedQuestNamePayload">
  ///     The projected translated quest title payload bytes, if available.
  /// </param>
  /// <param name="OriginalQuestMessagePayload">
  ///     The captured original quest body payload bytes, if available.
  /// </param>
  /// <param name="TranslatedQuestMessagePayload">
  ///     The projected translated quest body payload bytes, if available.
  /// </param>
  private sealed record JournalResultHoverState(
      string CacheKey,
      SourceClientLanguage SourceLanguage,
      string OriginalQuestName,
      string TranslatedQuestName,
      string OriginalQuestMessage,
      string TranslatedQuestMessage,
      string? QuestId,
      byte[]? OriginalQuestNamePayload,
      byte[]? TranslatedQuestNamePayload,
      byte[]? OriginalQuestMessagePayload,
      byte[]? TranslatedQuestMessagePayload)
  {
    /// <summary>
    ///     Gets whether the translated JournalResult payload is complete.
    /// </summary>
    public bool TranslatedPayloadReady =>
        IsTranslatedPayloadReady(this.TranslatedQuestName);
  }
}


