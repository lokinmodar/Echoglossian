// <copyright file="MapSurfaceStringArrayHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Handles map-family text exposed only through live
///     <see cref="StringArrayData" /> payloads.
/// </summary>
internal sealed class MapSurfaceStringArrayHandler : QuestAddonHandlerBase
{
  private static readonly ConcurrentDictionary<string, byte>
      InFlightPayloads = new(StringComparer.Ordinal);

  private static readonly ConcurrentDictionary<string, DateTime>
      FailedPayloadRetryUtc = new(StringComparer.Ordinal);

  private static readonly TimeSpan FailureRetryInterval =
      TimeSpan.FromSeconds(30);

  private readonly string addonName;

  private readonly string hoverPrefix;

  private readonly Dictionary<int, MapSurfaceRuntimeState> runtimeStates = [];

  private readonly Dictionary<nint, MapSurfaceTextNodeMutationState>
      mapSurfaceTextNodeMutations = [];

  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="MapSurfaceStringArrayHandler" /> class.
  /// </summary>
  /// <param name="addonName">The map-family addon name.</param>
  /// <param name="dependencies">The shared quest-handler dependencies.</param>
  public MapSurfaceStringArrayHandler(
      string addonName,
      QuestAddonHandlerDependencies dependencies)
      : base(dependencies)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(addonName);

    this.addonName = addonName;
    this.hoverPrefix = $"MapSurface-{addonName}-";

