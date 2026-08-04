// <copyright file="TooltipHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.UIOverlays.TranslationOverlay;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using Lumina.Text.ReadOnly;

namespace Echoglossian.NativeUI.AddonHandlers.Common;

/// <summary>
///     Handles DB-first translation for the Tooltip addon.
/// </summary>
internal sealed unsafe class TooltipHandler : DbFirstGameWindowAddonHandler
{
    private const ushort AdditionalTooltipWrapWidth = 8;
    private const int MinimumTooltipBackgroundHorizontalPadding = 8;
    private const int MinimumTooltipBackgroundVerticalPadding = 8;
    private readonly Dictionary<string, NativeTextNodeLayoutSnapshot>
        appliedLayoutSnapshots = new(StringComparer.Ordinal);
    private readonly List<string> pendingCapturedTexts = [];
    private readonly Func<TooltipText, TooltipText?> findTooltipText;
    private readonly Func<TooltipText, IReadOnlyList<TooltipText>>
        findTooltipTextCandidates;
    private readonly Func<TooltipText, Task<string>> insertTooltipTextAsync;
    private readonly TooltipAddonAnchoredOverlayRuntime
        tooltipAddonAnchoredOverlayRuntime;
    private readonly TranslationOverlay tooltipAddonOverlay;
    private bool nativeTooltipHiddenByOverlay;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TooltipHandler" />
    ///     class.
    /// </summary>
    /// <param name="configuration">The plugin configuration.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="tooltipAddonOverlay">
    ///     The shared anchored overlay surface for the Tooltip addon.
    /// </param>
    /// <param name="tooltipAddonAnchoredOverlayRuntime">
    ///     The Tooltip addon anchored overlay state runtime.
    /// </param>
    /// <param name="findTooltipText">Resolves a persisted Tooltip payload.</param>
    /// <param name="findTooltipTextCandidates">
    ///     Resolves candidate Tooltip payloads for canonical original
    ///     recovery.
    /// </param>
    /// <param name="insertTooltipTextAsync">
    ///     Persists a Tooltip payload asynchronously.
    /// </param>
    public TooltipHandler(
        Config configuration,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService,
        TranslationOverlay tooltipAddonOverlay,
        TooltipAddonAnchoredOverlayRuntime tooltipAddonAnchoredOverlayRuntime,
        Func<TooltipText, TooltipText?> findTooltipText,
        Func<TooltipText, IReadOnlyList<TooltipText>> findTooltipTextCandidates,
        Func<TooltipText, Task<string>> insertTooltipTextAsync)
        : base(
            addonName: "Tooltip",
            config: configuration,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration => configuration.TranslateTooltipAddon,
            useAtkValues: false,
            useTextNodes: true,
            displayModeSelector: static configuration =>
                configuration.TooltipAddonTranslationDisplayMode)
    {
        this.tooltipAddonOverlay = tooltipAddonOverlay;
        this.tooltipAddonAnchoredOverlayRuntime =
            tooltipAddonAnchoredOverlayRuntime;
        this.findTooltipText = findTooltipText;
        this.findTooltipTextCandidates = findTooltipTextCandidates;
        this.insertTooltipTextAsync = insertTooltipTextAsync;
    }

    /// <inheritdoc />
    protected override List<nint> ResolveTextNodeAddresses(AtkUnitBase* addon)
    {
        return AddonTextNodeResolvers.ResolveReadableTextNodes(addon);
    }

    /// <inheritdoc />
    protected override bool ShouldRefreshAppliedStateOnPreDraw()
    {
        return false;
    }

    /// <inheritdoc />
    protected override void OnPreDrawEvent(AddonEvent evt, AddonArgs args)
    {
        var addon = ResolveTooltipAddonInstance();
        var temporarilyRestoredVisibility = false;
        if (this.nativeTooltipHiddenByOverlay &&
            addon != null &&
            !addon->IsVisible)
        {
            addon->IsVisible = true;
            temporarilyRestoredVisibility = true;
        }

        base.OnPreDrawEvent(evt, args);

        addon = ResolveTooltipAddonInstance();
        if (!this.HandlerConfig.TranslateTooltipAddon)
        {
            this.ClearAnchoredOverlay(addon, "pre-draw-translation-disabled");
            return;
        }

        if (addon == null)
        {
            this.ClearAnchoredOverlay(
                addon: null,
                reason: "pre-draw-addon-unavailable");
            return;
        }

        if (!TooltipAddonAnchoredOverlayPresentationPolicy.UsesAnchoredOverlay(
                this.HandlerConfig.TooltipAddonTranslationDisplayMode,
                this.HandlerConfig.OverlayOnlyLanguage))
        {
            this.ClearAnchoredOverlay(addon, "pre-draw-anchored-overlay-disabled");
            return;
        }

        if (!this.HandlerConfig.TooltipAddonHideNativeTooltipWhenOverlayActive)
        {
            if (this.nativeTooltipHiddenByOverlay)
            {
                addon->IsVisible = true;
                this.nativeTooltipHiddenByOverlay = false;
            }

            return;
        }

        if (temporarilyRestoredVisibility &&
            this.nativeTooltipHiddenByOverlay)
        {
            addon->IsVisible = false;
        }
    }

