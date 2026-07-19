// <copyright file="JournalAcceptHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
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
              out var questMessage))
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

      var questPlate = this.CreateQuestPlate(
          sourceLanguage,
          questName,
          questMessage);
      if (QuestProgressResolver.TryResolveQuestProgress(
              questPlate,
              out var resolvedAcceptSnapshot))
      {
        questPlate.SourceContentHash = resolvedAcceptSnapshot.ContentHash;
      }

      var foundQuestPlate = this.FindQuestPlate(questPlate);
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

      var cacheKey = $"JournalAccept|{questName}|{questMessage}";

      if (!this.TryResolveJournalAcceptTranslation(
              cacheKey,
              questName,
              questMessage,
              foundQuestPlate,
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
            questPlate.SourceContentHash);
        this.QueueJournalAcceptTranslation(
            cacheKey,
            sourceLanguage,
            questName,
            questMessage,
            questPlate.SourceContentHash);
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
          questPlate.SourceContentHash);

      if (this.JournalAcceptWritesNativeTranslation)
      {
        setupAtkValues[5].SetManagedString(translatedQuestName);
        setupAtkValues[12].SetManagedString(translatedQuestMessage);
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
      this.ClearJournalAcceptRuntimeState();
      return;
    }

    if (this.currentJournalAcceptHoverState == null)
    {
      return;
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
      out string questMessage)
  {
    questName = string.Empty;
    questMessage = string.Empty;
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
    return !string.IsNullOrWhiteSpace(questName) &&
           !string.IsNullOrWhiteSpace(questMessage);
  }

  /// <summary>
  ///     Resolves a complete translated JournalAccept payload from the session
  ///     cache, persisted quest row, or completed broker result.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="questName">The original quest title.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="foundQuestPlate">The matching persisted quest row, if any.</param>
  /// <param name="translatedQuestName">The translated quest title.</param>
  /// <param name="translatedQuestMessage">The translated quest message.</param>
  /// <returns><c>true</c> when a complete translated payload exists.</returns>
  private bool TryResolveJournalAcceptTranslation(
      string cacheKey,
      string questName,
      string questMessage,
      QuestPlate? foundQuestPlate,
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
  ///     Enqueues the JournalAccept title and body translation through the
  ///     shared quest broker.
  /// </summary>
  /// <param name="cacheKey">The stable translation cache key.</param>
  /// <param name="sourceLanguage">The captured source language.</param>
  /// <param name="questName">The original quest title.</param>
  /// <param name="questMessage">The original quest message.</param>
  /// <param name="sourceContentHash">The resolved quest source hash.</param>
  private void QueueJournalAcceptTranslation(
      string cacheKey,
      SourceClientLanguage sourceLanguage,
      string questName,
      string questMessage,
      string? sourceContentHash)
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

          var translatedQuestPlate = this.CreateTranslatedQuestPlate(
              sourceLanguage,
              questName,
              questMessage,
              resolvedQuestName,
              resolvedQuestMessage,
              string.Empty);
          translatedQuestPlate.SourceContentHash = sourceContentHash;

          var result = this.InsertQuestPlate(translatedQuestPlate);
#if DEBUG
          PluginRuntimeLog.Debug(
              $"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
#endif
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
      var questPlate = this.CreateQuestPlate(
          state.SourceLanguage,
          state.OriginalQuestName,
          state.OriginalQuestMessage);
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
        state.SourceContentHash);
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
  private void RememberJournalAcceptHoverState(
      string cacheKey,
      SourceClientLanguage sourceLanguage,
      string originalQuestName,
      string originalQuestMessage,
      string translatedQuestName,
      string translatedQuestMessage,
      string? sourceContentHash)
  {
    this.currentJournalAcceptHoverState = new JournalAcceptHoverState(
        cacheKey,
        sourceLanguage,
        originalQuestName,
        originalQuestMessage,
        translatedQuestName,
        translatedQuestMessage,
        sourceContentHash);
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
    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestMessage,
            state.TranslatedQuestMessage,
            out var messageNode))
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalAccept-QuestBody-{(nint)messageNode:X}",
          messageNode,
          state.OriginalQuestMessage,
          state.TranslatedQuestMessage,
          translatedPayloadReady: canRenderTooltip,
          swapEnabled: this.JournalAcceptHoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
      registeredMessage = true;
    }

    if (!registeredName && !registeredMessage)
    {
      this.RegisterTranslatedHoverTooltip(
          $"JournalAccept-{(nint)addon:X}",
          addon,
          $"{state.OriginalQuestName}\n{state.OriginalQuestMessage}",
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
          state.OriginalQuestMessage,
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
    if (this.TryFindReadableTextNodeByText(
            addon,
            state.OriginalQuestMessage,
            state.TranslatedQuestMessage,
            out var messageNode))
    {
      messageNode->SetText(targetQuestMessage);
      appliedMessage = true;
    }

    if (appliedName && appliedMessage)
    {
      this.ownsJournalAcceptNativeMutation =
          this.JournalAcceptWritesNativeTranslation;
    }
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
  ///     Clears JournalAccept hover registrations when the addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnJournalAcceptCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (string.Equals(args.AddonName, JournalAcceptAddonName, StringComparison.Ordinal))
    {
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
  private sealed record JournalAcceptHoverState(
      string CacheKey,
      SourceClientLanguage SourceLanguage,
      string OriginalQuestName,
      string OriginalQuestMessage,
      string TranslatedQuestName,
      string TranslatedQuestMessage,
      string? SourceContentHash)
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


