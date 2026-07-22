// <copyright file="ActionItemDetailUiRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.UIOverlays.TranslationOverlay;
using Dalamud.Game.Gui;
using Dalamud.Utility;
using DetailKind = Dalamud.Game.Gui.DetailKind;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Echoglossian;

/// <summary>
///     Provides the live DB-first apply/runtime path for the ActionDetail and
///     ItemDetail addons, including trait-aware ActionDetail handling.
/// </summary>
public unsafe partial class Echoglossian
{
    private const string ActionDetailSurfaceName = "ActionDetail";
    private const string ItemDetailSurfaceName = "ItemDetail";
    private const uint StructuredTooltipContentKindAction = 1;
    private const uint StructuredTooltipContentKindTrait = 2;
    private const uint StructuredTooltipContentKindItem = 3;

    private static readonly AddonEvent[] StructuredTooltipLifecycleEvents =
    [
        AddonEvent.PreSetup,
        AddonEvent.PreRequestedUpdate,
        AddonEvent.PreRefresh,
        AddonEvent.PostRequestedUpdate,
        AddonEvent.PostRefresh,
        AddonEvent.PreHide,
        AddonEvent.PreFinalize,
    ];

    private readonly TranslationOverlay actionDetailOverlay = new();
    private readonly TranslationOverlay itemDetailOverlay = new();
    private readonly DiagnosticTelemetryHelper structuredTooltipTelemetry = new(
        "StructuredDetailDiag",
        TimeSpan.FromSeconds(1));

    private readonly StructuredTooltipLifecycleState actionDetailLifecycleState = new(
        ActionDetailSurfaceName,
        "ActionDetail",
        "_ActionDetail");

    private readonly StructuredTooltipLifecycleState itemDetailLifecycleState = new(
        ItemDetailSurfaceName,
        "ItemDetail",
        "_ItemDetail");

    private StructuredTooltipNativeState? currentActionDetailState;
    private StructuredTooltipNativeState? currentItemDetailState;
    private IAddonLifecycle.AddonEventDelegate? structuredTooltipLifecycleDelegate;
    private bool structuredTooltipRuntimeWasEnabled;

    private static readonly TimeSpan StructuredTooltipOnDemandPrefetchCooldown =
        TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, DateTime>
        structuredTooltipOnDemandPrefetchLastRequestedUtc =
            new(StringComparer.Ordinal);

    /// <summary>
    ///     Updates live action/item tooltip state before tooltip overlays are drawn.
    /// </summary>
    private void UpdateStructuredTooltipUiRuntime()
    {
        if (!this.ShouldRunStructuredTooltipUiRuntime())
        {
            this.HandleStructuredTooltipRuntimeDisabled();
            return;
        }

        this.structuredTooltipRuntimeWasEnabled = true;
        this.UpdateActionDetailUiRuntime();
        this.UpdateItemDetailUiRuntime();
    }

