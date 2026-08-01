// <copyright file="JournalAcceptHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.Helpers;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles the JournalAccept quest addon runtime inside the standalone
///     quest-handler model.
/// </summary>
internal sealed class JournalAcceptHandler : QuestAddonHandlerBase
{
  private const string JournalAcceptAddonName = "JournalAccept";

  private const string JournalAcceptHoverPrefix = "JournalAccept-";

  private const uint JournalAcceptSummaryTextId = 476;

  private static readonly TimeSpan JournalAcceptRetryInterval =
      TimeSpan.FromSeconds(2);

  private JournalAcceptHoverState? currentJournalAcceptHoverState;

  private bool hasPendingJournalAcceptTranslations;

  private bool ownsJournalAcceptNativeMutation;

  private JournalTranslationDisplayMode? lastAppliedDisplayMode;

  private DateTime nextJournalAcceptRetryUtc = DateTime.MinValue;

  private bool needsJournalAcceptHoverRefresh;

  /// <summary>
  ///     Initializes a new instance of the <see cref="JournalAcceptHandler" /> class.
  /// </summary>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public JournalAcceptHandler(QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    this.RegisterHandler(AddonEvent.PreSetup, this.OnJournalAcceptEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnJournalAcceptPreDrawEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnJournalAcceptCleanupEvent);
    this.RegisterHandler(
        AddonEvent.PreFinalize,
        this.OnJournalAcceptCleanupEvent);
  }

  /// <summary>
  ///     Gets whether the JournalAccept family should write translated text
  ///     into the native addon.
  /// </summary>
  private bool JournalAcceptWritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.JournalAcceptTranslationDisplayMode);

  /// <summary>
  ///     Gets whether the JournalAccept family hover tooltips should show the
  ///     original text.
  /// </summary>
  private bool JournalAcceptHoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.JournalAcceptTranslationDisplayMode);

  /// <summary>
  ///     Gets whether JournalAccept may render a hover tooltip for a payload
  ///     whose translated content is ready.
  /// </summary>
  /// <param name="translatedPayloadReady">
  ///     Whether the translated payload required by the current mode is ready.
  /// </param>
  /// <returns><c>true</c> when the hover tooltip may be rendered.</returns>
  private bool CanRenderJournalAcceptHoverTooltip(
      bool translatedPayloadReady) =>
      QuestAddonModeHelpers.CanRenderHoverTooltip(
          this.Config.JournalAcceptTranslationDisplayMode,
          translatedPayloadReady);

  /// <summary>
  ///     Gets whether translated JournalAccept text should be normalized before
  ///     being written into the native UI.
  /// </summary>
  private bool JournalAcceptShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.JournalAcceptTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  /// <summary>
  ///     Determines whether the translated JournalAccept title and body are
  ///     both ready for native application or tooltip rendering.
  /// </summary>
  /// <param name="translatedQuestName">The translated quest title.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  /// <returns><c>true</c> when both translated fields are available.</returns>
  internal static bool IsTranslatedPayloadReady(
      string? translatedQuestName,
      string? translatedQuestMessage)
  {
    return !string.IsNullOrWhiteSpace(translatedQuestName) &&
           !string.IsNullOrWhiteSpace(translatedQuestMessage);
  }

  /// <summary>
  ///     Handles JournalAccept setup events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalAcceptEvent(AddonEvent type, AddonArgs args)
  {
#if DEBUG
    PluginRuntimeLog.Debug($"JournalAcceptHandler AddonEvent: {type} {args.AddonName}");
#endif

    if (!string.Equals(
            args.AddonName,
            JournalAcceptAddonName,
            StringComparison.Ordinal))
    {
      return;
    }

    if (!this.Config.TranslateJournalAccept ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreJournalAcceptOriginals();
      this.ClearJournalAcceptRuntimeState();
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
      if (!TryReadJournalAcceptSetupText(
              setupAtkValues,
              out var questName,
              out var questMessage,
              out var questMessagePayload))
      {
        return;
      }

#if DEBUG
      PluginRuntimeLog.Debug(
          $"Language: {ClientStateInterface.ClientLanguage.Humanize()}");
      PluginRuntimeLog.Debug($"Quest name: {questName}");
      PluginRuntimeLog.Debug($"Quest message: {questMessage}");
#endif

      if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
              out var sourceLanguage))
      {
        return;
      }

      var hasCanonicalQuestId =
          QuestPopupIdentity.TryReadJournalAcceptQuestId(
              setupAtkValues,
              out var questId) &&
          !string.IsNullOrWhiteSpace(questId);
      questId = hasCanonicalQuestId ? questId : string.Empty;

      var questPlate = this.CreateQuestPlate(
          sourceLanguage,
          questName,
          questMessage,
          questId);
      if (QuestProgressResolver.TryResolveQuestProgress(
              questPlate,
              out var resolvedAcceptSnapshot))
      {
        questPlate.SourceContentHash = resolvedAcceptSnapshot.ContentHash;
      }

      var persistenceTarget = hasCanonicalQuestId
          ? QuestPopupPersistenceTarget.CanonicalQuestPlate
          : QuestPopupPersistenceTarget.DedicatedPopupTable;
      QuestPlate? foundQuestPlate = null;
      QuestPopupText? foundQuestPopupText = null;
      if (persistenceTarget == QuestPopupPersistenceTarget.CanonicalQuestPlate)
      {
        foundQuestPlate = this.FindQuestPlate(questPlate);
        if (foundQuestPlate != null &&
            !string.Equals(
                foundQuestPlate.GameVersion,
                GetGameVersion(),
                StringComparison.Ordinal))
        {
          this.UpdateQuestPlateGameVersion(
              foundQuestPlate.Id,
              GetGameVersion());
        }
      }
      else
      {
        var questPopupText = this.CreateQuestPopupText(
            JournalAcceptAddonName,
            sourceLanguage,
            questName,
            questMessage,
            questId: questId);
        questPopupText.SourceContentHash = questPlate.SourceContentHash;
        foundQuestPopupText = this.FindQuestPopupText(questPopupText);
      }

      var cacheKey = BuildJournalAcceptCacheKey(
          questName,
          questMessage,
          persistenceTarget,
          questId);

      if (!this.TryResolveJournalAcceptTranslation(
              cacheKey,
              questName,
              questMessage,
              foundQuestPlate,
              foundQuestPopupText,
              out var translatedQuestName,
              out var translatedQuestMessage))
      {
        this.RememberJournalAcceptHoverState(
            cacheKey,
            sourceLanguage,
            questName,
            questMessage,
            string.Empty,
            string.Empty,
            questPlate.SourceContentHash,
            persistenceTarget,
            questId,
            questMessagePayload);
        this.QueueJournalAcceptTranslation(
            cacheKey,
            sourceLanguage,
            questName,
            questMessage,
            questPlate.SourceContentHash,
            persistenceTarget,
            questId);
        this.RegisterJournalAcceptHoverTooltip();
        return;
      }