    /// <inheritdoc />
    protected override void OnCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        base.OnCleanupEvent(evt, args);
        this.ClearAnchoredOverlay(ResolveTooltipAddonInstance());
        this.appliedLayoutSnapshots.Clear();
        this.pendingCapturedTexts.Clear();
    }

    /// <inheritdoc />
    public override void OnPluginUnload()
    {
        base.OnPluginUnload();
        this.ClearAnchoredOverlay(ResolveTooltipAddonInstance());
        this.appliedLayoutSnapshots.Clear();
        this.pendingCapturedTexts.Clear();
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        var normalizedVisibleText =
            TooltipTextNormalizationHelper.NormalizeForCapture(visibleText);
        var preferredCaptureText =
            ResolvePreferredCaptureText(textNode, normalizedVisibleText);
        this.pendingCapturedTexts.Add(preferredCaptureText);
        return !string.IsNullOrWhiteSpace(preferredCaptureText);
    }

    /// <inheritdoc />
    protected override SortedDictionary<string, string> NormalizeCapturedTextNodes(
        SortedDictionary<string, string> capturedTextNodes)
    {
        var normalizedTextNodes = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        try
        {
            var captureIndex = 0;
            foreach (var (key, value) in capturedTextNodes)
            {
                var normalizedText = captureIndex < this.pendingCapturedTexts.Count
                    ? this.pendingCapturedTexts[captureIndex]
                    : TooltipTextNormalizationHelper.NormalizeForCapture(value);
                captureIndex++;
                if (string.IsNullOrWhiteSpace(normalizedText))
                {
                    continue;
                }

                normalizedTextNodes[key] = normalizedText;
            }

            if (this.CurrentRuntimeState != null)
            {
                return TooltipPayloadRecoveryHelper.CanonicalizeLiveTextNodes(
                    normalizedTextNodes,
                    this.CurrentRuntimeState.OriginalPayload.TextNodes,
                    this.CurrentRuntimeState.TranslatedPayload.TextNodes);
            }

            return normalizedTextNodes;
        }
        finally
        {
            this.pendingCapturedTexts.Clear();
        }
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalOriginalPayload(
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;
        var candidates = this.BuildRecoveryCandidates(
            sourceLanguage,
            livePayload);
        return TooltipPayloadRecoveryHelper.TryRecoverOriginalPayload(
            livePayload,
            candidates,
            out originalPayload);
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalTranslatedPayload(
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.HandlerConfig.Lang),
            this.GetOperationTranslationEngineId(),
            this.HandlerConfig.TranslateAlreadyTranslatedTexts);
        var lookup = this.CreateTooltipText(
            scope,
            originalPayload,
            translatedPayload: null);
        var storedPayload = this.findTooltipText(lookup);
        if (storedPayload == null ||
            !TryProjectStoredPayload(
                originalPayload,
                storedPayload.TranslatedTextsAsText,
                out translatedPayload))
        {
            translatedPayload = DbFirstGameWindowPayload.Empty;
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    private protected override Task<bool> TranslateAndPersistGameWindowPayloadAsync(
        TranslationReuseScope scope,
        DbFirstSourceOperation sourceOperation,
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload originalPayload)
    {
        if (!scope.TranslationEngine.HasValue)
        {
            return Task.FromResult(false);
        }

        var flattenedTextNodes = TooltipSemanticLineHelper
            .FlattenTextNodesForTranslation(originalPayload.TextNodes);
        var translatorResolution = this.HandlerTranslationService
            .CaptureTranslatorResolution(
                scope.TranslationEngine.Value,
                TranslationSurfaceGroup.Default);
        return GenericAddonHandlerHelper
            .TranslatePayloadAsync(
                originalPayload.AtkValues,
                originalPayload.StringArrayValues,
                flattenedTextNodes,
                originalPayload.AtkValues,
                originalPayload.StringArrayValues,
                flattenedTextNodes,
                sourceLanguage,
                scope.TargetLanguageCode,
                this.HandlerTranslationService,
                translatorResolution)
            .ContinueWith(
                task =>
                {
                    if (task.Status != TaskStatus.RanToCompletion ||
                        !task.Result.HasValue ||
                        !TooltipSemanticLineHelper.TryRebuildTranslatedTextNodes(
                            originalPayload.TextNodes,
                            task.Result.Value.TextNodes,
                            out var rebuiltTextNodes))
                    {
                        return false;
                    }

                    var translatedPayload = new DbFirstGameWindowPayload(
                        task.Result.Value.AtkValues,
                        task.Result.Value.StringArrayValues,
                        rebuiltTextNodes);
                    translatedPayload = this.NormalizeResolvedTranslatedPayload(
                        sourceLanguage,
                        originalPayload,
                        translatedPayload);
                    if (!this.ShouldAcceptResolvedTranslatedPayload(
                            originalPayload,
                            translatedPayload))
                    {
                        return false;
                    }

                    return this.PersistResolvedGameWindowPayload(
                        scope,
                        sourceOperation,
                        originalPayload,
                        translatedPayload,
                        this.GetPersistedGameWindowClassJobId(
                            originalPayload,
                            translatedPayload));
                },
                TaskScheduler.Default);
    }

    /// <inheritdoc />
    private protected override bool ShouldQueueNewGameWindowTranslation(
        TranslationReuseScope scope,
        DbFirstSourceOperation sourceOperation,
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload originalPayload,
        bool retryCoolingDown)
    {
        if (retryCoolingDown)
        {
            return false;
        }

        var candidates = this.BuildRecoveryCandidates(
            sourceLanguage,
            originalPayload);
        if (TooltipPayloadRecoveryHelper.HasTranslatedSlotEvidence(
                originalPayload,
                candidates))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    private protected override bool ShouldAcceptResolvedTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return TooltipSemanticLineHelper.HasCompatibleSemanticLineStructure(
            originalPayload.TextNodes,
            translatedPayload.TextNodes);
    }

    /// <inheritdoc />
    protected override bool ShouldRestoreStaleTranslatedTextNodesOnPayloadChange()
    {
        return true;
    }

    /// <inheritdoc />
    private protected override void AfterRestoreStaleTranslatedTextNodes(
        AtkUnitBase* addon)
    {
        this.ClearAnchoredOverlay(addon);
        this.RestoreAppliedLayoutSnapshots(addon);
    }

    /// <inheritdoc />
    private protected override bool TryPersistDedicatedPayload(
        TranslationReuseScope scope,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        var row = this.CreateTooltipText(
            scope,
            originalPayload,
            translatedPayload);
        _ = this.insertTooltipTextAsync(row);
        return true;
    }

    /// <inheritdoc />
    private protected override bool TryApplyCustomTextNodePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        this.ClearAnchoredOverlay(addon);
        var ordinalsByNodeId = new Dictionary<uint, int>();

        foreach (var nodeAddress in this.ResolveTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var textNodeKey = DbFirstTextNodeKeyAllocator.ConsumeVisibleNode(
                ordinalsByNodeId,
                textNode->AtkResNode.NodeId);
            if (!sourcePayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var sourceText) ||
                !targetPayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var targetText))
            {
                continue;
            }

            var currentText = this.ReadTextNode(textNode);
            if (string.Equals(
                    NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                        currentText),
                    NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                        targetText),
                    StringComparison.Ordinal))
            {
                continue;
            }

            var sourceTextPayload =
                ReadableSeStringPayloadHelper.TryCaptureMatchingPayload(
                    textNode,
                    sourceText);
            var replacementPayload =
                ReadableSeStringPayloadHelper.ProjectReadablePayloadBytes(
                    sourceTextPayload,
                    sourceText,
                    targetText);
            var layoutSnapshot =
                NativeTextNodeLayoutHelper.ApplyTextReplacementWithInferredReflow(
                    addon,
                    textNode,
                    targetText,
                    allowWidthGrowth: true,
                    additionalWrapWidth: AdditionalTooltipWrapWidth,
                    measureReplacementWidthBeforeApply: true,
                    minimumSecondaryHorizontalPadding:
                    MinimumTooltipBackgroundHorizontalPadding,
                    minimumSecondaryVerticalPadding:
                    MinimumTooltipBackgroundVerticalPadding,
                    replacementPayload: replacementPayload);
            if (layoutSnapshot != null)
            {
                this.appliedLayoutSnapshots[textNodeKey] = layoutSnapshot;
            }
        }

        return true;
    }

    /// <inheritdoc />
    private protected override bool TryRegisterCustomHoverTooltips(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        JournalTranslationDisplayMode displayMode)
    {
        if (!TooltipAddonAnchoredOverlayPresentationPolicy.UsesAnchoredOverlay(
                displayMode,
                this.HandlerConfig.OverlayOnlyLanguage))
        {
            OverlayPublicationDiagnostics.Log(
                "TooltipAddonOverlayDiag",
                "register-skip",
                $"{displayMode}|{this.HandlerConfig.OverlayOnlyLanguage}",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{displayMode}|{this.HandlerConfig.OverlayOnlyLanguage}"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"displayMode={displayMode} overlayOnlyLanguage={this.HandlerConfig.OverlayOnlyLanguage} " +
                    $"nativeHiddenByOverlay={this.nativeTooltipHiddenByOverlay}"));
            this.ClearAnchoredOverlay(addon, "register-anchored-overlay-disabled");
            return false;
        }

        if (!this.TryBuildTooltipAddonOverlayFrame(addon, out var frame))
        {
            this.ClearAnchoredOverlay(addon, "register-frame-build-failed");
            return true;
        }

        var originalBody = BuildTooltipOverlayBody(originalPayload);
        var translatedBody = BuildTooltipOverlayBody(translatedPayload);
        var showsOriginalOverlayText =
            TooltipAddonAnchoredOverlayPresentationPolicy.ShowsOriginalOverlayText(
                displayMode,
                this.HandlerConfig.OverlayOnlyLanguage);
        var overlayBody =
            TooltipAddonAnchoredOverlayPresentationPolicy.SelectOverlayBody(
                displayMode,
                this.HandlerConfig.OverlayOnlyLanguage,
                originalBody,
                translatedBody);
        var richOriginalTextPresentation = showsOriginalOverlayText
            ? this.TryBuildTooltipAddonRichOriginalTextPresentation(
                addon,
                originalPayload,
                originalBody)
            : null;
        if (string.IsNullOrWhiteSpace(overlayBody))
        {
            OverlayPublicationDiagnostics.Log(
                "TooltipAddonOverlayDiag",
                "register-empty-body",
                $"{displayMode}|{this.HandlerConfig.OverlayOnlyLanguage}",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{displayMode}|{this.HandlerConfig.OverlayOnlyLanguage}|" +
                    $"{OverlayPublicationDiagnostics.BuildPreview(originalBody)}|" +
                    $"{OverlayPublicationDiagnostics.BuildPreview(translatedBody)}"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"displayMode={displayMode} overlayOnlyLanguage={this.HandlerConfig.OverlayOnlyLanguage} " +
                    $"originalLen={originalBody.Length} originalPreview='{OverlayPublicationDiagnostics.BuildPreview(originalBody)}' " +
                    $"translatedLen={translatedBody.Length} translatedPreview='{OverlayPublicationDiagnostics.BuildPreview(translatedBody)}'"));
            this.ClearAnchoredOverlay(addon, "register-overlay-body-empty");
            return true;
        }

        OverlayPublicationDiagnostics.Log(
            "TooltipAddonOverlayDiag",
            "register-publish",
            $"{displayMode}|{OverlayPublicationDiagnostics.BuildPreview(overlayBody)}",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{displayMode}|{this.HandlerConfig.OverlayOnlyLanguage}|" +
                $"{showsOriginalOverlayText}|" +
                $"{this.HandlerConfig.TooltipAddonHideNativeTooltipWhenOverlayActive}|" +
                $"{richOriginalTextPresentation != null}|" +
                $"{OverlayPublicationDiagnostics.BuildPreview(overlayBody)}|" +
                $"{OverlayPublicationDiagnostics.RoundVector(frame.Position).X:0},{OverlayPublicationDiagnostics.RoundVector(frame.Position).Y:0}|" +
                $"{OverlayPublicationDiagnostics.RoundVector(frame.Size).X:0},{OverlayPublicationDiagnostics.RoundVector(frame.Size).Y:0}|" +
                $"{frame.NativeScale:0.##}|{frame.NativeVisible}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"displayMode={displayMode} overlayOnlyLanguage={this.HandlerConfig.OverlayOnlyLanguage} " +
                $"showsOriginal={showsOriginalOverlayText} " +
                $"hideNativeWhenOverlayActive={this.HandlerConfig.TooltipAddonHideNativeTooltipWhenOverlayActive} " +
                $"richOriginalPresentationReady={richOriginalTextPresentation != null} " +
                $"framePos={OverlayPublicationDiagnostics.FormatVector(frame.Position)} frameSize={OverlayPublicationDiagnostics.FormatVector(frame.Size)} " +
                $"frameNativeScale={frame.NativeScale:0.##} frameNativeVisible={frame.NativeVisible} " +
                $"bodyLen={overlayBody.Length} preview='{OverlayPublicationDiagnostics.BuildPreview(overlayBody)}'"));
        this.tooltipAddonAnchoredOverlayRuntime.Publish(
            this.tooltipAddonOverlay,
            frame,
            overlayBody,
            showsOriginalOverlayText,
            richOriginalTextPresentation,
            renderScaleAdjustment: 1f);

        if (this.HandlerConfig.TooltipAddonHideNativeTooltipWhenOverlayActive)
        {
            addon->IsVisible = false;
            this.nativeTooltipHiddenByOverlay = true;
        }
        else
        {
            if (this.nativeTooltipHiddenByOverlay)
            {
                addon->IsVisible = true;
            }

            this.nativeTooltipHiddenByOverlay = false;
        }

        return true;
    }

    /// <summary>
    ///     Tries to build one combined rich original-text presentation for the
    ///     anchored Tooltip overlay from the live native text nodes.
    /// </summary>
    /// <param name="addon">The live Tooltip addon.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="originalBody">The plain original overlay body.</param>
    /// <returns>
    ///     The combined rich original presentation, or <see langword="null" />
    ///     when one or more source segments could not be captured safely.
    /// </returns>
    private RichOriginalTextPresentation? TryBuildTooltipAddonRichOriginalTextPresentation(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload originalPayload,
        string originalBody)
    {
        if (addon == null ||
            string.IsNullOrWhiteSpace(originalBody) ||
            originalPayload.TextNodes.Count == 0)
        {
            return null;
        }

        var capturedPayloads = new Dictionary<string, byte[]>(
            StringComparer.Ordinal);
        var ordinalsByNodeId = new Dictionary<uint, int>();
        foreach (var nodeAddress in this.ResolveTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var nodeKey = DbFirstTextNodeKeyAllocator.ConsumeVisibleNode(
                ordinalsByNodeId,
                textNode->AtkResNode.NodeId);
            if (!originalPayload.TextNodes.TryGetValue(nodeKey, out var expectedText) ||
                string.IsNullOrWhiteSpace(expectedText))
            {
                continue;
            }

            var payload = ReadableSeStringPayloadHelper.TryCaptureMatchingPayload(
                textNode,
                expectedText);
            if (payload == null || payload.Length == 0)
            {
                return null;
            }

            capturedPayloads[nodeKey] = payload;
        }

        var orderedPayloads = new List<byte[]?>(originalPayload.TextNodes.Count);
        foreach (var (nodeKey, _) in GetOrderedTextNodes(originalPayload.TextNodes))
        {
            if (!capturedPayloads.TryGetValue(nodeKey, out var payload) ||
                payload.Length == 0)
            {
                return null;
            }

            orderedPayloads.Add(payload);
        }

        return TooltipAddonRichOriginalTextPresentationFactory.Create(
            originalBody,
            orderedPayloads);
    }

    /// <summary>
    ///     Prefers the original source text when the visible Tooltip text only
    ///     differs by wrap markers or inline formatting payloads, but keeps the
    ///     live visible text once the game is already showing translated
    ///     content.
    /// </summary>
    /// <param name="textNode">The live Tooltip text node.</param>
    /// <param name="normalizedVisibleText">The normalized visible text.</param>
    /// <returns>The preferred semantic capture text.</returns>
    private static string ResolvePreferredCaptureText(
        AtkTextNode* textNode,
        string normalizedVisibleText)
    {
        if (string.IsNullOrWhiteSpace(normalizedVisibleText))
        {
            return string.Empty;
        }

        var normalizedSourceText = ReadNormalizedSourceText(textNode);
        if (string.IsNullOrWhiteSpace(normalizedSourceText))
        {
            return normalizedVisibleText;
        }

        var comparisonVisibleText =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                normalizedVisibleText);
        var comparisonSourceText =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                normalizedSourceText);
        if (string.IsNullOrWhiteSpace(comparisonVisibleText) ||
            string.IsNullOrWhiteSpace(comparisonSourceText))
        {
            return normalizedVisibleText;
        }

        return string.Equals(
                   comparisonVisibleText,
                   comparisonSourceText,
                   StringComparison.Ordinal) ||
               comparisonVisibleText.Contains(
                   comparisonSourceText,
                   StringComparison.Ordinal) ||
               comparisonSourceText.Contains(
                   comparisonVisibleText,
                   StringComparison.Ordinal)
            ? normalizedSourceText
            : normalizedVisibleText;
    }

    /// <summary>
    ///     Reads the original Tooltip source text without live wrap markers so
    ///     capture can persist a semantic source string when the live text is
    ///     still game-owned.
    /// </summary>
    /// <param name="textNode">The live Tooltip text node.</param>
    /// <returns>The normalized source text, or an empty string.</returns>
    private static string ReadNormalizedSourceText(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return string.Empty;
        }

        try
        {
            var sourceText = textNode->OriginalTextPointer
                .AsReadOnlySeStringSpan()
                .ExtractText();
            return TooltipTextNormalizationHelper.NormalizeForCapture(sourceText);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    private protected override void AfterRestorePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload translatedPayload,
        DbFirstGameWindowPayload originalPayload)
    {
        this.ClearAnchoredOverlay(addon, "after-restore-payload");
        this.RestoreAppliedLayoutSnapshots(addon);
    }

    /// <summary>
    ///     Builds one dedicated Tooltip persistence row from the ordered
    ///     visible text-node payload.
    /// </summary>
    /// <param name="scope">The immutable source, target, and engine scope.</param>
    /// <param name="originalPayload">The ordered original-facing payload.</param>
    /// <param name="translatedPayload">The optional ordered translated payload.</param>
    /// <returns>The dedicated Tooltip row.</returns>
    private TooltipText CreateTooltipText(
        TranslationReuseScope scope,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload? translatedPayload)
    {
        var originalTextsAsText = JsonConvert.SerializeObject(
            GetOrderedTextNodes(originalPayload.TextNodes)
                .Select(pair => pair.Value));
        return new TooltipText
        {
            AddonName = this.AddonName,
            OriginalTextsAsText = originalTextsAsText,
            OriginalLang = scope.SourceLanguageCode,
            TranslatedTextsAsText = translatedPayload.HasValue
                ? JsonConvert.SerializeObject(
                    GetOrderedTextNodes(translatedPayload.Value.TextNodes)
                        .Select(pair => pair.Value))
                : string.Empty,
            TranslationLang = scope.TargetLanguageCode,
            TranslationEngine = scope.TranslationEngine,
            GameVersion = GetGameVersion() ?? string.Empty,
            SourceContentHash = ComputeSourceContentHash(originalTextsAsText),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };
    }

    /// <summary>
    ///     Projects one ordered dedicated Tooltip payload onto the current
    ///     text-node keys.
    /// </summary>
    /// <param name="originalPayload">The visible original-facing payload.</param>
    /// <param name="translatedTextsAsText">The stored ordered translations.</param>
    /// <param name="translatedPayload">Receives the projected payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the stored ordered payload matches the
    ///     visible text-node shape; otherwise <see langword="false" />.
    /// </returns>
    private IReadOnlyList<DbFirstPayloadRecoveryCandidate> BuildRecoveryCandidates(
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload livePayload)
    {
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.HandlerConfig.Lang),
            this.GetOperationTranslationEngineId(),
            this.HandlerConfig.TranslateAlreadyTranslatedTexts);
        var probe = this.CreateTooltipText(
            scope,
            livePayload,
            translatedPayload: null);
        var rows = this.findTooltipTextCandidates(probe);
        if (rows.Count == 0)
        {
            return [];
        }

        var candidates = new List<DbFirstPayloadRecoveryCandidate>();
        foreach (var row in rows)
        {
            if (!TryProjectStoredPayload(
                    livePayload,
                    row.OriginalTextsAsText,
                    out var originalCandidatePayload) ||
                !TryProjectStoredPayload(
                    livePayload,
                    row.TranslatedTextsAsText,
                    out var translatedCandidatePayload))
            {
                continue;
            }

            if (!TooltipPayloadRecoveryHelper.HasSemanticallyDistinctPayloads(
                    originalCandidatePayload,
                    translatedCandidatePayload))
            {
                continue;
            }

            candidates.Add(
                new DbFirstPayloadRecoveryCandidate(
                    originalCandidatePayload,
                    translatedCandidatePayload));
        }

        return candidates;
    }

    /// <summary>
    ///     Projects one ordered dedicated Tooltip payload onto the current
    ///     text-node keys.
    /// </summary>
    /// <param name="referencePayload">The visible payload shape to project to.</param>
    /// <param name="storedTextsAsText">The stored ordered payload values.</param>
    /// <param name="projectedPayload">Receives the projected payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the stored ordered payload matches the
    ///     visible text-node shape; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryProjectStoredPayload(
        DbFirstGameWindowPayload referencePayload,
        string? storedTextsAsText,
        out DbFirstGameWindowPayload projectedPayload)
    {
        projectedPayload = DbFirstGameWindowPayload.Empty;

        try
        {
            var storedTexts = JsonConvert.DeserializeObject<List<string>>(
                storedTextsAsText ?? string.Empty);
            if (storedTexts == null ||
                storedTexts.Count != referencePayload.TextNodes.Count ||
                storedTexts.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            var projectedTextNodes = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            using var originalKeys = GetOrderedTextNodes(
                    referencePayload.TextNodes)
                .Select(pair => pair.Key)
                .GetEnumerator();
            using var storedValues = storedTexts.GetEnumerator();
            while (originalKeys.MoveNext() && storedValues.MoveNext())
            {
                projectedTextNodes[originalKeys.Current] =
                    TooltipTextNormalizationHelper.NormalizeForCapture(
                        storedValues.Current);
            }

            projectedPayload = new DbFirstGameWindowPayload(
                [],
                [],
                projectedTextNodes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Restores cached native layout snapshots so one reused Tooltip addon
    ///     does not accumulate stale dimensions across distinct payloads.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    private void RestoreAppliedLayoutSnapshots(AtkUnitBase* addon)
    {
        if (this.appliedLayoutSnapshots.Count == 0)
        {
            return;
        }

        var restoredKeys = new List<string>();
        var ordinalsByNodeId = new Dictionary<uint, int>();
        foreach (var nodeAddress in this.ResolveTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null)
            {
                continue;
            }

            var textNodeKey = DbFirstTextNodeKeyAllocator.ConsumeVisibleNode(
                ordinalsByNodeId,
                textNode->AtkResNode.NodeId);
            if (!this.appliedLayoutSnapshots.TryGetValue(
                    textNodeKey,
                    out var layoutSnapshot))
            {
                continue;
            }

            NativeTextNodeLayoutHelper.RestoreLayoutSnapshot(
                layoutSnapshot,
                string.Empty,
                restoreText: false);
            restoredKeys.Add(textNodeKey);
        }

        foreach (var restoredKey in restoredKeys)
        {
            this.appliedLayoutSnapshots.Remove(restoredKey);
        }
    }

    /// <summary>
    ///     Clears Tooltip addon anchored overlay state and restores native
    ///     visibility when this handler hid the native tooltip.
    /// </summary>
    /// <param name="addon">The live Tooltip addon, if available.</param>
    private void ClearAnchoredOverlay(
        AtkUnitBase* addon,
        string reason = "unspecified")
    {
        OverlayPublicationDiagnostics.Log(
            "TooltipAddonOverlayDiag",
            "handler-clear",
            reason,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{reason}|{this.tooltipAddonOverlay.Display}|{this.nativeTooltipHiddenByOverlay}|" +
                $"{OverlayPublicationDiagnostics.BuildPreview(this.tooltipAddonOverlay.CurrentText)}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"reason={reason} overlayDisplay={this.tooltipAddonOverlay.Display} " +
                $"nativeTooltipHiddenByOverlay={this.nativeTooltipHiddenByOverlay} addonAvailable={addon != null} " +
                $"textLen={this.tooltipAddonOverlay.CurrentText.Length} " +
                $"preview='{OverlayPublicationDiagnostics.BuildPreview(this.tooltipAddonOverlay.CurrentText)}'"));
        this.tooltipAddonAnchoredOverlayRuntime.Clear(this.tooltipAddonOverlay);
        if (addon != null && this.nativeTooltipHiddenByOverlay)
        {
            addon->IsVisible = true;
        }

        this.nativeTooltipHiddenByOverlay = false;
    }

    /// <summary>
    ///     Builds the current Tooltip addon anchor frame for the dedicated
    ///     anchored overlay runtime.
    /// </summary>
    /// <param name="addon">The live Tooltip addon.</param>
    /// <param name="frame">Receives the resolved anchor frame.</param>
    /// <returns>
    ///     <see langword="true" /> when the Tooltip root bounds were
    ///     available; otherwise <see langword="false" />.
    /// </returns>
    private bool TryBuildTooltipAddonOverlayFrame(
        AtkUnitBase* addon,
        out TooltipAddonOverlayFrame frame)
    {
        frame = default;
        var rootNode = addon == null
            ? null
            : (addon->RootNode != null
                ? addon->RootNode
                : addon->UldManager.RootNode);
        if (addon == null || rootNode == null)
        {
            OverlayPublicationDiagnostics.Log(
                "TooltipAddonOverlayDiag",
                "frame-build-failed",
                addon == null ? "addon-null" : "root-null",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{(addon == null ? "addon-null" : "root-null")}|" +
                    $"{addon != null && addon->RootNode != null}|" +
                    $"{addon != null && addon->UldManager.RootNode != null}"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"reason={(addon == null ? "addon-null" : "root-null")} " +
                    $"directRootAvailable={addon != null && addon->RootNode != null} " +
                    $"uldRootAvailable={addon != null && addon->UldManager.RootNode != null}"));
            return false;
        }

        if (!rootNode->IsVisible())
        {
            OverlayPublicationDiagnostics.Log(
                "TooltipAddonOverlayDiag",
                "frame-build-failed",
                "root-invisible",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rootNode->ScreenX}|{rootNode->ScreenY}|{rootNode->Width}|{rootNode->Height}|{addon->IsVisible}"),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"reason=root-invisible addonVisible={addon->IsVisible} " +
                    $"rootPos=({rootNode->ScreenX:0.0},{rootNode->ScreenY:0.0}) rootSize=({rootNode->Width:0.0},{rootNode->Height:0.0})"));
            return false;
        }

        var nativeScale = addon->Scale > 0f ? addon->Scale : 1f;
        frame = new TooltipAddonOverlayFrame(
            new Vector2(rootNode->ScreenX, rootNode->ScreenY),
            new Vector2(
                Math.Max(1f, rootNode->Width * nativeScale),
                Math.Max(1f, rootNode->Height * nativeScale)),
            nativeScale,
            addon->IsVisible);
        OverlayPublicationDiagnostics.Log(
            "TooltipAddonOverlayDiag",
            "frame-built",
            $"{MathF.Round(frame.Position.X / 16f)},{MathF.Round(frame.Position.Y / 16f)}",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{OverlayPublicationDiagnostics.RoundVector(frame.Position).X:0},{OverlayPublicationDiagnostics.RoundVector(frame.Position).Y:0}|" +
                $"{OverlayPublicationDiagnostics.RoundVector(frame.Size).X:0},{OverlayPublicationDiagnostics.RoundVector(frame.Size).Y:0}|" +
                $"{frame.NativeScale:0.##}|{frame.NativeVisible}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"framePos={OverlayPublicationDiagnostics.FormatVector(frame.Position)} " +
                $"frameSize={OverlayPublicationDiagnostics.FormatVector(frame.Size)} " +
                $"nativeScale={frame.NativeScale:0.##} nativeVisible={frame.NativeVisible}"));
        return true;
    }

    /// <summary>
    ///     Builds one newline-delimited tooltip overlay body from the ordered
    ///     text-node payload.
    /// </summary>
    /// <param name="payload">The ordered payload.</param>
    /// <returns>The overlay body text.</returns>
    private static string BuildTooltipOverlayBody(
        DbFirstGameWindowPayload payload)
    {
        return string.Join(
            "\n",
            GetOrderedTextNodes(payload.TextNodes)
                .Select(static pair => pair.Value));
    }

    /// <summary>
    ///     Resolves the live Tooltip addon even when this handler previously
    ///     hid it for anchored overlay presentation.
    /// </summary>
    /// <returns>The live Tooltip addon instance, if any.</returns>
    private static AtkUnitBase* ResolveTooltipAddonInstance()
    {
        var atkStage = AtkStage.Instance();
        return atkStage == null
            ? null
            : atkStage->RaptureAtkUnitManager->GetAddonByName("Tooltip");
    }

    /// <summary>
    ///     Orders visible text-node payload entries by their numeric node id
    ///     and duplicate visible-node ordinal.
    /// </summary>
    /// <param name="textNodes">The text-node payload to order.</param>
    /// <returns>The entries in their stable numeric node-key order.</returns>
    private static IEnumerable<KeyValuePair<string, string>> GetOrderedTextNodes(
        SortedDictionary<string, string> textNodes)
    {
        return textNodes
            .OrderBy(pair => GetTextNodeKeyOrder(pair.Key).NodeId)
            .ThenBy(pair => GetTextNodeKeyOrder(pair.Key).Ordinal)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Parses one text-node payload key into numeric order parts.
    /// </summary>
    /// <param name="key">The text-node key.</param>
    /// <returns>The parsed node id and duplicate ordinal.</returns>
    private static (uint NodeId, int Ordinal) GetTextNodeKeyOrder(string key)
    {
        var separators = key.Split(':', 2);
        return separators.Length == 2 &&
               uint.TryParse(separators[0], out var nodeId) &&
               int.TryParse(separators[1], out var ordinal)
            ? (nodeId, ordinal)
            : (uint.MaxValue, int.MaxValue);
    }

    /// <summary>
    ///     Computes the stable hash used to key one ordered Tooltip original
    ///     payload.
    /// </summary>
    /// <param name="originalTextsAsText">The normalized ordered source JSON.</param>
    /// <returns>The uppercase SHA-256 hash.</returns>
    private static string ComputeSourceContentHash(string originalTextsAsText)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(originalTextsAsText));
        return Convert.ToHexString(bytes);
    }
}
