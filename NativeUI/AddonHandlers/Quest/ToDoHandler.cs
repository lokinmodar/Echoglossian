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
    private readonly Dictionary<nint, ToDoNativeMutation> nativeMutations = [];
    private readonly TranslationService translationService;

    private ToDoPresentationSnapshot? currentPresentation;
    private string? lastFailedContentHash;
    private string? lastVisibleContentHash;
    private string? translationInFlightContentHash;

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
        if (TryGetVisibleToDo(out var addon))
        {
            this.RestoreOwnedNativeMutations(addon);
        }

        this.ClearRuntimeState();
    }

    private bool UsesHoverTooltips =>
        QuestAddonModeHelpers.UsesHoverTooltips(
            this.configuration.ToDoTranslationDisplayMode);

    private bool WritesNativeTranslation =>
        QuestAddonModeHelpers.WritesNativeTranslation(
            this.configuration.ToDoTranslationDisplayMode);

    private bool HoverShowsOriginal =>
        QuestAddonModeHelpers.ShowsOriginalTooltips(
            this.configuration.ToDoTranslationDisplayMode);

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

        this.ProcessVisibleToDo(addon);
    }

    /// <summary>
    ///     Clears ToDo state when the native addon closes.
    /// </summary>
    /// <param name="evt">The triggering lifecycle event.</param>
    /// <param name="args">The associated lifecycle arguments.</param>
    private void OnToDoCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        if (!string.Equals(args.AddonName, ToDoAddonName, StringComparison.Ordinal))
        {
            return;
        }

        this.ClearRuntimeState();
    }

    /// <summary>
    ///     Resolves, applies, or queues the current dedicated ToDo payload.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    private unsafe void ProcessVisibleToDo(AtkUnitBase* addon)
    {
        if (!this.configuration.TranslateToDo)
        {
            this.RestoreOwnedNativeMutations(addon);
            this.ClearRuntimeState();
            return;
        }

        if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            return;
        }

        var currentPayload = this.ResolveCurrentPayload(addon);
        if (currentPayload.GetTranslatableTexts().Count == 0)
        {
            this.RestoreOwnedNativeMutations(addon);
            this.hoverTooltipManager.RemoveByPrefix(HoverTooltipPrefix);
            return;
        }

        var currentContentHash = currentPayload.ComputeSourceContentHash();
        if (string.Equals(
                currentContentHash,
                this.lastVisibleContentHash,
                StringComparison.Ordinal) &&
            this.TryReuseCurrentToDoPresentation(addon, currentPayload))
        {
            return;
        }

        this.lastVisibleContentHash = currentContentHash;
        this.RestoreOwnedNativeMutations(addon);
        this.hoverTooltipManager.RemoveByPrefix(HoverTooltipPrefix);

        if (this.TryFindPersistedPresentation(
                currentPayload,
                sourceLanguage,
                out var presentation))
        {
            this.currentPresentation = presentation;
            this.lastFailedContentHash = null;
            this.ApplyCurrentToDoPresentation(addon, presentation);
            return;
        }

        if (string.Equals(
                currentContentHash,
                this.lastFailedContentHash,
                StringComparison.Ordinal) ||
            string.Equals(
                currentContentHash,
                this.translationInFlightContentHash,
                StringComparison.Ordinal))
        {
            return;
        }

        this.translationInFlightContentHash = currentContentHash;
        _ = this.ResolveAndPersistPresentationAsync(
            currentPayload,
            sourceLanguage,
            currentContentHash);
    }

    /// <summary>
    ///     Reuses the current translated snapshot when only timer text changed.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <param name="currentPayload">The current visible payload.</param>
    /// <returns><c>true</c> when the existing presentation was reused.</returns>
    private unsafe bool TryReuseCurrentToDoPresentation(
        AtkUnitBase* addon,
        ToDoPayload currentPayload)
    {
        var presentation = this.currentPresentation;
        if (presentation == null ||
            !string.Equals(
                presentation.SourceContentHash,
                currentPayload.ComputeSourceContentHash(),
                StringComparison.Ordinal) ||
            presentation.DisplayMode != this.configuration.ToDoTranslationDisplayMode)
        {
            return false;
        }

        this.ApplyCurrentToDoPresentation(addon, presentation);
        return true;
    }

    /// <summary>
    ///     Resolves the source payload while recognizing native text this
    ///     handler wrote during an earlier display pass.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    /// <returns>The source-facing ToDo payload.</returns>
    private unsafe ToDoPayload ResolveCurrentPayload(AtkUnitBase* addon)
    {
        var visibleTexts = ToDoTextNodeResolvers.ResolveVisibleTexts(addon);
        var presentation = this.currentPresentation;
        if (presentation == null)
        {
            return new ToDoPayload(visibleTexts);
        }

        var sourceTextsByNodeId = presentation.OriginalPayload
            .GetTranslatableTexts()
            .ToDictionary(text => text.NodeId, text => text.Text);
        var translatedTextsByNodeId = presentation.TranslatedTexts
            .Select((text, index) => new
            {
                NodeId = presentation.OriginalPayload.GetTranslatableTexts()[index].NodeId,
                Text = text,
            })
            .ToDictionary(text => text.NodeId, text => text.Text);
        var sourceFacingTexts = visibleTexts.Select(text =>
        {
            if (text.IsTimerNode ||
                !sourceTextsByNodeId.TryGetValue(text.NodeId, out var sourceText) ||
                !translatedTextsByNodeId.TryGetValue(text.NodeId, out var translatedText) ||
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
    /// <param name="sourceLanguage">The source language captured for the payload.</param>
    /// <param name="presentation">Receives the resolved presentation.</param>
    /// <returns><c>true</c> when a complete persisted translation exists.</returns>
    private bool TryFindPersistedPresentation(
        ToDoPayload payload,
        SourceClientLanguage sourceLanguage,
        out ToDoPresentationSnapshot presentation)
    {
        presentation = null!;
        var lookup = this.CreateToDoText(payload, sourceLanguage, null);
        var stored = this.findToDoText(lookup);
        if (stored == null ||
            !TryDeserializeTranslatedTexts(payload, stored.TranslatedTextsAsText,
                out var translatedTexts))
        {
            return false;
        }

        presentation = new ToDoPresentationSnapshot(
            payload.ComputeSourceContentHash(),
            payload,
            translatedTexts,
            this.configuration.ToDoTranslationDisplayMode);
        return true;
    }

    /// <summary>
    ///     Translates and persists one uncached ToDo payload without blocking a
    ///     lifecycle callback.
    /// </summary>
    /// <param name="payload">The source-facing ToDo payload.</param>
    /// <param name="sourceLanguage">The captured source language.</param>
    /// <param name="sourceContentHash">The stable non-timer source hash.</param>
    /// <returns>A task representing the background translation work.</returns>
    private async Task ResolveAndPersistPresentationAsync(
        ToDoPayload payload,
        SourceClientLanguage sourceLanguage,
        string sourceContentHash)
    {
        try
        {
            List<string> translatedTexts = [];
            foreach (var text in payload.GetTranslatableTexts())
            {
                var translatedText = await this.translationService.TranslateAsync(
                    text.Text,
                    sourceLanguage,
                    LangDict[LanguageInt].Code,
                    originContext: "ToDoHandler/Text").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    this.lastFailedContentHash = sourceContentHash;
                    return;
                }

                translatedTexts.Add(translatedText);
            }

            var row = this.CreateToDoText(payload, sourceLanguage, translatedTexts);
            _ = await this.insertToDoTextAsync(row).ConfigureAwait(false);
            this.currentPresentation = new ToDoPresentationSnapshot(
                sourceContentHash,
                payload,
                translatedTexts,
                this.configuration.ToDoTranslationDisplayMode);
            this.lastFailedContentHash = null;
        }
        catch
        {
            this.lastFailedContentHash = sourceContentHash;
        }
        finally
        {
            if (string.Equals(
                    this.translationInFlightContentHash,
                    sourceContentHash,
                    StringComparison.Ordinal))
            {
                this.translationInFlightContentHash = null;
            }
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
        ToDoPresentationSnapshot presentation)
    {
        this.hoverTooltipManager.RemoveByPrefix(HoverTooltipPrefix);
        var translatedTextsByNodeId = presentation.TranslatedTexts
            .Select((text, index) => new
            {
                NodeId = presentation.OriginalPayload.GetTranslatableTexts()[index].NodeId,
                Text = text,
            })
            .ToDictionary(text => text.NodeId, text => text.Text);
        var sourceTextsByNodeId = presentation.OriginalPayload
            .GetTranslatableTexts()
            .ToDictionary(text => text.NodeId, text => text.Text);
        var timerNodeIds = presentation.OriginalPayload.VisibleTexts
            .Where(text => text.IsTimerNode)
            .Select(text => text.NodeId)
            .ToHashSet();

        foreach (var textNodeAddress in ToDoTextNodeResolvers
                     .ResolveVisibleTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)textNodeAddress;
            if (textNode == null)
            {
                continue;
            }

            var nodeId = (int)textNode->AtkResNode.NodeId;
            var visibleText = ToDoTextNodeResolvers.ReadTextNode(textNode);
            if (timerNodeIds.Contains(nodeId) ||
                !sourceTextsByNodeId.TryGetValue(nodeId, out var sourceText) ||
                !translatedTextsByNodeId.TryGetValue(nodeId, out var translatedText))
            {
                continue;
            }

            if (this.WritesNativeTranslation)
            {
                var nativeText = translatedText;
                if (!string.IsNullOrWhiteSpace(nativeText) &&
                    !string.Equals(visibleText, nativeText, StringComparison.Ordinal))
                {
                    textNode->SetText(nativeText);
                }

                this.nativeMutations[textNodeAddress] = new ToDoNativeMutation(
                    sourceText,
                    nativeText);
            }

            if (this.UsesHoverTooltips)
            {
                this.RegisterHoverTooltip(
                    nodeId,
                    textNode,
                    sourceText,
                    translatedText);
            }
        }

        if (!this.WritesNativeTranslation)
        {
            this.RestoreOwnedNativeMutations(addon);
        }
    }

    /// <summary>
    ///     Restores only native text previously written by this handler.
    /// </summary>
    /// <param name="addon">The visible ToDo addon.</param>
    private unsafe void RestoreOwnedNativeMutations(AtkUnitBase* addon)
    {
        foreach (var textNodeAddress in ToDoTextNodeResolvers
                     .ResolveVisibleTextNodeAddresses(addon))
        {
            if (!this.nativeMutations.TryGetValue(
                    textNodeAddress,
                    out var mutation))
            {
                continue;
            }

            var textNode = (AtkTextNode*)textNodeAddress;
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
    /// <param name="nodeId">The stable native node id.</param>
    /// <param name="textNode">The visible native text node.</param>
    /// <param name="originalText">The original ToDo text.</param>
    /// <param name="translatedText">The translated ToDo text.</param>
    private unsafe void RegisterHoverTooltip(
        int nodeId,
        AtkTextNode* textNode,
        string originalText,
        string translatedText)
    {
        var body = this.HoverShowsOriginal ? originalText : translatedText;
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var left = textNode->ScreenX - 12f;
        var top = textNode->ScreenY - 8f;
        var right = textNode->ScreenX + Math.Max(1f, textNode->GetWidth()) + 12f;
        var bottom = textNode->ScreenY + Math.Max(1f, textNode->GetHeight()) + 8f;
        this.hoverTooltipManager.Register(
            $"{HoverTooltipPrefix}{nodeId}",
            new Vector2(left, top),
            new Vector2(right, bottom),
            string.Empty,
            body,
            useGeneralFont: this.HoverShowsOriginal,
            displaysOriginalSwapText: this.HoverShowsOriginal);
    }

    /// <summary>
    ///     Creates the dedicated persistence row for one source payload.
    /// </summary>
    /// <param name="payload">The source-facing ToDo payload.</param>
    /// <param name="sourceLanguage">The captured source language.</param>
    /// <param name="translatedTexts">The optional complete translated payload.</param>
    /// <returns>The dedicated persistence row.</returns>
    private ToDoText CreateToDoText(
        ToDoPayload payload,
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string>? translatedTexts)
    {
        var originalTextsAsText = JsonConvert.SerializeObject(
            payload.GetTranslatableTexts().Select(text => text.Text));
        return new ToDoText
        {
            AddonName = ToDoAddonName,
            OriginalTextsAsText = originalTextsAsText,
            OriginalLang = sourceLanguage.PersistenceCode,
            TranslatedTextsAsText = translatedTexts == null
                ? string.Empty
                : JsonConvert.SerializeObject(translatedTexts),
            TranslationLang = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.configuration.Lang),
            TranslationEngine = this.configuration.ChosenTransEngine,
            GameVersion = GetGameVersion() ?? string.Empty,
            SourceContentHash = payload.ComputeSourceContentHash(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };
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
    ///     Tries to resolve the visible dedicated ToDo addon.
    /// </summary>
    /// <param name="addon">Receives the visible ToDo addon.</param>
    /// <returns><c>true</c> when ToDo is visible.</returns>
    private static unsafe bool TryGetVisibleToDo(out AtkUnitBase* addon)
    {
        addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
            ToDoAddonName);
        return addon != null && addon->IsVisible;
    }

    /// <summary>
    ///     Clears handler-owned runtime state without touching _ToDoList.
    /// </summary>
    private void ClearRuntimeState()
    {
        this.currentPresentation = null;
        this.lastFailedContentHash = null;
        this.lastVisibleContentHash = null;
        this.translationInFlightContentHash = null;
        this.nativeMutations.Clear();
        this.hoverTooltipManager.RemoveByPrefix(HoverTooltipPrefix);
    }

    /// <summary>
    ///     Represents a fully resolved dedicated ToDo presentation snapshot.
    /// </summary>
    /// <param name="SourceContentHash">The non-timer source payload hash.</param>
    /// <param name="OriginalPayload">The source-facing ToDo payload.</param>
    /// <param name="TranslatedTexts">The ordered translated text rows.</param>
    /// <param name="DisplayMode">The display mode that produced the snapshot.</param>
    private sealed record ToDoPresentationSnapshot(
        string SourceContentHash,
        ToDoPayload OriginalPayload,
        IReadOnlyList<string> TranslatedTexts,
        JournalTranslationDisplayMode DisplayMode);

    /// <summary>
    ///     Captures one native mutation owned by the dedicated ToDo handler.
    /// </summary>
    /// <param name="OriginalText">The game-owned original text.</param>
    /// <param name="AppliedText">The handler-applied native text.</param>
    private sealed record ToDoNativeMutation(
        string OriginalText,
        string AppliedText);
}
