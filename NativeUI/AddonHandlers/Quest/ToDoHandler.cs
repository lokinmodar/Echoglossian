// <copyright file="ToDoHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Helpers;
using Newtonsoft.Json;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Translates the dedicated ToDo addon without sharing _ToDoList runtime
///     state, persistence, or native mutations.
/// </summary>
internal sealed class ToDoHandler :
    IAddonTranslationHandler,
    IPluginUnloadAwareAddonHandler
{
    private const string ToDoAddonName = "ToDo";
    private const string HoverTooltipPrefix = "ToDo-";

    private readonly Config configuration;
    private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>>
        eventHandlers = [];
    private readonly Func<ToDoText, ToDoText?> findToDoText;
    private readonly HoverTooltipManager hoverTooltipManager;
    private readonly Func<ToDoText, Task<string>> insertToDoTextAsync;
    private readonly Dictionary<string, ToDoNativeMutation> nativeMutations = [];
    private readonly ToDoRuntimeRequestState runtimeRequestState = new();
    private readonly TranslationService translationService;

    private ToDoPresentationSnapshot? currentPresentation;
    private ToDoStablePreDrawSnapshot? stablePreDrawSnapshot;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToDoHandler" /> class.
    /// </summary>
    /// <param name="configuration">The plugin configuration.</param>
    /// <param name="translationService">The shared translation service.</param>
    /// <param name="findToDoText">Resolves persisted dedicated ToDo payloads.</param>
    /// <param name="insertToDoTextAsync">Persists translated ToDo payloads.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    public ToDoHandler(
        Config configuration,
        TranslationService translationService,
        Func<ToDoText, ToDoText?> findToDoText,
        Func<ToDoText, Task<string>> insertToDoTextAsync,
        HoverTooltipManager hoverTooltipManager)
    {
        this.configuration = configuration;
        this.translationService = translationService;
        this.findToDoText = findToDoText;
        this.insertToDoTextAsync = insertToDoTextAsync;
        this.hoverTooltipManager = hoverTooltipManager;

        this.RegisterHandler(AddonEvent.PreSetup, this.OnToDoEvent);
        this.RegisterHandler(AddonEvent.PreRefresh, this.OnToDoEvent);
        this.RegisterHandler(AddonEvent.PreRequestedUpdate, this.OnToDoEvent);
        this.RegisterHandler(AddonEvent.PreDraw, this.OnToDoEvent);
        this.RegisterHandler(AddonEvent.PreHide, this.OnToDoCleanupEvent);
        this.RegisterHandler(AddonEvent.PreFinalize, this.OnToDoCleanupEvent);
    }

    /// <summary>
    ///     Returns the event handlers required for the dedicated ToDo runtime.
    /// </summary>
    /// <returns>The lifecycle event handlers.</returns>
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
        if (TryGetToDo(out var addon))
        {
            this.RestoreOwnedNativeMutations(addon);
        }

        this.ClearRuntimeState();
    }

    private ToDoPresentationPolicy PresentationPolicy =>
        ToDoPresentationPolicy.Create(
            this.configuration.ToDoTranslationDisplayMode,
            this.configuration.OverlayOnlyLanguage);

    /// <summary>
    ///     Registers one local lifecycle handler.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="handler">The local event handler.</param>
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

    /// <summary>
    ///     Processes one ToDo lifecycle update.
    /// </summary>
    /// <param name="evt">The triggering lifecycle event.</param>
    /// <param name="args">The associated lifecycle arguments.</param>
    private unsafe void OnToDoEvent(AddonEvent evt, AddonArgs args)
    {
        if (!string.Equals(args.AddonName, ToDoAddonName, StringComparison.Ordinal) ||
            !TryGetVisibleToDo(out var addon))
        {
            return;
        }

        this.ProcessVisibleToDo(addon, evt);
    }

    /// <summary>
    ///     Clears ToDo state when the native addon closes.
    /// </summary>
    /// <param name="evt">The triggering lifecycle event.</param>
    /// <param name="args">The associated lifecycle arguments.</param>
    private unsafe void OnToDoCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        if (!string.Equals(args.AddonName, ToDoAddonName, StringComparison.Ordinal))
        {
            return;
        }

        if (TryGetToDo(out var addon))
        {
            this.RestoreOwnedNativeMutations(addon);
        }

        this.ClearRuntimeState();
    }

    /// <summary>
    ///     Resolves, applies, or queues the current dedicated ToDo payload.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <param name="evt">The lifecycle event that requested processing.</param>
    private unsafe void ProcessVisibleToDo(AtkUnitBase* addon, AddonEvent evt)
    {
        if (!this.configuration.TranslateToDo)
        {
            this.RestoreOwnedNativeMutations(addon);
            this.ClearRuntimeState();
            return;
        }

        if (evt == AddonEvent.PreDraw &&
            this.TryShortCircuitStablePreDraw(addon))
        {
            return;
        }

        if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            return;
        }

        var resolvedNodes = ToDoTextNodeResolvers.ResolveVisibleTextNodes(addon);
        var currentPayload = this.ResolveCurrentPayload(resolvedNodes);
        if (currentPayload.GetTranslatableTexts().Count == 0)
        {
            this.RestoreOwnedNativeMutations(addon, resolvedNodes);
            this.ClearRuntimeState();
            return;
        }

        var scope = this.CaptureTranslationScope(sourceLanguage);
        var operation = new ToDoTranslationOperation(
            currentPayload.ComputeSourceContentHash(),
            scope);
        var generation = this.runtimeRequestState.ObserveVisibleOperation(
            operation);
        if (this.TryReuseCurrentToDoPresentation(
                addon,
                operation,
                resolvedNodes))
        {
            return;
        }

        this.RestoreOwnedNativeMutations(addon, resolvedNodes);
        this.hoverTooltipManager.RemoveByPrefix(HoverTooltipPrefix);

        if (this.runtimeRequestState.ShouldSkipPersistenceLookup(operation))
        {
            this.CaptureStablePreDrawSnapshot(
                addon,
                operation,
                currentPayload,
                null,
                resolvedNodes);
            return;
        }

        if (this.TryFindPersistedPresentation(
                currentPayload,
                operation,
                out var presentation))
        {
            this.currentPresentation = presentation;
            this.ApplyCurrentToDoPresentation(
                addon,
                presentation,
                resolvedNodes);
            return;
        }

        if (!this.runtimeRequestState.TryStart(operation, generation))
        {
            this.CaptureStablePreDrawSnapshot(
                addon,
                operation,
                currentPayload,
                null,
                resolvedNodes);
            return;
        }

        this.CaptureStablePreDrawSnapshot(
            addon,
            operation,
            currentPayload,
            null,
            resolvedNodes);
        _ = this.ResolveAndPersistPresentationAsync(
            currentPayload,
            sourceLanguage,
            operation,
            generation);
    }

    /// <summary>
    ///     Validates cached node addresses and presentation state so an
    ///     unchanged PreDraw can avoid repeated traversal and apply work.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <returns>
    ///     <see langword="true" /> when the cached state remains reusable;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private unsafe bool TryShortCircuitStablePreDraw(AtkUnitBase* addon)
    {
        var snapshot = this.stablePreDrawSnapshot;
        if (snapshot == null ||
            snapshot.AddonAddress != (nint)addon ||
            snapshot.Policy != this.PresentationPolicy ||
            snapshot.Nodes.Count == 0)
        {
            return false;
        }

        var nodesStable = true;
        foreach (var node in snapshot.Nodes)
        {
            var textNode = (AtkTextNode*)node.Address;
            if (textNode == null || !textNode->AtkResNode.IsVisible())
            {
                nodesStable = false;
                break;
            }

            var liveText = ToDoTextNodeResolvers.ReadTextNode(textNode);
            if (node.IsTimerNode)
            {
                if (!ToDoTextNodeResolvers.IsTimerText(liveText))
                {
                    nodesStable = false;
                    break;
                }

                continue;
            }

            if (!string.Equals(
                    liveText,
                    node.ExpectedText,
                    StringComparison.Ordinal))
            {
                nodesStable = false;
                break;
            }

            if (node.TracksTooltipBounds)
            {
                var left = textNode->ScreenX - 12f;
                var top = textNode->ScreenY - 8f;
                var right = textNode->ScreenX +
                            Math.Max(1f, textNode->GetWidth()) + 12f;
                var bottom = textNode->ScreenY +
                             Math.Max(1f, textNode->GetHeight()) + 8f;
                if (left != node.Left ||
                    top != node.Top ||
                    right != node.Right ||
                    bottom != node.Bottom)
                {
                    nodesStable = false;
                    break;
                }
            }
        }

        var presentationStable =
            snapshot.AppliedPresentation != null &&
            ReferenceEquals(
                snapshot.AppliedPresentation,
                this.currentPresentation);
        return this.runtimeRequestState.ShouldShortCircuitStablePreDraw(
            snapshot.Operation,
            presentationStable,
            nodesStable);
    }

    /// <summary>
    ///     Captures resolved node addresses and their expected presentation
    ///     state for subsequent stable PreDraw validation.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <param name="operation">The current immutable operation.</param>
    /// <param name="payload">The source-facing payload.</param>
    /// <param name="presentation">The applied presentation, if one is ready.</param>
    /// <param name="resolvedNodes">The already-resolved visible node snapshot.</param>
    private unsafe void CaptureStablePreDrawSnapshot(
        AtkUnitBase* addon,
        ToDoTranslationOperation operation,
        ToDoPayload payload,
        ToDoPresentationSnapshot? presentation,
        IReadOnlyList<ToDoResolvedTextNode> resolvedNodes)
    {
        var sourceTextsByNodeKey = payload.VisibleTexts
            .ToDictionary(text => text.NodeKey, text => text.Text);
        Dictionary<string, string> translatedTextsByNodeKey = [];
        if (presentation != null)
        {
            var translatableTexts = payload.GetTranslatableTexts();
            translatedTextsByNodeKey = presentation.TranslatedTexts
                .Select((text, index) => new
                {
                    NodeKey = translatableTexts[index].NodeKey,
                    Text = text,
                })
                .ToDictionary(text => text.NodeKey, text => text.Text);
        }

        var policy = this.PresentationPolicy;
        List<ToDoStableNodeSnapshot> stableNodes = [];
        foreach (var node in resolvedNodes)
        {
            var textNode = (AtkTextNode*)node.Address;
            var hasSourceText = sourceTextsByNodeKey.TryGetValue(
                node.NodeKey,
                out var sourceText);
            var hasTranslatedText = translatedTextsByNodeKey.TryGetValue(
                node.NodeKey,
                out var translatedText);
            var expectedText = hasSourceText ? sourceText! : node.Text;
            if (policy.WritesNativeTranslation && hasTranslatedText)
            {
                expectedText = translatedText!;
            }

            var tracksTooltipBounds =
                policy.UsesHoverTooltips &&
                !node.IsTimerNode &&
                hasSourceText &&
                hasTranslatedText &&
                textNode != null;
            var left = tracksTooltipBounds ? textNode->ScreenX - 12f : 0f;
            var top = tracksTooltipBounds ? textNode->ScreenY - 8f : 0f;
            var right = tracksTooltipBounds
                ? textNode->ScreenX + Math.Max(1f, textNode->GetWidth()) + 12f
                : 0f;
            var bottom = tracksTooltipBounds
                ? textNode->ScreenY + Math.Max(1f, textNode->GetHeight()) + 8f
                : 0f;
            stableNodes.Add(new ToDoStableNodeSnapshot(
                node.Address,
                node.IsTimerNode,
                expectedText,
                tracksTooltipBounds,
                left,
                top,
                right,
                bottom));
        }

        this.stablePreDrawSnapshot = new ToDoStablePreDrawSnapshot(
            (nint)addon,
            operation,
            policy,
            presentation,
            stableNodes);
    }

    /// <summary>
    ///     Reuses the current translated snapshot when only timer text changed.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <param name="operation">The immutable operation for the visible payload.</param>
    /// <returns><c>true</c> when the existing presentation was reused.</returns>
    private unsafe bool TryReuseCurrentToDoPresentation(
        AtkUnitBase* addon,
        ToDoTranslationOperation operation,
        IReadOnlyList<ToDoResolvedTextNode> resolvedNodes)
    {
        var presentation = this.currentPresentation;
        if (presentation == null ||
            !Equals(presentation.Operation, operation))
        {
            return false;
        }

        this.ApplyCurrentToDoPresentation(addon, presentation, resolvedNodes);
        return true;
    }

    /// <summary>
    ///     Resolves the source payload while recognizing native text this
    ///     handler wrote during an earlier display pass.
    /// </summary>
    /// <param name="resolvedNodes">The already-resolved visible text nodes.</param>
    /// <returns>The source-facing ToDo payload.</returns>
    private ToDoPayload ResolveCurrentPayload(
        IReadOnlyList<ToDoResolvedTextNode> resolvedNodes)
    {
        var visibleTexts = resolvedNodes
            .Select(node => new ToDoCapturedText(
                node.NodeKey,
                node.NodeId,
                node.Text,
                node.IsTimerNode))
            .ToArray();
        var presentation = this.currentPresentation;
        if (presentation == null)
        {
            return new ToDoPayload(visibleTexts);
        }

        var sourceTextsByNodeKey = presentation.OriginalPayload
            .GetTranslatableTexts()
            .ToDictionary(text => text.NodeKey, text => text.Text);
        var translatedTextsByNodeKey = presentation.TranslatedTexts
            .Select((text, index) => new
            {
                NodeKey = presentation.OriginalPayload.GetTranslatableTexts()[index].NodeKey,
                Text = text,
            })
            .ToDictionary(text => text.NodeKey, text => text.Text);
        var sourceFacingTexts = visibleTexts.Select(text =>
        {
            if (text.IsTimerNode ||
                !sourceTextsByNodeKey.TryGetValue(text.NodeKey, out var sourceText) ||
                !translatedTextsByNodeKey.TryGetValue(text.NodeKey, out var translatedText) ||
                (!string.Equals(text.Text, sourceText, StringComparison.Ordinal) &&
                 !string.Equals(text.Text, translatedText, StringComparison.Ordinal)))
            {
                return text;
            }

            return text with { Text = sourceText };
        }).ToArray();
        return new ToDoPayload(sourceFacingTexts);
    }

    /// <summary>
    ///     Tries to resolve a complete dedicated ToDo translation from SQLite.
    /// </summary>
    /// <param name="payload">The source-facing ToDo payload.</param>
    /// <param name="operation">The immutable operation for the payload.</param>
    /// <param name="presentation">Receives the resolved presentation.</param>
    /// <returns><c>true</c> when a complete persisted translation exists.</returns>
    private bool TryFindPersistedPresentation(
        ToDoPayload payload,
        ToDoTranslationOperation operation,
        out ToDoPresentationSnapshot presentation)
    {
        presentation = null!;
        var lookup = this.CreateToDoText(payload, operation.Scope, null);
        var stored = this.findToDoText(lookup);
        if (stored == null ||
            !TryDeserializeTranslatedTexts(payload, stored.TranslatedTextsAsText,
                out var translatedTexts))
        {
            return false;
        }

        presentation = new ToDoPresentationSnapshot(
            operation,
            payload,
            translatedTexts);
        return true;
    }

    /// <summary>
    ///     Translates and persists one uncached ToDo payload without blocking a
    ///     lifecycle callback.
    /// </summary>
    /// <param name="payload">The source-facing ToDo payload.</param>
    /// <param name="sourceLanguage">The captured source language.</param>
    /// <param name="operation">The immutable operation being translated.</param>
    /// <param name="generation">The visible generation that scheduled the work.</param>
    /// <returns>A task representing the background translation work.</returns>
    private async Task ResolveAndPersistPresentationAsync(
        ToDoPayload payload,
        SourceClientLanguage sourceLanguage,
        ToDoTranslationOperation operation,
        long generation)
    {
        try
        {
            List<string> translatedTexts = [];
            foreach (var text in payload.GetTranslatableTexts())
            {
                var translatedText = await this.translationService.TranslateAsync(
                    text.Text,
                    sourceLanguage,
                    operation.Scope.TargetLanguageCode,
                    originContext: "ToDoHandler/Text").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    this.runtimeRequestState.MarkFailed(operation, generation);
                    return;
                }

                translatedTexts.Add(translatedText);
            }

            var row = this.CreateToDoText(
                payload,
                operation.Scope,
                translatedTexts);
            _ = await this.insertToDoTextAsync(row).ConfigureAwait(false);
            if (!this.runtimeRequestState.TryComplete(operation, generation))
            {
                return;
            }

            this.currentPresentation = new ToDoPresentationSnapshot(
                operation,
                payload,
                translatedTexts);
        }
        catch
        {
            this.runtimeRequestState.MarkFailed(operation, generation);
        }
    }

    /// <summary>
    ///     Applies native replacement and hover tooltips for one resolved ToDo
    ///     snapshot while leaving timer nodes untouched.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <param name="presentation">The resolved source and translated payloads.</param>
    private unsafe void ApplyCurrentToDoPresentation(
        AtkUnitBase* addon,
        ToDoPresentationSnapshot presentation,
        IReadOnlyList<ToDoResolvedTextNode> resolvedNodes)
    {
        this.hoverTooltipManager.RemoveByPrefix(HoverTooltipPrefix);
        var translatableTexts =
            presentation.OriginalPayload.GetTranslatableTexts();
        var translatedTextsByNodeKey = presentation.TranslatedTexts
            .Select((text, index) => new
            {
                NodeKey = translatableTexts[index].NodeKey,
                Text = text,
            })
            .ToDictionary(text => text.NodeKey, text => text.Text);
        var sourceTextsByNodeKey = translatableTexts
            .ToDictionary(text => text.NodeKey, text => text.Text);
        var policy = this.PresentationPolicy;

        foreach (var node in resolvedNodes)
        {
            var textNode = (AtkTextNode*)node.Address;
            if (textNode == null)
            {
                continue;
            }

            if (node.IsTimerNode ||
                !sourceTextsByNodeKey.TryGetValue(node.NodeKey, out var sourceText) ||
                !translatedTextsByNodeKey.TryGetValue(node.NodeKey, out var translatedText))
            {
                continue;
            }

            if (policy.WritesNativeTranslation)
            {
                var nativeText = translatedText;
                if (!string.IsNullOrWhiteSpace(nativeText) &&
                    !string.Equals(
                        ToDoTextNodeResolvers.ReadTextNode(textNode),
                        nativeText,
                        StringComparison.Ordinal))
                {
                    textNode->SetText(nativeText);
                }

                this.nativeMutations[node.NodeKey] = new ToDoNativeMutation(
                    sourceText,
                    nativeText);
            }

            if (policy.UsesHoverTooltips)
            {
                this.RegisterHoverTooltip(
                    node.NodeKey,
                    textNode,
                    sourceText,
                    translatedText,
                    policy.HoverShowsOriginal);
            }
        }

        if (!policy.WritesNativeTranslation)
        {
            this.RestoreOwnedNativeMutations(addon, resolvedNodes);
        }

        this.CaptureStablePreDrawSnapshot(
            addon,
            presentation.Operation,
            presentation.OriginalPayload,
            presentation,
            resolvedNodes);
    }

    /// <summary>
    ///     Restores only native text previously written by this handler.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <param name="resolvedNodes">
    ///     Optional already-resolved node snapshot for the current lifecycle
    ///     pass.
    /// </param>
    private unsafe void RestoreOwnedNativeMutations(
        AtkUnitBase* addon,
        IReadOnlyList<ToDoResolvedTextNode>? resolvedNodes = null)
    {
        resolvedNodes ??= ToDoTextNodeResolvers.ResolveVisibleTextNodes(addon);
        foreach (var node in resolvedNodes)
        {
            if (!this.nativeMutations.TryGetValue(
                    node.NodeKey,
                    out var mutation))
            {
                continue;
            }

            var textNode = (AtkTextNode*)node.Address;
            if (textNode != null &&
                string.Equals(
                    ToDoTextNodeResolvers.ReadTextNode(textNode),
                    mutation.AppliedText,
                    StringComparison.Ordinal))
            {
                textNode->SetText(mutation.OriginalText);
            }
        }

        this.nativeMutations.Clear();
    }

    /// <summary>
    ///     Registers a hover tooltip for one dedicated ToDo text node.
    /// </summary>
    /// <param name="nodeKey">The stable structural node key.</param>
    /// <param name="textNode">The visible native text node.</param>
    /// <param name="originalText">The original ToDo text.</param>
    /// <param name="translatedText">The translated ToDo text.</param>
    /// <param name="hoverShowsOriginal">Whether hover text should show the source.</param>
    private unsafe void RegisterHoverTooltip(
        string nodeKey,
        AtkTextNode* textNode,
        string originalText,
        string translatedText,
        bool hoverShowsOriginal)
    {
        var body = hoverShowsOriginal ? originalText : translatedText;
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var left = textNode->ScreenX - 12f;
        var top = textNode->ScreenY - 8f;
        var right = textNode->ScreenX + Math.Max(1f, textNode->GetWidth()) + 12f;
        var bottom = textNode->ScreenY + Math.Max(1f, textNode->GetHeight()) + 8f;
        this.hoverTooltipManager.Register(
            $"{HoverTooltipPrefix}{nodeKey}",
            new Vector2(left, top),
            new Vector2(right, bottom),
            string.Empty,
            body,
            useGeneralFont: hoverShowsOriginal,
            displaysOriginalSwapText: hoverShowsOriginal);
    }

    /// <summary>
    ///     Creates the dedicated persistence row for one source payload.
    /// </summary>
    /// <param name="payload">The source-facing ToDo payload.</param>
    /// <param name="scope">The immutable translation and persistence scope.</param>
    /// <param name="translatedTexts">The optional complete translated payload.</param>
    /// <returns>The dedicated persistence row.</returns>
    private ToDoText CreateToDoText(
        ToDoPayload payload,
        ToDoTranslationScope scope,
        IReadOnlyList<string>? translatedTexts)
    {
        var originalTextsAsText = JsonConvert.SerializeObject(
            payload.GetTranslatableTexts().Select(text => text.Text));
        return new ToDoText
        {
            AddonName = ToDoAddonName,
            OriginalTextsAsText = originalTextsAsText,
            OriginalLang = scope.SourceLanguageCode,
            TranslatedTextsAsText = translatedTexts == null
                ? string.Empty
                : JsonConvert.SerializeObject(translatedTexts),
            TranslationLang = scope.TargetLanguageCode,
            TranslationEngine = scope.TranslationEngine,
            GameVersion = scope.GameVersion,
            SourceContentHash = payload.ComputeSourceContentHash(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };
    }

    /// <summary>
    ///     Captures the mutable configuration values that determine translation
    ///     and persistence behavior before asynchronous work begins.
    /// </summary>
    /// <param name="sourceLanguage">The source language captured from the client.</param>
    /// <returns>The immutable scope for one ToDo translation operation.</returns>
    private ToDoTranslationScope CaptureTranslationScope(
        SourceClientLanguage sourceLanguage)
    {
        return new ToDoTranslationScope(
            sourceLanguage.PersistenceCode,
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.configuration.Lang),
            this.configuration.ChosenTransEngine,
            GetGameVersion() ?? string.Empty);
    }

    /// <summary>
    ///     Parses a persisted ordered translation payload for the current ToDo
    ///     source rows.
    /// </summary>
    /// <param name="payload">The current source payload.</param>
    /// <param name="serializedTexts">The serialized translated text array.</param>
    /// <param name="translatedTexts">Receives the complete translated payload.</param>
    /// <returns><c>true</c> when the stored payload is complete and usable.</returns>
    private static bool TryDeserializeTranslatedTexts(
        ToDoPayload payload,
        string? serializedTexts,
        out List<string> translatedTexts)
    {
        translatedTexts = [];
        try
        {
            var parsedTexts = JsonConvert.DeserializeObject<List<string>>(
                serializedTexts ?? string.Empty);
            if (parsedTexts == null ||
                parsedTexts.Count != payload.GetTranslatableTexts().Count ||
                parsedTexts.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            translatedTexts = parsedTexts;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Tries to resolve the dedicated ToDo addon, including an addon that
    ///     has become hidden but still owns native mutations.
    /// </summary>
    /// <param name="addon">Receives the live ToDo addon.</param>
    /// <returns><c>true</c> when ToDo exists.</returns>
    private static unsafe bool TryGetToDo(out AtkUnitBase* addon)
    {
        addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
            ToDoAddonName);
        return addon != null;
    }

    /// <summary>
    ///     Tries to resolve the visible dedicated ToDo addon.
    /// </summary>
    /// <param name="addon">Receives the visible ToDo addon.</param>
    /// <returns><c>true</c> when ToDo is visible.</returns>
    private static unsafe bool TryGetVisibleToDo(out AtkUnitBase* addon)
    {
        return TryGetToDo(out addon) && addon->IsVisible;
    }

    /// <summary>
    ///     Clears handler-owned runtime state without touching _ToDoList.
    /// </summary>
    private void ClearRuntimeState()
    {
        this.currentPresentation = null;
        this.stablePreDrawSnapshot = null;
        this.runtimeRequestState.Clear();
        this.nativeMutations.Clear();
        this.hoverTooltipManager.RemoveByPrefix(HoverTooltipPrefix);
    }

    /// <summary>
    ///     Represents a fully resolved dedicated ToDo presentation snapshot.
    /// </summary>
    /// <param name="Operation">The immutable operation that resolved this snapshot.</param>
    /// <param name="OriginalPayload">The source-facing ToDo payload.</param>
    /// <param name="TranslatedTexts">The ordered translated text rows.</param>
    private sealed record ToDoPresentationSnapshot(
        ToDoTranslationOperation Operation,
        ToDoPayload OriginalPayload,
        IReadOnlyList<string> TranslatedTexts);

    /// <summary>
    ///     Captures one native mutation owned by the dedicated ToDo handler.
    /// </summary>
    /// <param name="OriginalText">The game-owned original text.</param>
    /// <param name="AppliedText">The handler-applied native text.</param>
    private sealed record ToDoNativeMutation(
        string OriginalText,
        string AppliedText);

    /// <summary>
    ///     Retains the applied or pending node state needed to validate a stable
    ///     PreDraw without repeating addon-tree traversal.
    /// </summary>
    /// <param name="AddonAddress">The live addon address.</param>
    /// <param name="Operation">The visible operation represented by the nodes.</param>
    /// <param name="Policy">The presentation policy used for the snapshot.</param>
    /// <param name="AppliedPresentation">
    ///     The presentation applied when the snapshot was captured, if any.
    /// </param>
    /// <param name="Nodes">The resolved nodes and their expected native state.</param>
    private sealed record ToDoStablePreDrawSnapshot(
        nint AddonAddress,
        ToDoTranslationOperation Operation,
        ToDoPresentationPolicy Policy,
        ToDoPresentationSnapshot? AppliedPresentation,
        IReadOnlyList<ToDoStableNodeSnapshot> Nodes);

    /// <summary>
    ///     Retains one node address, expected text, and optional tooltip bounds.
    /// </summary>
    /// <param name="Address">The native text-node address.</param>
    /// <param name="IsTimerNode">Whether volatile timer changes are ignored.</param>
    /// <param name="ExpectedText">The expected stable native text.</param>
    /// <param name="TracksTooltipBounds">
    ///     Whether tooltip bounds must remain unchanged.
    /// </param>
    /// <param name="Left">The tooltip hit area left edge.</param>
    /// <param name="Top">The tooltip hit area top edge.</param>
    /// <param name="Right">The tooltip hit area right edge.</param>
    /// <param name="Bottom">The tooltip hit area bottom edge.</param>
    private sealed record ToDoStableNodeSnapshot(
        nint Address,
        bool IsTimerNode,
        string ExpectedText,
        bool TracksTooltipBounds,
        float Left,
        float Top,
        float Right,
        float Bottom);
}