#if DEBUG
      PluginRuntimeLog.Debug(
          $"Using QuestPlate Replace - {translatedQuestName}: {translatedQuestMessage}");
#endif

      if (this.JournalAcceptShouldRemoveDiacritics)
      {
        translatedQuestName = this.NormalizeQuestText(
            translatedQuestName ?? string.Empty);
        translatedQuestMessage = this.NormalizeQuestText(
            translatedQuestMessage ?? string.Empty);
      }

      this.RememberJournalAcceptHoverState(
          cacheKey,
          sourceLanguage,
          questName,
          questMessage,
          translatedQuestName,
          translatedQuestMessage,
          questPlate.SourceContentHash,
          persistenceTarget,
          questId,
          questMessagePayload);

      if (this.JournalAcceptWritesNativeTranslation)
      {
        setupAtkValues[5].SetManagedString(translatedQuestName);
        SetJournalAcceptMessageValue(
            &setupAtkValues[12],
            this.currentJournalAcceptHoverState?.TranslatedQuestMessagePayload,
            translatedQuestMessage);
        this.ownsJournalAcceptNativeMutation = true;
      }
      else
      {
        this.ownsJournalAcceptNativeMutation = false;
      }

      QuestUiTranslationCache.Remember(questName, translatedQuestName);
      QuestUiTranslationCache.Remember(
          questMessage,
          translatedQuestMessage);

      this.RegisterJournalAcceptHoverTooltip();
    }
    catch (Exception e)
    {
      PluginRuntimeLog.Error("Exception at JournalAcceptHandler: " + e);
    }
  }

  /// <summary>
  ///     Refreshes JournalAccept hover targets after the addon has visible
  ///     text nodes and after delayed translations settle.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalAcceptPreDrawEvent(AddonEvent type, AddonArgs args)
  {
    if (!string.Equals(
            args.AddonName,
            JournalAcceptAddonName,
            StringComparison.Ordinal))
    {
      return;
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(JournalAcceptAddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    if (!this.Config.TranslateJournalAccept ||
        this.DisableTranslationAccordingToState())
    {
      this.RestoreJournalAcceptOriginals();
      this.ClearJournalAcceptRuntimeState();
      return;
    }

    if (this.currentJournalAcceptHoverState == null)
    {
      return;
    }

    this.TryPromoteJournalAcceptVisibleBodyPayload(addon);

    if (!this.JournalAcceptWritesNativeTranslation &&
        this.ownsJournalAcceptNativeMutation)
    {
      this.RestoreJournalAcceptOriginals();
    }

    var shouldRefresh =
        this.needsJournalAcceptHoverRefresh ||
        this.lastAppliedDisplayMode !=
        this.Config.JournalAcceptTranslationDisplayMode ||
        (this.hasPendingJournalAcceptTranslations &&
         DateTime.UtcNow >= this.nextJournalAcceptRetryUtc);
    if (!shouldRefresh)
    {
      return;
    }

    if (this.hasPendingJournalAcceptTranslations)
    {
      this.TryRefreshJournalAcceptPendingTranslation();
    }

    this.RegisterJournalAcceptHoverTooltip();
  }

  /// <summary>
  ///     Reads the JournalAccept setup title and message values safely.
  /// </summary>
  /// <param name="setupAtkValues">The setup value array.</param>
  /// <param name="questName">The captured quest title.</param>
  /// <param name="questMessage">The captured quest message.</param>
  /// <returns><c>true</c> when both values are readable strings.</returns>
  private static unsafe bool TryReadJournalAcceptSetupText(
      AtkValue* setupAtkValues,
      out string questName,
      out string questMessage,
      out byte[]? questMessagePayload)
  {
    questName = string.Empty;
    questMessage = string.Empty;
    questMessagePayload = null;
    if (setupAtkValues == null ||
        setupAtkValues[5].Type != ValueType.String ||
        !setupAtkValues[5].String.HasValue ||
        setupAtkValues[12].Type != ValueType.String ||
        !setupAtkValues[12].String.HasValue)
    {
      return false;
    }

    questName = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)setupAtkValues[5].String.Value);
    questMessage = MemoryHelper.ReadSeStringAsString(
        out _,
        (nint)setupAtkValues[12].String.Value);
    try
    {
      questMessagePayload = MemoryHelper.ReadSeStringNullTerminated(
              (nint)setupAtkValues[12].String.Value)
          .Encode();
    }
    catch
    {
      questMessagePayload = null;
    }

    return !string.IsNullOrWhiteSpace(questName) &&
           !string.IsNullOrWhiteSpace(questMessage);
  }

  /// <summary>
  ///     Builds the stable JournalAccept cache key for the current persistence
  ///     target.
  /// </summary>
  /// <param name="questName">The original quest title.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="persistenceTarget">The persistence bucket in use.</param>
  /// <param name="questId">The optional canonical quest id.</param>
  /// <returns>The stable cache key for the current JournalAccept payload.</returns>
  private static string BuildJournalAcceptCacheKey(
      string questName,
      string questMessage,
      QuestPopupPersistenceTarget persistenceTarget,
      string? questId)
  {
    return persistenceTarget == QuestPopupPersistenceTarget.CanonicalQuestPlate
        ? $"JournalAccept|QuestId:{questId}|{questName}|{questMessage}"
        : $"JournalAccept|Popup|{questName}|{questMessage}";
  }

  /// <summary>
  ///     Resolves a complete translated JournalAccept payload from the session
  ///     cache, persisted quest row, or completed broker result.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="questName">The original quest title.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="foundQuestPlate">The matching persisted quest row, if any.</param>
  /// <param name="foundQuestPopupText">
  ///     The matching dedicated popup row, if any.
  /// </param>
  /// <param name="translatedQuestName">The translated quest title.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  /// <returns><c>true</c> when a complete translated payload exists.</returns>
  private bool TryResolveJournalAcceptTranslation(
      string cacheKey,
      string questName,
      string questMessage,
      QuestPlate? foundQuestPlate,
      QuestPopupText? foundQuestPopupText,
      out string translatedQuestName,
      out string translatedQuestMessage)
  {
    translatedQuestName = string.Empty;
    translatedQuestMessage = string.Empty;
    if (this.currentJournalAcceptHoverState is
        {
          TranslatedPayloadReady: true,
        } cachedState &&
        string.Equals(
            cachedState.CacheKey,
            cacheKey,
            StringComparison.Ordinal))
    {
      translatedQuestName = cachedState.TranslatedQuestName;
      translatedQuestMessage = cachedState.TranslatedQuestMessage;
      return true;
    }

    if (QuestUiTranslationCache.TryGetAppliedSnapshot(
            questName,
            out var cachedNameSnapshot) &&
        QuestUiTranslationCache.TryGetAppliedSnapshot(
            questMessage,
            out var cachedMessageSnapshot) &&
        IsTranslatedPayloadReady(
            cachedNameSnapshot.AppliedText,
            cachedMessageSnapshot.AppliedText))
    {
      translatedQuestName = cachedNameSnapshot.AppliedText;
      translatedQuestMessage = cachedMessageSnapshot.AppliedText;
      return true;
    }

    if (foundQuestPlate != null &&
        IsTranslatedPayloadReady(
            foundQuestPlate.TranslatedQuestName,
            foundQuestPlate.TranslatedQuestMessage))
    {
      translatedQuestName = foundQuestPlate.TranslatedQuestName ?? string.Empty;
      translatedQuestMessage =
          foundQuestPlate.TranslatedQuestMessage ?? string.Empty;
#if DEBUG
      PluginRuntimeLog.Debug(
          $"From database - Name: {translatedQuestName}, Message: {translatedQuestMessage}");
#endif
      return true;
    }

    if (foundQuestPopupText != null &&
        IsTranslatedPayloadReady(
            foundQuestPopupText.TranslatedTitle,
            foundQuestPopupText.TranslatedBody))
    {
      translatedQuestName = foundQuestPopupText.TranslatedTitle ?? string.Empty;
      translatedQuestMessage =
          foundQuestPopupText.TranslatedBody ?? string.Empty;
      return true;
    }

    if (this.TryGetQueuedTranslation(
            cacheKey,
            out var cachedTranslatedPayload) &&
        TryDeserializeTranslationPair(
            cachedTranslatedPayload,
            out translatedQuestName,
            out translatedQuestMessage) &&
        IsTranslatedPayloadReady(translatedQuestName, translatedQuestMessage))
    {
#if DEBUG
      PluginRuntimeLog.Debug(
          $"Translated quest name: {translatedQuestName}");
      PluginRuntimeLog.Debug(
          $"Translated quest message: {translatedQuestMessage}");
#endif
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Promotes the current JournalAccept runtime payload to the richer
  ///     visible body text when setup capture only exposed the short
  ///     quest-sync marker.
  /// </summary>
  /// <param name="addon">The live JournalAccept addon instance.</param>
  /// <returns>
  ///     <c>true</c> when the runtime state was promoted to a richer visible
  ///     body payload; otherwise, <c>false</c>.
  /// </returns>
  private unsafe bool TryPromoteJournalAcceptVisibleBodyPayload(AtkUnitBase* addon)
  {
    var state = this.currentJournalAcceptHoverState;
    if (state == null)
    {
      return false;
    }

    if (!this.TryFindJournalAcceptMessageNode(
            addon,
            state,
            out var messageNode,
            out _,
            out var promotedOriginalQuestMessage) ||
        string.IsNullOrWhiteSpace(promotedOriginalQuestMessage) ||
        string.Equals(
            promotedOriginalQuestMessage,
            state.OriginalQuestMessage,
            StringComparison.Ordinal))
    {
      return false;
    }

    var promotedOriginalQuestMessagePayload =
        TryCaptureJournalAcceptMessagePayload(
            messageNode,
            promotedOriginalQuestMessage) ??
        state.OriginalQuestMessagePayload;

    var cacheKey = BuildJournalAcceptCacheKey(
        state.OriginalQuestName,
        promotedOriginalQuestMessage,
        state.PersistenceTarget,
        state.QuestId);
    if (string.Equals(cacheKey, state.CacheKey, StringComparison.Ordinal))
    {
      return false;
    }

    QuestPlate? foundQuestPlate = null;
    QuestPopupText? foundQuestPopupText = null;
    if (state.PersistenceTarget ==
        QuestPopupPersistenceTarget.CanonicalQuestPlate)
    {
      var questPlate = this.CreateQuestPlate(
          state.SourceLanguage,
          state.OriginalQuestName,
          promotedOriginalQuestMessage,
          state.QuestId);
      questPlate.SourceContentHash = state.SourceContentHash;
      foundQuestPlate = this.FindQuestPlate(questPlate);
    }
    else
    {
      var questPopupText = this.CreateQuestPopupText(
          JournalAcceptAddonName,
          state.SourceLanguage,
          state.OriginalQuestName,
          promotedOriginalQuestMessage,
          questId: state.QuestId);
      questPopupText.SourceContentHash = state.SourceContentHash;
      foundQuestPopupText = this.FindQuestPopupText(questPopupText);
    }

    var translatedQuestName = string.Empty;
    var translatedQuestMessage = string.Empty;
    if (this.TryResolveJournalAcceptTranslation(
            cacheKey,
            state.OriginalQuestName,
            promotedOriginalQuestMessage,
            foundQuestPlate,
            foundQuestPopupText,
            out var resolvedQuestName,
            out var resolvedQuestMessage))
    {
      translatedQuestName = resolvedQuestName;
      translatedQuestMessage = resolvedQuestMessage;
    }
    else if (!string.IsNullOrWhiteSpace(state.TranslatedQuestName))
    {
      translatedQuestName = state.TranslatedQuestName;
    }

    if (this.ownsJournalAcceptNativeMutation)
    {
      if (addon->AtkValues != null && addon->AtkValuesCount > 12)
      {
        addon->AtkValues[5].SetManagedString(state.OriginalQuestName);
        SetJournalAcceptMessageValue(
            &addon->AtkValues[12],
            state.OriginalQuestMessagePayload,
            state.OriginalQuestMessage);
      }

      if (this.TryFindReadableTextNodeByText(
              addon,
              state.OriginalQuestName,
              state.TranslatedQuestName,
              out var nameNode))
      {
        nameNode->SetText(state.OriginalQuestName);
      }

      if (messageNode != null && messageNode->IsVisible())
      {
        SetJournalAcceptMessageNode(
            messageNode,
            state.OriginalQuestMessagePayload,
            state.OriginalQuestMessage);
      }

      this.ownsJournalAcceptNativeMutation = false;
    }

    this.RememberJournalAcceptHoverState(
        cacheKey,
        state.SourceLanguage,
        state.OriginalQuestName,
        promotedOriginalQuestMessage,
        translatedQuestName,
        translatedQuestMessage,
        state.SourceContentHash,
        state.PersistenceTarget,
        state.QuestId,
        promotedOriginalQuestMessagePayload);
    if (!IsTranslatedPayloadReady(translatedQuestName, translatedQuestMessage))
    {
      this.QueueJournalAcceptTranslation(
          cacheKey,
          state.SourceLanguage,
          state.OriginalQuestName,
          promotedOriginalQuestMessage,
          state.SourceContentHash,
          state.PersistenceTarget,
          state.QuestId);
    }

    return true;
  }

  /// <summary>
  ///     Enqueues the JournalAccept title and body translation through the
  ///     shared quest broker.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="sourceLanguage">The captured source language.</param>
  /// <param name="questName">The original quest title.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="sourceContentHash">The resolved quest source hash.</param>
  /// <param name="persistenceTarget">The persistence bucket to update.</param>
  /// <param name="questId">The optional canonical quest id.</param>
  private void QueueJournalAcceptTranslation(
      string cacheKey,
      SourceClientLanguage sourceLanguage,
      string questName,
      string questMessage,
      string? sourceContentHash,
      QuestPopupPersistenceTarget persistenceTarget,
      string? questId)
  {
    this.QueueTranslation(
        cacheKey,
        () => SerializeTranslationPair(
            this.Translate(questName, sourceLanguage),
            this.Translate(questMessage, sourceLanguage)),
        translatedPayload =>
        {
          if (!TryDeserializeTranslationPair(
                  translatedPayload,
                  out var resolvedQuestName,
                  out var resolvedQuestMessage) ||
              !IsTranslatedPayloadReady(
                  resolvedQuestName,
                  resolvedQuestMessage))
          {
            return;
          }

          if (persistenceTarget ==
              QuestPopupPersistenceTarget.CanonicalQuestPlate)
          {
            var translatedQuestPlate = this.CreateTranslatedQuestPlate(
                sourceLanguage,
                questName,
                questMessage,
                resolvedQuestName,
                resolvedQuestMessage,
                questId);
            translatedQuestPlate.SourceContentHash = sourceContentHash;

            var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
            PluginRuntimeLog.Debug(
                $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
            return;
          }

          var translatedQuestPopupText = this.CreateQuestPopupText(
              JournalAcceptAddonName,
              sourceLanguage,
              questName,
              questMessage,
              resolvedQuestName,
              resolvedQuestMessage,
              questId);
          translatedQuestPopupText.SourceContentHash = sourceContentHash;
          _ = Task.Run(
              () => this.InsertQuestPopupTextAsync(translatedQuestPopupText));
        });
  }

  /// <summary>
  ///     Attempts to promote a pending JournalAccept payload from the broker or
  ///     from the persisted quest row.
  /// </summary>
  /// <returns><c>true</c> when a complete payload became available.</returns>
  private bool TryRefreshJournalAcceptPendingTranslation()
  {
    var state = this.currentJournalAcceptHoverState;
    if (state == null)
    {
      return false;
    }

    var translatedQuestName = string.Empty;
    var translatedQuestMessage = string.Empty;
    var translatedPayloadReady = false;
    if (this.TryGetQueuedTranslation(
            state.CacheKey,
            out var cachedTranslatedPayload) &&
        TryDeserializeTranslationPair(
            cachedTranslatedPayload,
            out translatedQuestName,
            out translatedQuestMessage))
    {
      translatedPayloadReady = IsTranslatedPayloadReady(
          translatedQuestName,
          translatedQuestMessage);
    }

    if (!translatedPayloadReady)
    {
      if (state.PersistenceTarget ==
          QuestPopupPersistenceTarget.CanonicalQuestPlate)
      {
        var questPlate = this.CreateQuestPlate(
            state.SourceLanguage,
            state.OriginalQuestName,
            state.OriginalQuestMessage,
            state.QuestId);
        questPlate.SourceContentHash = state.SourceContentHash;
        var foundQuestPlate = this.FindQuestPlate(questPlate);
        if (foundQuestPlate != null &&
            IsTranslatedPayloadReady(
                foundQuestPlate.TranslatedQuestName,
                foundQuestPlate.TranslatedQuestMessage))
        {
          translatedQuestName =
              foundQuestPlate.TranslatedQuestName ?? string.Empty;
          translatedQuestMessage =
              foundQuestPlate.TranslatedQuestMessage ?? string.Empty;
          translatedPayloadReady = true;
        }
      }
      else
      {
        var questPopupText = this.CreateQuestPopupText(
            JournalAcceptAddonName,
            state.SourceLanguage,
            state.OriginalQuestName,
            state.OriginalQuestMessage,
            questId: state.QuestId);
        questPopupText.SourceContentHash = state.SourceContentHash;
        var foundQuestPopupText = this.FindQuestPopupText(questPopupText);
        if (foundQuestPopupText != null &&
            IsTranslatedPayloadReady(
                foundQuestPopupText.TranslatedTitle,
                foundQuestPopupText.TranslatedBody))
        {
          translatedQuestName =
              foundQuestPopupText.TranslatedTitle ?? string.Empty;
          translatedQuestMessage =
              foundQuestPopupText.TranslatedBody ?? string.Empty;
          translatedPayloadReady = true;
        }
      }
    }

    if (!translatedPayloadReady)
    {
      this.nextJournalAcceptRetryUtc =
          DateTime.UtcNow + JournalAcceptRetryInterval;
      return false;
    }

    if (this.JournalAcceptShouldRemoveDiacritics)
    {
      translatedQuestName = this.NormalizeQuestText(translatedQuestName);
      translatedQuestMessage = this.NormalizeQuestText(translatedQuestMessage);
    }

    QuestUiTranslationCache.Remember(
        state.OriginalQuestName,
        translatedQuestName);
    QuestUiTranslationCache.Remember(
        state.OriginalQuestMessage,
        translatedQuestMessage);
    this.RememberJournalAcceptHoverState(
        state.CacheKey,
        state.SourceLanguage,
        state.OriginalQuestName,
        state.OriginalQuestMessage,
        translatedQuestName,
        translatedQuestMessage,
        state.SourceContentHash,
        state.PersistenceTarget,
        state.QuestId,
        state.OriginalQuestMessagePayload);
    return true;
  }

  /// <summary>
  ///     Stores the current JournalAccept hover payload and retry state.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="sourceLanguage">The captured source language.</param>
  /// <param name="originalQuestName">The original quest title.</param>
  /// <param name="originalQuestMessage">The original quest message.</param>
  /// <param name="translatedQuestName">The translated quest title.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  /// <param name="sourceContentHash">The resolved quest source hash.</param>
  /// <param name="persistenceTarget">The persistence bucket in use.</param>
  /// <param name="questId">The optional canonical quest id.</param>
  private void RememberJournalAcceptHoverState(
      string cacheKey,
      SourceClientLanguage sourceLanguage,
      string originalQuestName,
      string originalQuestMessage,
      string translatedQuestName,
      string translatedQuestMessage,
      string? sourceContentHash,
      QuestPopupPersistenceTarget persistenceTarget,
      string? questId,
      byte[]? originalQuestMessagePayload)
  {
    var translatedQuestMessagePayload = ProjectReadablePayloadBytes(
        originalQuestMessagePayload,
        originalQuestMessage,
        translatedQuestMessage);
    this.currentJournalAcceptHoverState = new JournalAcceptHoverState(
        cacheKey,
        sourceLanguage,
        originalQuestName,
        originalQuestMessage,
        translatedQuestName,
        translatedQuestMessage,
        sourceContentHash,
        persistenceTarget,
        questId,
        originalQuestMessagePayload,
        translatedQuestMessagePayload);
    this.hasPendingJournalAcceptTranslations =
        !this.currentJournalAcceptHoverState.TranslatedPayloadReady;
    this.nextJournalAcceptRetryUtc = this.hasPendingJournalAcceptTranslations
        ? DateTime.UtcNow + JournalAcceptRetryInterval
        : DateTime.MinValue;
    this.lastAppliedDisplayMode =
        this.Config.JournalAcceptTranslationDisplayMode;
    this.needsJournalAcceptHoverRefresh = true;
  }

  /// <summary>
  ///     Registers JournalAccept hover tooltips on text nodes when possible,
  ///     falling back to the addon window only when native text nodes are not
  ///     readable.
  /// </summary>
  private unsafe void RegisterJournalAcceptHoverTooltip()
  {
    this.RemoveHoverTooltipsByPrefix(JournalAcceptHoverPrefix);

    var state = this.currentJournalAcceptHoverState;
    if (state == null)
    {
      this.lastAppliedDisplayMode =
          this.Config.JournalAcceptTranslationDisplayMode;
      this.needsJournalAcceptHoverRefresh = false;
      return;
    }

    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(JournalAcceptAddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    this.ApplyJournalAcceptNativeState(addon, state);

    var nativeReadyForSwap =
        !this.JournalAcceptHoverShowsOriginal ||
        !this.JournalAcceptWritesNativeTranslation ||
        this.ownsJournalAcceptNativeMutation;
    var canRenderTooltip = this.CanRenderJournalAcceptHoverTooltip(
        state.TranslatedPayloadReady && nativeReadyForSwap);
    if (!canRenderTooltip)
    {
      this.lastAppliedDisplayMode =
          this.Config.JournalAcceptTranslationDisplayMode;
      this.needsJournalAcceptHoverRefresh = false;
      return;
    }

    var registeredName = false;
    var originalHoverQuestMessage = state.OriginalQuestMessage;
    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestName,
            state.TranslatedQuestName,
            out var nameNode))
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalAccept-QuestName-{(nint)nameNode:X}",
          nameNode,
          state.OriginalQuestName,
          state.TranslatedQuestName,
          translatedPayloadReady: canRenderTooltip,
          swapEnabled: this.JournalAcceptHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
      registeredName = true;
    }

    var registeredMessage = false;
    if (this.TryFindJournalAcceptMessageNode(
            addon,
            state,
            out var messageNode,
            out var preferredHoverNode,
            out originalHoverQuestMessage))
    {
      var messageHoverKey = $"JournalAccept-QuestBody-{(nint)messageNode:X}";
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
            originalHoverQuestMessage,
            state.TranslatedQuestMessage,
            canRenderTooltip,
            this.JournalAcceptHoverShowsOriginal,
            true);
      }
      else
      {
        this.RegisterTranslatedHoverTooltip(
            messageHoverKey,
            messageNode,
            originalHoverQuestMessage,
            state.TranslatedQuestMessage,
            translatedPayloadReady: canRenderTooltip,
            swapEnabled: this.JournalAcceptHoverShowsOriginal,
            forceEnabled: true,
            denseHitbox: true);
      }

      registeredMessage = true;
    }

    if (!registeredName && !registeredMessage)
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalAccept-{(nint)addon:X}",
          addon,
          $"{state.OriginalQuestName}\n{originalHoverQuestMessage}",
          $"{state.TranslatedQuestName}\n{state.TranslatedQuestMessage}",
          translatedPayloadReady: canRenderTooltip,
          swapEnabled: this.JournalAcceptHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
    }
    else if (!registeredMessage)
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalAccept-QuestBody-{(nint)addon:X}",
          addon,
          originalHoverQuestMessage,
          state.TranslatedQuestMessage,
          translatedPayloadReady: canRenderTooltip,
          swapEnabled: this.JournalAcceptHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
    }

    this.lastAppliedDisplayMode =
        this.Config.JournalAcceptTranslationDisplayMode;
    this.needsJournalAcceptHoverRefresh = false;
  }

  /// <summary>
  ///     Finds the visible JournalAccept body node, including the expanded
  ///     runtime body that keeps the quest-sync marker ahead of the readable
  ///     message text.
  /// </summary>
  /// <param name="addon">The live JournalAccept addon instance.</param>
  /// <param name="state">The current JournalAccept hover state.</param>
  /// <param name="messageNode">The resolved body node, if any.</param>
  /// <param name="preferredHoverNode">
  ///     The optional structural body node used only for tooltip geometry.
  /// </param>
  /// <param name="originalHoverQuestMessage">
  ///     The preferred original body text for hover presentation.
  /// </param>
  /// <returns><c>true</c> when the body node was found.</returns>
  private unsafe bool TryFindJournalAcceptMessageNode(
      AtkUnitBase* addon,
      JournalAcceptHoverState state,
      out AtkTextNode* messageNode,
      out AtkResNode* preferredHoverNode,
      out string originalHoverQuestMessage)
  {
    messageNode = null;
    preferredHoverNode = null;
    originalHoverQuestMessage = state.OriginalQuestMessage;
    AtkTextNode* directMatchNode = null;
    string directMatchHoverMessage = state.OriginalQuestMessage;

    foreach (var nodeAddress in AddonTextNodeResolvers.ResolveReadableTextNodes(addon))
    {
      var candidate = (AtkTextNode*)nodeAddress;
      if (candidate == null || !candidate->IsVisible())
      {
        continue;
      }

      var visibleText = ReadReadableTextNode(candidate);
      var expandedVisibleBody = ResolveJournalAcceptExpandedVisibleBodyMatch(
          state.OriginalQuestMessage,
          visibleText);
      if (!string.IsNullOrWhiteSpace(expandedVisibleBody))
      {
        messageNode = candidate;
        this.TryResolveJournalAcceptPreferredHoverNode(
            addon,
            messageNode,
            out preferredHoverNode);
        originalHoverQuestMessage = expandedVisibleBody;
        return true;
      }

      if (directMatchNode == null &&
          (JournalAcceptTextNodePayloadMatches(
               visibleText,
               state.OriginalQuestMessage) ||
            (!string.IsNullOrWhiteSpace(state.TranslatedQuestMessage) &&
             JournalAcceptTextNodePayloadMatches(
                 visibleText,
                 state.TranslatedQuestMessage))))
      {
        directMatchNode = candidate;
        directMatchHoverMessage = ResolveJournalAcceptOriginalHoverBody(
            state.OriginalQuestMessage,
            visibleText);
      }
    }

    if (directMatchNode != null)
    {
      messageNode = directMatchNode;
      this.TryResolveJournalAcceptPreferredHoverNode(
          addon,
          messageNode,
          out preferredHoverNode);
      originalHoverQuestMessage = directMatchHoverMessage;
      return true;
    }

    if (TryFindPopupSectionBodyTextNodeByHeadingTextId(
            addon,
            JournalAcceptSummaryTextId,
            out var structuralMessageNode,
            out preferredHoverNode))
    {
      messageNode = structuralMessageNode;
      originalHoverQuestMessage = state.OriginalQuestMessage;
      return true;
    }

    return false;
  }

  /// <summary>
  ///     Resolves the structural body node only when the heading-based popup
  ///     resolver identifies the same live text node selected for payload work.
  /// </summary>
  /// <param name="addon">The live JournalAccept addon instance.</param>
  /// <param name="messageNode">The live body text node.</param>
  /// <param name="preferredHoverNode">
  ///     The matching structural body node used only for tooltip geometry.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when a matching structural body node was
  ///     resolved; otherwise, <see langword="false" />.
  /// </returns>
  private unsafe bool TryResolveJournalAcceptPreferredHoverNode(
      AtkUnitBase* addon,
      AtkTextNode* messageNode,
      out AtkResNode* preferredHoverNode)
  {
    preferredHoverNode = null;
    return messageNode != null &&
           TryFindPopupSectionBodyTextNodeByHeadingTextId(
               addon,
               JournalAcceptSummaryTextId,
               out var structuralMessageNode,
               out var structuralHoverNode) &&
           structuralMessageNode == messageNode &&
           (preferredHoverNode = structuralHoverNode) != null;
  }

  /// <summary>
  ///     Compares a visible JournalAccept text node payload with one expected
  ///     payload while allowing wrapping and readable-text normalization.
  /// </summary>
  /// <param name="visibleText">The current text read from the node.</param>
  /// <param name="expectedText">The expected original or translated payload.</param>
  /// <returns><c>true</c> when both texts describe the same payload.</returns>
  private static bool JournalAcceptTextNodePayloadMatches(
      string visibleText,
      string expectedText)
  {
    var normalizedVisibleText = NormalizeReadableText(visibleText);
    var normalizedExpectedText = NormalizeReadableText(expectedText);
    if (string.IsNullOrWhiteSpace(normalizedVisibleText) ||
        string.IsNullOrWhiteSpace(normalizedExpectedText))
    {
      return false;
    }

    if (string.Equals(
            normalizedVisibleText,
            normalizedExpectedText,
            StringComparison.Ordinal))
    {
      return true;
    }

    if (normalizedVisibleText.Length < 4 || normalizedExpectedText.Length < 4)
    {
      return false;
    }

    return normalizedVisibleText.Contains(
               normalizedExpectedText,
               StringComparison.Ordinal) ||
           normalizedExpectedText.Contains(
               normalizedVisibleText,
               StringComparison.Ordinal);
  }

  /// <summary>
  ///     Promotes the original JournalAccept hover body to the richer visible
  ///     message text when setup capture only exposed the short quest-sync
  ///     marker.
  /// </summary>
  /// <param name="setupMessage">The setup-captured JournalAccept body.</param>
  /// <param name="visibleText">The current visible body node text.</param>
  /// <returns>The preferred original body text for hover presentation.</returns>
  private static string ResolveJournalAcceptOriginalHoverBody(
      string setupMessage,
      string visibleText)
  {
    if (string.IsNullOrWhiteSpace(setupMessage))
    {
      return string.Empty;
    }

    if (string.IsNullOrWhiteSpace(visibleText))
    {
      return setupMessage;
    }

    var normalizedSetupMessage = NormalizeReadableText(
        NormalizeJournalAcceptSetupMessageForComparison(setupMessage));
    var normalizedVisibleSetupText = NormalizeReadableText(
        NormalizeJournalAcceptSetupMessageForComparison(visibleText));
    if (string.IsNullOrWhiteSpace(normalizedSetupMessage) ||
        string.IsNullOrWhiteSpace(normalizedVisibleSetupText))
    {
      return setupMessage;
    }

    if (string.Equals(
            normalizedSetupMessage,
            normalizedVisibleSetupText,
            StringComparison.Ordinal))
    {
      return setupMessage;
    }

    var normalizedVisibleText = TrimLeadingJournalAcceptPayloadMarkers(
        NormalizeReadableText(visibleText));
    if (!normalizedVisibleText.StartsWith(
            normalizedSetupMessage,
            StringComparison.Ordinal))
    {
      return setupMessage;
    }

    if (normalizedVisibleText.Length <= normalizedSetupMessage.Length)
    {
      return setupMessage;
    }

    var bodySuffix = TrimLeadingJournalAcceptPayloadMarkers(
        normalizedVisibleText[normalizedSetupMessage.Length..].TrimStart());
    if (string.IsNullOrWhiteSpace(bodySuffix))
    {
      return setupMessage;
    }

    return $"{normalizedSetupMessage} {bodySuffix}";
  }

  /// <summary>
  ///     Returns the promoted original body text when a visible node clearly
  ///     belongs to the expanded JournalAccept body payload.
  /// </summary>
  /// <param name="setupMessage">The setup-captured JournalAccept body.</param>
  /// <param name="visibleText">The current visible node text.</param>
  /// <returns>
  ///     The promoted original body text when the node belongs to the expanded
  ///     JournalAccept body; otherwise an empty string.
  /// </returns>
  private static string ResolveJournalAcceptExpandedVisibleBodyMatch(
      string setupMessage,
      string visibleText)
  {
    var promotedBody = ResolveJournalAcceptOriginalHoverBody(
        setupMessage,
        visibleText);
    if (string.Equals(promotedBody, setupMessage, StringComparison.Ordinal))
    {
      return string.Empty;
    }

    return promotedBody;
  }

  /// <summary>
  ///     Resolves the preferred JournalAccept visible body payload from the
  ///     readable-node scan order, preferring the richer expanded body even
  ///     when a shorter quest-sync marker appears first.
  /// </summary>
  /// <param name="setupMessage">The setup-captured JournalAccept body.</param>
  /// <param name="visibleTexts">The readable visible-node texts in scan order.</param>
  /// <returns>
  ///     The preferred visible body payload for runtime use, or the setup body
  ///     when no richer visible node exists.
  /// </returns>
  private static string ResolvePreferredJournalAcceptVisibleBody(
      string setupMessage,
      IEnumerable<string> visibleTexts)
  {
    foreach (var visibleText in visibleTexts)
    {
      var promotedBody = ResolveJournalAcceptExpandedVisibleBodyMatch(
          setupMessage,
          visibleText);
      if (!string.IsNullOrWhiteSpace(promotedBody))
      {
        return promotedBody;
      }
    }

    return setupMessage;
  }

  /// <summary>
  ///     Removes setup-only emphasis wrappers from JournalAccept body text so
  ///     readable-node comparison can match the richer visible message.
  /// </summary>
  /// <param name="setupMessage">The setup-captured JournalAccept body.</param>
  /// <returns>The comparable setup message.</returns>
  private static string NormalizeJournalAcceptSetupMessageForComparison(
      string setupMessage)
  {
    if (string.IsNullOrWhiteSpace(setupMessage))
    {
      return string.Empty;
    }

    var trimmedMessage = setupMessage.Trim();
    if (trimmedMessage.Length > 4 &&
        trimmedMessage.StartsWith("**", StringComparison.Ordinal) &&
        trimmedMessage.EndsWith("**", StringComparison.Ordinal))
    {
      return trimmedMessage[2..^2].Trim();
    }

    return trimmedMessage;
  }

  /// <summary>
  ///     Removes leading raw JournalAccept formatting marker tokens that leak
  ///     into readable node text as consecutive <c>H</c>/<c>I</c> words.
  /// </summary>
  /// <param name="text">The normalized readable text.</param>
  /// <returns>The readable text without leading payload markers.</returns>
  private static string TrimLeadingJournalAcceptPayloadMarkers(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return string.Empty;
    }

    var tokens = text.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var markerCount = 0;
    while (markerCount < tokens.Length &&
           (string.Equals(tokens[markerCount], "H", StringComparison.Ordinal) ||
            string.Equals(tokens[markerCount], "I", StringComparison.Ordinal)))
    {
      markerCount++;
    }

    if (markerCount < 2)
    {
      return string.Join(" ", tokens);
    }

    return string.Join(" ", tokens[markerCount..]);
  }

  /// <summary>
  ///     Applies or restores JournalAccept native text when the current display
  ///     mode requires this handler-owned mutation.
  /// </summary>
  /// <param name="addon">The visible JournalAccept addon.</param>
  /// <param name="state">The current JournalAccept hover state.</param>
  private unsafe void ApplyJournalAcceptNativeState(
      AtkUnitBase* addon,
      JournalAcceptHoverState state)
  {
    if (!state.TranslatedPayloadReady)
    {
      return;
    }

    if (!this.JournalAcceptWritesNativeTranslation &&
        !this.ownsJournalAcceptNativeMutation)
    {
      return;
    }

    var targetQuestName = this.JournalAcceptWritesNativeTranslation
        ? state.TranslatedQuestName
        : state.OriginalQuestName;
    var targetQuestMessage = this.JournalAcceptWritesNativeTranslation
        ? state.TranslatedQuestMessage
        : state.OriginalQuestMessage;
    var appliedName = false;
    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestName,
            state.TranslatedQuestName,
            out var nameNode))
    {
      nameNode->SetText(targetQuestName);
      appliedName = true;
    }

    var appliedMessage = false;
    if (this.TryFindJournalAcceptMessageNode(
            addon,
            state,
            out var messageNode,
            out _,
            out _))
    {
      SetJournalAcceptMessageNode(
          messageNode,
          this.JournalAcceptWritesNativeTranslation
              ? state.TranslatedQuestMessagePayload
              : state.OriginalQuestMessagePayload,
          targetQuestMessage);
      appliedMessage = true;
    }

    if (appliedName && appliedMessage)
    {
      this.ownsJournalAcceptNativeMutation =
          this.JournalAcceptWritesNativeTranslation;
    }
  }

  /// <summary>
  ///     Restores the original JournalAccept title and body when this handler
  ///     owns the visible native mutation.
  /// </summary>
  private unsafe void RestoreJournalAcceptOriginals()
  {
    if (!this.ownsJournalAcceptNativeMutation)
    {
      return;
    }

    var state = this.currentJournalAcceptHoverState;
    var addon = AtkStage.Instance()->RaptureAtkUnitManager
        ->GetAddonByName(JournalAcceptAddonName);
    if (addon == null || state == null)
    {
      this.ownsJournalAcceptNativeMutation = false;
      return;
    }

    if (addon->AtkValues != null && addon->AtkValuesCount > 12)
    {
      addon->AtkValues[5].SetManagedString(state.OriginalQuestName);
      SetJournalAcceptMessageValue(
          &addon->AtkValues[12],
          state.OriginalQuestMessagePayload,
          state.OriginalQuestMessage);
    }

    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestName,
            state.TranslatedQuestName,
            out var nameNode))
    {
      nameNode->SetText(state.OriginalQuestName);
    }

    if (this.TryFindJournalAcceptMessageNode(
            addon,
            state,
            out var messageNode,
            out _,
            out _))
    {
      SetJournalAcceptMessageNode(
          messageNode,
          state.OriginalQuestMessagePayload,
          state.OriginalQuestMessage);
    }

    this.ownsJournalAcceptNativeMutation = false;
  }

  /// <summary>
  ///     Clears JournalAccept hover registrations and local runtime state.
  /// </summary>
  private void ClearJournalAcceptRuntimeState()
  {
    this.currentJournalAcceptHoverState = null;
    this.hasPendingJournalAcceptTranslations = false;
    this.ownsJournalAcceptNativeMutation = false;
    this.lastAppliedDisplayMode = null;
    this.nextJournalAcceptRetryUtc = DateTime.MinValue;
    this.needsJournalAcceptHoverRefresh = false;
    this.RemoveHoverTooltipsByPrefix(JournalAcceptHoverPrefix);
  }

  /// <summary>
  ///     Captures the original JournalAccept body payload from the live text
  ///     node when it still matches the expected readable source text.
  /// </summary>
  /// <param name="messageNode">The live JournalAccept body node.</param>
  /// <param name="expectedText">The expected readable original body text.</param>
  /// <returns>The captured original payload bytes, if available.</returns>
  private static unsafe byte[]? TryCaptureJournalAcceptMessagePayload(
      AtkTextNode* messageNode,
      string expectedText)
  {
    return ReadableSeStringPayloadHelper.TryCaptureMatchingPayload(
        messageNode,
        expectedText);
  }

  /// <summary>
  ///     Writes a JournalAccept body payload back into an addon setup value
  ///     while falling back to plain text when no rich payload is available.
  /// </summary>
  /// <param name="atkValue">The setup value to write.</param>
  /// <param name="payload">The optional rich payload bytes.</param>
  /// <param name="fallbackText">The plain-text fallback.</param>
  private static unsafe void SetJournalAcceptMessageValue(
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
  ///     Writes a JournalAccept body payload back into the live text node while
  ///     falling back to plain text when no rich payload is available.
  /// </summary>
  /// <param name="messageNode">The live JournalAccept body node.</param>
  /// <param name="payload">The optional rich payload bytes.</param>
  /// <param name="fallbackText">The plain-text fallback.</param>
  private static unsafe void SetJournalAcceptMessageNode(
      AtkTextNode* messageNode,
      byte[]? payload,
      string fallbackText)
  {
    if (messageNode == null)
    {
      return;
    }

    if (payload is { Length: > 0 })
    {
      messageNode->SetText(payload);
      return;
    }

    messageNode->SetText(fallbackText);
  }

  /// <summary>
  ///     Clears JournalAccept hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnJournalAcceptCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, JournalAcceptAddonName, StringComparison.Ordinal))
    {
      this.RestoreJournalAcceptOriginals();
      this.ClearJournalAcceptRuntimeState();
    }
  }

  /// <summary>
  ///     Local JournalAccept payload retained between setup and visible draw
  ///     passes.
  /// </summary>
  /// <param name="CacheKey">The stable translation cache key.</param>
  /// <param name="SourceLanguage">The captured source language.</param>
  /// <param name="OriginalQuestName">The original quest title.</param>
  /// <param name="OriginalQuestMessage">The original quest message.</param>
  /// <param name="TranslatedQuestName">The translated quest title.</param>
  /// <param name="TranslatedQuestMessage">The translated quest message.</param>
  /// <param name="SourceContentHash">The resolved source content hash.</param>
  /// <param name="PersistenceTarget">The persistence bucket in use.</param>
  /// <param name="QuestId">The optional canonical quest id.</param>
  /// <param name="OriginalQuestMessagePayload">
  ///     The captured original body payload bytes, if available.
  /// </param>
  /// <param name="TranslatedQuestMessagePayload">
  ///     The projected translated body payload bytes, if available.
  /// </param>
  private sealed record JournalAcceptHoverState(
      string CacheKey,
      SourceClientLanguage SourceLanguage,
      string OriginalQuestName,
      string OriginalQuestMessage,
      string TranslatedQuestName,
      string TranslatedQuestMessage,
      string? SourceContentHash,
      QuestPopupPersistenceTarget PersistenceTarget,
      string? QuestId,
      byte[]? OriginalQuestMessagePayload,
      byte[]? TranslatedQuestMessagePayload)
  {
    /// <summary>
    ///     Gets whether the translated JournalAccept payload is complete.
    /// </summary>
    public bool TranslatedPayloadReady =>
        IsTranslatedPayloadReady(
            this.TranslatedQuestName,
            this.TranslatedQuestMessage);
  }
}