    this.RegisterHandler(AddonEvent.PreRefresh, this.OnMapSurfaceEvent);
    this.RegisterHandler(
        AddonEvent.PreRequestedUpdate,
        this.OnMapSurfaceEvent);
    this.RegisterHandler(AddonEvent.PreDraw, this.OnMapSurfacePreDrawEvent);
    this.RegisterHandler(AddonEvent.PreHide, this.OnMapSurfaceCleanupEvent);
    this.RegisterHandler(
        AddonEvent.PreFinalize,
        this.OnMapSurfaceCleanupEvent);
  }

  /// <inheritdoc />
  public override void OnPluginUnload()
  {
    this.ClearMapSurfaceState();
  }

  private bool UsesHoverTooltips =>
      QuestAddonModeHelpers.UsesHoverTooltips(
          this.Config.AreaMapTranslationDisplayMode);

  private bool WritesNativeTranslation =>
      QuestAddonModeHelpers.WritesNativeTranslation(
          this.Config.AreaMapTranslationDisplayMode);

  private bool HoverShowsOriginal =>
      QuestAddonModeHelpers.ShowsOriginalTooltips(
          this.Config.AreaMapTranslationDisplayMode);

  private bool ShouldRemoveDiacritics =>
      QuestAddonModeHelpers.ShouldRemoveDiacritics(
          this.Config.AreaMapTranslationDisplayMode,
          this.Config.RemoveDiacriticsWhenUsingReplacementQuest);

  private int EffectiveTranslationEngineId =>
      this.TranslationService.GetEffectiveTranslationEngineId(
          TranslationSurfaceGroup.Default);

  /// <summary>
  ///     Handles setup and refresh events.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnMapSurfaceEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.IsOwnAddonEvent(args))
    {
      return;
    }

    this.ProcessMapSurface();
  }

  /// <summary>
  ///     Handles draw-time retries and late background translation completion.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private unsafe void OnMapSurfacePreDrawEvent(AddonEvent type, AddonArgs args)
  {
    if (!this.IsOwnAddonEvent(args))
    {
      return;
    }

    this.ProcessMapSurface();
  }

  /// <summary>
  ///     Clears owned state when the map addon closes.
  /// </summary>
  /// <param name="type">The addon lifecycle event.</param>
  /// <param name="args">The addon lifecycle arguments.</param>
  private void OnMapSurfaceCleanupEvent(AddonEvent type, AddonArgs args)
  {
    if (this.IsOwnAddonEvent(args))
    {
      this.ClearMapSurfaceState();
    }
  }

  /// <summary>
  ///     Processes the visible map-family string-array payloads.
  /// </summary>
  private unsafe void ProcessMapSurface()
  {
    if (!this.Config.TranslateAreaMap ||
        this.DisableTranslationAccordingToState())
    {
      this.ClearMapSurfaceState();
      return;
    }

    if (!FrameworkAccessGuard.IsClientReadyForPlayerScopedFrameworkAccess() ||
        !RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
            out var sourceLanguage) ||
        !this.TryGetVisibleAddon(out var addon))
    {
      return;
    }

    if (!this.WritesNativeTranslation)
    {
      this.RestoreOriginalPayloadsIfNeeded();
    }

    if (!this.TryCaptureLiveMapSurfaceArrays(addon, out var liveArrays))
    {
      this.RemoveHoverTooltipsByPrefix(this.hoverPrefix);
      return;
    }

    this.RemoveHoverTooltipsByPrefix(this.hoverPrefix);

    foreach (var liveArray in liveArrays)
    {
      this.ProcessLiveArray(addon, liveArray, sourceLanguage);
    }
  }

  /// <summary>
  ///     Resolves, applies, or queues one captured map string-array payload.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <param name="liveArray">The captured live string-array payload.</param>
  /// <param name="sourceLanguage">The operation-captured source language.</param>
  private unsafe void ProcessLiveArray(
      AtkUnitBase* addon,
      MapSurfaceLiveArray liveArray,
      SourceClientLanguage sourceLanguage)
  {
    var originalPayload = this.ResolveOriginalPayload(liveArray);
    if (!MapSurfaceStringArraySchema.IsMapSurfacePayload(originalPayload))
    {
      return;
    }

    var scope = this.CreateReuseScope(sourceLanguage);
    var payloadKey = this.BuildPayloadKey(scope, originalPayload);
    if (this.TryFindTranslatedPayload(
            sourceLanguage,
            scope,
            originalPayload,
            out var translatedPayload,
            out var projection))
    {
      this.ApplyMapSurfacePresentation(
          addon,
          liveArray,
          originalPayload,
          translatedPayload,
          projection);
      ClearFailedPayloadRetry(payloadKey);
      return;
    }

    this.RestoreTextNodeMutationsIfNeeded();

    if (TryGetFailedPayloadRetryUtc(payloadKey, out _))
    {
      return;
    }

    this.QueueTranslationIfNeeded(
        sourceLanguage,
        scope,
        originalPayload,
        payloadKey);
  }

  /// <summary>
  ///     Applies the selected display mode to one map string-array payload.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <param name="liveArray">The captured live string-array payload.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="translatedPayload">The translated structured payload.</param>
  /// <param name="projection">The translated live-slot projection.</param>
  private unsafe void ApplyMapSurfacePresentation(
      AtkUnitBase* addon,
      MapSurfaceLiveArray liveArray,
      StringArrayStructuredPayload originalPayload,
      StringArrayStructuredPayload translatedPayload,
      DbFirstStructuredStringArrayProjection projection)
  {
    SortedDictionary<int, string> appliedTranslatedTexts = [];
    if (this.WritesNativeTranslation)
    {
      appliedTranslatedTexts = this.ApplyTranslatedStringArrayValues(
          liveArray,
          originalPayload,
          projection);
      var appliedTextNodeKeys = this.ApplyTranslatedTextNodeValues(
          addon,
          originalPayload,
          translatedPayload);
      this.RestoreTextNodeMutationsIfNeeded(appliedTextNodeKeys);
      this.runtimeStates[liveArray.ArrayIndex] = new MapSurfaceRuntimeState(
          liveArray.ArrayIndex,
          originalPayload,
          appliedTranslatedTexts);
    }

    if (this.UsesHoverTooltips)
    {
      this.RegisterHoverTooltip(
          addon,
          liveArray.ArrayIndex,
          originalPayload,
          translatedPayload);
    }
  }

  /// <summary>
  ///     Writes translated text into translatable map string-array slots.
  /// </summary>
  /// <param name="liveArray">The captured live string-array payload.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="projection">The translated live-slot projection.</param>
  /// <returns>The exact native strings written by this handler.</returns>
  private unsafe SortedDictionary<int, string> ApplyTranslatedStringArrayValues(
      MapSurfaceLiveArray liveArray,
      StringArrayStructuredPayload originalPayload,
      DbFirstStructuredStringArrayProjection projection)
  {
    var appliedTranslatedTexts = new SortedDictionary<int, string>();
    var stringArrayData = (StringArrayData*)liveArray.ArrayAddress;
    if (stringArrayData == null || stringArrayData->StringArray == null)
    {
      return appliedTranslatedTexts;
    }

    foreach (var (index, translatedText) in projection.StringArrayValues)
    {
      if ((uint)index >= stringArrayData->Size ||
          !originalPayload.Slots.TryGetValue(index, out var originalSlot) ||
          !originalSlot.IsTranslatable)
      {
        continue;
      }

      var nativeText = this.ShouldRemoveDiacritics
          ? this.NormalizeQuestText(translatedText)
          : translatedText;
      if (string.IsNullOrWhiteSpace(nativeText))
      {
        continue;
      }

      var currentText = ReadStringArrayValue(stringArrayData, index);
      if (string.Equals(currentText, nativeText, StringComparison.Ordinal))
      {
        appliedTranslatedTexts[index] = nativeText;
        continue;
      }

      stringArrayData->SetValue(index, nativeText, suppressUpdates: true);
      appliedTranslatedTexts[index] = nativeText;
    }

    return appliedTranslatedTexts;
  }

  /// <summary>
  ///     Registers one aggregate hover tooltip for a map string-array payload.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <param name="arrayIndex">The live string-array index.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="translatedPayload">The translated structured payload.</param>
  private unsafe void RegisterHoverTooltip(
      AtkUnitBase* addon,
      int arrayIndex,
      StringArrayStructuredPayload originalPayload,
      StringArrayStructuredPayload translatedPayload)
  {
    var matchedTextNodes = this.RegisterTextNodeHoverTooltips(
        addon,
        arrayIndex,
        originalPayload,
        translatedPayload);
    if (matchedTextNodes.Count == 0)
    {
      this.RegisterAggregateHoverTooltip(
          addon,
          arrayIndex,
          originalPayload,
          translatedPayload);
    }
  }

  /// <summary>
  ///     Registers one hover tooltip per visible text node matching a
  ///     translated map string-array slot.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <param name="arrayIndex">The live string-array index.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="translatedPayload">The translated structured payload.</param>
  /// <returns>The text-node keys matched while registering tooltips.</returns>
  private unsafe HashSet<nint> RegisterTextNodeHoverTooltips(
      AtkUnitBase* addon,
      int arrayIndex,
      StringArrayStructuredPayload originalPayload,
      StringArrayStructuredPayload translatedPayload)
  {
    HashSet<nint> matchedTextNodes = [];
    foreach (var (index, originalSlot) in originalPayload.Slots)
    {
      if (!originalSlot.IsTranslatable ||
          !translatedPayload.Slots.TryGetValue(index, out var translatedSlot) ||
          string.IsNullOrWhiteSpace(translatedSlot.TranslatedText))
      {
        continue;
      }

      if (!this.TryFindReadableTextNodeByText(
              addon,
              originalSlot.OriginalText,
              translatedSlot.TranslatedText,
              out var textNode))
      {
        continue;
      }

      var textNodeKey = (nint)textNode;
      if (!matchedTextNodes.Add(textNodeKey))
      {
        continue;
      }

      this.RegisterTranslatedHoverTooltip(
          $"{this.hoverPrefix}{arrayIndex.ToString(CultureInfo.InvariantCulture)}-TextNode-{textNodeKey:X}",
          textNode,
          originalSlot.OriginalText,
          translatedSlot.TranslatedText,
          translatedPayloadReady: QuestAddonModeHelpers.CanRenderHoverTooltip(
              this.Config.AreaMapTranslationDisplayMode,
              translatedPayloadReady: true),
          swapEnabled: this.HoverShowsOriginal,
          forceEnabled: true,
          denseHitbox: true);
    }

    return matchedTextNodes;
  }

  /// <summary>
  ///     Registers one aggregate hover tooltip for map payloads whose strings
  ///     do not currently map to visible text nodes.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <param name="arrayIndex">The live string-array index.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="translatedPayload">The translated structured payload.</param>
  private unsafe void RegisterAggregateHoverTooltip(
      AtkUnitBase* addon,
      int arrayIndex,
      StringArrayStructuredPayload originalPayload,
      StringArrayStructuredPayload translatedPayload)
  {
    var originalText = BuildTooltipText(originalPayload, null);
    var translatedText = BuildTooltipText(originalPayload, translatedPayload);
    if (string.IsNullOrWhiteSpace(originalText) ||
        string.IsNullOrWhiteSpace(translatedText))
    {
      return;
    }

    this.RegisterTranslatedHoverTooltip(
        $"{this.hoverPrefix}{arrayIndex.ToString(CultureInfo.InvariantCulture)}",
        addon,
        originalText,
        translatedText,
        translatedPayloadReady: QuestAddonModeHelpers.CanRenderHoverTooltip(
            this.Config.AreaMapTranslationDisplayMode,
            translatedPayloadReady: true),
        swapEnabled: this.HoverShowsOriginal,
        forceEnabled: true,
        denseHitbox: true);
  }

  /// <summary>
  ///     Applies translated map string-array values to matching visible text
  ///     nodes so native and swap modes are visible before the addon refreshes
  ///     its backing array.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="translatedPayload">The translated structured payload.</param>
  /// <returns>The text-node keys written by this pass.</returns>
  private unsafe HashSet<nint> ApplyTranslatedTextNodeValues(
      AtkUnitBase* addon,
      StringArrayStructuredPayload originalPayload,
      StringArrayStructuredPayload translatedPayload)
  {
    HashSet<nint> appliedTextNodeKeys = [];
    foreach (var (index, originalSlot) in originalPayload.Slots)
    {
      if (!originalSlot.IsTranslatable ||
          !translatedPayload.Slots.TryGetValue(index, out var translatedSlot) ||
          string.IsNullOrWhiteSpace(translatedSlot.TranslatedText))
      {
        continue;
      }

      var nativeText = this.ShouldRemoveDiacritics
          ? this.NormalizeQuestText(translatedSlot.TranslatedText)
          : translatedSlot.TranslatedText;
      if (string.IsNullOrWhiteSpace(nativeText) ||
          !this.TryFindReadableTextNodeByText(
              addon,
              originalSlot.OriginalText,
              nativeText,
              out var textNode))
      {
        continue;
      }

      var textNodeKey = (nint)textNode;
      if (!appliedTextNodeKeys.Add(textNodeKey))
      {
        continue;
      }

      var currentText = ReadTextNodeText(textNode);
      if (!string.Equals(currentText, nativeText, StringComparison.Ordinal))
      {
        textNode->NodeText.SetString(nativeText);
      }

      this.mapSurfaceTextNodeMutations[textNodeKey] =
          new MapSurfaceTextNodeMutationState(
              textNodeKey,
              originalSlot.OriginalText,
              nativeText);
    }

    return appliedTextNodeKeys;
  }

  /// <summary>
  ///     Resolves a translated payload from in-memory cache or SQLite.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source language.</param>
  /// <param name="scope">The captured translation reuse scope.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="translatedPayload">The resolved translated payload.</param>
  /// <param name="projection">The projected translated live values.</param>
  /// <returns><c>true</c> when a complete translated payload exists.</returns>
  private bool TryFindTranslatedPayload(
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope scope,
      StringArrayStructuredPayload originalPayload,
      out StringArrayStructuredPayload translatedPayload,
      out DbFirstStructuredStringArrayProjection projection)
  {
    var gameVersion = GetGameVersion();
    var sourceHash = originalPayload.ComputeSourceContentHash();
    var row = StringArrayDataCacheManager.TryFindCanonicalMatch(
        originalPayload.Type,
        originalPayload.ContextKey,
        scope,
        gameVersion,
        sourceHash);
    if (!MatchesSourceLanguage(row?.OriginalLang, sourceLanguage))
    {
      row = null;
    }

    if (row == null)
    {
      var probe = StringArrayDataPersistenceHelper.CreateCanonicalRow(
          originalPayload.Type,
          sourceLanguage.PersistenceCode,
          scope.TargetLanguageCode,
          scope.TranslationEngine.GetValueOrDefault(),
          gameVersion,
          originalPayload);

      row = StringArrayDataPersistenceHelper.FindStringArrayData(
          ConfigDirectory,
          probe,
          scope);
      if (row != null &&
          MatchesSourceLanguage(row.OriginalLang, sourceLanguage))
      {
        StringArrayDataCacheManager.Update(row);
      }
      else
      {
        row = null;
      }
    }

    if (row == null ||
        !StringArrayStructuredPayloadResolver.TryResolvePayloads(
            row,
            out _,
            out var resolvedTranslatedPayload) ||
        resolvedTranslatedPayload == null ||
        !DbFirstStructuredStringArrayHelper.TryProjectTranslatedPayload(
            originalPayload,
            resolvedTranslatedPayload,
            out projection))
    {
      translatedPayload = new StringArrayStructuredPayload();
      projection = DbFirstStructuredStringArrayProjection.Empty;
      return false;
    }

    translatedPayload = resolvedTranslatedPayload;
    return true;
  }

  /// <summary>
  ///     Queues one missing map string-array payload for background
  ///     translation and persistence.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source language.</param>
  /// <param name="scope">The captured translation reuse scope.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <param name="payloadKey">The stable in-flight key.</param>
  private void QueueTranslationIfNeeded(
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope scope,
      StringArrayStructuredPayload originalPayload,
      string payloadKey)
  {
    if (!InFlightPayloads.TryAdd(payloadKey, 0))
    {
      return;
    }

    _ = Task.Run(() => this.TranslateAndPersistPayloadAsync(
            sourceLanguage,
            scope,
            originalPayload))
        .ContinueWith(
            task =>
            {
              InFlightPayloads.TryRemove(payloadKey, out _);
              if (task.Status == TaskStatus.RanToCompletion &&
                  task.Result != null)
              {
                StringArrayDataCacheManager.Update(task.Result);
                ClearFailedPayloadRetry(payloadKey);
                return;
              }

              if (task.Exception != null)
              {
                PluginRuntimeLog.Error(
                    $"[{this.addonName}] Map surface translation failed: {task.Exception}");
              }

              MarkFailedPayloadRetry(payloadKey);
            },
            TaskScheduler.Default);
  }

  /// <summary>
  ///     Translates and persists one structured map string-array payload.
  /// </summary>
  /// <param name="sourceLanguage">The operation-captured source language.</param>
  /// <param name="scope">The captured translation reuse scope.</param>
  /// <param name="originalPayload">The original structured payload.</param>
  /// <returns>The persisted row snapshot, or <see langword="null" />.</returns>
  private async Task<StringArrayDatas?> TranslateAndPersistPayloadAsync(
      SourceClientLanguage sourceLanguage,
      TranslationReuseScope scope,
      StringArrayStructuredPayload originalPayload)
  {
    var engineId = scope.TranslationEngine.GetValueOrDefault();
    var translatorResolution = this.TranslationService.CaptureTranslatorResolution(
        engineId,
        TranslationSurfaceGroup.Default);
    var originContext = $"[{this.addonName}] StringArrayData";
    var translatedPayload = await DbFirstStructuredStringArrayHelper
        .TranslatePayloadAsync(
            originalPayload,
            this.TranslationService,
            sourceLanguage,
            scope.TargetLanguageCode,
            translatorResolution,
            originContext);
    if (!DbFirstStructuredStringArrayHelper.TryProjectTranslatedPayload(
            originalPayload,
            translatedPayload,
            out _))
    {
      return null;
    }

    var row = StringArrayDataPersistenceHelper.CreateCanonicalRow(
        originalPayload.Type,
        scope.SourceLanguageCode,
        scope.TargetLanguageCode,
        engineId,
        GetGameVersion(),
        originalPayload,
        translatedPayload);
    _ = StringArrayDataPersistenceHelper.InsertStringArrayData(
        ConfigDirectory,
        row);
    return row;
  }

  /// <summary>
  ///     Resolves the original payload when the live string array may already
  ///     contain a plugin-owned translated payload.
  /// </summary>
  /// <param name="liveArray">The captured live string-array payload.</param>
  /// <returns>The source-facing original payload.</returns>
  private StringArrayStructuredPayload ResolveOriginalPayload(
      MapSurfaceLiveArray liveArray)
  {
    if (!this.runtimeStates.TryGetValue(
            liveArray.ArrayIndex,
            out var runtimeState))
    {
      return liveArray.Payload;
    }

    if (PayloadMatchesOriginal(liveArray.Payload, runtimeState.OriginalPayload) ||
        PayloadMatchesAppliedTranslation(liveArray.Payload, runtimeState))
    {
      return runtimeState.OriginalPayload;
    }

    this.runtimeStates.Remove(liveArray.ArrayIndex);
    return liveArray.Payload;
  }

  /// <summary>
  ///     Captures live map-surface string arrays subscribed by the addon.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <param name="liveArrays">The captured live arrays.</param>
  /// <returns><c>true</c> when at least one map payload was captured.</returns>
  private unsafe bool TryCaptureLiveMapSurfaceArrays(
      AtkUnitBase* addon,
      out List<MapSurfaceLiveArray> liveArrays)
  {
    liveArrays = [];
    if (!TryGetRaptureAtkModule(out var raptureAtkModule))
    {
      return false;
    }

    var arrayHolder = raptureAtkModule->AtkArrayDataHolder;
    for (var arrayIndex = 0;
         arrayIndex < arrayHolder.StringArrayCount;
         arrayIndex++)
    {
      var stringArrayData = arrayHolder.StringArrays[arrayIndex];
      if (!IsUsableSubscribedStringArray(addon, stringArrayData))
      {
        continue;
      }

      var slotTexts = ReadStringArrayValues(stringArrayData);
      var payload = MapSurfaceStringArraySchema.BuildPayload(
          this.addonName,
          arrayIndex,
          slotTexts);
      if (!MapSurfaceStringArraySchema.IsMapSurfacePayload(payload))
      {
        continue;
      }

      liveArrays.Add(new MapSurfaceLiveArray(
          arrayIndex,
          (nint)stringArrayData,
          payload));
    }

    return liveArrays.Count != 0;
  }

  /// <summary>
  ///     Resolves the visible addon by name.
  /// </summary>
  /// <param name="addon">The visible addon.</param>
  /// <returns><c>true</c> when the addon is visible.</returns>
  private unsafe bool TryGetVisibleAddon(out AtkUnitBase* addon)
  {
    addon = null;
    if (!FrameworkAccessGuard.TryGetRaptureAtkUnitManager(out var manager))
    {
      return false;
    }

    addon = manager->GetAddonByName(this.addonName);
    return addon != null && addon->IsVisible;
  }

  /// <summary>
  ///     Restores all plugin-owned native string-array mutations.
  /// </summary>
  private unsafe void RestoreOriginalPayloadsIfNeeded()
  {
    if (this.runtimeStates.Count != 0)
    {
      foreach (var runtimeState in this.runtimeStates.Values.ToArray())
      {
        this.RestoreOriginalPayloadIfNeeded(runtimeState);
      }
    }

    this.runtimeStates.Clear();
    this.RestoreTextNodeMutationsIfNeeded();
  }

  /// <summary>
  ///     Restores one plugin-owned native string-array mutation.
  /// </summary>
  /// <param name="runtimeState">The runtime state to restore.</param>
  private unsafe void RestoreOriginalPayloadIfNeeded(
      MapSurfaceRuntimeState runtimeState)
  {
    if (!TryResolveLiveStringArrayByIndex(
            runtimeState.ArrayIndex,
            out var stringArrayData))
    {
      return;
    }

    foreach (var (index, originalSlot) in runtimeState.OriginalPayload.Slots)
    {
      if (!originalSlot.IsTranslatable ||
          !runtimeState.AppliedTranslatedTexts.TryGetValue(
              index,
              out var appliedText) ||
          (uint)index >= stringArrayData->Size)
      {
        continue;
      }

      var currentText = ReadStringArrayValue(stringArrayData, index);
      var stringArrayAddress = (nint)stringArrayData;
      NativeMutationOwnership.TryRestore(
          currentText,
          appliedText,
          originalSlot.OriginalText,
          restoredText =>
              ((StringArrayData*)stringArrayAddress)->SetValue(
                  index,
                  restoredText,
                  suppressUpdates: true));
    }
  }

  /// <summary>
  ///     Resolves a live string array by array index.
  /// </summary>
  /// <param name="arrayIndex">The live array index.</param>
  /// <param name="stringArrayData">The resolved string array.</param>
  /// <returns><c>true</c> when the string array is available.</returns>
  private static unsafe bool TryResolveLiveStringArrayByIndex(
      int arrayIndex,
      out StringArrayData* stringArrayData)
  {
    stringArrayData = null;
    if (!TryGetRaptureAtkModule(out var raptureAtkModule))
    {
      return false;
    }

    var arrayHolder = raptureAtkModule->AtkArrayDataHolder;
    if (arrayIndex < 0 || arrayIndex >= arrayHolder.StringArrayCount)
    {
      return false;
    }

    stringArrayData = arrayHolder.StringArrays[arrayIndex];
    return stringArrayData != null &&
           stringArrayData->StringArray != null &&
           stringArrayData->Size > 0;
  }

  /// <summary>
  ///     Clears hover and native mutation state.
  /// </summary>
  private void ClearMapSurfaceState()
  {
    this.RestoreOriginalPayloadsIfNeeded();
    this.runtimeStates.Clear();
    this.mapSurfaceTextNodeMutations.Clear();
    this.RemoveHoverTooltipsByPrefix(this.hoverPrefix);
  }

  /// <summary>
  ///     Restores visible map text nodes still containing plugin-owned native
  ///     translations.
  /// </summary>
  /// <param name="retainedTextNodeKeys">
  ///     Optional text nodes written by the current pass that should not be
  ///     restored.
  /// </param>
  private unsafe void RestoreTextNodeMutationsIfNeeded(
      IReadOnlySet<nint>? retainedTextNodeKeys = null)
  {
    if (this.mapSurfaceTextNodeMutations.Count == 0)
    {
      return;
    }

    if (!this.TryGetVisibleAddon(out var addon))
    {
      this.mapSurfaceTextNodeMutations.Clear();
      return;
    }

    foreach (var mutation in this.mapSurfaceTextNodeMutations.Values.ToArray())
    {
      if (retainedTextNodeKeys?.Contains(mutation.TextNodeKey) == true)
      {
        continue;
      }

      if (this.TryFindReadableTextNodeByText(
              addon,
              mutation.OriginalText,
              mutation.AppliedText,
              out var textNode))
      {
        var textNodeAddress = (nint)textNode;
        var currentText = ReadTextNodeText(textNode);
        NativeMutationOwnership.TryRestore(
            currentText,
            mutation.AppliedText,
            mutation.OriginalText,
            restoredText =>
                ((AtkTextNode*)textNodeAddress)->NodeText.SetString(
                    restoredText));
      }

      this.mapSurfaceTextNodeMutations.Remove(mutation.TextNodeKey);
    }
  }

  private TranslationReuseScope CreateReuseScope(
      SourceClientLanguage sourceLanguage)
  {
    return new TranslationReuseScope(
        sourceLanguage.PersistenceCode,
        RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.Config.Lang),
        this.EffectiveTranslationEngineId,
        this.Config.TranslateAlreadyTranslatedTexts);
  }

  private string BuildPayloadKey(
      TranslationReuseScope scope,
      StringArrayStructuredPayload payload)
  {
    return DbFirstGameWindowWorkKey.Build(
        this.addonName,
        scope,
        GetGameVersion(),
        payload.ComputeSourceContentHash());
  }

  private bool IsOwnAddonEvent(AddonArgs args)
  {
    return string.Equals(
        args.AddonName,
        this.addonName,
        StringComparison.Ordinal);
  }

  private static unsafe bool TryGetRaptureAtkModule(
      out RaptureAtkModule* raptureAtkModule)
  {
    raptureAtkModule = null;
    if (!FrameworkAccessGuard.IsClientReadyForFrameworkAccess())
    {
      return false;
    }

    try
    {
      raptureAtkModule = RaptureAtkModule.Instance();
      return raptureAtkModule != null;
    }
    catch (InvalidOperationException)
    {
      return false;
    }
  }

  private static unsafe bool IsUsableSubscribedStringArray(
      AtkUnitBase* addon,
      StringArrayData* stringArrayData)
  {
    return addon != null &&
           stringArrayData != null &&
           stringArrayData->StringArray != null &&
           stringArrayData->Size > 0 &&
           stringArrayData->SubscribedAddonsCount > 0 &&
           stringArrayData->SubscribedAddons.Contains((byte)addon->Id);
  }

  private static unsafe SortedDictionary<int, string?> ReadStringArrayValues(
      StringArrayData* stringArrayData)
  {
    var slotTexts = new SortedDictionary<int, string?>();
    if (stringArrayData == null || stringArrayData->StringArray == null)
    {
      return slotTexts;
    }

    for (var index = 0; index < stringArrayData->Size; index++)
    {
      slotTexts[index] = ReadStringArrayValue(stringArrayData, index);
    }

    return slotTexts;
  }

  private static unsafe string ReadStringArrayValue(
      StringArrayData* stringArrayData,
      int index)
  {
    try
    {
      return stringArrayData->StringArray[index]
          .AsReadOnlySeStringSpan()
          .ExtractText();
    }
    catch
    {
      return string.Empty;
    }
  }

  /// <summary>
  ///     Reads the best plain-text representation from a map text node.
  /// </summary>
  /// <param name="textNode">The map text node.</param>
  /// <returns>The readable text, or an empty string.</returns>
  private static unsafe string ReadTextNodeText(AtkTextNode* textNode)
  {
    if (textNode == null)
    {
      return string.Empty;
    }

    var currentText = textNode->NodeText.ToString();
    if (!string.IsNullOrWhiteSpace(currentText))
    {
      return currentText;
    }

    try
    {
      var originalText = textNode->OriginalTextPointer
          .AsReadOnlySeStringSpan()
          .ExtractText();
      if (!string.IsNullOrWhiteSpace(originalText))
      {
        return originalText;
      }
    }
    catch
    {
      // Keep falling through to the legacy buffer read below.
    }

    try
    {
      return MemoryHelper.ReadSeStringAsString(
          out _,
          (nint)textNode->NodeText.StringPtr.Value);
    }
    catch
    {
      return string.Empty;
    }
  }

  private static string BuildTooltipText(
      StringArrayStructuredPayload originalPayload,
      StringArrayStructuredPayload? translatedPayload)
  {
    var lines = new List<string>();
    foreach (var (index, originalSlot) in originalPayload.Slots)
    {
      if (!originalSlot.IsTranslatable)
      {
        continue;
      }

      var text = translatedPayload == null
          ? originalSlot.OriginalText
          : translatedPayload.Slots.TryGetValue(index, out var translatedSlot)
              ? translatedSlot.TranslatedText
              : string.Empty;
      if (!string.IsNullOrWhiteSpace(text))
      {
        lines.Add(text);
      }
    }

    return string.Join(Environment.NewLine, lines);
  }

  private static bool PayloadMatchesOriginal(
      StringArrayStructuredPayload livePayload,
      StringArrayStructuredPayload originalPayload)
  {
    foreach (var (index, originalSlot) in originalPayload.Slots)
    {
      if (!livePayload.Slots.TryGetValue(index, out var liveSlot) ||
          !string.Equals(
              liveSlot.OriginalText,
              originalSlot.OriginalText,
              StringComparison.Ordinal))
      {
        return false;
      }
    }

    return true;
  }

  private static bool PayloadMatchesAppliedTranslation(
      StringArrayStructuredPayload livePayload,
      MapSurfaceRuntimeState runtimeState)
  {
    foreach (var (index, originalSlot) in runtimeState.OriginalPayload.Slots)
    {
      if (!livePayload.Slots.TryGetValue(index, out var liveSlot))
      {
        return false;
      }

      var expectedText = runtimeState.AppliedTranslatedTexts.TryGetValue(
          index,
          out var appliedText)
          ? appliedText
          : originalSlot.OriginalText;
      if (!string.Equals(
              liveSlot.OriginalText,
              expectedText,
              StringComparison.Ordinal))
      {
        return false;
      }
    }

    return true;
  }

  private static bool MatchesSourceLanguage(
      string? persistedSourceLanguage,
      SourceClientLanguage sourceLanguage)
  {
    return RuntimeLanguageHelper.LanguagesMatch(
        persistedSourceLanguage,
        sourceLanguage.PersistenceCode);
  }

  private static bool TryGetFailedPayloadRetryUtc(
      string payloadKey,
      out DateTime retryUtc)
  {
    retryUtc = DateTime.MinValue;
    if (!FailedPayloadRetryUtc.TryGetValue(payloadKey, out retryUtc))
    {
      return false;
    }

    if (DateTime.UtcNow < retryUtc)
    {
      return true;
    }

    FailedPayloadRetryUtc.TryRemove(payloadKey, out _);
    return false;
  }

  private static void MarkFailedPayloadRetry(string payloadKey)
  {
    FailedPayloadRetryUtc[payloadKey] = DateTime.UtcNow + FailureRetryInterval;
  }

  private static void ClearFailedPayloadRetry(string payloadKey)
  {
    FailedPayloadRetryUtc.TryRemove(payloadKey, out _);
  }

  private readonly record struct MapSurfaceLiveArray(
      int ArrayIndex,
      nint ArrayAddress,
      StringArrayStructuredPayload Payload);

  private sealed record MapSurfaceRuntimeState(
      int ArrayIndex,
      StringArrayStructuredPayload OriginalPayload,
      SortedDictionary<int, string> AppliedTranslatedTexts);

  /// <summary>
  ///     Captures one plugin-owned map text-node mutation for guarded restore.
  /// </summary>
  /// <param name="TextNodeKey">The visible text-node pointer key.</param>
  /// <param name="OriginalText">The game-owned original text.</param>
  /// <param name="AppliedText">The plugin-applied translated text.</param>
  private sealed record MapSurfaceTextNodeMutationState(
      nint TextNodeKey,
      string OriginalText,
      string AppliedText);
}
