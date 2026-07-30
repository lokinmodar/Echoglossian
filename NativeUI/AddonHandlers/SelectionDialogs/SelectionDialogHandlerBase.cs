// <copyright file="SelectionDialogHandlerBase.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

using global::Echoglossian.NativeUI.Helpers;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

/// <summary>
///     Shared runtime for generic selection-dialog add-ons that can capture
///     text from <c>AtkValues</c>, <c>StringArrayData</c>, or readable text
///     nodes and then apply translation through the active display mode.
/// </summary>
public abstract class SelectionDialogHandlerBase :
    IAddonTranslationHandler,
    IPluginUnloadAwareAddonHandler
{
    private readonly string addonName;
    private readonly Config config;
    private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>>
        eventHandlers = new();
    private readonly HoverTooltipManager hoverTooltipManager;
    private readonly Func<bool> isTranslationEnabled;
    private readonly Func<JournalTranslationDisplayMode> resolveDisplayMode;
    private readonly Func<string, string> normalizeReplacementText;
    private readonly object stateGate = new();
    private readonly TranslationService translationService;
    private readonly string hoverTooltipKeyPrefix;

    private DialogState state = new();

    /// <summary>
    ///     In-memory state for the currently visible selection dialog.
    /// </summary>
    private sealed class DialogState
    {
        public int ActiveRequestId;

        public SelectionDialogPayload? CurrentPayload { get; set; }

        public string CurrentSourceLanguageCode { get; set; } = string.Empty;

        public List<string> CurrentTranslatedTexts { get; set; } = [];

        public List<string> CurrentReplacementTexts { get; set; } = [];

        public string LastFailedSourceKey { get; set; } = string.Empty;

        public bool OwnsNativeMutation { get; set; }

        public bool TranslationInFlight { get; set; }
    }

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="SelectionDialogHandlerBase" /> class.
    /// </summary>
    /// <param name="addonName">The native addon name.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="isTranslationEnabled">
    ///     Resolves whether the handler is enabled.
    /// </param>
    /// <param name="resolveDisplayMode">Resolves the active display mode.</param>
    /// <param name="hoverTooltipManager">
    ///     The shared hover-tooltip manager.
    /// </param>
    /// <param name="normalizeReplacementText">
    ///     Normalizes translated text for native replacement.
    /// </param>
    protected SelectionDialogHandlerBase(
        string addonName,
        Config config,
        TranslationService translationService,
        HoverTooltipManager hoverTooltipManager,
        Func<bool> isTranslationEnabled,
        Func<JournalTranslationDisplayMode> resolveDisplayMode,
        Func<string, string> normalizeReplacementText)
    {
        this.addonName = addonName;
        this.config = config;
        this.translationService = translationService;
        this.hoverTooltipManager = hoverTooltipManager;
        this.isTranslationEnabled = isTranslationEnabled;
        this.resolveDisplayMode = resolveDisplayMode;
        this.normalizeReplacementText = normalizeReplacementText;
        this.hoverTooltipKeyPrefix = $"{addonName}-SelectionDialog-";

        this.RegisterHandler(AddonEvent.PreSetup, this.OnCaptureDialog);
        this.RegisterHandler(AddonEvent.PreRefresh, this.OnCaptureDialog);
        this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnCaptureDialog);
        this.RegisterHandler(AddonEvent.PostUpdate, this.OnUpdateVisibleAddon);
        this.RegisterHandler(AddonEvent.PreDraw, this.OnUpdateVisibleAddon);
        this.RegisterHandler(AddonEvent.PreHide, this.OnResetState);
        this.RegisterHandler(AddonEvent.PreFinalize, this.OnResetState);
    }

    /// <summary>
    ///     Gets the native addon name.
    /// </summary>
    protected string AddonName => this.addonName;

    /// <summary>
    ///     Gets the active target language code.
    /// </summary>
    protected string TargetLanguageCode => LangDict[LanguageInt].Code;

    /// <summary>
    ///     Returns the event handlers required to drive the selection-dialog
    ///     flow.
    /// </summary>
    /// <returns>The stable event-handler map.</returns>
    public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>
        GetEventHandlers()
    {
        return this.eventHandlers.ToDictionary(
            pair => pair.Key,
            pair => new IAddonLifecycle.AddonEventDelegate((evt, args) =>
            {
                foreach (var handler in pair.Value)
                {
                    handler(evt, args);
                }
            }));
    }

    /// <inheritdoc />
    public unsafe void OnPluginUnload()
    {
        if (this.TryGetVisibleAddon(out var addon))
        {
            this.TryRestoreNativeMutation(addon);
        }

        lock (this.stateGate)
        {
            this.state = new DialogState();
        }

        this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
    }

    /// <summary>
    ///     Tries to resolve a persisted translation for the captured payload.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="originalTexts">The ordered source payload.</param>
    /// <param name="translatedTexts">Receives the translated payload.</param>
    /// <returns>
    ///     <see langword="true" /> when a translated payload was found;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    protected abstract bool TryFindStoredTranslation(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        out List<string> translatedTexts);

    /// <summary>
    ///     Persists one translated payload.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="originalTexts">The ordered source payload.</param>
    /// <param name="translatedTexts">The ordered translated payload.</param>
    /// <returns>A task that completes with the persistence result message.</returns>
    protected abstract Task<string> PersistTranslationAsync(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> translatedTexts);

    /// <summary>
    ///     Determines whether the hover tooltip should promote the first
    ///     captured text into the title slot.
    /// </summary>
    /// <returns>
     ///     <see langword="true" /> when the first text should be the title;
     ///     otherwise, <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldPromoteFirstOverlayTextToTitle()
    {
        return true;
    }

    /// <summary>
    ///     Builds one selection-dialog lookup row for the dedicated generic
    ///     selection-dialog table.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="originalTexts">The ordered source payload.</param>
    /// <returns>The lookup row, or <see langword="null" />.</returns>
    protected SelectionDialogText? BuildSelectionDialogLookup(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts)
    {
        if (string.IsNullOrWhiteSpace(this.TargetLanguageCode))
        {
            return null;
        }

        return new SelectionDialogText(
            this.addonName,
            JsonConvert.SerializeObject(originalTexts),
            sourceLanguage.PersistenceCode,
            string.Empty,
            this.TargetLanguageCode,
            this.GetDialogueTranslationEngineId(),
            GetGameVersion(),
            sourceContentHash: null,
            DateTime.Now,
            DateTime.Now);
    }

    /// <summary>
    ///     Builds one selection-dialog persistence row for the dedicated
    ///     generic selection-dialog table.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="originalTexts">The ordered source payload.</param>
    /// <param name="translatedTexts">The ordered translated payload.</param>
    /// <returns>The persistence row, or <see langword="null" />.</returns>
    protected SelectionDialogText? BuildSelectionDialogRow(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> translatedTexts)
    {
        if (string.IsNullOrWhiteSpace(this.TargetLanguageCode))
        {
            return null;
        }

        return new SelectionDialogText(
            this.addonName,
            JsonConvert.SerializeObject(originalTexts),
            sourceLanguage.PersistenceCode,
            JsonConvert.SerializeObject(translatedTexts),
            this.TargetLanguageCode,
            this.GetDialogueTranslationEngineId(),
            GetGameVersion(),
            sourceContentHash: null,
            DateTime.Now,
            DateTime.Now);
    }

    /// <summary>
    ///     Builds one select-string lookup row when the payload shape matches
    ///     the normal select-string dialog contract.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="originalTexts">The ordered source payload.</param>
    /// <returns>The lookup row, or <see langword="null" />.</returns>
    protected SelectString? BuildSelectStringLookup(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts)
    {
        if (!CanPersistAsSelectString(originalTexts) ||
            string.IsNullOrWhiteSpace(this.TargetLanguageCode))
        {
            return null;
        }

        return new SelectString(
            originalTexts[0],
            sourceLanguage.PersistenceCode,
            JsonConvert.SerializeObject(originalTexts.Skip(1).ToList()),
            string.Empty,
            string.Empty,
            this.TargetLanguageCode,
            this.GetDialogueTranslationEngineId(),
            DateTime.Now,
            DateTime.Now);
    }

    /// <summary>
    ///     Builds one select-string persistence row when the payload shape
    ///     matches the normal select-string dialog contract.
    /// </summary>
    /// <param name="sourceLanguage">The resolved source language.</param>
    /// <param name="originalTexts">The ordered source payload.</param>
    /// <param name="translatedTexts">The ordered translated payload.</param>
    /// <returns>The persistence row, or <see langword="null" />.</returns>
    protected SelectString? BuildSelectStringRow(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> translatedTexts)
    {
        if (!CanPersistAsSelectString(originalTexts) ||
            translatedTexts.Count != originalTexts.Count ||
            string.IsNullOrWhiteSpace(this.TargetLanguageCode))
        {
            return null;
        }

        return new SelectString(
            originalTexts[0],
            sourceLanguage.PersistenceCode,
            JsonConvert.SerializeObject(originalTexts.Skip(1).ToList()),
            translatedTexts[0],
            JsonConvert.SerializeObject(translatedTexts.Skip(1).ToList()),
            this.TargetLanguageCode,
            this.GetDialogueTranslationEngineId(),
            DateTime.Now,
            DateTime.Now);
    }

    /// <summary>
    ///     Deserializes stored option payloads from the select-string table.
    /// </summary>
    /// <param name="optionsJson">The serialized options.</param>
    /// <returns>The materialized options list.</returns>
    protected static List<string> DeserializeOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return [];
        }

        try
        {
            return JsonConvert.DeserializeObject<List<string>>(optionsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    ///     Determines whether the captured payload shape can safely reuse the
    ///     select-string table.
    /// </summary>
    /// <param name="texts">The ordered source payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload can reuse the
    ///     select-string table; otherwise, <see langword="false" />.
    /// </returns>
    protected static bool CanPersistAsSelectString(IReadOnlyList<string> texts)
    {
        return texts.Count >= 2 &&
               texts.All(text => !string.IsNullOrWhiteSpace(text));
    }

    private JournalTranslationDisplayMode DisplayMode =>
        this.resolveDisplayMode();

    private void RegisterHandler(
        AddonEvent evt,
        LocalAddonHandlerDelegate handler)
    {
        if (!this.eventHandlers.TryGetValue(evt, out var handlers))
        {
            handlers = [];
            this.eventHandlers[evt] = handlers;
        }

        handlers.Add(handler);
    }

    private unsafe void OnCaptureDialog(AddonEvent type, AddonArgs args)
    {
        if (!this.ShouldHandleAddon(args, out var addon) ||
            !RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            return;
        }

        var livePayload = this.CaptureLivePayload(addon);
        if (livePayload == null)
        {
            return;
        }

        var effectivePayload = this.ResolveEffectivePayload(
            livePayload,
            sourceLanguage);

        if (this.TryGetCurrentResolvedTranslation(
                effectivePayload,
                sourceLanguage,
                out _,
                out _))
        {
            return;
        }

        if (this.TryLoadStoredTranslation(
                effectivePayload,
                sourceLanguage,
                out var storedTranslatedTexts,
                out var storedReplacementTexts))
        {
            this.SetResolvedState(
                effectivePayload,
                sourceLanguage,
                storedTranslatedTexts,
                storedReplacementTexts);
            return;
        }

        this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
        if (this.TryQueueTranslation(
                effectivePayload,
                sourceLanguage,
                out var requestId))
        {
            _ = Task.Run(() => this.ResolveTranslationAsync(
                effectivePayload,
                requestId,
                sourceLanguage));
        }
    }

    private unsafe void OnUpdateVisibleAddon(AddonEvent type, AddonArgs args)
    {
        if (!this.ShouldHandleAddon(args, out var addon) ||
            !RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            return;
        }

        var livePayload = this.CaptureLivePayload(addon);
        if (livePayload == null)
        {
            return;
        }

        var effectivePayload = this.ResolveEffectivePayload(
            livePayload,
            sourceLanguage);
        if (!this.TryGetCurrentResolvedTranslation(
                effectivePayload,
                sourceLanguage,
                out var translatedTexts,
                out var replacementTexts))
        {
            this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
            return;
        }

        if (!this.ShouldApplyNativeText())
        {
            this.TryRestoreNativeMutation(addon);
        }

        this.RefreshHoverTooltips(
            addon,
            effectivePayload,
            translatedTexts);
        if (!this.ShouldApplyNativeText())
        {
            return;
        }

        this.ApplyNativeTranslation(
            addon,
            effectivePayload,
            replacementTexts);
    }

    private unsafe void OnResetState(AddonEvent type, AddonArgs args)
    {
        if (args.AddonName == this.addonName &&
            args.Addon.Address != IntPtr.Zero)
        {
            this.TryRestoreNativeMutation((AtkUnitBase*)args.Addon.Address);
        }

        lock (this.stateGate)
        {
            this.state = new DialogState();
        }

        this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
    }

    private unsafe SelectionDialogPayload? CaptureLivePayload(AtkUnitBase* addon)
    {
        var atkValuePayload = this.CaptureAtkValuePayload(addon);
        var stringArrayPayload = this.CaptureStringArrayPayload(addon);
        var textNodePayload = this.CaptureTextNodePayload(addon);
        var sourceKind = SelectionDialogCapturePolicy.ResolveBestSource(
            atkValuePayload != null,
            stringArrayPayload != null,
            textNodePayload != null);

        var preferredPayload = sourceKind switch
        {
            SelectionDialogCaptureSourceKind.AtkValues => atkValuePayload,
            SelectionDialogCaptureSourceKind.StringArrayData => stringArrayPayload,
            SelectionDialogCaptureSourceKind.TextNodes => textNodePayload,
            _ => null,
        };

        if (preferredPayload != null &&
            textNodePayload != null &&
            SelectionDialogCapturePolicy.ShouldPreferTextNodePayload(
                preferredPayload,
                textNodePayload))
        {
            return textNodePayload;
        }

        return preferredPayload;
    }

    private unsafe SelectionDialogPayload? CaptureAtkValuePayload(AtkUnitBase* addon)
    {
        if (addon == null ||
            addon->AtkValues == null ||
            addon->AtkValuesCount == 0)
        {
            return null;
        }

        var indices = new List<int>();
        var texts = new List<string>();
        var atkValueSpan = new Span<AtkValue>(
            addon->AtkValues,
            addon->AtkValuesCount);
        for (var index = 0; index < atkValueSpan.Length; index++)
        {
            ref var value = ref atkValueSpan[index];
            if (!IsStringValue(in value))
            {
                continue;
            }

            var text = this.ReadAtkValueText(in value);
            if (!ShouldCaptureText(text))
            {
                continue;
            }

            indices.Add(index);
            texts.Add(text);
        }

        return texts.Count == 0
            ? null
            : SelectionDialogPayload.FromAtkValues(indices, texts);
    }

    private unsafe SelectionDialogPayload? CaptureStringArrayPayload(
        AtkUnitBase* addon)
    {
        var raptureAtkModule = RaptureAtkModule.Instance();
        if (addon == null || raptureAtkModule == null)
        {
            return null;
        }

        var arrayHolder = raptureAtkModule->AtkArrayDataHolder;
        for (var arrayIndex = 0;
             arrayIndex < arrayHolder.StringArrayCount;
             arrayIndex++)
        {
            var stringArrayData = arrayHolder.StringArrays[arrayIndex];
            if (stringArrayData == null ||
                stringArrayData->StringArray == null ||
                stringArrayData->Size <= 0 ||
                stringArrayData->SubscribedAddonsCount <= 0 ||
                !stringArrayData->SubscribedAddons.Contains((byte)addon->Id))
            {
                continue;
            }

            var indices = new List<int>();
            var texts = new List<string>();
            for (var valueIndex = 0; valueIndex < stringArrayData->Size; valueIndex++)
            {
                var text = this.ReadStringArrayValue(stringArrayData, valueIndex);
                if (!ShouldCaptureText(text))
                {
                    continue;
                }

                indices.Add(valueIndex);
                texts.Add(text);
            }

            if (texts.Count == 0)
            {
                continue;
            }

            return SelectionDialogPayload.FromStringArrayData(
                arrayIndex,
                indices,
                texts);
        }

        return null;
    }

    private unsafe SelectionDialogPayload? CaptureTextNodePayload(AtkUnitBase* addon)
    {
        var textNodeAddresses = SelectionDialogNodeResolvers
            .ResolveReadableTextNodes(addon);
        if (textNodeAddresses.Count == 0)
        {
            return null;
        }

        var texts = new List<string>(textNodeAddresses.Count);
        var keptAddresses = new List<nint>(textNodeAddresses.Count);
        foreach (var nodeAddress in textNodeAddresses)
        {
            var text = SelectionDialogNodeResolvers.ReadTextNode(
                (AtkTextNode*)nodeAddress);
            if (!ShouldCaptureText(text))
            {
                continue;
            }

            keptAddresses.Add(nodeAddress);
            texts.Add(text);
        }

        return texts.Count == 0
            ? null
            : SelectionDialogPayload.FromTextNodes(keptAddresses, texts);
    }

    private SelectionDialogPayload ResolveEffectivePayload(
        SelectionDialogPayload livePayload,
        SourceClientLanguage sourceLanguage)
    {
        lock (this.stateGate)
        {
            var currentPayload = this.state.CurrentPayload;
            if (currentPayload == null ||
                !MatchesSourceLanguage(
                    this.state.CurrentSourceLanguageCode,
                    sourceLanguage) ||
                !currentPayload.MatchesStructure(livePayload))
            {
                return livePayload;
            }

            if (currentPayload.TextsMatch(
                    livePayload.Texts,
                    this.TextMatches))
            {
                return currentPayload;
            }

            if (this.state.CurrentReplacementTexts.Count > 0 &&
                livePayload.TextsMatch(
                    this.state.CurrentReplacementTexts,
                    this.TextMatches))
            {
                return currentPayload;
            }

            return livePayload;
        }
    }

    private bool TryGetCurrentResolvedTranslation(
        SelectionDialogPayload payload,
        SourceClientLanguage sourceLanguage,
        out List<string> translatedTexts,
        out List<string> replacementTexts)
    {
        lock (this.stateGate)
        {
            var currentPayload = this.state.CurrentPayload;
            if (currentPayload == null ||
                !MatchesSourceLanguage(
                    this.state.CurrentSourceLanguageCode,
                    sourceLanguage) ||
                !currentPayload.MatchesStructure(payload) ||
                !currentPayload.TextsMatch(
                    payload.Texts,
                    this.TextMatches) ||
                this.state.CurrentTranslatedTexts.Count == 0)
            {
                translatedTexts = [];
                replacementTexts = [];
                return false;
            }

            translatedTexts = [.. this.state.CurrentTranslatedTexts];
            replacementTexts = [.. this.state.CurrentReplacementTexts];
            return true;
        }
    }

    private bool TryLoadStoredTranslation(
        SelectionDialogPayload payload,
        SourceClientLanguage sourceLanguage,
        out List<string> translatedTexts,
        out List<string> replacementTexts)
    {
        if (!this.TryFindStoredTranslation(
                sourceLanguage,
                payload.Texts,
                out translatedTexts) ||
            translatedTexts.Count != payload.Texts.Count)
        {
            translatedTexts = [];
            replacementTexts = [];
            return false;
        }

        replacementTexts = translatedTexts
            .Select(this.NormalizeForReplacement)
            .ToList();
        return true;
    }

    private bool TryQueueTranslation(
        SelectionDialogPayload payload,
        SourceClientLanguage sourceLanguage,
        out int requestId)
    {
        var sourceKey = payload.BuildSourceKey(this.NormalizeForComparison);
        lock (this.stateGate)
        {
            var matchesCurrentSource =
                this.state.CurrentPayload != null &&
                this.state.CurrentPayload.MatchesStructure(payload) &&
                MatchesSourceLanguage(
                    this.state.CurrentSourceLanguageCode,
                    sourceLanguage) &&
                this.state.CurrentPayload.TextsMatch(
                    payload.Texts,
                    this.TextMatches);
            if (matchesCurrentSource &&
                (this.state.TranslationInFlight ||
                 this.state.CurrentTranslatedTexts.Count > 0 ||
                 string.Equals(
                     this.state.LastFailedSourceKey,
                     sourceKey,
                     StringComparison.Ordinal)))
            {
                requestId = this.state.ActiveRequestId;
                return false;
            }

            this.state.ActiveRequestId++;
            requestId = this.state.ActiveRequestId;
            this.state.CurrentSourceLanguageCode = sourceLanguage.PersistenceCode;
            this.state.CurrentPayload = payload;
            this.state.CurrentTranslatedTexts = [];
            this.state.CurrentReplacementTexts = [];
            this.state.TranslationInFlight = true;
            this.state.LastFailedSourceKey = string.Empty;
            this.state.OwnsNativeMutation = false;
            return true;
        }
    }

    private async Task ResolveTranslationAsync(
        SelectionDialogPayload payload,
        int requestId,
        SourceClientLanguage sourceLanguage)
    {
        List<string> translatedTexts;
        try
        {
            translatedTexts = await this.TranslatePayloadAsync(
                payload.Texts,
                sourceLanguage).ConfigureAwait(false);
        }
        catch
        {
            translatedTexts = [];
        }

        if (!this.HasUsableTranslatedPayload(
                payload.Texts,
                translatedTexts,
                sourceLanguage.ProviderCode,
                this.TargetLanguageCode))
        {
            lock (this.stateGate)
            {
                if (this.state.ActiveRequestId == requestId &&
                    this.state.CurrentPayload?.MatchesStructure(payload) == true)
                {
                    this.state.TranslationInFlight = false;
                    this.state.LastFailedSourceKey = payload.BuildSourceKey(
                        this.NormalizeForComparison);
                }
            }

            return;
        }

        var replacementTexts = translatedTexts
            .Select(this.NormalizeForReplacement)
            .ToList();
        _ = await this.PersistTranslationAsync(
                sourceLanguage,
                payload.Texts,
                translatedTexts)
            .ConfigureAwait(false);
        this.SetResolvedState(
            payload,
            sourceLanguage,
            translatedTexts,
            replacementTexts,
            requestId);
    }

    private async Task<List<string>> TranslatePayloadAsync(
        IReadOnlyList<string> originalTexts,
        SourceClientLanguage sourceLanguage)
    {
        var indexedEntries = new List<(int Index, string Text)>(originalTexts.Count);
        for (var index = 0; index < originalTexts.Count; index++)
        {
            indexedEntries.Add((index, originalTexts[index]));
        }

        var translatedMap = new Dictionary<int, string>();
        foreach (var chunk in BuildIndexedTranslationChunks(indexedEntries))
        {
            var translatedChunk = await this.translationService.TranslateAsync(
                    chunk,
                    sourceLanguage,
                    this.TargetLanguageCode,
                    TranslationSurfaceGroup.Dialogue,
                    originContext: $"{this.addonName}/Chunk")
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(translatedChunk))
            {
                continue;
            }

            foreach (var (index, value) in ParseIndexedTranslationPairs(
                         translatedChunk))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    translatedMap[index] = value;
                }
            }
        }

        if (translatedMap.Count == 0)
        {
            return await this.TranslatePayloadIndividuallyAsync(
                    originalTexts,
                    sourceLanguage)
                .ConfigureAwait(false);
        }

        var translatedTexts = new List<string>(originalTexts.Count);
        for (var index = 0; index < originalTexts.Count; index++)
        {
            if (translatedMap.TryGetValue(index, out var translatedText) &&
                !string.IsNullOrWhiteSpace(translatedText))
            {
                translatedTexts.Add(translatedText);
                continue;
            }

            translatedTexts.Add(originalTexts[index]);
        }

        return translatedTexts;
    }

    private async Task<List<string>> TranslatePayloadIndividuallyAsync(
        IReadOnlyList<string> originalTexts,
        SourceClientLanguage sourceLanguage)
    {
        var translatedTexts = new List<string>(originalTexts.Count);
        foreach (var originalText in originalTexts)
        {
            translatedTexts.Add(
                await this.TranslateOrFallbackAsync(
                        originalText,
                        sourceLanguage)
                    .ConfigureAwait(false));
        }

        return translatedTexts;
    }

    private async Task<string> TranslateOrFallbackAsync(
        string text,
        SourceClientLanguage sourceLanguage)
    {
        var translatedText = await this.translationService.TranslateAsync(
                text,
                sourceLanguage,
                this.TargetLanguageCode,
                TranslationSurfaceGroup.Dialogue,
                originContext: $"{this.addonName}/Text")
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(translatedText) ? text : translatedText;
    }

    private void SetResolvedState(
        SelectionDialogPayload payload,
        SourceClientLanguage sourceLanguage,
        List<string> translatedTexts,
        List<string> replacementTexts,
        int requestId = -1)
    {
        lock (this.stateGate)
        {
            if (requestId >= 0 &&
                this.state.ActiveRequestId != requestId)
            {
                return;
            }

            this.state.CurrentSourceLanguageCode = sourceLanguage.PersistenceCode;
            this.state.CurrentPayload = payload;
            this.state.CurrentTranslatedTexts = [.. translatedTexts];
            this.state.CurrentReplacementTexts = [.. replacementTexts];
            this.state.TranslationInFlight = false;
            this.state.LastFailedSourceKey = string.Empty;
        }
    }

    private unsafe void RefreshHoverTooltips(
        AtkUnitBase* addon,
        SelectionDialogPayload payload,
        IReadOnlyList<string> translatedTexts)
    {
        this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);

        if (!this.ShouldUseHoverTooltips() ||
            payload.SourceKind != SelectionDialogCaptureSourceKind.TextNodes ||
            payload.TextNodeAddresses.Count == 0 ||
            translatedTexts.Count != payload.Texts.Count)
        {
            return;
        }

        var promoteFirstTextAsTitle = this.ShouldPromoteFirstOverlayTextToTitle();
        var originalParts = payload.ToOverlayParts(promoteFirstTextAsTitle);
        var translatedParts = ToOverlayParts(
            translatedTexts,
            promoteFirstTextAsTitle);
        var tooltipTitle = this.ShouldShowOriginalTooltips()
            ? originalParts.Title
            : translatedParts.Title;
        var tooltipBody = this.ShouldShowOriginalTooltips()
            ? originalParts.Body
            : translatedParts.Body;
        if (string.IsNullOrWhiteSpace(tooltipTitle) &&
            string.IsNullOrWhiteSpace(tooltipBody))
        {
            return;
        }

        for (var index = 0; index < payload.TextNodeAddresses.Count; index++)
        {
            var textNode = (AtkTextNode*)payload.TextNodeAddresses[index];
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var width = Math.Max(1f, textNode->GetWidth());
            var height = Math.Max(1f, textNode->GetHeight());
            this.hoverTooltipManager.Register(
                $"{this.hoverTooltipKeyPrefix}{index}",
                new Vector2(textNode->ScreenX - 12f, textNode->ScreenY - 8f),
                new Vector2(
                    textNode->ScreenX + width + 12f,
                    textNode->ScreenY + height + 8f),
                tooltipTitle,
                tooltipBody,
                enabled: true);
        }
    }

    private unsafe void ApplyNativeTranslation(
        AtkUnitBase* addon,
        SelectionDialogPayload payload,
        IReadOnlyList<string> replacementTexts)
    {
        switch (payload.SourceKind)
        {
            case SelectionDialogCaptureSourceKind.AtkValues:
                this.ApplyAtkValueTranslation(addon, payload, replacementTexts);
                break;
            case SelectionDialogCaptureSourceKind.StringArrayData:
                this.ApplyStringArrayTranslation(payload, replacementTexts);
                break;
            case SelectionDialogCaptureSourceKind.TextNodes:
                this.ApplyTextNodeTranslation(payload, replacementTexts);
                break;
        }

        lock (this.stateGate)
        {
            if (this.state.CurrentPayload?.MatchesStructure(payload) == true)
            {
                this.state.OwnsNativeMutation = true;
            }
        }
    }

    private unsafe void ApplyAtkValueTranslation(
        AtkUnitBase* addon,
        SelectionDialogPayload payload,
        IReadOnlyList<string> replacementTexts)
    {
        if (addon == null || addon->AtkValues == null)
        {
            return;
        }

        var valueCount = Math.Min(
            payload.AtkValueIndices.Count,
            replacementTexts.Count);
        for (var index = 0; index < valueCount; index++)
        {
            var valueIndex = payload.AtkValueIndices[index];
            if ((uint)valueIndex >= addon->AtkValuesCount)
            {
                continue;
            }

            ref var value = ref addon->AtkValues[valueIndex];
            if (!IsStringValue(in value))
            {
                continue;
            }

            var replacementText = replacementTexts[index];
            var currentText = this.ReadAtkValueText(in value);
            if (this.TextMatches(currentText, replacementText))
            {
                continue;
            }

            value.SetManagedString(replacementText);
        }
    }

    private unsafe void ApplyStringArrayTranslation(
        SelectionDialogPayload payload,
        IReadOnlyList<string> replacementTexts)
    {
        if (payload.StringArrayIndex == null ||
            !this.TryGetStringArrayData(
                payload.StringArrayIndex.Value,
                out var stringArrayData))
        {
            return;
        }

        var valueCount = Math.Min(
            payload.StringArrayValueIndices.Count,
            replacementTexts.Count);
        for (var index = 0; index < valueCount; index++)
        {
            var valueIndex = payload.StringArrayValueIndices[index];
            if ((uint)valueIndex >= stringArrayData->Size)
            {
                continue;
            }

            var replacementText = replacementTexts[index];
            var currentText = this.ReadStringArrayValue(
                stringArrayData,
                valueIndex);
            if (this.TextMatches(currentText, replacementText))
            {
                continue;
            }

            stringArrayData->SetValue(
                valueIndex,
                replacementText,
                suppressUpdates: true);
        }
    }

    private unsafe void ApplyTextNodeTranslation(
        SelectionDialogPayload payload,
        IReadOnlyList<string> replacementTexts)
    {
        var valueCount = Math.Min(
            payload.TextNodeAddresses.Count,
            replacementTexts.Count);
        for (var index = 0; index < valueCount; index++)
        {
            var nodeAddress = payload.TextNodeAddresses[index];
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null)
            {
                continue;
            }

            var replacementText = replacementTexts[index];
            var currentText = SelectionDialogNodeResolvers.ReadTextNode(textNode);
            if (this.TextMatches(currentText, replacementText))
            {
                continue;
            }

            textNode->SetText(replacementText);
        }
    }

    private unsafe bool TryRestoreNativeMutation(AtkUnitBase* addon)
    {
        SelectionDialogPayload? payload;
        List<string> originalTexts;
        List<string> replacementTexts;
        lock (this.stateGate)
        {
            if (!this.state.OwnsNativeMutation ||
                this.state.CurrentPayload == null)
            {
                return false;
            }

            payload = this.state.CurrentPayload;
            originalTexts = [.. this.state.CurrentPayload.Texts];
            replacementTexts = [.. this.state.CurrentReplacementTexts];
            this.state.OwnsNativeMutation = false;
        }

        if (payload == null)
        {
            return false;
        }

        return payload.SourceKind switch
        {
            SelectionDialogCaptureSourceKind.AtkValues =>
                this.RestoreAtkValueTranslation(
                    addon,
                    payload,
                    originalTexts,
                    replacementTexts),
            SelectionDialogCaptureSourceKind.StringArrayData =>
                this.RestoreStringArrayTranslation(
                    payload,
                    originalTexts,
                    replacementTexts),
            SelectionDialogCaptureSourceKind.TextNodes =>
                this.RestoreTextNodeTranslation(
                    payload,
                    originalTexts,
                    replacementTexts),
            _ => false,
        };
    }

    private unsafe bool RestoreAtkValueTranslation(
        AtkUnitBase* addon,
        SelectionDialogPayload payload,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> replacementTexts)
    {
        if (addon == null || addon->AtkValues == null)
        {
            return false;
        }

        var restoredAny = false;
        var valueCount = Math.Min(
            payload.AtkValueIndices.Count,
            Math.Min(originalTexts.Count, replacementTexts.Count));
        for (var index = 0; index < valueCount; index++)
        {
            var valueIndex = payload.AtkValueIndices[index];
            if ((uint)valueIndex >= addon->AtkValuesCount)
            {
                continue;
            }

            ref var value = ref addon->AtkValues[valueIndex];
            if (!IsStringValue(in value))
            {
                continue;
            }

            var currentText = this.ReadAtkValueText(in value);
            var addonAddress = (nint)addon;
            restoredAny |= TryRestoreOwnedText(
                currentText,
                replacementTexts[index],
                originalTexts[index],
                restoredText =>
                    ((AtkUnitBase*)addonAddress)->AtkValues[valueIndex]
                        .SetManagedString(restoredText));
        }

        return restoredAny;
    }

    private unsafe bool RestoreStringArrayTranslation(
        SelectionDialogPayload payload,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> replacementTexts)
    {
        if (payload.StringArrayIndex == null ||
            !this.TryGetStringArrayData(
                payload.StringArrayIndex.Value,
                out var stringArrayData))
        {
            return false;
        }

        var restoredAny = false;
        var valueCount = Math.Min(
            payload.StringArrayValueIndices.Count,
            Math.Min(originalTexts.Count, replacementTexts.Count));
        for (var index = 0; index < valueCount; index++)
        {
            var valueIndex = payload.StringArrayValueIndices[index];
            if ((uint)valueIndex >= stringArrayData->Size)
            {
                continue;
            }

            var currentText = this.ReadStringArrayValue(
                stringArrayData,
                valueIndex);
            var stringArrayAddress = (nint)stringArrayData;
            restoredAny |= TryRestoreOwnedText(
                currentText,
                replacementTexts[index],
                originalTexts[index],
                restoredText =>
                    ((StringArrayData*)stringArrayAddress)->SetValue(
                        valueIndex,
                        restoredText,
                        suppressUpdates: true));
        }

        return restoredAny;
    }

    private unsafe bool RestoreTextNodeTranslation(
        SelectionDialogPayload payload,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> replacementTexts)
    {
        var restoredAny = false;
        var valueCount = Math.Min(
            payload.TextNodeAddresses.Count,
            Math.Min(originalTexts.Count, replacementTexts.Count));
        for (var index = 0; index < valueCount; index++)
        {
            var textNode = (AtkTextNode*)payload.TextNodeAddresses[index];
            if (textNode == null)
            {
                continue;
            }

            var currentText = SelectionDialogNodeResolvers.ReadTextNode(textNode);
            var textNodeAddress = payload.TextNodeAddresses[index];
            restoredAny |= TryRestoreOwnedText(
                currentText,
                replacementTexts[index],
                originalTexts[index],
                restoredText =>
                    ((AtkTextNode*)textNodeAddress)->SetText(restoredText));
        }

        return restoredAny;
    }

    private unsafe bool ShouldHandleAddon(AddonArgs args, out AtkUnitBase* addon)
    {
        addon = null;
        if (!this.isTranslationEnabled() ||
            args.AddonName != this.addonName ||
            args.Addon.Address == IntPtr.Zero)
        {
            return false;
        }

        addon = (AtkUnitBase*)args.Addon.Address;
        return addon != null && addon->IsVisible;
    }

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

    private unsafe bool TryGetStringArrayData(
        int arrayIndex,
        out StringArrayData* stringArrayData)
    {
        stringArrayData = null;
        var raptureAtkModule = RaptureAtkModule.Instance();
        if (raptureAtkModule == null)
        {
            return false;
        }

        var arrayHolder = raptureAtkModule->AtkArrayDataHolder;
        if (arrayIndex < 0 || arrayIndex >= arrayHolder.StringArrayCount)
        {
            return false;
        }

        stringArrayData = arrayHolder.StringArrays[arrayIndex];
        return stringArrayData != null && stringArrayData->StringArray != null;
    }

    private unsafe string ReadAtkValueText(in AtkValue value)
    {
        var stringPointer = (nint)value.String.Value;
        if (stringPointer == 0)
        {
            return string.Empty;
        }

        try
        {
            return MemoryHelper.ReadSeStringAsString(
                       out _,
                       stringPointer) ??
                   string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private unsafe string ReadStringArrayValue(
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

    private unsafe bool IsEffectivelyVisible(AtkResNode* node)
    {
        for (var currentNode = node;
             currentNode != null;
             currentNode = currentNode->ParentNode)
        {
            if (!currentNode->IsVisible())
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldUseHoverTooltips()
    {
        return this.isTranslationEnabled() &&
               TranslationDisplayModeHelper.UsesHoverTooltips(
                   this.DisplayMode,
                   this.config.OverlayOnlyLanguage);
    }

    private bool ShouldApplyNativeText()
    {
        return this.isTranslationEnabled() &&
               TranslationDisplayModeHelper.WritesNativeTranslation(
                   this.DisplayMode,
                   this.config.OverlayOnlyLanguage);
    }

    private bool ShouldShowOriginalTooltips()
    {
        return TranslationDisplayModeHelper.ShowsOriginalTooltips(
            this.DisplayMode,
            this.config.OverlayOnlyLanguage);
    }

    private int GetDialogueTranslationEngineId()
    {
        return this.translationService.GetEffectiveTranslationEngineId(
            TranslationSurfaceGroup.Dialogue);
    }

    private string NormalizeForReplacement(string text)
    {
        return this.config.RemoveDiacriticsWhenUsingReplacementTalkBTalk
            ? this.normalizeReplacementText(text)
            : text;
    }

    private string NormalizeForComparison(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var replacementText = this.NormalizeForReplacement(text);
        return string.Join(
            " ",
            replacementText.Split(
                ['\r', '\n', '\t', ' '],
                StringSplitOptions.RemoveEmptyEntries));
    }

    private bool TextMatches(string? left, string? right)
    {
        return string.Equals(
            this.NormalizeForComparison(left),
            this.NormalizeForComparison(right),
            StringComparison.Ordinal);
    }

    private bool HasUsableTranslatedPayload(
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> translatedTexts,
        string sourceLanguage,
        string targetLanguage)
    {
        var valueCount = Math.Min(originalTexts.Count, translatedTexts.Count);
        for (var index = 0; index < valueCount; index++)
        {
            if (TranslationPersistenceGuard.IsUsableDialogueTranslation(
                    originalTexts[index],
                    translatedTexts[index],
                    sourceLanguage,
                    targetLanguage))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSourceLanguage(
        string? storedSourceLanguageCode,
        SourceClientLanguage? sourceLanguage)
    {
        return sourceLanguage.HasValue &&
               !string.IsNullOrWhiteSpace(storedSourceLanguageCode) &&
               RuntimeLanguageHelper.LanguagesMatch(
                   storedSourceLanguageCode,
                   sourceLanguage.Value.PersistenceCode);
    }

    private static bool TryRestoreOwnedText(
        string? liveText,
        string? replacementText,
        string originalText,
        Action<string> restore)
    {
        ArgumentNullException.ThrowIfNull(restore);

        if (string.IsNullOrWhiteSpace(replacementText) ||
            !string.Equals(
                liveText,
                replacementText,
                StringComparison.Ordinal))
        {
            return false;
        }

        restore(originalText);
        return true;
    }

    private static (string Title, string Body) ToOverlayParts(
        IReadOnlyList<string> texts,
        bool treatFirstTextAsTitle = true)
    {
        if (texts.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (texts.Count == 1 || !treatFirstTextAsTitle)
        {
            return (string.Empty, string.Join('\n', texts));
        }

        return (texts[0], string.Join('\n', texts.Skip(1)));
    }

    private static bool IsStringValue(in AtkValue value)
    {
        return value.Type is
            ValueType.String or
            ValueType.String8 or
            ValueType.ManagedString;
    }

    private static bool ShouldCaptureText(string? text)
    {
        return !string.IsNullOrWhiteSpace(text);
    }
}