    /// <summary>
    ///     Restores native tooltip state and clears action/item tooltip overlays.
    /// </summary>
    private void ResetStructuredTooltipUiRuntime()
    {
        if (this.TryResolveTooltipAddon(out var actionAddon, "ActionDetail", "_ActionDetail"))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, actionAddon);
        }
        else
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, null);
        }

        if (this.TryResolveTooltipAddon(out var itemAddon, "ItemDetail", "_ItemDetail"))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemDetailState, itemAddon);
        }
        else
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemDetailState, null);
        }

        this.ClearStructuredTooltipOverlay(
            ActionDetailSurfaceName,
            this.actionDetailOverlay,
            reason: "reset");
        this.ClearStructuredTooltipOverlay(
            ItemDetailSurfaceName,
            this.itemDetailOverlay,
            reason: "reset");
    }

    /// <summary>
    ///     Draws the active action/item tooltip overlays, if any.
    /// </summary>
    private void DrawStructuredTooltipOverlays()
    {
        this.DrawStructuredTooltipOverlay(
            ActionDetailSurfaceName,
            this.actionDetailOverlay,
            this.BuildTooltipOverlayConfig(
                TranslationOverlaySurfaceId.ActionDetail,
                Resources.OverlayWindowTitleActionTooltipTranslation));
        this.DrawStructuredTooltipOverlay(
            ItemDetailSurfaceName,
            this.itemDetailOverlay,
            this.BuildTooltipOverlayConfig(
                TranslationOverlaySurfaceId.ItemDetail,
                Resources.OverlayWindowTitleItemTooltipTranslation));
    }

    /// <summary>
    ///     Updates the live action-tooltip runtime.
    /// </summary>
    private void UpdateActionDetailUiRuntime()
    {
        if (!this.TryResolveLifecycleTooltipAddon(
                this.actionDetailLifecycleState,
                out var addon))
        {
            return;
        }

        if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            return;
        }

        var hoveredAction = GameGuiInterface.HoveredAction;
        var hoveredItemId = (uint)GameGuiInterface.HoveredItem;
        var hoveredActionKind = hoveredAction.DetailKind;
        var agentFallbackActionId = ShouldUseActionDetailAgentFallback(
            hoveredAction.ActionId,
            hoveredItemId)
            ? this.GetActiveActionDetailId()
            : 0;
        var hoveredActionId = ResolveActionDetailReferenceId(
            hoveredActionKind,
            hoveredAction.BaseActionId,
            hoveredAction.ActionId,
            agentFallbackActionId);
        if (hoveredActionId == 0 ||
            !TryGetCurrentClassJobId(out var currentClassJobId))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
            this.LogStructuredTooltipState(
                ActionDetailSurfaceName,
                "state",
                hoveredActionId,
                0,
                displayMode: null,
                reason: hoveredActionId == 0
                    ? hoveredItemId != 0
                        ? "hovered-action-suppressed-by-item"
                        : "hovered-action-missing"
                    : "classjob-missing");
            this.ClearStructuredTooltipOverlay(
                ActionDetailSurfaceName,
                this.actionDetailOverlay,
                hoveredActionId == 0
                    ? hoveredItemId != 0
                        ? "hovered-action-suppressed-by-item"
                        : "hovered-action-missing"
                    : "classjob-missing");
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.configuration.TooltipTranslationDisplayMode,
            this.configuration.OverlayOnlyLanguage);
        var useOverlayOnly =
            !TranslationDisplayModeHelper.WritesNativeTranslation(displayMode);
        var useSwapOverlay =
            TranslationDisplayModeHelper.ShowsOriginalTooltips(displayMode);
        if (IsTraitHoverActionKind(hoveredActionKind))
        {
            if (!TryBuildTraitCanonicalPayload(
                    hoveredActionId,
                    currentClassJobId,
                    out var originalTraitPayload))
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
                this.LogStructuredTooltipState(
                    ActionDetailSurfaceName,
                    "lookup",
                    hoveredActionId,
                    StructuredTooltipContentKindTrait,
                    displayMode,
                    reason: "trait-payload-build-miss");
                this.ClearStructuredTooltipOverlay(
                    ActionDetailSurfaceName,
                    this.actionDetailOverlay,
                    reason: "trait-payload-build-miss",
                    contentId: hoveredActionId,
                    contentKind: StructuredTooltipContentKindTrait);
                return;
            }

            if (!this.TryFindTranslatedTraitTooltipPayload(
                    sourceLanguage,
                    originalTraitPayload,
                    out var translatedTraitPayload))
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
                this.LogStructuredTooltipState(
                    ActionDetailSurfaceName,
                    "lookup",
                    hoveredActionId,
                    StructuredTooltipContentKindTrait,
                    displayMode,
                    reason: "translated-trait-payload-missing",
                    name: originalTraitPayload.Name,
                    description: originalTraitPayload.Description);
                this.ClearStructuredTooltipOverlay(
                    ActionDetailSurfaceName,
                    this.actionDetailOverlay,
                    reason: "translated-trait-payload-missing",
                    contentId: hoveredActionId,
                    contentKind: StructuredTooltipContentKindTrait);
                return;
            }

            this.LogStructuredTooltipState(
                ActionDetailSurfaceName,
                "lookup",
                originalTraitPayload.TraitId,
                StructuredTooltipContentKindTrait,
                displayMode,
                reason: "translated-trait-payload-ready",
                name: translatedTraitPayload.TranslatedName ?? originalTraitPayload.Name,
                description: translatedTraitPayload.TranslatedDescription ?? originalTraitPayload.Description);
            this.RestoreStructuredTooltipOriginalsIfContentChanged(
                ref this.currentActionDetailState,
                addon,
                originalTraitPayload.TraitId,
                StructuredTooltipContentKindTrait,
                originalTraitPayload.ComputeSourceContentHash());
            if (RequiresStructuredTooltipLiveNameMatch(useOverlayOnly) &&
                !this.HasStructuredTooltipLiveNameMatch(
                    addon,
                    StructuredTooltipContentKindTrait,
                    originalTraitPayload.Name,
                    translatedTraitPayload.TranslatedName))
            {
                this.ClearStructuredTooltipOverlay(
                    ActionDetailSurfaceName,
                    this.actionDetailOverlay,
                    reason: "awaiting-live-name-match",
                    contentId: originalTraitPayload.TraitId,
                    contentKind: StructuredTooltipContentKindTrait);
                return;
            }

            var traitNativeApplySucceeded = false;
            if (useOverlayOnly)
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
            }
            else
            {
                traitNativeApplySucceeded = this.ApplyStructuredTraitTooltipNative(
                    addon,
                    originalTraitPayload,
                    translatedTraitPayload);
            }

            if (ShouldShowStructuredTooltipOverlay(
                    useOverlayOnly,
                    useSwapOverlay,
                    traitNativeApplySucceeded))
            {
                var overlayText = useSwapOverlay && traitNativeApplySucceeded
                    ? originalTraitPayload.BuildOriginalTooltipText()
                    : translatedTraitPayload.BuildTranslatedTooltipText();
                this.UpdateStructuredTooltipOverlay(
                    ActionDetailSurfaceName,
                    this.actionDetailOverlay,
                    addon,
                    originalTraitPayload.TraitId,
                    StructuredTooltipContentKindTrait,
                    overlayText);
            }
            else
            {
                this.ClearStructuredTooltipOverlay(
                    ActionDetailSurfaceName,
                    this.actionDetailOverlay,
                    reason: "overlay-disabled-by-display-mode",
                    contentId: originalTraitPayload.TraitId,
                    contentKind: StructuredTooltipContentKindTrait);
            }

            return;
        }

        var translatedLookupReferenceId = hoveredActionId;
        var usedStructuredActionPayload =
            RequiresStructuredActionReferencePayload(hoveredActionKind);
        ReferenceTextCanonicalPayload? originalReferencePayload = null;
        ActionTooltipCanonicalPayload originalPayload;
        if (usedStructuredActionPayload)
        {
            if (!TryBuildStructuredActionTooltipCanonicalPayload(
                    hoveredActionId,
                    hoveredActionKind,
                    currentClassJobId,
                    out originalPayload,
                    out translatedLookupReferenceId,
                    out originalReferencePayload))
            {
                this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
                this.LogStructuredTooltipState(
                    ActionDetailSurfaceName,
                    "lookup",
                    hoveredActionId,
                    StructuredTooltipContentKindAction,
                    displayMode,
                    reason: "action-payload-build-miss");
                this.ClearStructuredTooltipOverlay(
                    ActionDetailSurfaceName,
                    this.actionDetailOverlay,
                    reason: "action-payload-build-miss",
                    contentId: hoveredActionId,
                    contentKind: StructuredTooltipContentKindAction);
                return;
            }
        }
        else if (!TryBuildActionTooltipCanonicalPayload(
                     hoveredActionId,
                     currentClassJobId,
                     out originalPayload))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
            this.LogStructuredTooltipState(
                ActionDetailSurfaceName,
                "lookup",
                hoveredActionId,
                StructuredTooltipContentKindAction,
                displayMode: null,
                reason: "action-payload-build-miss");
            this.ClearStructuredTooltipOverlay(
                ActionDetailSurfaceName,
                this.actionDetailOverlay,
                reason: "action-payload-build-miss",
                contentId: hoveredActionId,
                contentKind: StructuredTooltipContentKindAction);
            return;
        }

        if (!this.TryFindTranslatedActionTooltipPayload(
                sourceLanguage,
                hoveredActionKind,
                usedStructuredActionPayload,
                originalPayload,
                originalReferencePayload,
                out var translatedPayload))
        {
            this.TryRequestActionDetailOnDemandPrefetch(
                sourceLanguage,
                translatedLookupReferenceId,
                hoveredActionKind,
                originalPayload,
                currentClassJobId,
                usedStructuredActionPayload);
            this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
            this.LogStructuredTooltipState(
                ActionDetailSurfaceName,
                "lookup",
                originalPayload.ActionId,
                StructuredTooltipContentKindAction,
                displayMode,
                reason: "translated-action-payload-missing",
                name: originalPayload.Name,
                description: originalPayload.Description);
            this.ClearStructuredTooltipOverlay(
                ActionDetailSurfaceName,
                this.actionDetailOverlay,
                reason: "translated-action-payload-missing",
                contentId: originalPayload.ActionId,
                contentKind: StructuredTooltipContentKindAction);
            return;
        }

        this.LogStructuredTooltipState(
            ActionDetailSurfaceName,
            "lookup",
            originalPayload.ActionId,
            StructuredTooltipContentKindAction,
            displayMode,
            reason: "translated-action-payload-ready",
            name: translatedPayload.TranslatedName ?? originalPayload.Name,
            description: translatedPayload.TranslatedDescription ?? originalPayload.Description);
        this.RestoreStructuredTooltipOriginalsIfContentChanged(
            ref this.currentActionDetailState,
            addon,
            originalPayload.ActionId,
            StructuredTooltipContentKindAction,
            originalPayload.ComputeSourceContentHash());
        if (RequiresStructuredTooltipLiveNameMatch(useOverlayOnly) &&
            !this.HasStructuredTooltipLiveNameMatch(
                addon,
                StructuredTooltipContentKindAction,
                originalPayload.Name,
                translatedPayload.TranslatedName))
        {
            this.ClearStructuredTooltipOverlay(
                ActionDetailSurfaceName,
                this.actionDetailOverlay,
                reason: "awaiting-live-name-match",
                contentId: originalPayload.ActionId,
                contentKind: StructuredTooltipContentKindAction);
            return;
        }

        var nativeApplySucceeded = false;
        if (useOverlayOnly)
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentActionDetailState, addon);
        }
        else
        {
            nativeApplySucceeded = this.ApplyStructuredActionTooltipNative(
                addon,
                originalPayload,
                translatedPayload);
        }

        if (ShouldShowStructuredTooltipOverlay(
                useOverlayOnly,
                useSwapOverlay,
                nativeApplySucceeded))
        {
            var overlayText = useSwapOverlay && nativeApplySucceeded
                ? originalPayload.BuildOriginalTooltipText()
                : translatedPayload.BuildTranslatedTooltipText();
            this.UpdateStructuredTooltipOverlay(
                ActionDetailSurfaceName,
                this.actionDetailOverlay,
                addon,
                originalPayload.ActionId,
                StructuredTooltipContentKindAction,
                overlayText);
        }
        else
        {
            this.ClearStructuredTooltipOverlay(
                ActionDetailSurfaceName,
                this.actionDetailOverlay,
                reason: "overlay-disabled-by-display-mode",
                contentId: originalPayload.ActionId,
                contentKind: StructuredTooltipContentKindAction);
        }
    }

    /// <summary>
    ///     Updates the live item-tooltip runtime.
    /// </summary>
    private void UpdateItemDetailUiRuntime()
    {
        if (!this.TryResolveLifecycleTooltipAddon(
                this.itemDetailLifecycleState,
                out var addon))
        {
            return;
        }

        var hoveredAction = GameGuiInterface.HoveredAction;
        var hoveredActionId = hoveredAction.ActionId;
        if (ShouldSuppressItemDetailDuringActionHover(
                hoveredActionId,
                this.actionDetailLifecycleState.IsActive))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemDetailState, addon);
            this.LogStructuredTooltipState(
                ItemDetailSurfaceName,
                "state",
                0,
                StructuredTooltipContentKindItem,
                displayMode: null,
                reason: "hovered-item-suppressed-by-action");
            this.ClearStructuredTooltipOverlay(
                ItemDetailSurfaceName,
                this.itemDetailOverlay,
                reason: "hovered-item-suppressed-by-action");
            return;
        }

        if (!RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(
                out var sourceLanguage))
        {
            return;
        }

        var hoveredItemKind = hoveredAction.DetailKind;
        var hoveredItemId = (uint)GameGuiInterface.HoveredItem;
        if (hoveredItemId == 0 &&
            ShouldUseItemDetailAgentFallback(
                hoveredItemId,
                hoveredActionId))
        {
            hoveredItemId = this.GetActiveItemDetailId();
        }

        if (hoveredItemId == 0)
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemDetailState, addon);
            this.LogStructuredTooltipState(
                ItemDetailSurfaceName,
                "state",
                0,
                StructuredTooltipContentKindItem,
                displayMode: null,
                reason: "hovered-item-missing");
            this.ClearStructuredTooltipOverlay(
                ItemDetailSurfaceName,
                this.itemDetailOverlay,
                reason: "hovered-item-missing");
            return;
        }

        if (!TryBuildItemTooltipCanonicalPayload(
                hoveredItemId,
                hoveredItemKind,
                out var originalPayload,
                out var itemSourceKind))
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemDetailState, addon);
            this.LogStructuredTooltipState(
                ItemDetailSurfaceName,
                "lookup",
                hoveredItemId,
                StructuredTooltipContentKindItem,
                displayMode: null,
                reason: "item-payload-build-miss");
            this.ClearStructuredTooltipOverlay(
                ItemDetailSurfaceName,
                this.itemDetailOverlay,
                reason: "item-payload-build-miss",
                contentId: hoveredItemId,
                contentKind: StructuredTooltipContentKindItem);
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.configuration.TooltipTranslationDisplayMode,
            this.configuration.OverlayOnlyLanguage);
        var useOverlayOnly =
            !TranslationDisplayModeHelper.WritesNativeTranslation(displayMode);
        var useSwapOverlay =
            TranslationDisplayModeHelper.ShowsOriginalTooltips(displayMode);

        if (!this.TryFindTranslatedItemTooltipPayload(
                sourceLanguage,
                itemSourceKind,
                originalPayload,
                out var translatedPayload))
        {
            this.TryRequestItemDetailOnDemandPrefetch(
                sourceLanguage,
                itemSourceKind,
                originalPayload);
            this.RestoreStructuredTooltipOriginals(ref this.currentItemDetailState, addon);
            this.LogStructuredTooltipState(
                ItemDetailSurfaceName,
                "lookup",
                originalPayload.ItemId,
                StructuredTooltipContentKindItem,
                displayMode,
                reason: "translated-item-payload-missing",
                name: originalPayload.Name,
                description: originalPayload.Description);
            this.ClearStructuredTooltipOverlay(
                ItemDetailSurfaceName,
                this.itemDetailOverlay,
                reason: "translated-item-payload-missing",
                contentId: originalPayload.ItemId,
                contentKind: StructuredTooltipContentKindItem);
            return;
        }

        this.LogStructuredTooltipState(
            ItemDetailSurfaceName,
            "lookup",
            originalPayload.ItemId,
            StructuredTooltipContentKindItem,
            displayMode,
            reason: "translated-item-payload-ready",
            name: translatedPayload.TranslatedName ?? originalPayload.Name,
            description: translatedPayload.TranslatedDescription ?? originalPayload.Description);
        this.RestoreStructuredTooltipOriginalsIfContentChanged(
            ref this.currentItemDetailState,
            addon,
            originalPayload.ItemId,
            StructuredTooltipContentKindItem,
            originalPayload.ComputeSourceContentHash());
        if (RequiresStructuredTooltipLiveNameMatch(useOverlayOnly) &&
            !this.HasStructuredTooltipLiveNameMatch(
                addon,
                StructuredTooltipContentKindItem,
                originalPayload.Name,
                translatedPayload.TranslatedName))
        {
            this.ClearStructuredTooltipOverlay(
                ItemDetailSurfaceName,
                this.itemDetailOverlay,
                reason: "awaiting-live-name-match",
                contentId: originalPayload.ItemId,
                contentKind: StructuredTooltipContentKindItem);
            return;
        }

        var nativeApplySucceeded = false;
        if (useOverlayOnly)
        {
            this.RestoreStructuredTooltipOriginals(ref this.currentItemDetailState, addon);
        }
        else
        {
            nativeApplySucceeded = this.ApplyStructuredItemTooltipNative(
                addon,
                originalPayload,
                translatedPayload);
        }

        if (ShouldShowStructuredTooltipOverlay(
                useOverlayOnly,
                useSwapOverlay,
                nativeApplySucceeded))
        {
            var overlayText = useSwapOverlay && nativeApplySucceeded
                ? originalPayload.BuildOriginalTooltipText()
                : translatedPayload.BuildTranslatedTooltipText();
            this.UpdateStructuredTooltipOverlay(
                ItemDetailSurfaceName,
                this.itemDetailOverlay,
                addon,
                originalPayload.ItemId,
                StructuredTooltipContentKindItem,
                overlayText);
        }
        else
        {
            this.ClearStructuredTooltipOverlay(
                ItemDetailSurfaceName,
                this.itemDetailOverlay,
                reason: "overlay-disabled-by-display-mode",
                contentId: originalPayload.ItemId,
                contentKind: StructuredTooltipContentKindItem);
        }
    }

    /// <summary>
    ///     Gets whether the DB-first tooltip runtime should execute.
    /// </summary>
    /// <returns><see langword="true" /> when the runtime should execute.</returns>
    private bool ShouldRunStructuredTooltipUiRuntime()
    {
        return this.configuration.Translate &&
               this.configuration.TranslateTooltips &&
               !GameGuiInterface.GameUiHidden &&
               FrameworkAccessGuard.IsClientReadyForPlayerScopedFrameworkAccess();
    }

    /// <summary>
    ///     Gets whether the current tooltip mode must prove that the live
    ///     tooltip still exposes the expected name before continuing.
    /// </summary>
    /// <param name="useOverlayOnly">
    ///     Whether the selected mode renders only our overlay and does not
    ///     write translated text into the native tooltip.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when live-name validation is required.
    /// </returns>
    internal static bool RequiresStructuredTooltipLiveNameMatch(
        bool useOverlayOnly)
    {
        return !useOverlayOnly;
    }

    /// <summary>
    ///     Gets whether a translated overlay must remain visible when native
    ///     tooltip mutation is unavailable for the current rendered payload.
    /// </summary>
    /// <param name="useOverlayOnly">Whether the selected mode is overlay-only.</param>
    /// <param name="useSwapOverlay">Whether the selected mode requests swap presentation.</param>
    /// <param name="nativeApplySucceeded">
    ///     Whether native presentation completed without omitting structured
    ///     tooltip fields.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the selected display mode requires a
    ///     plugin tooltip.
    /// </returns>
    internal static bool ShouldShowStructuredTooltipOverlay(
        bool useOverlayOnly,
        bool useSwapOverlay,
        bool nativeApplySucceeded)
    {
        return useOverlayOnly || useSwapOverlay;
    }

    /// <summary>
    ///     Queues non-blocking background work for one visible ActionDetail
    ///     payload that was not translated by the batch prefetch yet.
    /// </summary>
    /// <param name="sourceLanguage">The captured source identity.</param>
    /// <param name="referenceId">The action or structured reference row id.</param>
    /// <param name="hoverActionKind">The active hover action kind.</param>
    /// <param name="originalPayload">The canonical source payload.</param>
    /// <param name="currentClassJobId">The current class/job identifier.</param>
    /// <param name="usedStructuredActionPayload">
    ///     Whether the payload came from a structured reference-text sheet.
    /// </param>
    private void TryRequestActionDetailOnDemandPrefetch(
        SourceClientLanguage sourceLanguage,
        uint referenceId,
        DetailKind hoverActionKind,
        ActionTooltipCanonicalPayload originalPayload,
        byte currentClassJobId,
        bool usedStructuredActionPayload)
    {
        if (!this.ShouldPrefetchActionAdjacentCanonicalTooltips() ||
            !this.TryCreateCapturedTranslationScope(sourceLanguage, out var scope))
        {
            return;
        }

        var prefetchKey = BuildStructuredTooltipOnDemandPrefetchKey(
            ActionDetailSurfaceName,
            sourceLanguage,
            scope,
            referenceId,
            StructuredTooltipContentKindAction,
            originalPayload.ComputeSourceContentHash());
        if (!this.TryAcquireStructuredTooltipOnDemandPrefetchSlot(prefetchKey))
        {
            return;
        }

        if (usedStructuredActionPayload)
        {
            if (this.TryResolveReferenceTextPrefetchRegistration(
                    hoverActionKind,
                    out var registration))
            {
                this.PrefetchReferenceText(registration, referenceId);
            }

            return;
        }

        this.PrefetchActionDetail(originalPayload.ActionId, currentClassJobId);
    }

    /// <summary>
    ///     Queues non-blocking background work for one visible ItemDetail
    ///     payload that was not translated by the batch prefetch yet.
    /// </summary>
    /// <param name="sourceLanguage">The captured source identity.</param>
    /// <param name="sourceKind">The sheet family that produced the payload.</param>
    /// <param name="originalPayload">The canonical source payload.</param>
    private void TryRequestItemDetailOnDemandPrefetch(
        SourceClientLanguage sourceLanguage,
        StructuredTooltipItemSourceKind sourceKind,
        ItemTooltipCanonicalPayload originalPayload)
    {
        if (!this.ShouldPrefetchStructuredTooltips() ||
            !this.TryCreateCapturedTranslationScope(sourceLanguage, out var scope))
        {
            return;
        }

        var prefetchKey = BuildStructuredTooltipOnDemandPrefetchKey(
            ItemDetailSurfaceName,
            sourceLanguage,
            scope,
            originalPayload.ItemId,
            (uint)sourceKind,
            originalPayload.ComputeSourceContentHash());
        if (!this.TryAcquireStructuredTooltipOnDemandPrefetchSlot(prefetchKey))
        {
            return;
        }

        if (sourceKind == StructuredTooltipItemSourceKind.Item)
        {
            this.PrefetchItemDetail(originalPayload.ItemId);
            return;
        }

        if (this.TryResolveReferenceTextPrefetchRegistration(
                sourceKind,
                out var registration))
        {
            this.PrefetchReferenceText(registration, originalPayload.ItemId);
        }
    }

    /// <summary>
    ///     Builds a source-scoped identity for one tooltip on-demand prefetch
    ///     cooldown entry.
    /// </summary>
    /// <param name="surfaceName">The logical tooltip surface.</param>
    /// <param name="sourceLanguage">The captured source identity.</param>
    /// <param name="scope">The target and engine reuse scope.</param>
    /// <param name="contentId">The source content identifier.</param>
    /// <param name="contentKind">The source content family.</param>
    /// <param name="sourceHash">The canonical source-content hash.</param>
    /// <returns>The cooldown identity.</returns>
    private static string BuildStructuredTooltipOnDemandPrefetchKey(
        string surfaceName,
        SourceClientLanguage sourceLanguage,
        TranslationReuseScope scope,
        uint contentId,
        uint contentKind,
        string sourceHash)
    {
        var engineKey =
            scope.TranslationEngine?.ToString(CultureInfo.InvariantCulture) ??
            "<none>";
        return $"{surfaceName}|{sourceLanguage.PersistenceCode}|{scope.TargetLanguageCode}|{engineKey}|{contentKind}|{contentId}|{sourceHash}";
    }

    /// <summary>
    ///     Reserves one on-demand prefetch slot while suppressing per-frame
    ///     duplicate requests for the same tooltip payload.
    /// </summary>
    /// <param name="prefetchKey">The source-scoped payload identity.</param>
    /// <returns><see langword="true" /> when new work may be requested.</returns>
    private bool TryAcquireStructuredTooltipOnDemandPrefetchSlot(
        string prefetchKey)
    {
        var now = DateTime.UtcNow;
        if (this.structuredTooltipOnDemandPrefetchLastRequestedUtc.TryGetValue(
                prefetchKey,
                out var lastRequestedUtc) &&
            now - lastRequestedUtc < StructuredTooltipOnDemandPrefetchCooldown)
        {
            return false;
        }

        this.structuredTooltipOnDemandPrefetchLastRequestedUtc[prefetchKey] = now;
        if (this.structuredTooltipOnDemandPrefetchLastRequestedUtc.Count > 512)
        {
            foreach (var staleKey in this.structuredTooltipOnDemandPrefetchLastRequestedUtc
                         .Where(entry =>
                             now - entry.Value >
                             StructuredTooltipOnDemandPrefetchCooldown * 2)
                         .Select(entry => entry.Key)
                         .ToList())
            {
                this.structuredTooltipOnDemandPrefetchLastRequestedUtc.Remove(
                    staleKey);
            }
        }

        return true;
    }

    /// <summary>
    ///     Resolves the reference-text prefetch registration for one structured
    ///     action hover kind.
    /// </summary>
    /// <param name="hoverActionKind">The active hover action kind.</param>
    /// <param name="registration">The resolved registration.</param>
    /// <returns><see langword="true" /> when a registration is enabled.</returns>
    private bool TryResolveReferenceTextPrefetchRegistration(
        DetailKind hoverActionKind,
        out ReferenceTextPrefetchRegistration registration)
    {
        var key = hoverActionKind switch
        {
            DetailKind.GeneralAction => "GeneralActionPrefetch",
            DetailKind.BuddyAction or DetailKind.Companion or
                DetailKind.BuddyOrder => "BuddyActionPrefetch",
            DetailKind.CompanyAction => "CompanyActionPrefetch",
            DetailKind.CraftingAction => "CraftActionPrefetch",
            DetailKind.PetOrder => "PetActionPrefetch",
            DetailKind.Mount => "MountActionPrefetch",
            DetailKind.BgcArmyAction => "BgcArmyActionPrefetch",
            DetailKind.EurekaMagiaAction => "EurekaMagiaActionPrefetch",
            DetailKind.MainCommand or DetailKind.ExtraCommand =>
                "MainCommandPrefetch",
            _ => null,
        };

        return this.TryResolveReferenceTextPrefetchRegistration(
            key,
            out registration);
    }

    /// <summary>
    ///     Resolves the reference-text prefetch registration for one
    ///     item-adjacent tooltip family.
    /// </summary>
    /// <param name="sourceKind">The item source family.</param>
    /// <param name="registration">The resolved registration.</param>
    /// <returns><see langword="true" /> when a registration is enabled.</returns>
    private bool TryResolveReferenceTextPrefetchRegistration(
        StructuredTooltipItemSourceKind sourceKind,
        out ReferenceTextPrefetchRegistration registration)
    {
        var key = sourceKind switch
        {
            StructuredTooltipItemSourceKind.EventItem => "EventItemPrefetch",
            StructuredTooltipItemSourceKind.DeepDungeonItem =>
                "DeepDungeonItemPrefetch",
            _ => null,
        };

        return this.TryResolveReferenceTextPrefetchRegistration(
            key,
            out registration);
    }

    /// <summary>
    ///     Resolves one enabled reference-text prefetch registration by key.
    /// </summary>
    /// <param name="registrationKey">The registration key.</param>
    /// <param name="registration">The resolved registration.</param>
    /// <returns><see langword="true" /> when a registration is enabled.</returns>
    private bool TryResolveReferenceTextPrefetchRegistration(
        string? registrationKey,
        out ReferenceTextPrefetchRegistration registration)
    {
        registration = null!;
        if (string.IsNullOrWhiteSpace(registrationKey))
        {
            return false;
        }

        foreach (var candidate in this.GetReferenceTextPrefetchRegistrations())
        {
            if (string.Equals(
                    candidate.Key,
                    registrationKey,
                    StringComparison.Ordinal) &&
                candidate.IsEnabled())
            {
                registration = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Handles the shared structured-tooltip runtime transition from enabled
    ///     to disabled without redoing reset work every frame.
    /// </summary>
    private void HandleStructuredTooltipRuntimeDisabled()
    {
        if (!this.structuredTooltipRuntimeWasEnabled)
        {
            return;
        }

        this.structuredTooltipRuntimeWasEnabled = false;
        this.LogStructuredTooltipState(
            ActionDetailSurfaceName,
            "state",
            0,
            0,
            displayMode: null,
            reason: "runtime-disabled");
        this.LogStructuredTooltipState(
            ItemDetailSurfaceName,
            "state",
            0,
            0,
            displayMode: null,
            reason: "runtime-disabled");
        this.ResetStructuredTooltipUiRuntime();
    }

    /// <summary>
    ///     Registers one shared lifecycle listener for the structured tooltip
    ///     addons.
    /// </summary>
    private void RegisterStructuredTooltipLifecycleHandlers()
    {
        this.structuredTooltipLifecycleDelegate ??=
            new IAddonLifecycle.AddonEventDelegate(
                this.OnStructuredTooltipLifecycleEvent);
        foreach (var evt in StructuredTooltipLifecycleEvents)
        {
            AddonLifecycle.RegisterListener(
                evt,
                this.actionDetailLifecycleState.AddonNames,
                this.structuredTooltipLifecycleDelegate);
            AddonLifecycle.RegisterListener(
                evt,
                this.itemDetailLifecycleState.AddonNames,
                this.structuredTooltipLifecycleDelegate);
        }
    }

    /// <summary>
    ///     Unregisters the shared lifecycle listener for the structured tooltip
    ///     addons.
    /// </summary>
    private void UnregisterStructuredTooltipLifecycleHandlers()
    {
        if (this.structuredTooltipLifecycleDelegate == null)
        {
            return;
        }

        foreach (var evt in StructuredTooltipLifecycleEvents)
        {
            AddonLifecycle.UnregisterListener(
                evt,
                this.actionDetailLifecycleState.AddonNames,
                this.structuredTooltipLifecycleDelegate);
            AddonLifecycle.UnregisterListener(
                evt,
                this.itemDetailLifecycleState.AddonNames,
                this.structuredTooltipLifecycleDelegate);
        }
    }

    /// <summary>
    ///     Updates structured-tooltip addon activity from addon lifecycle events.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="args">The event arguments.</param>
    private void OnStructuredTooltipLifecycleEvent(AddonEvent evt, AddonArgs args)
    {
        if (this.actionDetailLifecycleState.Matches(args.AddonName))
        {
            this.HandleStructuredTooltipLifecycleEvent(
                this.actionDetailLifecycleState,
                evt);
            return;
        }

        if (this.itemDetailLifecycleState.Matches(args.AddonName))
        {
            this.HandleStructuredTooltipLifecycleEvent(
                this.itemDetailLifecycleState,
                evt);
        }
    }

    /// <summary>
    ///     Applies one lifecycle transition to one structured-tooltip surface.
    /// </summary>
    /// <param name="state">The tracked surface state.</param>
    /// <param name="evt">The lifecycle event.</param>
    private void HandleStructuredTooltipLifecycleEvent(
        StructuredTooltipLifecycleState state,
        AddonEvent evt)
    {
        if (evt is AddonEvent.PreHide or AddonEvent.PreFinalize)
        {
            state.MarkInactive();
            this.ResetStructuredTooltipSurfaceRuntime(state.SurfaceName);
            return;
        }

        state.MarkActive();
    }

    /// <summary>
    ///     Tries to resolve one tooltip addon, but only while lifecycle state says
    ///     the surface is active.
    /// </summary>
    /// <param name="state">The tracked surface state.</param>
    /// <param name="addon">The resolved visible addon.</param>
    /// <returns><see langword="true" /> when one visible addon was resolved.</returns>
    private bool TryResolveLifecycleTooltipAddon(
        StructuredTooltipLifecycleState state,
        out AtkUnitBase* addon)
    {
        addon = null;
        if (!state.IsActive)
        {
            return false;
        }

        if (this.TryResolveTooltipAddon(
                out addon,
                state.AddonNames))
        {
            return true;
        }

        state.MarkInactive();
        this.ResetStructuredTooltipSurfaceRuntime(state.SurfaceName);
        return false;
    }

    /// <summary>
    ///     Resets one structured-tooltip surface runtime immediately.
    /// </summary>
    /// <param name="surfaceName">The logical surface name.</param>
    private void ResetStructuredTooltipSurfaceRuntime(string surfaceName)
    {
        if (string.Equals(
                surfaceName,
                ActionDetailSurfaceName,
                StringComparison.Ordinal))
        {
            this.RestoreStructuredTooltipOriginals(
                ref this.currentActionDetailState,
                null);
            this.ClearOverlay(this.actionDetailOverlay, clearText: true);
            return;
        }

        if (string.Equals(
                surfaceName,
                ItemDetailSurfaceName,
                StringComparison.Ordinal))
        {
            this.RestoreStructuredTooltipOriginals(
                ref this.currentItemDetailState,
                null);
            this.ClearOverlay(this.itemDetailOverlay, clearText: true);
        }
    }

    /// <summary>
    ///     Tries to resolve one visible tooltip addon by name.
    /// </summary>
    /// <param name="addon">The resolved addon, if any.</param>
    /// <param name="addonNames">The candidate addon names.</param>
    /// <returns><see langword="true" /> when a visible addon was resolved.</returns>
    private bool TryResolveTooltipAddon(
        out AtkUnitBase* addon,
        params string[] addonNames)
    {
        addon = null;

        foreach (var addonName in addonNames)
        {
            var addonPtr = GameGuiInterface.GetAddonByName(addonName, 1);
            if (addonPtr.Address == IntPtr.Zero)
            {
                continue;
            }

            var resolvedAddon = (AtkUnitBase*)addonPtr.Address;
            if (!IsStructuredTooltipAddonReady(resolvedAddon))
            {
                continue;
            }

            addon = resolvedAddon;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets the active ActionDetail action id from the action-detail agent.
    /// </summary>
    /// <returns>The active action id, or zero when unavailable.</returns>
    private uint GetActiveActionDetailId()
    {
        var agent = AgentActionDetail.Instance();
        if (agent == null)
        {
            return 0;
        }

        return agent->ActionId != 0
            ? agent->ActionId
            : agent->OriginalId;
    }

    /// <summary>
    ///     Gets the active ItemDetail item id from the item-detail agent.
    /// </summary>
    /// <returns>The active item id, or zero when unavailable.</returns>
    private uint GetActiveItemDetailId()
    {
        var agent = AgentItemDetail.Instance();
        if (agent == null)
        {
            return 0;
        }

        return agent->ItemId;
    }

    /// <summary>
    ///     Tries to build one runtime action-detail payload from one
    ///     structured reference-text family when the standard
    ///     <c>Action</c>/<c>ActionTransient</c> path does not apply.
    /// </summary>
    /// <param name="referenceId">The hovered action or command identifier.</param>
    /// <param name="hoverActionKind">The active hover action kind.</param>
    /// <param name="currentClassJobId">The current class/job identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <param name="resolvedReferenceId">
    ///     The stable structured row identifier actually used for lookup.
    /// </param>
    /// <param name="referencePayload">
    ///     The original structured reference payload used to validate a cached
    ///     translation before it is displayed.
    /// </param>
    /// <returns><see langword="true" /> when one structured payload was built.</returns>
    private static bool TryBuildStructuredActionTooltipCanonicalPayload(
        uint referenceId,
        DetailKind hoverActionKind,
        byte currentClassJobId,
        out ActionTooltipCanonicalPayload payload,
        out uint resolvedReferenceId,
        out ReferenceTextCanonicalPayload? referencePayload)
    {
        payload = new ActionTooltipCanonicalPayload();
        resolvedReferenceId = referenceId;
        referencePayload = null;

        var normalizedReferenceId = NormalizeStructuredActionReferenceId(
            referenceId,
            hoverActionKind);
        var candidateReferenceIds = normalizedReferenceId != 0 &&
                                    normalizedReferenceId != referenceId
            ? new[] { normalizedReferenceId, referenceId }
            : [referenceId];

        foreach (var candidateReferenceId in candidateReferenceIds)
        {
            if (!TryBuildStructuredReferencePayload(
                    candidateReferenceId,
                    hoverActionKind,
                    out var candidateReferencePayload))
            {
                continue;
            }

            payload = CreateActionTooltipPayloadFromReferencePayload(
                candidateReferencePayload,
                currentClassJobId);
            resolvedReferenceId = candidateReferenceId;
            referencePayload = candidateReferencePayload;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one translated action-tooltip payload from
    ///     canonical storage.
    /// </summary>
    /// <param name="sourceLanguage">The operation-captured source identity.</param>
    /// <param name="hoverActionKind">The hovered action family.</param>
    /// <param name="usesStructuredActionPayload">
    ///     Whether the payload belongs to a structured reference-text family.
    /// </param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="originalReferencePayload">
    ///     The original structured reference payload, when the action belongs
    ///     to an action-adjacent sheet family.
    /// </param>
    /// <param name="translatedPayload">The translated payload, if any.</param>
    /// <returns><see langword="true" /> when a complete translation is available.</returns>
    private bool TryFindTranslatedActionTooltipPayload(
        SourceClientLanguage sourceLanguage,
        DetailKind hoverActionKind,
        bool usesStructuredActionPayload,
        ActionTooltipCanonicalPayload originalPayload,
        ReferenceTextCanonicalPayload? originalReferencePayload,
        out ActionTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = new ActionTooltipCanonicalPayload();

        var targetLanguage = LangDict[LanguageInt].Code;
        var gameVersion = GetGameVersion();
        var engine = this.configuration.ChosenTransEngine;
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            engine,
            this.configuration.TranslateAlreadyTranslatedTexts);
        if (usesStructuredActionPayload && originalReferencePayload != null)
        {
            return ReferenceTextCacheRegistry.TryFindTranslatedActionCanonicalPayload(
                       hoverActionKind,
                       originalReferencePayload,
                       scope,
                       gameVersion,
                       out var translatedReferencePayload) &&
                   TryBuildTranslatedActionTooltipPayloadFromReferencePayload(
                       translatedReferencePayload,
                       originalPayload,
                       out translatedPayload);
        }

        var row = ActionTooltipCacheManager.TryFindCanonicalMatch(
            originalPayload.ActionId,
            scope,
            gameVersion,
            originalPayload.ComputeSourceContentHash());
        if (DbFirstGameWindowAddonHandler.MatchesPersistedSourceIdentity(
                row?.OriginalLang,
                sourceLanguage) &&
            TryBuildTranslatedActionTooltipPayloadFromRow(
                row,
                originalPayload,
                out translatedPayload))
        {
            return true;
        }

        var probe = ActionTooltipPersistenceHelper.CreateCanonicalRow(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            engine,
            gameVersion,
            originalPayload);

        row = this.FindActionTooltip(probe);
        if (DbFirstGameWindowAddonHandler.MatchesPersistedSourceIdentity(
                row?.OriginalLang,
                sourceLanguage) &&
            TryBuildTranslatedActionTooltipPayloadFromRow(
                row,
                originalPayload,
                out translatedPayload))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one translated trait payload from canonical
    ///     storage.
    /// </summary>
    /// <param name="sourceLanguage">The operation-captured source identity.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated payload, if any.</param>
    /// <returns><see langword="true" /> when a complete translation is available.</returns>
    private bool TryFindTranslatedTraitTooltipPayload(
        SourceClientLanguage sourceLanguage,
        TraitCanonicalPayload originalPayload,
        out TraitCanonicalPayload translatedPayload)
    {
        translatedPayload = new TraitCanonicalPayload();

        var targetLanguage = LangDict[LanguageInt].Code;
        var gameVersion = GetGameVersion();
        var engine = this.configuration.ChosenTransEngine;
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            engine,
            this.configuration.TranslateAlreadyTranslatedTexts);
        var row = TraitCacheManager.TryFindCanonicalMatch(
            originalPayload.TraitId,
            scope,
            gameVersion,
            originalPayload.ComputeSourceContentHash());
        if (DbFirstGameWindowAddonHandler.MatchesPersistedSourceIdentity(
                row?.OriginalLang,
                sourceLanguage) &&
            TryBuildTranslatedTraitTooltipPayloadFromRow(
                row,
                originalPayload,
                out translatedPayload))
        {
            return true;
        }

        var probe = TraitPersistenceHelper.CreateCanonicalRow(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            engine,
            gameVersion,
            originalPayload);

        row = this.FindTrait(probe);
        return DbFirstGameWindowAddonHandler.MatchesPersistedSourceIdentity(
                   row?.OriginalLang,
                   sourceLanguage) &&
               TryBuildTranslatedTraitTooltipPayloadFromRow(
                   row,
                   originalPayload,
                   out translatedPayload);
    }

    /// <summary>
    ///     Tries to resolve one translated item-tooltip payload from canonical
    ///     storage.
    /// </summary>
    /// <param name="sourceLanguage">The operation-captured source identity.</param>
    /// <param name="sourceKind">The sheet family that produced the payload.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated payload, if any.</param>
    /// <returns><see langword="true" /> when a complete translation is available.</returns>
    private bool TryFindTranslatedItemTooltipPayload(
        SourceClientLanguage sourceLanguage,
        StructuredTooltipItemSourceKind sourceKind,
        ItemTooltipCanonicalPayload originalPayload,
        out ItemTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = new ItemTooltipCanonicalPayload();

        var targetLanguage = LangDict[LanguageInt].Code;
        var gameVersion = GetGameVersion();
        var engine = this.configuration.ChosenTransEngine;
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            targetLanguage,
            engine,
            this.configuration.TranslateAlreadyTranslatedTexts);
        if (sourceKind == StructuredTooltipItemSourceKind.Item)
        {
            var row = ItemTooltipCacheManager.TryFindCanonicalMatch(
                originalPayload.ItemId,
                scope,
                gameVersion,
                originalPayload.ComputeSourceContentHash());
            if (DbFirstGameWindowAddonHandler.MatchesPersistedSourceIdentity(
                    row?.OriginalLang,
                    sourceLanguage) &&
                TryBuildTranslatedItemTooltipPayloadFromRow(
                    row,
                    originalPayload,
                    out translatedPayload))
            {
                return true;
            }

            var probe = ItemTooltipPersistenceHelper.CreateCanonicalRow(
                sourceLanguage.PersistenceCode,
                targetLanguage,
                engine,
                gameVersion,
                originalPayload);

            row = this.FindItemTooltip(probe);
            return DbFirstGameWindowAddonHandler.MatchesPersistedSourceIdentity(
                       row?.OriginalLang,
                       sourceLanguage) &&
                   TryBuildTranslatedItemTooltipPayloadFromRow(
                       row,
                       originalPayload,
                       out translatedPayload);
        }

        return (sourceKind is StructuredTooltipItemSourceKind.EventItem or
            StructuredTooltipItemSourceKind.DeepDungeonItem) &&
            ReferenceTextCacheRegistry.TryFindTranslatedItemCanonicalPayload(
                CreateStructuredItemReferencePayload(originalPayload),
                scope,
                gameVersion,
                out var translatedReferencePayload) &&
            TryBuildTranslatedItemTooltipPayloadFromReferencePayload(
                translatedReferencePayload,
                originalPayload,
                out translatedPayload);
    }

    /// <summary>
    ///     Creates the reference-text source identity corresponding to one
    ///     EventItem or DeepDungeonItem tooltip payload.
    /// </summary>
    /// <param name="itemPayload">The original ItemDetail payload.</param>
    /// <returns>The matching reference-text payload.</returns>
    private static ReferenceTextCanonicalPayload
        CreateStructuredItemReferencePayload(
            ItemTooltipCanonicalPayload itemPayload)
    {
        return new ReferenceTextCanonicalPayload
        {
            ReferenceId = itemPayload.ItemId,
            ActionId = itemPayload.ItemActionId == 0
                ? null
                : itemPayload.ItemActionId,
            IconId = itemPayload.IconId == 0 ? null : itemPayload.IconId,
            Name = itemPayload.Name,
            Description = string.IsNullOrWhiteSpace(itemPayload.Description)
                ? null
                : itemPayload.Description,
        };
    }

    /// <summary>
    ///     Tries to build one translated action payload from one stored row.
    /// </summary>
    /// <param name="row">The stored row, if any.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns><see langword="true" /> when a complete translated payload was built.</returns>
    private static bool TryBuildTranslatedActionTooltipPayloadFromRow(
        ActionTooltip? row,
        ActionTooltipCanonicalPayload originalPayload,
        out ActionTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = new ActionTooltipCanonicalPayload();
        if (row == null)
        {
            return false;
        }

        var resolvedPayload = ActionTooltipCanonicalPayload.Deserialize(
            row.CanonicalPayloadAsText);
        if (resolvedPayload?.HasCompleteTranslation == true)
        {
            translatedPayload = resolvedPayload;
            return true;
        }

        translatedPayload = CreateTranslatedActionTooltipPayload(
            originalPayload,
            row.TranslatedActionName,
            row.TranslatedActionDescription);
        return translatedPayload.HasCompleteTranslation;
    }

    /// <summary>
    ///     Tries to build one translated action payload from one translated
    ///     structured reference payload.
    /// </summary>
    /// <param name="referencePayload">The translated reference payload.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns><see langword="true" /> when a complete translated payload was built.</returns>
    private static bool TryBuildTranslatedActionTooltipPayloadFromReferencePayload(
        ReferenceTextCanonicalPayload referencePayload,
        ActionTooltipCanonicalPayload originalPayload,
        out ActionTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = CreateTranslatedActionTooltipPayload(
            originalPayload,
            referencePayload.TranslatedName,
            referencePayload.TranslatedDescription);
        return translatedPayload.HasCompleteTranslation;
    }

    /// <summary>
    ///     Tries to build one translated trait payload from one stored row.
    /// </summary>
    /// <param name="row">The stored row, if any.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns><see langword="true" /> when a complete translated payload was built.</returns>
    private static bool TryBuildTranslatedTraitTooltipPayloadFromRow(
        Trait? row,
        TraitCanonicalPayload originalPayload,
        out TraitCanonicalPayload translatedPayload)
    {
        translatedPayload = new TraitCanonicalPayload();
        if (row == null)
        {
            return false;
        }

        var resolvedPayload = TraitCanonicalPayload.Deserialize(
            row.CanonicalPayloadAsText);
        if (resolvedPayload?.HasCompleteTranslation == true)
        {
            translatedPayload = resolvedPayload;
            return true;
        }

        translatedPayload = CreateTranslatedTraitTooltipPayload(
            originalPayload,
            row.TranslatedTraitName,
            row.TranslatedTraitDescription);
        return translatedPayload.HasCompleteTranslation;
    }

    /// <summary>
    ///     Tries to build one translated item payload from one stored row.
    /// </summary>
    /// <param name="row">The stored row, if any.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns><see langword="true" /> when a complete translated payload was built.</returns>
    private static bool TryBuildTranslatedItemTooltipPayloadFromRow(
        ItemTooltip? row,
        ItemTooltipCanonicalPayload originalPayload,
        out ItemTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = new ItemTooltipCanonicalPayload();
        if (row == null)
        {
            return false;
        }

        var resolvedPayload = ItemTooltipCanonicalPayload.Deserialize(
            row.CanonicalPayloadAsText);
        if (resolvedPayload?.HasCompleteTranslation == true)
        {
            translatedPayload = resolvedPayload;
            return true;
        }

        translatedPayload = CreateTranslatedItemTooltipPayload(
            originalPayload,
            row.TranslatedItemName,
            row.TranslatedItemDescription);
        return translatedPayload.HasCompleteTranslation;
    }

    /// <summary>
    ///     Tries to build one translated item payload from one translated
    ///     structured reference payload.
    /// </summary>
    /// <param name="referencePayload">The translated reference payload.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns><see langword="true" /> when a complete translated payload was built.</returns>
    private static bool TryBuildTranslatedItemTooltipPayloadFromReferencePayload(
        ReferenceTextCanonicalPayload referencePayload,
        ItemTooltipCanonicalPayload originalPayload,
        out ItemTooltipCanonicalPayload translatedPayload)
    {
        translatedPayload = CreateTranslatedItemTooltipPayload(
            originalPayload,
            referencePayload.TranslatedName,
            referencePayload.TranslatedDescription);
        return translatedPayload.HasCompleteTranslation;
    }

    /// <summary>
    ///     Creates one translated action payload by layering translated fields
    ///     over the canonical original payload.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    /// <returns>The layered payload.</returns>
    private static ActionTooltipCanonicalPayload CreateTranslatedActionTooltipPayload(
        ActionTooltipCanonicalPayload originalPayload,
        string? translatedName,
        string? translatedDescription)
    {
        return new ActionTooltipCanonicalPayload
        {
            SchemaVersion = originalPayload.SchemaVersion,
            ActionId = originalPayload.ActionId,
            IconId = originalPayload.IconId,
            ActionCategoryId = originalPayload.ActionCategoryId,
            ClassJobId = originalPayload.ClassJobId,
            ClassJobCategoryId = originalPayload.ClassJobCategoryId,
            Name = originalPayload.Name,
            Description = originalPayload.Description,
            TranslatedName = translatedName,
            TranslatedDescription = string.IsNullOrWhiteSpace(
                originalPayload.Description)
                ? null
                : translatedDescription,
        };
    }

    /// <summary>
    ///     Creates one translated trait payload by layering translated fields
    ///     over the canonical original payload.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    /// <returns>The layered payload.</returns>
    private static TraitCanonicalPayload CreateTranslatedTraitTooltipPayload(
        TraitCanonicalPayload originalPayload,
        string? translatedName,
        string? translatedDescription)
    {
        return new TraitCanonicalPayload
        {
            SchemaVersion = originalPayload.SchemaVersion,
            TraitId = originalPayload.TraitId,
            IconId = originalPayload.IconId,
            ClassJobId = originalPayload.ClassJobId,
            ClassJobCategoryId = originalPayload.ClassJobCategoryId,
            Name = originalPayload.Name,
            Description = originalPayload.Description,
            TranslatedName = translatedName,
            TranslatedDescription = string.IsNullOrWhiteSpace(
                originalPayload.Description)
                ? null
                : translatedDescription,
        };
    }

    /// <summary>
    ///     Creates one translated item payload by layering translated fields
    ///     over the canonical original payload.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    /// <returns>The layered payload.</returns>
    private static ItemTooltipCanonicalPayload CreateTranslatedItemTooltipPayload(
        ItemTooltipCanonicalPayload originalPayload,
        string? translatedName,
        string? translatedDescription)
    {
        return new ItemTooltipCanonicalPayload
        {
            SchemaVersion = originalPayload.SchemaVersion,
            ItemId = originalPayload.ItemId,
            IconId = originalPayload.IconId,
            ItemActionId = originalPayload.ItemActionId,
            ItemUiCategoryId = originalPayload.ItemUiCategoryId,
            ClassJobCategoryId = originalPayload.ClassJobCategoryId,
            Name = originalPayload.Name,
            Description = originalPayload.Description,
            TranslatedName = translatedName,
            TranslatedDescription = string.IsNullOrWhiteSpace(
                originalPayload.Description)
                ? null
                : translatedDescription,
        };
    }

    /// <summary>
    ///     Creates one runtime action-detail payload from one structured
    ///     reference payload.
    /// </summary>
    /// <param name="referencePayload">The structured reference payload.</param>
    /// <param name="currentClassJobId">The current class/job identifier.</param>
    /// <returns>The converted runtime payload.</returns>
    internal static ActionTooltipCanonicalPayload CreateActionTooltipPayloadFromReferencePayload(
        ReferenceTextCanonicalPayload referencePayload,
        byte currentClassJobId)
    {
        return new ActionTooltipCanonicalPayload
        {
            ActionId = referencePayload.ActionId ?? referencePayload.ReferenceId,
            IconId = referencePayload.IconId ?? 0,
            ActionCategoryId = 0,
            ClassJobId = currentClassJobId,
            ClassJobCategoryId = 0,
            Name = referencePayload.Name,
            Description = referencePayload.Description ?? string.Empty,
        };
    }

    /// <summary>
    ///     Applies translated native text for the action tooltip when safe to do so.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated canonical payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the complete tooltip was safely
    ///     presented through native nodes.
    /// </returns>
    private bool ApplyStructuredActionTooltipNative(
        AtkUnitBase* addon,
        ActionTooltipCanonicalPayload originalPayload,
        ActionTooltipCanonicalPayload translatedPayload)
    {
        return this.ApplyStructuredTooltipNative(
            ActionDetailSurfaceName,
            addon,
            originalPayload.ActionId,
            StructuredTooltipContentKindAction,
            originalPayload.ComputeSourceContentHash(),
            originalPayload.Name,
            originalPayload.Description,
            translatedPayload.TranslatedName ?? originalPayload.Name,
            translatedPayload.TranslatedDescription ?? originalPayload.Description,
            ref this.currentActionDetailState);
    }

    /// <summary>
    ///     Applies translated native text for the trait tooltip when safe to do so.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated canonical payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the complete tooltip was safely
    ///     presented through native nodes.
    /// </returns>
    private bool ApplyStructuredTraitTooltipNative(
        AtkUnitBase* addon,
        TraitCanonicalPayload originalPayload,
        TraitCanonicalPayload translatedPayload)
    {
        return this.ApplyStructuredTooltipNative(
            ActionDetailSurfaceName,
            addon,
            originalPayload.TraitId,
            StructuredTooltipContentKindTrait,
            originalPayload.ComputeSourceContentHash(),
            originalPayload.Name,
            originalPayload.Description,
            translatedPayload.TranslatedName ?? originalPayload.Name,
            translatedPayload.TranslatedDescription ?? originalPayload.Description,
            ref this.currentActionDetailState);
    }

    /// <summary>
    ///     Applies translated native text for the item tooltip when safe to do so.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="originalPayload">The original canonical payload.</param>
    /// <param name="translatedPayload">The translated canonical payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the complete tooltip was safely
    ///     presented through native nodes.
    /// </returns>
    private bool ApplyStructuredItemTooltipNative(
        AtkUnitBase* addon,
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltipCanonicalPayload translatedPayload)
    {
        return this.ApplyStructuredTooltipNative(
            ItemDetailSurfaceName,
            addon,
            originalPayload.ItemId,
            StructuredTooltipContentKindItem,
            originalPayload.ComputeSourceContentHash(),
            originalPayload.Name,
            originalPayload.Description,
            translatedPayload.TranslatedName ?? originalPayload.Name,
            translatedPayload.TranslatedDescription ?? originalPayload.Description,
            ref this.currentItemDetailState);
    }

    /// <summary>
    ///     Applies translated native text to the name and description nodes of a tooltip.
    /// </summary>
    /// <param name="surfaceName">The logical surface name.</param>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="contentKind">The logical content kind.</param>
    /// <param name="sourceContentHash">The canonical source-payload identity.</param>
    /// <param name="originalName">The original name text.</param>
    /// <param name="originalDescription">The original description text.</param>
    /// <param name="translatedName">The translated name text.</param>
    /// <param name="translatedDescription">The translated description text.</param>
    /// <param name="runtimeState">The active native-runtime state.</param>
    /// <returns>
    ///     <see langword="true" /> when the complete tooltip was safely
    ///     presented through native nodes.
    /// </returns>
    private bool ApplyStructuredTooltipNative(
        string surfaceName,
        AtkUnitBase* addon,
        uint contentId,
        uint contentKind,
        string sourceContentHash,
        string originalName,
        string originalDescription,
        string translatedName,
        string translatedDescription,
        ref StructuredTooltipNativeState? runtimeState)
    {
        if (!IsStructuredTooltipAddonReady(addon) || contentId == 0)
        {
            this.LogStructuredTooltipState(
                surfaceName,
                "native-apply",
                contentId,
                contentKind,
                displayMode: null,
                reason: "addon-not-ready-or-content-missing");
            this.RestoreStructuredTooltipOriginals(ref runtimeState, addon);
            return false;
        }

        if (runtimeState != null &&
            ((nint)addon != runtimeState.AddonAddress ||
             !HasStructuredTooltipContentIdentity(
                 runtimeState.ContentId,
                 runtimeState.ContentKind,
                 runtimeState.SourceContentHash,
                 contentId,
                 contentKind,
                 sourceContentHash)))
        {
            this.RestoreStructuredTooltipOriginals(ref runtimeState, addon);
        }

        if (!this.TryEnsureStructuredTooltipNodeAddresses(
                addon,
                contentId,
                contentKind,
                sourceContentHash,
                originalName,
                originalDescription,
                translatedName,
                ref runtimeState))
        {
            this.LogStructuredTooltipState(
                surfaceName,
                "native-apply",
                contentId,
                contentKind,
                displayMode: null,
                reason: "node-resolution-miss",
                name: originalName,
                description: originalDescription);
            return false;
        }

        var currentRuntimeState = runtimeState;
        if (currentRuntimeState == null)
        {
            this.LogStructuredTooltipState(
                surfaceName,
                "native-apply",
                contentId,
                contentKind,
                displayMode: null,
                reason: "node-resolution-state-missing",
                name: originalName,
                description: originalDescription);
            return false;
        }

        var descriptionExpected = !string.IsNullOrWhiteSpace(originalDescription);
        if (!CanApplyStructuredTooltipNative(
                descriptionExpected,
                currentRuntimeState.NameNodeAddress != 0,
                currentRuntimeState.NameNodeSupportsPlainTextMutation,
                currentRuntimeState.DescriptionNodeAddress != 0,
                currentRuntimeState.DescriptionNodeSupportsPlainTextMutation))
        {
            this.LogStructuredTooltipState(
                surfaceName,
                "native-apply",
                contentId,
                contentKind,
                displayMode: null,
                reason: GetStructuredTooltipNativeApplyDeferredReason(
                    descriptionExpected,
                    currentRuntimeState),
                name: originalName,
                description: originalDescription,
                nameNodeAddress: currentRuntimeState.NameNodeAddress,
                descriptionNodeAddress: currentRuntimeState.DescriptionNodeAddress);
            return false;
        }

        var nameApplied = false;
        var descriptionApplied = false;

        if (currentRuntimeState.NameNodeAddress != 0)
        {
            var nameNode = (AtkTextNode*)currentRuntimeState.NameNodeAddress;
            if (nameNode != null &&
                !this.DoesStructuredTooltipNodeMatchTarget(
                    nameNode,
                    translatedName))
            {
                nameNode->SetText(translatedName);
                nameApplied = true;
                currentRuntimeState = currentRuntimeState with
                {
                    NameWasMutated = true,
                    AppliedNameText = translatedName,
                };
                runtimeState = currentRuntimeState;
            }
        }

        if (currentRuntimeState.DescriptionNodeAddress != 0 &&
            !string.IsNullOrWhiteSpace(originalDescription))
        {
            var descriptionNode = (AtkTextNode*)currentRuntimeState.DescriptionNodeAddress;
            if (descriptionNode != null &&
                !this.DoesStructuredTooltipNodeMatchTarget(
                    descriptionNode,
                    translatedDescription))
            {
                descriptionNode->SetText(translatedDescription);
                descriptionApplied = true;
                currentRuntimeState = currentRuntimeState with
                {
                    DescriptionWasMutated = true,
                    AppliedDescriptionText = translatedDescription,
                };
                runtimeState = currentRuntimeState;
            }
        }

        this.LogStructuredTooltipState(
            surfaceName,
            "native-apply",
            contentId,
            contentKind,
            displayMode: null,
            reason: "native-apply-attempted",
            name: translatedName,
            description: translatedDescription,
            nameNodeAddress: currentRuntimeState.NameNodeAddress,
            descriptionNodeAddress: currentRuntimeState.DescriptionNodeAddress,
            nameApplied: nameApplied,
            descriptionApplied: descriptionApplied);
        return true;
    }

    /// <summary>
    ///     Restores prior native mutations immediately when the visible tooltip
    ///     surface transitions to different logical content on the same addon.
    /// </summary>
    /// <param name="runtimeState">The active native-runtime state.</param>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="contentKind">The logical content kind.</param>
    /// <param name="sourceContentHash">The canonical source-payload identity.</param>
    private void RestoreStructuredTooltipOriginalsIfContentChanged(
        ref StructuredTooltipNativeState? runtimeState,
        AtkUnitBase* addon,
        uint contentId,
        uint contentKind,
        string sourceContentHash)
    {
        if (runtimeState == null)
        {
            return;
        }

        if ((nint)addon != runtimeState.AddonAddress ||
            !HasStructuredTooltipContentIdentity(
                runtimeState.ContentId,
                runtimeState.ContentKind,
                runtimeState.SourceContentHash,
                contentId,
                contentKind,
                sourceContentHash))
        {
            this.RestoreStructuredTooltipOriginals(ref runtimeState, addon);
        }
    }

    /// <summary>
    ///     Ensures the native runtime has the best currently available tooltip
    ///     node addresses for the active content, enriching partial state after
    ///     the addon finishes populating its late text nodes.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="contentKind">The logical content kind.</param>
    /// <param name="sourceContentHash">The canonical source-payload identity.</param>
    /// <param name="originalName">The canonical original name.</param>
    /// <param name="originalDescription">The canonical original description.</param>
    /// <param name="translatedName">
    ///     The translated live name, if the tooltip was already mutated.
    /// </param>
    /// <param name="runtimeState">The current runtime state to enrich.</param>
    /// <returns><see langword="true" /> when at least one relevant node is available.</returns>
    private bool TryEnsureStructuredTooltipNodeAddresses(
        AtkUnitBase* addon,
        uint contentId,
        uint contentKind,
        string sourceContentHash,
        string originalName,
        string originalDescription,
        string translatedName,
        ref StructuredTooltipNativeState? runtimeState)
    {
        if (runtimeState == null)
        {
            if (!this.TryResolveTooltipNodeAddresses(
                    addon,
                    contentId,
                    contentKind,
                    sourceContentHash,
                    originalName,
                    originalDescription,
                    translatedName,
                    out var resolvedRuntimeState))
            {
                return false;
            }

            runtimeState = resolvedRuntimeState;
            return true;
        }

        if (!this.AreStructuredTooltipRuntimeNodesCurrent(addon, runtimeState))
        {
            runtimeState = null;
            return this.TryEnsureStructuredTooltipNodeAddresses(
                addon,
                contentId,
                contentKind,
                sourceContentHash,
                originalName,
                originalDescription,
                translatedName,
                ref runtimeState);
        }

        var needsNameNode = runtimeState.NameNodeAddress == 0 ||
                            !runtimeState.NameNodeSupportsPlainTextMutation;
        var needsDescriptionNode =
            (runtimeState.DescriptionNodeAddress == 0 ||
             !runtimeState.DescriptionNodeSupportsPlainTextMutation) &&
            !string.IsNullOrWhiteSpace(originalDescription);
        if (!needsNameNode && !needsDescriptionNode)
        {
            return true;
        }

        var textNodeCandidates = this.CollectStructuredTooltipTextNodeCandidates(
            addon,
            contentKind);
        if (textNodeCandidates.Count == 0)
        {
            return runtimeState.NameNodeAddress != 0 ||
                   runtimeState.DescriptionNodeAddress != 0;
        }

        if (needsNameNode &&
            this.TryFindStructuredTooltipNameNodeCandidate(
                textNodeCandidates,
                originalName,
                translatedName,
                runtimeState.DescriptionNodeAddress,
                out var nameCandidate))
        {
            runtimeState = runtimeState with
            {
                NameNodeAddress = nameCandidate.NodeAddress,
                OriginalNameText = GetStructuredTooltipSafeRestoreText(
                    nameCandidate.VisibleText,
                    originalName),
                NameNodeSupportsPlainTextMutation = nameCandidate.SupportsPlainTextMutation,
            };
        }

        if (needsDescriptionNode &&
            TryFindBestStructuredTooltipDescriptionNodeCandidate(
                textNodeCandidates,
                originalDescription,
                runtimeState.NameNodeAddress,
                out var descriptionCandidate))
        {
            runtimeState = runtimeState with
            {
                DescriptionNodeAddress = descriptionCandidate.NodeAddress,
                OriginalDescriptionText = GetStructuredTooltipSafeRestoreText(
                    descriptionCandidate.VisibleText,
                    originalDescription),
                DescriptionNodeSupportsPlainTextMutation = descriptionCandidate.SupportsPlainTextMutation,
            };
        }

        return runtimeState.NameNodeAddress != 0 ||
               runtimeState.DescriptionNodeAddress != 0;
    }

    /// <summary>
    ///     Gets whether the structured-tooltip native path can safely mutate
    ///     the active tooltip without partial or markup-corrupt writes.
    /// </summary>
    /// <param name="descriptionExpected">
    ///     Whether the canonical payload provides a description that can be
    ///     matched against the visible tooltip.
    /// </param>
    /// <param name="nameNodeResolved">Whether the name node was resolved.</param>
    /// <param name="nameNodeSupportsPlainTextMutation">Whether the name node can be safely rewritten as plain text.</param>
    /// <param name="descriptionNodeResolved">Whether the description node was resolved.</param>
    /// <param name="descriptionNodeSupportsPlainTextMutation">Whether the description node can be safely rewritten as plain text.</param>
    /// <returns><see langword="true" /> when native mutation is safe to perform atomically.</returns>
    internal static bool CanApplyStructuredTooltipNative(
        bool descriptionExpected,
        bool nameNodeResolved,
        bool nameNodeSupportsPlainTextMutation,
        bool descriptionNodeResolved,
        bool descriptionNodeSupportsPlainTextMutation)
    {
        if (!descriptionExpected ||
            !nameNodeResolved ||
            !nameNodeSupportsPlainTextMutation)
        {
            return false;
        }

        return descriptionNodeResolved &&
               descriptionNodeSupportsPlainTextMutation;
    }

    /// <summary>
    ///     Gets whether cached tooltip text-node addresses are still present in
    ///     the current addon tree.
    /// </summary>
    /// <param name="currentNodeAddresses">The text-node addresses visible in the current tree.</param>
    /// <param name="nameNodeAddress">The cached name-node address.</param>
    /// <param name="descriptionNodeAddress">The cached description-node address.</param>
    /// <returns><see langword="true" /> when every cached non-zero address is current.</returns>
    internal static bool AreStructuredTooltipNodeAddressesCurrent(
        IReadOnlySet<nint> currentNodeAddresses,
        nint nameNodeAddress,
        nint descriptionNodeAddress)
    {
        if (currentNodeAddresses.Count == 0 ||
            (nameNodeAddress == 0 && descriptionNodeAddress == 0))
        {
            return false;
        }

        return IsStructuredTooltipNodeAddressCurrent(
                   currentNodeAddresses,
                   nameNodeAddress) &&
               IsStructuredTooltipNodeAddressCurrent(
                   currentNodeAddresses,
                   descriptionNodeAddress);
    }

    /// <summary>
    ///     Gets whether one cached tooltip node address is either unused or
    ///     still present in the current addon tree.
    /// </summary>
    /// <param name="currentNodeAddresses">The current text-node addresses.</param>
    /// <param name="nodeAddress">The cached node address.</param>
    /// <returns><see langword="true" /> when the address is safe to reuse.</returns>
    private static bool IsStructuredTooltipNodeAddressCurrent(
        IReadOnlySet<nint> currentNodeAddresses,
        nint nodeAddress)
    {
        return nodeAddress == 0 || currentNodeAddresses.Contains(nodeAddress);
    }

    /// <summary>
    ///     Gets whether one tooltip addon is ready for native tree traversal or
    ///     mutation.
    /// </summary>
    /// <param name="addon">The candidate tooltip addon.</param>
    /// <returns><see langword="true" /> when the addon is visible and ready.</returns>
    private static bool IsStructuredTooltipAddonReady(AtkUnitBase* addon)
    {
        return addon != null &&
               addon->IsReady &&
               addon->IsVisible &&
               addon->RootNode != null &&
               addon->UldManager.NodeList != null;
    }

    /// <summary>
    ///     Gets the compact reason describing why native structured-tooltip
    ///     mutation was deferred for the current frame.
    /// </summary>
    /// <param name="descriptionExpected">Whether the tooltip is expected to have a description node.</param>
    /// <param name="runtimeState">The current runtime state.</param>
    /// <returns>The compact deferred-apply reason.</returns>
    private static string GetStructuredTooltipNativeApplyDeferredReason(
        bool descriptionExpected,
        StructuredTooltipNativeState runtimeState)
    {
        if (runtimeState.NameNodeAddress == 0)
        {
            return "awaiting-name-node";
        }

        if (!runtimeState.NameNodeSupportsPlainTextMutation)
        {
            return "unsafe-name-node-format";
        }

        if (descriptionExpected && runtimeState.DescriptionNodeAddress == 0)
        {
            return "awaiting-description-node";
        }

        if (descriptionExpected &&
            !runtimeState.DescriptionNodeSupportsPlainTextMutation)
        {
            return "unsafe-description-node-format";
        }

        return "native-apply-deferred";
    }

    /// <summary>
    ///     Restores original tooltip text when the runtime previously mutated native nodes.
    /// </summary>
    /// <param name="runtimeState">The runtime state to restore.</param>
    /// <param name="addon">The current visible tooltip addon, if any.</param>
    private void RestoreStructuredTooltipOriginals(
        ref StructuredTooltipNativeState? runtimeState,
        AtkUnitBase* addon)
    {
        if (runtimeState == null)
        {
            return;
        }

        if (!IsStructuredTooltipAddonReady(addon) ||
            (nint)addon != runtimeState.AddonAddress ||
            !this.AreStructuredTooltipRuntimeNodesCurrent(addon, runtimeState))
        {
            runtimeState = null;
            return;
        }

        if (runtimeState.NameWasMutated &&
            runtimeState.NameNodeAddress != 0)
        {
            var nameNode = (AtkTextNode*)runtimeState.NameNodeAddress;
            if (nameNode != null &&
                ShouldRestoreStructuredTooltipNodeText(
                    this.ReadTooltipTextNode(nameNode),
                    runtimeState.AppliedNameText))
            {
                nameNode->SetText(runtimeState.OriginalNameText);
            }
        }

        if (runtimeState.DescriptionWasMutated &&
            runtimeState.DescriptionNodeAddress != 0)
        {
            var descriptionNode = (AtkTextNode*)runtimeState.DescriptionNodeAddress;
            if (descriptionNode != null &&
                ShouldRestoreStructuredTooltipNodeText(
                    this.ReadTooltipTextNode(descriptionNode),
                    runtimeState.AppliedDescriptionText))
            {
                descriptionNode->SetText(runtimeState.OriginalDescriptionText);
            }
        }

        runtimeState = null;
    }

    /// <summary>
    ///     Tries to resolve tooltip text-node addresses that match the canonical name and description.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="contentKind">The logical content kind.</param>
    /// <param name="sourceContentHash">The canonical source-payload identity.</param>
    /// <param name="originalName">The canonical original name.</param>
    /// <param name="originalDescription">The canonical original description.</param>
    /// <param name="translatedName">
    ///     The translated live name, if the tooltip was already mutated.
    /// </param>
    /// <param name="runtimeState">The resolved node-address map.</param>
    /// <returns><see langword="true" /> when at least one relevant node was resolved.</returns>
    private bool TryResolveTooltipNodeAddresses(
        AtkUnitBase* addon,
        uint contentId,
        uint contentKind,
        string sourceContentHash,
        string originalName,
        string originalDescription,
        string translatedName,
        out StructuredTooltipNativeState runtimeState)
    {
        runtimeState = new StructuredTooltipNativeState(
            (nint)addon,
            contentId,
            contentKind,
            sourceContentHash,
            0,
            originalName,
            null,
            0,
            originalDescription,
            null,
            false,
            false,
            false,
            false);

        if (addon == null || string.IsNullOrWhiteSpace(originalName))
        {
            return false;
        }

        var textNodeCandidates = this.CollectStructuredTooltipTextNodeCandidates(
            addon,
            contentKind);
        if (textNodeCandidates.Count == 0)
        {
            return false;
        }

        if (this.TryFindStructuredTooltipNameNodeCandidate(
                textNodeCandidates,
                originalName,
                translatedName,
                excludedNodeAddress: 0,
                out var nameCandidate))
        {
            runtimeState = runtimeState with
            {
                NameNodeAddress = nameCandidate.NodeAddress,
                OriginalNameText = GetStructuredTooltipSafeRestoreText(
                    nameCandidate.VisibleText,
                    originalName),
                NameNodeSupportsPlainTextMutation = nameCandidate.SupportsPlainTextMutation,
            };
        }

        if (!string.IsNullOrWhiteSpace(originalDescription) &&
            TryFindBestStructuredTooltipDescriptionNodeCandidate(
                textNodeCandidates,
                originalDescription,
                runtimeState.NameNodeAddress,
                out var descriptionCandidate))
        {
            runtimeState = runtimeState with
            {
                DescriptionNodeAddress = descriptionCandidate.NodeAddress,
                OriginalDescriptionText = GetStructuredTooltipSafeRestoreText(
                    descriptionCandidate.VisibleText,
                    originalDescription),
                DescriptionNodeSupportsPlainTextMutation = descriptionCandidate.SupportsPlainTextMutation,
            };
        }

        return runtimeState.NameNodeAddress != 0 ||
               runtimeState.DescriptionNodeAddress != 0;
    }

    /// <summary>
    ///     Collects the readable text-node candidates exposed by a structured
    ///     tooltip addon.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <returns>The collected text-node candidates.</returns>
    private List<StructuredTooltipTextNodeCandidate> CollectStructuredTooltipTextNodeCandidates(
        AtkUnitBase* addon,
        uint contentKind)
    {
        List<StructuredTooltipTextNodeCandidate> candidates = [];
        if (!IsStructuredTooltipAddonReady(addon))
        {
            return candidates;
        }

        HashSet<nint> seenNodeAddresses = [];
        if (contentKind == StructuredTooltipContentKindItem)
        {
            this.CollectItemDetailTextNodeCandidates(
                (AddonItemDetail*)addon,
                candidates,
                seenNodeAddresses);
        }

        foreach (var nodeAddress in AddonTextNodeResolvers.ResolveReadableTextNodes(addon))
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (seenNodeAddresses.Contains(nodeAddress))
            {
                continue;
            }

            this.TryAddStructuredTooltipTextNodeCandidate(
                textNode,
                candidates,
                seenNodeAddresses);
        }

        return candidates;
    }

    /// <summary>
    ///     Collects semantically anchored <c>ItemDetail</c> text nodes before
    ///     falling back to generic tooltip tree traversal.
    /// </summary>
    /// <param name="addon">The typed <c>ItemDetail</c> addon.</param>
    /// <param name="candidates">Receives collected candidates.</param>
    /// <param name="seenNodeAddresses">Receives deduplicated node addresses.</param>
    private void CollectItemDetailTextNodeCandidates(
        AddonItemDetail* addon,
        List<StructuredTooltipTextNodeCandidate> candidates,
        HashSet<nint> seenNodeAddresses)
    {
        if (addon == null)
        {
            return;
        }

        this.TryAddStructuredTooltipTextNodeCandidate(
            addon->ItemNameText,
            candidates,
            seenNodeAddresses);
        this.TryAddStructuredTooltipTextNodeCandidate(
            addon->DescriptionText,
            candidates,
            seenNodeAddresses);
    }

    /// <summary>
    ///     Gets whether the currently visible tooltip addon already exposes a
    ///     live name that matches the canonical payload being processed.
    /// </summary>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentKind">The logical content kind.</param>
    /// <param name="originalName">The canonical original name.</param>
    /// <param name="translatedName">The translated live name, if already applied.</param>
    /// <returns><see langword="true" /> when the live name matches.</returns>
    private bool HasStructuredTooltipLiveNameMatch(
        AtkUnitBase* addon,
        uint contentKind,
        string originalName,
        string? translatedName)
    {
        if (!IsStructuredTooltipAddonReady(addon) ||
            string.IsNullOrWhiteSpace(originalName))
        {
            return false;
        }

        var textNodeCandidates = this.CollectStructuredTooltipTextNodeCandidates(
            addon,
            contentKind);
        if (this.TryFindStructuredTooltipNameNodeCandidate(
                textNodeCandidates,
                originalName,
                translatedName,
                excludedNodeAddress: 0,
                out _))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve the live tooltip name node using strict normalized
    ///     equality against either the original or already-applied translated
    ///     name.
    /// </summary>
    /// <param name="candidates">The candidate nodes.</param>
    /// <param name="originalName">The canonical original name.</param>
    /// <param name="translatedName">The translated live name, if any.</param>
    /// <param name="excludedNodeAddress">One node address to exclude.</param>
    /// <param name="bestCandidate">The best matching candidate, if any.</param>
    /// <returns><see langword="true" /> when a name candidate was found.</returns>
    private bool TryFindStructuredTooltipNameNodeCandidate(
        IReadOnlyList<StructuredTooltipTextNodeCandidate> candidates,
        string originalName,
        string? translatedName,
        nint excludedNodeAddress,
        out StructuredTooltipTextNodeCandidate bestCandidate)
    {
        if (TryFindBestStructuredTooltipExactTextNodeCandidate(
                candidates,
                originalName,
                excludedNodeAddress,
                out bestCandidate))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(translatedName) &&
               TryFindBestStructuredTooltipExactTextNodeCandidate(
                   candidates,
                   translatedName,
                   excludedNodeAddress,
                   out bestCandidate);
    }

    /// <summary>
    ///     Adds one readable structured-tooltip text-node candidate when the
    ///     node has not already been collected.
    /// </summary>
    /// <param name="textNode">The text node to inspect.</param>
    /// <param name="candidates">Receives collected candidates.</param>
    /// <param name="seenNodeAddresses">Receives deduplicated node addresses.</param>
    private void TryAddStructuredTooltipTextNodeCandidate(
        AtkTextNode* textNode,
        List<StructuredTooltipTextNodeCandidate> candidates,
        HashSet<nint> seenNodeAddresses,
        bool trustPlainTextMutation = false)
    {
        if (textNode == null)
        {
            return;
        }

        var nodeAddress = (nint)textNode;
        if (!seenNodeAddresses.Add(nodeAddress))
        {
            return;
        }

        var visibleText = this.ReadTooltipTextNode(textNode);
        var normalizedVisibleText = NormalizeStructuredTooltipLookupText(visibleText);
        if (string.IsNullOrWhiteSpace(normalizedVisibleText))
        {
            return;
        }

        candidates.Add(new StructuredTooltipTextNodeCandidate(
            nodeAddress,
            visibleText,
            normalizedVisibleText,
            trustPlainTextMutation ||
            this.IsStructuredTooltipNodePlainTextMutable(textNode)));
    }

    /// <summary>
    ///     Gets whether one tooltip text node can be safely rewritten using
    ///     plain-text <c>SetText(string)</c> without corrupting inline payloads.
    /// </summary>
    /// <param name="textNode">The text node to inspect.</param>
    /// <returns><see langword="true" /> when plain-text mutation is safe.</returns>
    private bool IsStructuredTooltipNodePlainTextMutable(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return false;
        }

        try
        {
            return textNode->OriginalTextPointer
                .AsReadOnlySeStringSpan()
                .IsTextOnly();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Tries to find the best live text-node candidate for a canonical
    ///     structured-tooltip text value.
    /// </summary>
    /// <param name="candidates">The candidate nodes.</param>
    /// <param name="canonicalText">The canonical source text.</param>
    /// <param name="excludedNodeAddress">One node address to exclude.</param>
    /// <param name="bestCandidate">The best matching candidate, if any.</param>
    /// <returns><see langword="true" /> when a suitable candidate was found.</returns>
    internal static bool TryFindBestStructuredTooltipTextNodeCandidate(
        IReadOnlyList<StructuredTooltipTextNodeCandidate> candidates,
        string canonicalText,
        nint excludedNodeAddress,
        out StructuredTooltipTextNodeCandidate bestCandidate)
    {
        bestCandidate = default;
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(canonicalText))
        {
            return false;
        }

        var bestScore = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.NodeAddress == excludedNodeAddress)
            {
                continue;
            }

            var score = ComputeStructuredTooltipTextMatchScore(
                candidate.NormalizedVisibleText,
                canonicalText);
            if (score < bestScore)
            {
                continue;
            }

            if (score == bestScore &&
                bestScore > 0 &&
                !candidate.SupportsPlainTextMutation &&
                bestCandidate.SupportsPlainTextMutation)
            {
                continue;
            }

            if (score == bestScore &&
                candidate.SupportsPlainTextMutation ==
                bestCandidate.SupportsPlainTextMutation &&
                bestCandidate.NodeAddress != 0)
            {
                continue;
            }

            bestScore = score;
            bestCandidate = candidate;
        }

        return bestScore > 0;
    }

    /// <summary>
    ///     Tries to find the best live text-node candidate for a canonical
    ///     structured-tooltip value using strict normalized equality only.
    /// </summary>
    /// <param name="candidates">The candidate nodes.</param>
    /// <param name="canonicalText">The canonical source text.</param>
    /// <param name="excludedNodeAddress">One node address to exclude.</param>
    /// <param name="bestCandidate">The best exact-match candidate, if any.</param>
    /// <returns><see langword="true" /> when an exact-match candidate was found.</returns>
    internal static bool TryFindBestStructuredTooltipExactTextNodeCandidate(
        IReadOnlyList<StructuredTooltipTextNodeCandidate> candidates,
        string canonicalText,
        nint excludedNodeAddress,
        out StructuredTooltipTextNodeCandidate bestCandidate)
    {
        bestCandidate = default;
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(canonicalText))
        {
            return false;
        }

        var normalizedCanonicalText =
            NormalizeStructuredTooltipLookupText(canonicalText);
        if (string.IsNullOrWhiteSpace(normalizedCanonicalText))
        {
            return false;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.NodeAddress == excludedNodeAddress ||
                !IsStructuredTooltipExactNormalizedTextMatch(
                    candidate.NormalizedVisibleText,
                    normalizedCanonicalText))
            {
                continue;
            }

            if (bestCandidate.NodeAddress == 0 ||
                (candidate.SupportsPlainTextMutation &&
                 !bestCandidate.SupportsPlainTextMutation))
            {
                bestCandidate = candidate;
            }
        }

        return bestCandidate.NodeAddress != 0;
    }

    /// <summary>
    ///     Tries to find the exact visible description candidate for one
    ///     canonical tooltip payload.
    /// </summary>
    /// <param name="candidates">The candidate nodes.</param>
    /// <param name="canonicalText">The canonical description text.</param>
    /// <param name="excludedNodeAddress">One node address to exclude.</param>
    /// <param name="bestCandidate">The best matching candidate, if any.</param>
    /// <returns><see langword="true" /> when a description candidate was found.</returns>
    private static bool TryFindBestStructuredTooltipDescriptionNodeCandidate(
        IReadOnlyList<StructuredTooltipTextNodeCandidate> candidates,
        string canonicalText,
        nint excludedNodeAddress,
        out StructuredTooltipTextNodeCandidate bestCandidate)
    {
        bestCandidate = default;
        return TryFindBestStructuredTooltipExactTextNodeCandidate(
            candidates,
            canonicalText,
            excludedNodeAddress,
            out bestCandidate);
    }

    /// <summary>
    ///     Normalizes one structured-tooltip text value so live wrapped text and
    ///     canonical sheet text can be compared safely.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    internal static string NormalizeStructuredTooltipLookupText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            var category = char.GetUnicodeCategory(character);
            if (char.IsControl(character) ||
                category == UnicodeCategory.Format ||
                category == UnicodeCategory.PrivateUse ||
                IsStructuredTooltipSeparatorCategory(category))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(character);
        }

        return string.Join(
            " ",
            builder.ToString().Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///     Computes one score describing how well a visible structured-tooltip
    ///     text matches its canonical source text.
    /// </summary>
    /// <param name="visibleText">The visible live text.</param>
    /// <param name="canonicalText">The canonical source text.</param>
    /// <returns>The match score, or zero when the texts do not match.</returns>
    internal static int ComputeStructuredTooltipTextMatchScore(
        string visibleText,
        string canonicalText)
    {
        var normalizedVisibleText = NormalizeStructuredTooltipLookupText(visibleText);
        var normalizedCanonicalText = NormalizeStructuredTooltipLookupText(canonicalText);
        if (string.IsNullOrWhiteSpace(normalizedVisibleText) ||
            string.IsNullOrWhiteSpace(normalizedCanonicalText))
        {
            return 0;
        }

        if (IsStructuredTooltipExactNormalizedTextMatch(
                normalizedVisibleText,
                normalizedCanonicalText))
        {
            return 10_000 + normalizedVisibleText.Length;
        }

        if (normalizedVisibleText.Length < 4 || normalizedCanonicalText.Length < 4)
        {
            return 0;
        }

        if (normalizedVisibleText.Contains(
                normalizedCanonicalText,
                StringComparison.OrdinalIgnoreCase) ||
            normalizedCanonicalText.Contains(
                normalizedVisibleText,
                StringComparison.OrdinalIgnoreCase))
        {
            return 5_000 + Math.Min(
                normalizedVisibleText.Length,
                normalizedCanonicalText.Length);
        }

        return 0;
    }

    /// <summary>
    ///     Gets whether two structured-tooltip payload identities refer to the
    ///     same source content on a potentially recycled native addon.
    /// </summary>
    /// <param name="leftContentId">The first logical content identifier.</param>
    /// <param name="leftContentKind">The first logical content kind.</param>
    /// <param name="leftSourceContentHash">The first canonical source-payload hash.</param>
    /// <param name="rightContentId">The second logical content identifier.</param>
    /// <param name="rightContentKind">The second logical content kind.</param>
    /// <param name="rightSourceContentHash">The second canonical source-payload hash.</param>
    /// <returns><see langword="true" /> when both identities refer to the same source payload.</returns>
    internal static bool HasStructuredTooltipContentIdentity(
        uint leftContentId,
        uint leftContentKind,
        string? leftSourceContentHash,
        uint rightContentId,
        uint rightContentKind,
        string? rightSourceContentHash)
    {
        return leftContentId == rightContentId &&
               leftContentKind == rightContentKind &&
               !string.IsNullOrWhiteSpace(leftSourceContentHash) &&
               string.Equals(
                   leftSourceContentHash,
                   rightSourceContentHash,
                   StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets whether a native tooltip node still contains the exact
    ///     translation written by this runtime and is therefore safe to restore.
    /// </summary>
    /// <param name="liveText">The current text read from the native node.</param>
    /// <param name="translatedText">The translation previously written by this runtime.</param>
    /// <returns><see langword="true" /> when restoring the original text is safe.</returns>
    internal static bool ShouldRestoreStructuredTooltipNodeText(
        string liveText,
        string? translatedText)
    {
        return !string.IsNullOrWhiteSpace(translatedText) &&
               IsStructuredTooltipExactTextMatch(liveText, translatedText);
    }

    /// <summary>
    ///     Gets whether two structured-tooltip text values match exactly after
    ///     normalization.
    /// </summary>
    /// <param name="visibleText">The visible live text.</param>
    /// <param name="canonicalText">The canonical source text.</param>
    /// <returns><see langword="true" /> when the texts match exactly.</returns>
    internal static bool IsStructuredTooltipExactTextMatch(
        string visibleText,
        string canonicalText)
    {
        return IsStructuredTooltipExactNormalizedTextMatch(
            NormalizeStructuredTooltipLookupText(visibleText),
            NormalizeStructuredTooltipLookupText(canonicalText));
    }

    /// <summary>
    ///     Gets the safest restore text for one structured-tooltip node,
    ///     preserving the live original text only when it still matches the
    ///     canonical payload exactly after normalization.
    /// </summary>
    /// <param name="visibleText">The visible live text captured from the node.</param>
    /// <param name="canonicalText">The canonical original payload text.</param>
    /// <returns>The safest text to restore into the node later.</returns>
    private static string GetStructuredTooltipSafeRestoreText(
        string visibleText,
        string canonicalText)
    {
        return IsStructuredTooltipExactTextMatch(
            visibleText,
            canonicalText)
            ? visibleText
            : canonicalText;
    }

    /// <summary>
    ///     Gets whether two already-normalized structured-tooltip text values
    ///     match exactly.
    /// </summary>
    /// <param name="normalizedVisibleText">The normalized visible live text.</param>
    /// <param name="normalizedCanonicalText">The normalized canonical text.</param>
    /// <returns><see langword="true" /> when the texts match exactly.</returns>
    private static bool IsStructuredTooltipExactNormalizedTextMatch(
        string normalizedVisibleText,
        string normalizedCanonicalText)
    {
        return !string.IsNullOrWhiteSpace(normalizedVisibleText) &&
               !string.IsNullOrWhiteSpace(normalizedCanonicalText) &&
               string.Equals(
                   normalizedVisibleText,
                   normalizedCanonicalText,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets whether one Unicode category should behave like a separator when
    ///     normalizing structured-tooltip text for live matching.
    /// </summary>
    /// <param name="category">The Unicode category to inspect.</param>
    /// <returns><see langword="true" /> when the category should collapse to space.</returns>
    private static bool IsStructuredTooltipSeparatorCategory(
        UnicodeCategory category)
    {
        return category is UnicodeCategory.ConnectorPunctuation or
               UnicodeCategory.DashPunctuation or
               UnicodeCategory.OpenPunctuation or
               UnicodeCategory.ClosePunctuation or
               UnicodeCategory.InitialQuotePunctuation or
               UnicodeCategory.FinalQuotePunctuation or
               UnicodeCategory.OtherPunctuation or
               UnicodeCategory.MathSymbol or
               UnicodeCategory.CurrencySymbol or
               UnicodeCategory.ModifierSymbol or
               UnicodeCategory.OtherSymbol;
    }

    /// <summary>
    ///     Gets whether the hovered ActionDetail payload should use the trait pipeline.
    /// </summary>
    /// <param name="hoverActionKind">The hovered action kind.</param>
    /// <returns><see langword="true" /> when the hover kind represents a trait.</returns>
    private static bool IsTraitHoverActionKind(DetailKind hoverActionKind)
    {
        return hoverActionKind is DetailKind.Trait or
               DetailKind.PvPSelectTrait or
               DetailKind.MKDTrait;
    }

    /// <summary>
    ///     Gets whether an ActionDetail hover must resolve its canonical text
    ///     from a structured reference-text sheet instead of <c>Action</c>.
    /// </summary>
    /// <param name="hoverActionKind">The active hover action kind.</param>
    /// <returns>
    ///     <see langword="true" /> when numeric identifiers must stay scoped
    ///     to a structured reference-text family.
    /// </returns>
    internal static bool RequiresStructuredActionReferencePayload(
        DetailKind hoverActionKind)
    {
        return hoverActionKind is DetailKind.GeneralAction or
               DetailKind.BuddyAction or
               DetailKind.Companion or
               DetailKind.BuddyOrder or
               DetailKind.CompanyAction or
               DetailKind.CraftingAction or
               DetailKind.PetOrder or
               DetailKind.Mount or
               DetailKind.BgcArmyAction or
               DetailKind.EurekaMagiaAction or
               DetailKind.MainCommand or
               DetailKind.ExtraCommand;
    }

    /// <summary>
    ///     Resolves the source row identity for the currently displayed
    ///     ActionDetail payload.
    /// </summary>
    /// <param name="hoverActionKind">The action family captured by Dalamud.</param>
    /// <param name="baseActionId">
    ///     The unadjusted identifier supplied to the game's hover handler.
    /// </param>
    /// <param name="resolvedActionId">
    ///     The adjusted identifier exposed by the live action-detail agent.
    /// </param>
    /// <param name="agentFallbackActionId">
    ///     The agent-backed fallback identifier when no direct hover exists.
    /// </param>
    /// <returns>The best source row identity, or zero when none is available.</returns>
    internal static uint ResolveActionDetailReferenceId(
        DetailKind hoverActionKind,
        uint baseActionId,
        uint resolvedActionId,
        uint agentFallbackActionId)
    {
        if ((RequiresStructuredActionReferencePayload(hoverActionKind) ||
             IsTraitHoverActionKind(hoverActionKind)) &&
            baseActionId != 0)
        {
            return baseActionId;
        }

        if (resolvedActionId != 0)
        {
            return resolvedActionId;
        }

        if (baseActionId != 0)
        {
            return baseActionId;
        }

        return agentFallbackActionId;
    }

    /// <summary>
    ///     Tries to build one structured action-adjacent reference payload for
    ///     the requested hover family.
    /// </summary>
    /// <param name="referenceId">The candidate structured row identifier.</param>
    /// <param name="hoverActionKind">The active hover action kind.</param>
    /// <param name="referencePayload">The resolved reference payload.</param>
    /// <returns><see langword="true" /> when a payload was built.</returns>
    private static bool TryBuildStructuredReferencePayload(
        uint referenceId,
        DetailKind hoverActionKind,
        out ReferenceTextCanonicalPayload referencePayload)
    {
        referencePayload = new ReferenceTextCanonicalPayload();

        switch (hoverActionKind)
        {
            case DetailKind.GeneralAction:
                return TryBuildGeneralActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.BuddyAction:
            case DetailKind.Companion:
            case DetailKind.BuddyOrder:
                return TryBuildBuddyActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.CompanyAction:
                return TryBuildCompanyActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.CraftingAction:
                return TryBuildCraftActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.PetOrder:
                return TryBuildPetActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.Mount:
                return TryBuildMountActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.BgcArmyAction:
                return TryBuildBgcArmyActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.EurekaMagiaAction:
                return TryBuildEurekaMagiaActionPayload(
                    referenceId,
                    out referencePayload);
            case DetailKind.MainCommand:
            case DetailKind.ExtraCommand:
                return TryBuildMainCommandPayload(
                    referenceId,
                    out referencePayload);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Normalizes one structured action reference id through Dalamud's
    ///     ActStr redirect when the hover family supports it.
    /// </summary>
    /// <param name="referenceId">The raw hovered identifier.</param>
    /// <param name="hoverActionKind">The active hover action kind.</param>
    /// <returns>The normalized row identifier, or the raw identifier.</returns>
    internal static uint NormalizeStructuredActionReferenceId(
        uint referenceId,
        DetailKind hoverActionKind)
    {
        if (referenceId == 0 ||
            !TryMapHoverActionKindToActionKind(
                hoverActionKind,
                out var actionKind))
        {
            return referenceId;
        }

        var actStrId = ActionKindExtensions.GetActStrId(
            actionKind,
            referenceId);
        return actStrId != 0 ? actStrId : referenceId;
    }

    /// <summary>
    ///     Tries to map one action-detail hover kind to the corresponding
    ///     Dalamud action kind used by ActStr redirects.
    /// </summary>
    /// <param name="hoverActionKind">The hover action kind.</param>
    /// <param name="actionKind">The mapped action kind.</param>
    /// <returns><see langword="true" /> when the mapping exists.</returns>
    private static bool TryMapHoverActionKindToActionKind(
        DetailKind hoverActionKind,
        out Dalamud.Game.ActionKind actionKind)
    {
        actionKind = default;

        switch (hoverActionKind)
        {
            case DetailKind.GeneralAction:
                actionKind = Dalamud.Game.ActionKind.GeneralAction;
                return true;
            case DetailKind.BuddyAction:
                actionKind = Dalamud.Game.ActionKind.BuddyAction;
                return true;
            case DetailKind.Companion:
            case DetailKind.BuddyOrder:
                actionKind = Dalamud.Game.ActionKind.Companion;
                return true;
            case DetailKind.CompanyAction:
                actionKind = Dalamud.Game.ActionKind.CompanyAction;
                return true;
            case DetailKind.CraftingAction:
                actionKind = Dalamud.Game.ActionKind.CraftAction;
                return true;
            case DetailKind.PetOrder:
                actionKind = Dalamud.Game.ActionKind.PetAction;
                return true;
            case DetailKind.Mount:
                actionKind = Dalamud.Game.ActionKind.Mount;
                return true;
            case DetailKind.BgcArmyAction:
                actionKind = Dalamud.Game.ActionKind.BgcArmyAction;
                return true;
            case DetailKind.MainCommand:
            case DetailKind.ExtraCommand:
                actionKind = Dalamud.Game.ActionKind.MainCommand;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Reads one tooltip text node using the same fallback order used elsewhere in the repo.
    /// </summary>
    /// <param name="textNode">The text node to read.</param>
    /// <returns>The resolved text, or <see cref="string.Empty" />.</returns>
    private string ReadTooltipTextNode(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return string.Empty;
        }

        try
        {
            var directText = textNode->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(directText))
            {
                return directText;
            }
        }
        catch
        {
            // Fall through to the legacy buffer read.
        }

        try
        {
            return MemoryHelper.ReadSeStringAsString(
                       out _,
                       (nint)textNode->NodeText.StringPtr.Value) ??
                   string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Gets whether one live tooltip text node already exposes the desired
    ///     target text after normalization.
    /// </summary>
    /// <param name="textNode">The live text node.</param>
    /// <param name="targetText">The desired target text.</param>
    /// <returns><see langword="true" /> when the live node already matches.</returns>
    private bool DoesStructuredTooltipNodeMatchTarget(
        AtkTextNode* textNode,
        string targetText)
    {
        if (textNode == null || string.IsNullOrWhiteSpace(targetText))
        {
            return false;
        }

        return IsStructuredTooltipExactTextMatch(
            this.ReadTooltipTextNode(textNode),
            targetText);
    }

    /// <summary>
    ///     Clears one structured-tooltip overlay and emits a compact reason log.
    /// </summary>
    /// <param name="surfaceName">The logical overlay surface name.</param>
    /// <param name="overlay">The overlay instance to clear.</param>
    /// <param name="reason">The compact clear reason.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="contentKind">The logical content kind.</param>
    private void ClearStructuredTooltipOverlay(
        string surfaceName,
        TranslationOverlay overlay,
        string reason,
        uint contentId = 0,
        uint contentKind = 0)
    {
        if (!HasPublishedStructuredTooltipOverlayContent(overlay))
        {
            this.ClearOverlay(overlay, clearText: true);
            return;
        }

        this.LogStructuredTooltipState(
            surfaceName,
            "overlay-clear",
            contentId,
            contentKind,
            displayMode: null,
            reason: reason);
        this.ClearOverlay(overlay, clearText: true);
    }

    /// <summary>
    ///     Gets whether one structured tooltip overlay currently has visible or
    ///     published content worth logging before clear.
    /// </summary>
    /// <param name="overlay">The overlay instance to inspect.</param>
    /// <returns><see langword="true" /> when the overlay currently has content.</returns>
    private static bool HasPublishedStructuredTooltipOverlayContent(
        TranslationOverlay overlay)
    {
        overlay.Semaphore.Wait();
        try
        {
            return overlay.Display ||
                   !string.IsNullOrWhiteSpace(overlay.CurrentText);
        }
        finally
        {
            overlay.Semaphore.Release();
        }
    }

    /// <summary>
    ///     Emits one throttled structured-tooltip diagnostic covering runtime,
    ///     native-apply, and overlay stages.
    /// </summary>
    /// <param name="surfaceName">The logical surface name.</param>
    /// <param name="category">The logical diagnostic category.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="contentKind">The logical content kind.</param>
    /// <param name="displayMode">The effective display mode, if relevant.</param>
    /// <param name="reason">The compact state reason.</param>
    /// <param name="name">The optional name text.</param>
    /// <param name="description">The optional description text.</param>
    /// <param name="overlayText">The optional overlay text.</param>
    /// <param name="nameNodeAddress">The resolved name-node address.</param>
    /// <param name="descriptionNodeAddress">The resolved description-node address.</param>
    /// <param name="nameApplied">Whether the translated name was written.</param>
    /// <param name="descriptionApplied">Whether the translated description was written.</param>
    private void LogStructuredTooltipState(
        string surfaceName,
        string category,
        uint contentId,
        uint contentKind,
        JournalTranslationDisplayMode? displayMode,
        string reason,
        string? name = null,
        string? description = null,
        string? overlayText = null,
        nint nameNodeAddress = 0,
        nint descriptionNodeAddress = 0,
        bool? nameApplied = null,
        bool? descriptionApplied = null)
    {
        var nameHash = ComputeStructuredTooltipDiagnosticHash(name);
        var descriptionHash = ComputeStructuredTooltipDiagnosticHash(description);
        var overlayHash = ComputeStructuredTooltipDiagnosticHash(overlayText);
        this.structuredTooltipTelemetry.Debug(
            category,
            $"surface={surfaceName} contentId={contentId} kind={FormatStructuredTooltipContentKind(contentKind)} displayMode={FormatStructuredTooltipDisplayMode(displayMode)} reason={reason} nameHash={nameHash} descriptionHash={descriptionHash} overlayHash={overlayHash} nameNode={FormatStructuredTooltipNodeAddress(nameNodeAddress)} descriptionNode={FormatStructuredTooltipNodeAddress(descriptionNodeAddress)} nameApplied={FormatStructuredTooltipOptionalBool(nameApplied)} descriptionApplied={FormatStructuredTooltipOptionalBool(descriptionApplied)} name=[{BuildStructuredTooltipPreview(name)}] description=[{BuildStructuredTooltipPreview(description)}] overlay=[{BuildStructuredTooltipPreview(overlayText)}]",
            signature: $"{surfaceName}|{category}|{contentId}|{contentKind}|{FormatStructuredTooltipDisplayMode(displayMode)}|{reason}|{nameHash}|{descriptionHash}|{overlayHash}|{nameNodeAddress}|{descriptionNodeAddress}|{nameApplied}|{descriptionApplied}");
    }

    /// <summary>
    ///     Updates one tooltip overlay from the currently visible tooltip addon.
    /// </summary>
    /// <param name="surfaceName">The logical surface name.</param>
    /// <param name="overlay">The overlay instance to update.</param>
    /// <param name="addon">The visible tooltip addon.</param>
    /// <param name="contentId">The logical content identifier.</param>
    /// <param name="contentKind">The logical content kind.</param>
    /// <param name="text">The tooltip text to render.</param>
    private void UpdateStructuredTooltipOverlay(
        string surfaceName,
        TranslationOverlay overlay,
        AtkUnitBase* addon,
        uint contentId,
        uint contentKind,
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !IsStructuredTooltipAddonReady(addon))
        {
            this.ClearStructuredTooltipOverlay(
                surfaceName,
                overlay,
                reason: string.IsNullOrWhiteSpace(text)
                    ? "overlay-text-empty"
                    : "overlay-addon-not-ready",
                contentId: contentId,
                contentKind: contentKind);
            return;
        }

        this.UpdateOverlayContent(
            overlay,
            string.Empty,
            text,
            string.Empty);
        this.UpdateOverlayBounds(overlay, addon);
        this.LogStructuredTooltipState(
            surfaceName,
            "overlay-update",
            contentId,
            contentKind,
            displayMode: null,
            reason: "overlay-content-updated",
            overlayText: text);
    }

    /// <summary>
    ///     Draws one structured tooltip overlay when it has published content.
    /// </summary>
    /// <param name="surfaceName">The logical surface name.</param>
    /// <param name="overlay">The overlay instance to draw.</param>
    /// <param name="config">The overlay configuration.</param>
    private void DrawStructuredTooltipOverlay(
        string surfaceName,
        TranslationOverlay overlay,
        TranslationWindowConfig config)
    {
        overlay.Semaphore.Wait();
        var shouldDraw = overlay.Display;
        var currentText = overlay.CurrentText;
        overlay.Semaphore.Release();

        if (!shouldDraw)
        {
            return;
        }

        this.LogStructuredTooltipState(
            surfaceName,
            "overlay-draw",
            0,
            0,
            displayMode: null,
            reason: "overlay-render",
            overlayText: currentText);
        this.DrawTranslationWindow(overlay, config);
    }

    /// <summary>
    ///     Builds the shared overlay configuration used by structured tooltips.
    /// </summary>
    /// <param name="surfaceId">The stable overlay surface identifier.</param>
    /// <param name="defaultTitle">The default overlay title.</param>
    /// <returns>The overlay configuration.</returns>
    private TranslationWindowConfig BuildTooltipOverlayConfig(
        TranslationOverlaySurfaceId surfaceId,
        string defaultTitle)
    {
        return new TranslationWindowConfig(
            SurfaceId: surfaceId,
            DefaultTitle: defaultTitle,
            FontScale: 1.0f,
            WidthMultiplier: 1.0f,
            HeightMultiplier: 1.0f,
            TextColor: new Vector4(1f, 1f, 1f, 1f),
            PosCorrection: Vector2.Zero,
            ForceShowTitle: false,
            BackgroundOpacity: 0.95f,
            NoBackground: false,
            UseFixedWindowSize: false,
            CenterOnAddon: false,
            AutoSizeToTextWithMaxWidth: true,
            ExpandWidthToFitText: true,
            MaxAutoExpandedWidthMultiplier: 1.35f,
            MinWidthViewportFraction: 0.18f,
            MaxWidthViewportFraction: 0.36f);
    }

    /// <summary>
    ///     Normalizes hovered item ids so HQ items resolve against the base Item row.
    /// </summary>
    /// <param name="hoveredItemId">The raw hovered item identifier.</param>
    /// <returns>The normalized base item row identifier.</returns>
    private static uint NormalizeHoveredItemId(uint hoveredItemId)
    {
        return hoveredItemId > 1_000_000u
            ? hoveredItemId - 1_000_000u
            : hoveredItemId;
    }

    /// <summary>
    ///     Gets whether ActionDetail may safely fall back to the agent-backed
    ///     action id when direct hovered-action state is empty.
    /// </summary>
    /// <param name="hoveredActionId">The direct hovered action identifier.</param>
    /// <param name="hoveredItemId">The direct hovered item identifier.</param>
    /// <returns><see langword="true" /> when agent fallback is safe.</returns>
    internal static bool ShouldUseActionDetailAgentFallback(
        uint hoveredActionId,
        uint hoveredItemId)
    {
        return hoveredActionId == 0 &&
               hoveredItemId == 0;
    }

    /// <summary>
    ///     Gets whether ItemDetail may safely fall back to the agent-backed
    ///     item id when direct hovered-item state is empty.
    /// </summary>
    /// <param name="hoveredItemId">The direct hovered item identifier.</param>
    /// <param name="hoveredActionId">The direct hovered action identifier.</param>
    /// <returns><see langword="true" /> when agent fallback is safe.</returns>
    internal static bool ShouldUseItemDetailAgentFallback(
        uint hoveredItemId,
        uint hoveredActionId)
    {
        return hoveredItemId == 0 &&
               hoveredActionId == 0;
    }

    /// <summary>
    ///     Gets whether a live action hover takes precedence over a lingering
    ///     direct ItemDetail hover value.
    /// </summary>
    /// <param name="hoveredActionId">The live action identifier.</param>
    /// <param name="isActionDetailActive">
    ///     Whether the ActionDetail surface is currently visible.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when ItemDetail must clear its presentation.
    /// </returns>
    internal static bool ShouldSuppressItemDetailDuringActionHover(
        uint hoveredActionId,
        bool isActionDetailActive)
    {
        return hoveredActionId != 0 && isActionDetailActive;
    }

    /// <summary>
    ///     Computes one short diagnostic hash for structured-tooltip logs.
    /// </summary>
    /// <param name="value">The optional text to hash.</param>
    /// <returns>The short uppercase hash, or <c>EMPTY</c>.</returns>
    private static string ComputeStructuredTooltipDiagnosticHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "EMPTY";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 6));
    }

    /// <summary>
    ///     Builds one compact single-line preview for structured-tooltip logs.
    /// </summary>
    /// <param name="value">The optional text to preview.</param>
    /// <returns>The compact preview.</returns>
    private static string BuildStructuredTooltipPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        var preview = NormalizeStructuredTooltipLookupText(value);
        if (preview.Length <= 96)
        {
            return preview;
        }

        return preview[..96] + "...";
    }

    /// <summary>
    ///     Formats one optional structured-tooltip display mode.
    /// </summary>
    /// <param name="displayMode">The optional display mode.</param>
    /// <returns>The formatted text.</returns>
    private static string FormatStructuredTooltipDisplayMode(
        JournalTranslationDisplayMode? displayMode)
    {
        return displayMode?.ToString() ?? "<n/a>";
    }

    /// <summary>
    ///     Formats one structured-tooltip content kind.
    /// </summary>
    /// <param name="contentKind">The content kind value.</param>
    /// <returns>The formatted text.</returns>
    private static string FormatStructuredTooltipContentKind(uint contentKind)
    {
        return contentKind switch
        {
            StructuredTooltipContentKindAction => "Action",
            StructuredTooltipContentKindTrait => "Trait",
            StructuredTooltipContentKindItem => "Item",
            _ => "<none>",
        };
    }

    /// <summary>
    ///     Formats one optional node address for diagnostics.
    /// </summary>
    /// <param name="nodeAddress">The node address.</param>
    /// <returns>The formatted node address.</returns>
    private static string FormatStructuredTooltipNodeAddress(nint nodeAddress)
    {
        return nodeAddress == 0
            ? "<none>"
            : $"0x{nodeAddress:X}";
    }

    /// <summary>
    ///     Formats one optional boolean for diagnostics.
    /// </summary>
    /// <param name="value">The optional boolean.</param>
    /// <returns>The formatted text.</returns>
    private static string FormatStructuredTooltipOptionalBool(bool? value)
    {
        return value?.ToString() ?? "<n/a>";
    }

    /// <summary>
    ///     Gets whether the cached native tooltip state still points at nodes
    ///     present in the addon's current tree.
    /// </summary>
    /// <param name="addon">The current visible tooltip addon.</param>
    /// <param name="runtimeState">The cached native state.</param>
    /// <returns><see langword="true" /> when cached node addresses are current.</returns>
    private bool AreStructuredTooltipRuntimeNodesCurrent(
        AtkUnitBase* addon,
        StructuredTooltipNativeState runtimeState)
    {
        if (!IsStructuredTooltipAddonReady(addon))
        {
            return false;
        }

        var currentNodeAddresses = new HashSet<nint>();
        foreach (var candidate in this.CollectStructuredTooltipTextNodeCandidates(
                     addon,
                     runtimeState.ContentKind))
        {
            currentNodeAddresses.Add(candidate.NodeAddress);
        }

        return AreStructuredTooltipNodeAddressesCurrent(
            currentNodeAddresses,
            runtimeState.NameNodeAddress,
            runtimeState.DescriptionNodeAddress);
    }

    /// <summary>
    ///     Captures the minimal mutable state for one structured tooltip instance.
    /// </summary>
    /// <param name="AddonAddress">The visible tooltip addon address.</param>
    /// <param name="ContentId">The logical item/action identifier.</param>
    /// <param name="ContentKind">The logical item/action content kind.</param>
    /// <param name="SourceContentHash">The canonical source-payload identity.</param>
    /// <param name="NameNodeAddress">The resolved name-node address.</param>
    /// <param name="OriginalNameText">The original name-node text.</param>
    /// <param name="AppliedNameText">The translation written to the name node.</param>
    /// <param name="DescriptionNodeAddress">The resolved description-node address.</param>
    /// <param name="OriginalDescriptionText">The original description-node text.</param>
    /// <param name="AppliedDescriptionText">The translation written to the description node.</param>
    private sealed record StructuredTooltipNativeState(
        nint AddonAddress,
        uint ContentId,
        uint ContentKind,
        string SourceContentHash,
        nint NameNodeAddress,
        string OriginalNameText,
        string? AppliedNameText,
        nint DescriptionNodeAddress,
        string OriginalDescriptionText,
        string? AppliedDescriptionText,
        bool NameNodeSupportsPlainTextMutation,
        bool NameWasMutated,
        bool DescriptionNodeSupportsPlainTextMutation,
        bool DescriptionWasMutated);

    /// <summary>
    ///     Captures one readable structured-tooltip text node together with its
    ///     normalized comparison text.
    /// </summary>
    /// <param name="NodeAddress">The text-node address.</param>
    /// <param name="VisibleText">The visible text read from the node.</param>
    /// <param name="NormalizedVisibleText">The normalized visible text.</param>
    internal readonly record struct StructuredTooltipTextNodeCandidate(
        nint NodeAddress,
        string VisibleText,
        string NormalizedVisibleText,
        bool SupportsPlainTextMutation);

    /// <summary>
    ///     Tracks whether one structured-tooltip addon family is currently active
    ///     according to addon lifecycle events.
    /// </summary>
    /// <param name="SurfaceName">The logical surface name.</param>
    /// <param name="AddonNames">The addon names bound to that surface.</param>
    private sealed class StructuredTooltipLifecycleState(
        string surfaceName,
        params string[] addonNames)
    {
        /// <summary>
        ///     Gets the logical surface name.
        /// </summary>
        public string SurfaceName { get; } = surfaceName;

        /// <summary>
        ///     Gets the addon names tracked for this surface.
        /// </summary>
        public string[] AddonNames { get; } = addonNames;

        /// <summary>
        ///     Gets a value indicating whether this surface is currently active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        ///     Marks this surface as active.
        /// </summary>
        public void MarkActive()
        {
            this.IsActive = true;
        }

        /// <summary>
        ///     Marks this surface as inactive.
        /// </summary>
        public void MarkInactive()
        {
            this.IsActive = false;
        }

        /// <summary>
        ///     Gets whether one addon name belongs to this tracked surface.
        /// </summary>
        /// <param name="addonName">The addon name to test.</param>
        /// <returns><see langword="true" /> when the addon name matches.</returns>
        public bool Matches(string addonName)
        {
            return this.AddonNames.Contains(addonName, StringComparer.Ordinal);
        }
    }
}
