// <copyright file="DbFirstGameWindowAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Handlers;
using Echoglossian.NativeUI.Helpers;
using Lumina.Text.ReadOnly;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Common;

/// <summary>
///     Provides a DB-first runtime for addon-local surfaces backed by
///     <see cref="GameWindow" /> rows.
/// </summary>
public abstract unsafe class DbFirstGameWindowAddonHandler
    : IAddonTranslationHandler, IPluginUnloadAwareAddonHandler
{
    private static readonly Regex NumericLikePattern = new(
        @"^\s*([€£$¥]?\s*\d+([.,]\d+)?\s*[%€£$¥]?\s*|(\d+/\d+))\s*$",
        RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, byte>
        InFlightPayloads = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, DateTime>
        FailedPayloadRetryUtc = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, DbFirstGameWindowPayload>
        ParsedPayloadCache = new(StringComparer.Ordinal);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FailureRetryInterval =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DeferredCleanupGraceInterval =
        TimeSpan.FromSeconds(2);

    private readonly string addonName;
    private readonly Config config;
    private readonly HoverTooltipManager hoverTooltipManager;
    private readonly TranslationService translationService;
    private readonly bool useAtkValues;
    private readonly bool useTextNodes;
    private readonly StringArrayType? stringArrayDataType;
    private readonly Func<Config, bool> enabledSelector;
    private readonly Func<Config, JournalTranslationDisplayMode> displayModeSelector;
    private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>>
        eventHandlers = [];
    private readonly string hoverTooltipKeyPrefix;

    private DbFirstGameWindowRuntimeState? runtimeState;
    private DbFirstGameWindowRuntimeState? lastResolvedState;
    private JournalTranslationDisplayMode? lastAppliedDisplayMode;
    private bool lastOriginalRecoveryWasUnstableTranslatedState;
    private bool deferredCleanupPending;
    private AddonEvent deferredCleanupEvent;
    private DateTime deferredCleanupUtc = DateTime.MinValue;
    private DateTime appliedStatePreDrawRefreshUntilUtc = DateTime.MinValue;

    private DateTime nextRetryUtc = DateTime.MinValue;

    /// <summary>
    ///     Clears the shared DB-first in-memory session caches so one fresh
    ///     plugin load never inherits stale payload parse results, in-flight
    ///     work markers, or failed-payload cooldowns from an earlier session.
    /// </summary>
    public static void ClearSessionCaches()
    {
        InFlightPayloads.Clear();
        FailedPayloadRetryUtc.Clear();
        ParsedPayloadCache.Clear();
        PluginRuntimeLog.Debug("[DbFirstGameWindowAddonHandler] Cleared DB-first session caches.");
    }

    /// <summary>
    ///     Gets the target addon name handled by this runtime.
    /// </summary>
    private protected string AddonName => this.addonName;

    /// <summary>
    ///     Gets the active plugin configuration for derived handlers that need
    ///     narrow addon-local policy decisions.
    /// </summary>
    private protected Config HandlerConfig => this.config;

    /// <summary>
    ///     Gets the shared translation service so addon-local async translation
    ///     hooks can reuse the canonical pipeline without recreating services.
    /// </summary>
    private protected TranslationService HandlerTranslationService =>
        this.translationService;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="DbFirstGameWindowAddonHandler" /> class.
    /// </summary>
    /// <param name="addonName">The target addon name.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="hoverTooltipManager">
    ///     The shared hover-tooltip manager used for tooltip and swap modes.
    /// </param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="enabledSelector">
    ///     Resolves whether this addon should be active.
    /// </param>
    /// <param name="useAtkValues">
    ///     Indicates whether ATK string values are part of the payload.
    /// </param>
    /// <param name="useTextNodes">
    ///     Indicates whether visible text nodes are part of the payload.
    /// </param>
    /// <param name="stringArrayDataType">
    ///     The backing <see cref="StringArrayType" />, if any.
    /// </param>
    /// <param name="displayModeSelector">
    ///     Resolves the configured display mode for this addon family.
    /// </param>
    protected DbFirstGameWindowAddonHandler(
        string addonName,
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService,
        Func<Config, bool> enabledSelector,
        bool useAtkValues,
        bool useTextNodes = false,
        StringArrayType? stringArrayDataType = null,
        Func<Config, JournalTranslationDisplayMode>? displayModeSelector = null)
    {
        this.addonName = addonName;
        this.config = config;
        this.hoverTooltipManager = hoverTooltipManager;
        this.translationService = translationService;
        this.enabledSelector = enabledSelector;
        this.useAtkValues = useAtkValues;
        this.useTextNodes = useTextNodes;
        this.stringArrayDataType = stringArrayDataType;
        this.displayModeSelector =
            displayModeSelector ??
            (static _ => JournalTranslationDisplayMode.NativeUiTranslation);
        this.hoverTooltipKeyPrefix = $"{addonName}-DbFirst-";

        this.RegisterHandler(AddonEvent.PreSetup, this.OnLifecycleEvent);
        this.RegisterHandler(AddonEvent.PreRefresh, this.OnLifecycleEvent);
        this.RegisterHandler(
            AddonEvent.PreRequestedUpdate,
            this.OnLifecycleEvent);
        this.RegisterHandler(AddonEvent.PreDraw, this.OnPreDrawEvent);
        this.RegisterHandler(AddonEvent.PreHide, this.OnCleanupEvent);
        this.RegisterHandler(AddonEvent.PreFinalize, this.OnCleanupEvent);
    }

    /// <summary>
    ///     Gets the registered addon lifecycle handlers.
    /// </summary>
    /// <returns>The handler map.</returns>
    public Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>
        GetEventHandlers()
    {
        return this.eventHandlers.ToDictionary(
            kvp => kvp.Key,
            kvp => new IAddonLifecycle.AddonEventDelegate((evt, args) =>
            {
                foreach (var handler in kvp.Value)
                {
                    handler(evt, args);
                }
            }));
    }

    /// <summary>
    ///     Registers a local lifecycle handler.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="handler">The event handler.</param>
    protected void RegisterHandler(
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
    ///     Determines whether this handler should capture live
    ///     <c>StringArrayData</c> values for canonical lookup and persistence
    ///     when the backing array is subscribed by the given number of addons.
    /// </summary>
    /// <param name="subscribedAddonsCount">
    ///     The runtime subscriber count reported by the backing array.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the live string-array values are safe
    ///     to capture for this addon; otherwise <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldCaptureStringArrayValues(
        byte subscribedAddonsCount)
    {
        return true;
    }

    /// <summary>
    ///     Determines whether one visible text node should participate in the
    ///     canonical payload for this addon.
    /// </summary>
    /// <param name="textNode">The live text node.</param>
    /// <param name="visibleText">The currently visible text.</param>
    /// <returns>
    ///     <see langword="true" /> when the text node is stable enough to
    ///     capture and later reapply; otherwise <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return true;
    }

    /// <summary>
    ///     Normalizes the captured text-node payload before it participates in
    ///     persistence, matching, and native apply.
    /// </summary>
    /// <param name="capturedTextNodes">The freshly captured text-node payload.</param>
    /// <returns>
    ///     The normalized payload to persist and reuse for this addon.
    /// </returns>
    protected virtual SortedDictionary<string, string> NormalizeCapturedTextNodes(
        SortedDictionary<string, string> capturedTextNodes)
    {
        return capturedTextNodes;
    }

    /// <summary>
    ///     Determines whether this handler should write translated
    ///     <c>StringArrayData</c> values back into the live addon when the
    ///     backing array is subscribed by the given number of addons.
    /// </summary>
    /// <param name="subscribedAddonsCount">
    ///     The runtime subscriber count reported by the backing array.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when native writes/restores are safe for the
    ///     backing string array; otherwise <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldWriteStringArrayValues(
        byte subscribedAddonsCount)
    {
        return true;
    }

    /// <summary>
    ///     Determines whether string-array writes for this addon should request
    ///     subscribed-addon updates immediately after the value changes.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when native writes should request updates;
    ///     otherwise <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldRequestStringArrayUpdates()
    {
        return false;
    }

    /// <summary>
    ///     Determines whether this handler may reuse a compatible persisted
    ///     payload when no exact original-payload match is available.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when compatible payload reuse is safe for
    ///     this addon; otherwise <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldReuseCompatiblePayloads()
    {
        return true;
    }

    /// <summary>
    ///     Determines whether one newly resolved payload pair should be
    ///     persisted as a fresh <see cref="GameWindow" /> row when no exact
    ///     persisted row already exists.
    /// </summary>
    /// <param name="originalPayload">
    ///     The original-facing payload currently visible in the addon.
    /// </param>
    /// <param name="translatedPayload">
    ///     The translated payload resolved for the current live state.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the payload is ready for persistence;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private protected virtual bool ShouldPersistNewGameWindowPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return true;
    }

    /// <summary>
    ///     Normalizes one resolved translated payload before it is applied to
    ///     the live addon surface.
    /// </summary>
    /// <param name="originalPayload">
    ///     The original-facing payload currently visible in the addon.
    /// </param>
    /// <param name="translatedPayload">
    ///     The translated payload that was resolved from cache, DB, or one
    ///     addon-local supplemental source.
    /// </param>
    /// <returns>
    ///     The translated payload that should be applied for this addon.
    /// </returns>
    private protected virtual DbFirstGameWindowPayload NormalizeResolvedTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return translatedPayload;
    }

    /// <summary>
    ///     Determines whether one resolved translated payload is complete
    ///     enough to be used for apply/persist in the current addon.
    /// </summary>
    /// <param name="originalPayload">
    ///     The original-facing payload currently visible in the addon.
    /// </param>
    /// <param name="translatedPayload">
    ///     The translated payload that would be applied.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the resolved translated payload should
    ///     be accepted; otherwise <see langword="false" />.
    /// </returns>
    private protected virtual bool ShouldAcceptResolvedTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return true;
    }

    /// <summary>
    ///     Determines whether one newly observed payload should be sent to the
    ///     remote translation path when no exact persisted row or supplemental
    ///     translated payload exists yet.
    /// </summary>
    /// <param name="originalPayload">
    ///     The original-facing payload currently visible in the addon.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when background translation may be queued;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private protected virtual bool ShouldQueueNewGameWindowTranslation(
        DbFirstGameWindowPayload originalPayload)
    {
        return true;
    }

    /// <summary>
    ///     Resolves the persisted class/job discriminator for one canonical
    ///     <see cref="GameWindow" /> row when the addon content varies by the
    ///     active class or job.
    /// </summary>
    /// <param name="originalPayload">The original payload being persisted.</param>
    /// <param name="translatedPayload">The translated payload being persisted.</param>
    /// <returns>
    ///     The class/job identifier to persist for this payload, or
    ///     <see langword="null" /> when the addon does not require job-aware
    ///     persistence.
    /// </returns>
    private protected virtual uint? GetPersistedGameWindowClassJobId(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return null;
    }

    /// <summary>
    ///     Resolves the class/job discriminator that should scope persisted
    ///     <see cref="GameWindow" /> lookups for one live payload.
    /// </summary>
    /// <param name="payload">The live or original-facing payload.</param>
    /// <returns>
    ///     The class/job identifier that should scope persisted lookup for the
    ///     payload, or <see langword="null" /> when the addon does not
    ///     require job-aware lookup.
    /// </returns>
    private protected virtual uint? GetLookupGameWindowClassJobId(
        DbFirstGameWindowPayload payload)
    {
        return null;
    }

    /// <summary>
    ///     Determines whether this handler should selectively restore stale
    ///     translated text-node values from the previously applied runtime
    ///     state before capturing a new payload for the same live addon.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when stale translated text-node cleanup is
    ///     safe and desirable for this addon; otherwise
    ///     <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldRestoreStaleTranslatedTextNodesOnPayloadChange()
    {
        return false;
    }

    /// <summary>
    ///     Determines whether this handler should keep polling on
    ///     <see cref="AddonEvent.PreDraw" /> after a native translation has
    ///     already been applied successfully.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when continuous pre-draw polling is still
    ///     useful for this addon; otherwise <see langword="false" />.
    /// </returns>
    protected virtual bool ShouldRefreshAppliedStateOnPreDraw()
    {
        return true;
    }

    /// <summary>
    ///     Gets the additive grace window during which this handler may keep
    ///     refreshing on <see cref="AddonEvent.PreDraw" /> after one
    ///     lifecycle event has already requested a refresh.
    /// </summary>
    /// <returns>
    ///     The post-lifecycle pre-draw refresh window. A zero value disables
    ///     this behavior.
    /// </returns>
    protected virtual TimeSpan GetAppliedStatePreDrawRefreshWindow()
    {
        return TimeSpan.Zero;
    }

    /// <summary>
    ///     Performs addon-local follow-up work after one original payload has
    ///     been restored into the live native surface.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="translatedPayload">
    ///     The translated payload that was visible before restoration.
    /// </param>
    /// <param name="originalPayload">
    ///     The original payload that has just been restored.
    /// </param>
    private protected virtual void AfterRestorePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload translatedPayload,
        DbFirstGameWindowPayload originalPayload)
    {
    }

    /// <summary>
    ///     Tries to apply one text-node payload using addon-local matching
    ///     semantics instead of the default stable-key path.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="sourcePayload">
    ///     The payload describing the currently visible text values that should
    ///     be rewritten.
    /// </param>
    /// <param name="targetPayload">
    ///     The payload describing the desired post-apply text values.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the addon handled text-node apply
    ///     itself; otherwise <see langword="false" /> to fall back to the
    ///     stable-key path.
    /// </returns>
    private protected virtual bool TryApplyCustomTextNodePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        return false;
    }

    /// <summary>
    ///     Tries to resolve a translated payload from one addon-specific
    ///     canonical source other than persisted <see cref="GameWindow" />
    ///     rows.
    /// </summary>
    /// <param name="originalPayload">The visible original-facing payload.</param>
    /// <param name="translatedPayload">
    ///     Receives the translated payload when resolution succeeds.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when a translated payload was resolved;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private protected virtual bool TryResolveSupplementalTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;
        return false;
    }

    /// <summary>
    ///     Tries to recover one canonical original payload from one addon-
    ///     specific source other than persisted rows when the live UI is
    ///     already showing translated text.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="originalPayload">
    ///     Receives the recovered original payload.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one canonical original payload was
    ///     resolved; otherwise <see langword="false" />.
    /// </returns>
    private protected virtual bool TryResolveSupplementalOriginalPayload(
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;
        return false;
    }

    /// <summary>
    ///     Tries to project one cached resolved payload pair onto the current
    ///     live payload shape when an exact mode-switch reapply match fails.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="runtimeState">The last resolved runtime state.</param>
    /// <param name="originalPayload">
    ///     Receives the projected original payload when projection succeeds.
    /// </param>
    /// <param name="translatedPayload">
    ///     Receives the projected translated payload when projection succeeds.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the addon wants to reuse one projected
    ///     payload pair for mode-switch reapply; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private protected virtual bool TryResolveProjectedModeSwitchPayloads(
        DbFirstGameWindowPayload livePayload,
        DbFirstGameWindowRuntimeState runtimeState,
        out DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;
        translatedPayload = DbFirstGameWindowPayload.Empty;
        return false;
    }

    /// <summary>
    ///     Determines whether cleanup should be deferred when one cleanup
    ///     lifecycle event fires but the addon is still visibly present.
    /// </summary>
    /// <param name="evt">The cleanup event being handled.</param>
    /// <returns>
    ///     <see langword="true" /> when cleanup should be deferred for this
    ///     addon; otherwise <see langword="false" />.
    /// </returns>
    private protected virtual bool ShouldDeferCleanupWhileVisible(
        AddonEvent evt)
    {
        return false;
    }

    /// <summary>
    ///     Determines whether a named addon is currently visible.
    /// </summary>
    /// <param name="addonName">The addon name to probe.</param>
    /// <returns>
    ///     <see langword="true" /> when the addon exists and is visible;
    ///     otherwise <see langword="false" />.
    /// </returns>
    protected bool IsAddonVisible(string addonName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addonName);

        var addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
            addonName,
            1);
        return addon != null && addon->IsVisible;
    }

    /// <summary>
    ///     Tries to register addon-specific hover targets when the default
    ///     visible-text-node path is not the right surface for this addon.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <param name="displayMode">The active display mode.</param>
    /// <returns>
    ///     <see langword="true" /> when custom hover targets were registered
    ///     and the default text-node path should be skipped.
    /// </returns>
    private protected virtual bool TryRegisterCustomHoverTooltips(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        JournalTranslationDisplayMode displayMode)
    {
        return false;
    }

    /// <summary>
    ///     Handles setup/refresh/update events.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="args">The event args.</param>
    protected virtual void OnLifecycleEvent(AddonEvent evt, AddonArgs args)
    {
        this.TryFinalizeDeferredCleanup();
        this.ArmAppliedStatePreDrawRefreshWindow();
        this.RefreshOrQueue();
    }

    /// <summary>
    ///     Handles lightweight retries and config toggles.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="args">The event args.</param>
    protected virtual void OnPreDrawEvent(AddonEvent evt, AddonArgs args)
    {
        this.TryFinalizeDeferredCleanup();

        if (!this.enabledSelector(this.config))
        {
            this.RestoreOriginalPayloadIfNeeded();
            this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
            this.lastResolvedState = null;
            this.lastAppliedDisplayMode = null;
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.displayModeSelector(this.config),
            this.config.OverlayOnlyLanguage);
        var usesHoverTooltips =
            TranslationDisplayModeHelper.UsesHoverTooltips(displayMode);
        var shouldContinueAppliedStateRefresh =
            this.ShouldContinueAppliedStateRefreshOnPreDraw();

        if (this.lastResolvedState != null &&
            this.lastAppliedDisplayMode != null &&
            this.lastAppliedDisplayMode != displayMode &&
            this.TryGetVisibleAddon(out var visibleAddon))
        {
            var livePayload = this.CaptureLivePayload(visibleAddon);
            var matchesOriginal = false;
            var matchesTranslated = false;
            var originalPayload = DbFirstGameWindowPayload.Empty;
            var translatedPayload = DbFirstGameWindowPayload.Empty;
            if (!livePayload.IsEmpty &&
                (livePayload.MatchesOriginal(
                     this.lastResolvedState.OriginalPayload) ||
                 livePayload.MatchesTranslated(
                     this.lastResolvedState.TranslatedPayload)))
            {
                matchesOriginal = livePayload.MatchesOriginal(
                    this.lastResolvedState.OriginalPayload);
                matchesTranslated = livePayload.MatchesTranslated(
                    this.lastResolvedState.TranslatedPayload);
                originalPayload = this.lastResolvedState.OriginalPayload;
                translatedPayload = this.lastResolvedState.TranslatedPayload;
            }
            else if (!livePayload.IsEmpty &&
                     this.TryResolveProjectedModeSwitchPayloads(
                         livePayload,
                         this.lastResolvedState,
                         out originalPayload,
                         out translatedPayload))
            {
            }

            if (!originalPayload.IsEmpty && !translatedPayload.IsEmpty)
            {
                if (!TranslationDisplayModeHelper.WritesNativeTranslation(
                        displayMode))
                {
                    this.RestorePayloadIfNeeded(
                        visibleAddon,
                        translatedPayload,
                        originalPayload);
                }

                this.ApplyPayload(
                    visibleAddon,
                    originalPayload,
                    translatedPayload,
                    this.lastResolvedState.PayloadKey,
                    displayMode);
                this.nextRetryUtc = DateTime.MinValue;
                return;
            }
        }

        if (this.lastAppliedDisplayMode == displayMode &&
            !shouldContinueAppliedStateRefresh)
        {
            if (this.runtimeState != null)
            {
                if (usesHoverTooltips)
                {
                    this.hoverTooltipManager.TouchByPrefix(
                        this.hoverTooltipKeyPrefix);
                }

                return;
            }

            if (usesHoverTooltips &&
                this.lastResolvedState != null)
            {
                this.hoverTooltipManager.TouchByPrefix(
                    this.hoverTooltipKeyPrefix);
                return;
            }
        }

        if (this.runtimeState == null && DateTime.UtcNow < this.nextRetryUtc)
        {
            return;
        }

        this.RefreshOrQueue();
    }

    /// <summary>
    ///     Handles cleanup when the addon hides or finalizes.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="args">The event args.</param>
    protected virtual void OnCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        if (this.ShouldDeferCleanupWhileVisible(evt))
        {
            this.deferredCleanupPending = true;
            this.deferredCleanupEvent = evt;
            this.deferredCleanupUtc = DateTime.UtcNow;
            return;
        }

        this.ClearResolvedState();
    }

    /// <inheritdoc />
    public virtual void OnPluginUnload()
    {
        this.ClearResolvedState();
    }

    /// <summary>
    ///     Registers one translated hover target using explicit bounds.
    /// </summary>
    /// <param name="keySuffix">The stable per-target key suffix.</param>
    /// <param name="topLeft">The top-left screen coordinate.</param>
    /// <param name="bottomRight">The bottom-right screen coordinate.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <param name="displayMode">The active display mode.</param>
    private protected void RegisterTranslatedHoverTooltip(
        string keySuffix,
        Vector2 topLeft,
        Vector2 bottomRight,
        string originalText,
        string translatedText,
        JournalTranslationDisplayMode displayMode)
    {
        var showOriginalTooltips =
            TranslationDisplayModeHelper.ShowsOriginalTooltips(displayMode);
        var tooltipBody = showOriginalTooltips ? originalText : translatedText;
        if (!ShouldCaptureText(tooltipBody))
        {
            return;
        }

        this.hoverTooltipManager.Register(
            $"{this.hoverTooltipKeyPrefix}{keySuffix}",
            topLeft,
            bottomRight,
            string.Empty,
            tooltipBody,
            true);
    }

    /// <summary>
    ///     Performs the DB-first refresh path for the addon.
    /// </summary>
    private void RefreshOrQueue()
    {
        if (!this.enabledSelector(this.config))
        {
            this.RestoreOriginalPayloadIfNeeded();
            this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
            this.lastResolvedState = null;
            this.lastAppliedDisplayMode = null;
            return;
        }

        if (!this.TryGetVisibleAddon(out var addon))
        {
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.displayModeSelector(this.config),
            this.config.OverlayOnlyLanguage);
        if (!TranslationDisplayModeHelper.WritesNativeTranslation(displayMode))
        {
            this.RestoreOriginalPayloadIfNeeded();
        }

        var livePayload = this.CaptureLivePayload(addon);
        if (livePayload.IsEmpty)
        {
            this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
            return;
        }

        if (this.useTextNodes &&
            this.runtimeState != null &&
            this.ShouldRestoreStaleTranslatedTextNodesOnPayloadChange() &&
            !livePayload.MatchesTranslated(this.runtimeState.TranslatedPayload) &&
            !livePayload.MatchesOriginal(this.runtimeState.OriginalPayload) &&
            this.TryRestoreStaleTranslatedTextNodes(addon, livePayload))
        {
            livePayload = this.CaptureLivePayload(addon);
            if (livePayload.IsEmpty)
            {
                this.hoverTooltipManager.RemoveByPrefix(
                    this.hoverTooltipKeyPrefix);
                return;
            }
        }

        var originalPayload = this.ResolveOriginalPayload(livePayload);
        if (this.stringArrayDataType is { })
        {
            var originalStructuredPayload =
                this.BuildStructuredPayload(originalPayload);
            var structuredPayloadKey =
                this.BuildPayloadKey(originalStructuredPayload);

            DbFirstStructuredStringArrayProjection projection = default!;
            var exactStructuredMatch = this.TryFindStructuredPayload(
                originalStructuredPayload,
                out var translatedStructuredPayload);
            var exactProjectionMatch = exactStructuredMatch &&
                                       DbFirstStructuredStringArrayHelper
                                           .TryProjectTranslatedPayload(
                                               originalStructuredPayload,
                                               translatedStructuredPayload,
                                               out projection);

            if (!exactStructuredMatch ||
                !exactProjectionMatch)
            {
                var supplementalResolved =
                    this.TryResolveSupplementalTranslatedPayload(
                        originalPayload,
                        out var supplementalTranslatedPayload);

                if (supplementalResolved)
                {
                    supplementalTranslatedPayload =
                        supplementalTranslatedPayload.ProjectToShape(
                            originalPayload);
                    this.ApplyPayload(
                        addon,
                        originalPayload,
                        supplementalTranslatedPayload,
                        structuredPayloadKey,
                        displayMode);
                    ClearFailedPayloadRetry(structuredPayloadKey);
                    this.nextRetryUtc = DateTime.MinValue;
                    return;
                }

                if (this.lastOriginalRecoveryWasUnstableTranslatedState)
                {
                    this.hoverTooltipManager.RemoveByPrefix(
                        this.hoverTooltipKeyPrefix);
                    this.nextRetryUtc = DateTime.UtcNow + RetryInterval;
                    return;
                }

                if (TryGetFailedPayloadRetryUtc(
                        structuredPayloadKey,
                        out var failedRetryUtc))
                {
                    this.hoverTooltipManager.RemoveByPrefix(
                        this.hoverTooltipKeyPrefix);
                    this.nextRetryUtc = failedRetryUtc;
                    return;
                }

                this.QueueTranslationIfNeeded(
                    originalPayload,
                    structuredPayloadKey,
                    originalStructuredPayload);
                this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
                this.nextRetryUtc = DateTime.UtcNow + RetryInterval;
                return;
            }

            this.ApplyPayload(
                addon,
                originalPayload,
                new DbFirstGameWindowPayload(
                    projection.AtkValues,
                    projection.StringArrayValues,
                    projection.TextNodes),
                structuredPayloadKey,
                displayMode);
            ClearFailedPayloadRetry(structuredPayloadKey);
            this.nextRetryUtc = DateTime.MinValue;
            return;
        }

        var originalJson = originalPayload.Serialize();
        var payloadKey = this.BuildPayloadKey(originalJson);

        var translatedPayload = DbFirstGameWindowPayload.Empty;
        var hasExactTranslatedPayload =
            this.TryFindGameWindow(
                originalPayload,
                originalJson,
                out var gameWindow) &&
            TryParseTranslatedPayload(
                gameWindow?.TranslatedWindowStrings,
                originalPayload,
                out translatedPayload);
        if (hasExactTranslatedPayload)
        {
            translatedPayload = this.NormalizeResolvedTranslatedPayload(
                originalPayload,
                translatedPayload);
            hasExactTranslatedPayload =
                this.ShouldAcceptResolvedTranslatedPayload(
                    originalPayload,
                    translatedPayload);
        }

        if (!hasExactTranslatedPayload)
        {
            if (this.ShouldReuseCompatiblePayloads() &&
                this.TryResolveCompatibleGameWindowPayload(
                    originalPayload,
                    out var compatibleOriginalPayload,
                    out var compatibleTranslatedPayload))
            {
                compatibleOriginalPayload =
                    compatibleOriginalPayload.ProjectToShape(originalPayload);
                compatibleTranslatedPayload =
                    compatibleTranslatedPayload.ProjectToShape(originalPayload);
                compatibleTranslatedPayload =
                    this.NormalizeResolvedTranslatedPayload(
                        compatibleOriginalPayload,
                        compatibleTranslatedPayload);
                if (!this.ShouldAcceptResolvedTranslatedPayload(
                        compatibleOriginalPayload,
                        compatibleTranslatedPayload))
                {
                    goto TrySupplementalTranslatedPayload;
                }

                var compatiblePayloadKey = this.BuildPayloadKey(
                    compatibleOriginalPayload.Serialize());
                this.ApplyPayload(
                    addon,
                    compatibleOriginalPayload,
                    compatibleTranslatedPayload,
                    compatiblePayloadKey,
                    displayMode);
                ClearFailedPayloadRetry(compatiblePayloadKey);
                this.nextRetryUtc = DateTime.MinValue;
                return;
            }

        TrySupplementalTranslatedPayload:
            if (this.TryResolveSupplementalTranslatedPayload(
                    originalPayload,
                    out var supplementalTranslatedPayload))
            {
                supplementalTranslatedPayload =
                    supplementalTranslatedPayload.ProjectToShape(
                        originalPayload);
                supplementalTranslatedPayload =
                    this.NormalizeResolvedTranslatedPayload(
                        originalPayload,
                        supplementalTranslatedPayload);
                if (!this.ShouldAcceptResolvedTranslatedPayload(
                        originalPayload,
                        supplementalTranslatedPayload))
                {
                    goto QueueNewTranslation;
                }

                if (this.ShouldPersistNewGameWindowPayload(
                        originalPayload,
                        supplementalTranslatedPayload))
                {
                    this.PersistResolvedGameWindowPayload(
                        originalPayload,
                        supplementalTranslatedPayload);
                }

                this.ApplyPayload(
                    addon,
                    originalPayload,
                    supplementalTranslatedPayload,
                    payloadKey,
                    displayMode);
                ClearFailedPayloadRetry(payloadKey);
                this.nextRetryUtc = DateTime.MinValue;
                return;
            }

        QueueNewTranslation:
            if (!this.ShouldQueueNewGameWindowTranslation(originalPayload))
            {
                this.hoverTooltipManager.RemoveByPrefix(
                    this.hoverTooltipKeyPrefix);
                this.nextRetryUtc = DateTime.UtcNow + RetryInterval;
                return;
            }

            if (TryGetFailedPayloadRetryUtc(payloadKey, out var failedRetryUtc))
            {
                this.hoverTooltipManager.RemoveByPrefix(
                    this.hoverTooltipKeyPrefix);
                this.nextRetryUtc = failedRetryUtc;
                return;
            }

            this.QueueTranslationIfNeeded(originalPayload, payloadKey);
            this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
            this.nextRetryUtc = DateTime.UtcNow + RetryInterval;
            return;
        }

        this.ApplyPayload(
            addon,
            originalPayload,
            translatedPayload,
            payloadKey,
            displayMode);
        ClearFailedPayloadRetry(payloadKey);
        this.nextRetryUtc = DateTime.MinValue;
    }

    /// <summary>
    ///     Finalizes one deferred cleanup when the grace window expires or
    ///     cancels it when the addon remains visibly alive across the transient
    ///     lifecycle blip.
    /// </summary>
    private void TryFinalizeDeferredCleanup()
    {
        if (!this.deferredCleanupPending)
        {
            return;
        }

        var age = DateTime.UtcNow - this.deferredCleanupUtc;
        if (this.TryGetVisibleAddon(out _))
        {
            if (age <= DeferredCleanupGraceInterval)
            {
                this.deferredCleanupPending = false;
                this.deferredCleanupEvent = default;
                this.deferredCleanupUtc = DateTime.MinValue;
                return;
            }

            this.ClearResolvedState();
            return;
        }

        if (age <= DeferredCleanupGraceInterval)
        {
            return;
        }

        this.ClearResolvedState();
    }

    /// <summary>
    ///     Clears all resolved runtime state and hover registrations for this
    ///     addon handler.
    /// </summary>
    private void ClearResolvedState()
    {
        this.RestoreOriginalPayloadIfNeeded();
        this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
        this.runtimeState = null;
        this.lastResolvedState = null;
        this.lastAppliedDisplayMode = null;
        this.nextRetryUtc = DateTime.MinValue;
        this.deferredCleanupPending = false;
        this.deferredCleanupEvent = default;
        this.deferredCleanupUtc = DateTime.MinValue;
        this.appliedStatePreDrawRefreshUntilUtc = DateTime.MinValue;
    }

    /// <summary>
    ///     Arms one short pre-draw refresh window after a lifecycle event so
    ///     addons with slightly delayed chrome population can settle without
    ///     requiring permanent per-frame polling.
    /// </summary>
    private void ArmAppliedStatePreDrawRefreshWindow()
    {
        var duration = this.GetAppliedStatePreDrawRefreshWindow();
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        this.appliedStatePreDrawRefreshUntilUtc = DateTime.UtcNow + duration;
    }

    /// <summary>
    ///     Gets whether the handler should keep polling on
    ///     <see cref="AddonEvent.PreDraw" /> even after one translated runtime
    ///     state is already available.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when continuous polling is explicitly
    ///     requested or the lifecycle-triggered grace window is still active;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool ShouldContinueAppliedStateRefreshOnPreDraw()
    {
        return this.ShouldRefreshAppliedStateOnPreDraw() ||
               DateTime.UtcNow < this.appliedStatePreDrawRefreshUntilUtc;
    }

    /// <summary>
    ///     Tries to resolve the live addon.
    /// </summary>
    /// <param name="addon">The addon pointer.</param>
    /// <returns>True when the addon is visible.</returns>
    private bool TryGetVisibleAddon(out AtkUnitBase* addon)
    {
        addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
            this.addonName);
        return addon != null && addon->IsVisible;
    }

    /// <summary>
    ///     Captures the current live payload from the addon and its backing
    ///     string array, if any.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <returns>The captured payload.</returns>
    private DbFirstGameWindowPayload CaptureLivePayload(AtkUnitBase* addon)
    {
        SortedDictionary<int, string> atkValues = new();
        SortedDictionary<int, string> stringArrayValues = new();
        SortedDictionary<string, string> textNodes =
            new(StringComparer.Ordinal);

        if (this.useAtkValues)
        {
            var atkValueSpan = new Span<AtkValue>(
                addon->AtkValues,
                addon->AtkValuesCount);
            for (var index = 0; index < atkValueSpan.Length; index++)
            {
                ref var value = ref atkValueSpan[index];
                if (value.Type is not
                    (ValueType.String or
                     ValueType.String8 or
                     ValueType.ManagedString))
                {
                    continue;
                }

                var text = this.ReadAtkValueText(in value);
                if (!ShouldCaptureText(text))
                {
                    continue;
                }

                atkValues[index] = text;
            }
        }

        if (this.stringArrayDataType is { } arrayType)
        {
            var stringArrayData = AtkStage.Instance()->GetStringArrayData(
                arrayType);
            if (stringArrayData != null &&
                stringArrayData->StringArray != null &&
                stringArrayData->Size > 0 &&
                this.ShouldCaptureStringArrayValues(
                    stringArrayData->SubscribedAddonsCount))
            {
                for (var index = 0; index < stringArrayData->Size; index++)
                {
                    var span = stringArrayData->StringArray[index]
                        .AsReadOnlySeStringSpan();
                    var text = span.ExtractText();
                    if (!ShouldCaptureText(text))
                    {
                        continue;
                    }

                    stringArrayValues[index] = text;
                }
            }
        }

        if (this.useTextNodes)
        {
            textNodes = this.CaptureVisibleTextNodes(addon);
        }

        return new DbFirstGameWindowPayload(
            atkValues,
            stringArrayValues,
            textNodes);
    }

    /// <summary>
    ///     Captures the currently visible text nodes in stable tree order using
    ///     a per-node key derived from node id and duplicate ordinal.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <returns>The captured text-node map.</returns>
    private SortedDictionary<string, string> CaptureVisibleTextNodes(
        AtkUnitBase* addon)
    {
        var capturedNodes = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        var nodeAddresses = AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(
            addon);
        var ordinalsByNodeId = new Dictionary<uint, int>();

        foreach (var nodeAddress in nodeAddresses)
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var visibleText = this.ReadTextNode(textNode);
            if (!ShouldCaptureText(visibleText))
            {
                continue;
            }

            if (!this.ShouldCaptureTextNode(textNode, visibleText))
            {
                continue;
            }

            var nodeId = textNode->AtkResNode.NodeId;
            ordinalsByNodeId.TryGetValue(nodeId, out var ordinal);
            ordinalsByNodeId[nodeId] = ordinal + 1;
            capturedNodes[BuildTextNodeKey(nodeId, ordinal)] = visibleText;
        }

        return this.NormalizeCapturedTextNodes(capturedNodes);
    }

    /// <summary>
    ///     Resolves the original payload to use for DB lookup.
    /// </summary>
    /// <param name="livePayload">The currently visible payload.</param>
    /// <returns>The original payload for DB lookup and restore.</returns>
    private DbFirstGameWindowPayload ResolveOriginalPayload(
        DbFirstGameWindowPayload livePayload)
    {
        this.lastOriginalRecoveryWasUnstableTranslatedState = false;

        if (this.runtimeState == null)
        {
            if (this.TryRecoverOriginalPayloadFromPersistedRows(
                    livePayload,
                    out var recoveredPayload))
            {
                return recoveredPayload;
            }

            return this.TryResolveSupplementalOriginalPayload(
                livePayload,
                out var supplementalOriginalPayload)
                ? supplementalOriginalPayload
                : livePayload;
        }

        if (livePayload.MatchesTranslated(this.runtimeState.TranslatedPayload))
        {
            return this.runtimeState.OriginalPayload;
        }

        if (livePayload.MatchesOriginal(this.runtimeState.OriginalPayload))
        {
            return this.runtimeState.OriginalPayload;
        }

        this.runtimeState = null;
        if (this.TryRecoverOriginalPayloadFromPersistedRows(
                livePayload,
                out var persistedRecoveredPayload))
        {
            return persistedRecoveredPayload;
        }

        return this.TryResolveSupplementalOriginalPayload(
            livePayload,
            out var finalSupplementalOriginalPayload)
            ? finalSupplementalOriginalPayload
            : livePayload;
    }

    /// <summary>
    ///     Builds a stable in-flight key for one payload.
    /// </summary>
    /// <param name="originalJson">The serialized original payload.</param>
    /// <returns>The stable payload key.</returns>
    private string BuildPayloadKey(string originalJson)
    {
        var targetLanguageCode =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang);
        return $"{this.addonName}|{targetLanguageCode}|{this.config.ChosenTransEngine}|{GetGameVersion()}|{originalJson}";
    }

    /// <summary>
    ///     Builds a stable in-flight key for one canonical structured payload.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <returns>The stable payload key.</returns>
    private string BuildPayloadKey(StringArrayStructuredPayload originalPayload)
    {
        var targetLanguageCode =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang);
        return $"{this.addonName}|{targetLanguageCode}|{this.config.ChosenTransEngine}|{GetGameVersion()}|{originalPayload.ComputeSourceContentHash()}";
    }

    /// <summary>
    ///     Builds the canonical structured payload used by
    ///     <c>StringArrayDatas</c> lookup and persistence.
    /// </summary>
    /// <param name="payload">The live payload to encode.</param>
    /// <returns>The canonical structured payload.</returns>
    private StringArrayStructuredPayload BuildStructuredPayload(
        DbFirstGameWindowPayload payload)
    {
        return DbFirstStructuredStringArrayHelper.BuildCanonicalPayload(
            this.stringArrayDataType!.Value.ToString(),
            $"addon:{this.addonName}",
            payload.AtkValues,
            payload.StringArrayValues,
            payload.TextNodes);
    }

    /// <summary>
    ///     Tries to find a matching canonical <c>StringArrayDatas</c> row in
    ///     cache or DB and resolve its translated payload.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The resolved translated payload.</param>
    /// <returns>True when a canonical translated payload was found.</returns>
    private bool TryFindStructuredPayload(
        StringArrayStructuredPayload originalPayload,
        out StringArrayStructuredPayload translatedPayload)
    {
        var language = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
            this.config.Lang);
        var gameVersion = GetGameVersion();
        var sourceHash = originalPayload.ComputeSourceContentHash();

        var row = StringArrayDataCacheManager.TryFindCanonicalMatch(
            originalPayload.Type,
            originalPayload.ContextKey,
            language,
            this.config.ChosenTransEngine,
            gameVersion,
            sourceHash);
        if (row == null)
        {
            var probe = StringArrayDataPersistenceHelper.CreateCanonicalRow(
                originalPayload.Type,
                ClientStateInterface.ClientLanguage.Humanize(),
                language,
                this.config.ChosenTransEngine,
                gameVersion,
                originalPayload);

            row = StringArrayDataPersistenceHelper.FindStringArrayData(
                ConfigDirectory,
                probe);
            if (row != null)
            {
                StringArrayDataCacheManager.Update(row);
            }
        }

        if (row == null ||
            !StringArrayStructuredPayloadResolver.TryResolvePayloads(
                row,
                out _,
                out var resolvedTranslatedPayload) ||
            resolvedTranslatedPayload == null)
        {
            translatedPayload = new StringArrayStructuredPayload();
            return false;
        }

        translatedPayload = resolvedTranslatedPayload;
        return true;
    }

    /// <summary>
    ///     Tries to recover the canonical original payload from persisted rows
    ///     when the live UI is already showing translated or mixed text.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="originalPayload">The recovered original payload.</param>
    /// <returns>
    ///     <see langword="true" /> when a unique persisted candidate matches
    ///     the live payload.
    /// </returns>
    private bool TryRecoverOriginalPayloadFromPersistedRows(
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        if (this.stringArrayDataType is { })
        {
            return this.TryRecoverStructuredOriginalPayload(
                livePayload,
                out originalPayload);
        }

        return this.TryRecoverGameWindowOriginalPayload(
            livePayload,
            out originalPayload);
    }

    /// <summary>
    ///     Tries to recover one canonical original payload from persisted
    ///     <c>StringArrayDatas</c> rows.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="originalPayload">The recovered original payload.</param>
    /// <returns>
    ///     <see langword="true" /> when a unique persisted candidate matches
    ///     the live payload.
    /// </returns>
    private bool TryRecoverStructuredOriginalPayload(
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        var scopeProbe = this.BuildStructuredPayload(livePayload);
        var sourceLanguageCode =
            RuntimeLanguageHelper.GetCurrentGameLanguageCode();
        var targetLanguageCode =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang);
        var candidates = new List<DbFirstPayloadRecoveryCandidate>();
        foreach (var row in StringArrayDataCacheManager.GetCandidates(
                     scopeProbe.Type,
                     scopeProbe.ContextKey,
                     targetLanguageCode,
                     this.config.ChosenTransEngine,
                     GetGameVersion()))
        {
            if (!StringArrayStructuredPayloadResolver.TryResolvePayloads(
                    row,
                    out var resolvedOriginalPayload,
                    out var resolvedTranslatedPayload) ||
                resolvedOriginalPayload == null ||
                resolvedTranslatedPayload == null ||
                !DbFirstStructuredStringArrayHelper.TryProjectTranslatedPayload(
                    resolvedOriginalPayload,
                    resolvedTranslatedPayload,
                    out var translatedProjection))
            {
                continue;
            }

            var originalProjection =
                DbFirstStructuredStringArrayHelper.ProjectOriginalPayload(
                    resolvedOriginalPayload);
            var originalCandidatePayload = new DbFirstGameWindowPayload(
                originalProjection.AtkValues,
                originalProjection.StringArrayValues,
                originalProjection.TextNodes);
            var translatedCandidatePayload = new DbFirstGameWindowPayload(
                translatedProjection.AtkValues,
                translatedProjection.StringArrayValues,
                translatedProjection.TextNodes);
            if (!IsUsableRecoveryCandidate(
                    row.OriginalLang,
                    row.TranslationLang,
                    originalCandidatePayload,
                    translatedCandidatePayload,
                    sourceLanguageCode,
                    targetLanguageCode))
            {
                continue;
            }

            candidates.Add(
                new DbFirstPayloadRecoveryCandidate(
                    originalCandidatePayload,
                    translatedCandidatePayload));
        }

        if (DbFirstPayloadRecoveryHelper.TryRecoverOriginalPayload(
                livePayload,
                candidates,
                out originalPayload))
        {
            return true;
        }

        this.lastOriginalRecoveryWasUnstableTranslatedState =
            DbFirstPayloadRecoveryHelper.HasTranslatedSlotEvidence(
                livePayload,
                candidates);
        return false;
    }

    /// <summary>
    ///     Persists one resolved original/translated payload pair as a
    ///     canonical <see cref="GameWindow" /> row and refreshes cache state.
    /// </summary>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    private protected void PersistResolvedGameWindowPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        var classJobId = this.GetPersistedGameWindowClassJobId(
            originalPayload,
            translatedPayload);
        this.PersistResolvedGameWindowPayload(
            originalPayload,
            translatedPayload,
            classJobId);
    }

    /// <summary>
    ///     Persists one resolved original/translated payload pair as a
    ///     canonical <see cref="GameWindow" /> row using an explicit
    ///     class/job discriminator captured by the caller.
    /// </summary>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <param name="classJobId">
    ///     The class/job identifier to persist with the row.
    /// </param>
    private protected void PersistResolvedGameWindowPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        uint? classJobId)
    {
        var row = new GameWindow(
            this.addonName,
            originalPayload.Serialize(),
            ClientStateInterface.ClientLanguage.Humanize(),
            translatedPayload.Serialize(),
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang),
            this.config.ChosenTransEngine,
            GetGameVersion(),
            createdDate: null,
            updatedDate: null,
            classJobId: classJobId);

        _ = GameWindowPersistenceHelper.InsertGameWindow(
            ConfigDirectory,
            row,
            GameWindowCacheManager.Update);
    }

    /// <summary>
    ///     Translates and persists one non-structured
    ///     <see cref="GameWindow" /> payload.
    /// </summary>
    /// <param name="originalPayload">The original payload to translate.</param>
    /// <returns>
    ///     A task whose result is <see langword="true" /> when translation
    ///     completed successfully; otherwise <see langword="false" />.
    /// </returns>
    private protected virtual Task<bool> TranslateAndPersistGameWindowPayloadAsync(
        DbFirstGameWindowPayload originalPayload)
    {
        return GenericAddonHandlerHelper.PerformTranslationAndSaveAsync<GameWindow>(
            this.addonName,
            new Dictionary<int, string>(originalPayload.AtkValues),
            new Dictionary<int, string>(originalPayload.StringArrayValues),
            new Dictionary<string, string>(
                originalPayload.TextNodes,
                StringComparer.Ordinal),
            new Dictionary<int, string>(originalPayload.AtkValues),
            new Dictionary<int, string>(originalPayload.StringArrayValues),
            new Dictionary<string, string>(
                originalPayload.TextNodes,
                StringComparer.Ordinal),
            this.config,
            this.translationService);
    }

    /// <summary>
    ///     Tries to recover one canonical original payload from persisted
    ///     <see cref="GameWindow" /> rows.
    /// </summary>
    /// <param name="livePayload">The currently visible live payload.</param>
    /// <param name="originalPayload">The recovered original payload.</param>
    /// <returns>
    ///     <see langword="true" /> when a unique persisted candidate matches
    ///     the live payload.
    /// </returns>
    private bool TryRecoverGameWindowOriginalPayload(
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        var sourceLanguageCode =
            RuntimeLanguageHelper.GetCurrentGameLanguageCode();
        var targetLanguageCode =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang);
        var classJobId = this.GetLookupGameWindowClassJobId(livePayload);
        var candidates = new List<DbFirstPayloadRecoveryCandidate>();
        foreach (var row in GameWindowCacheManager.GetCandidates(
                     this.addonName,
                     targetLanguageCode,
                     this.config.ChosenTransEngine,
                     GetGameVersion(),
                     classJobId))
        {
            if (!TryParseSerializedPayload(
                    row.OriginalWindowStrings,
                    out var rowOriginalPayload) ||
                !TryParseSerializedPayload(
                    row.TranslatedWindowStrings,
                    out var rowTranslatedPayload))
            {
                continue;
            }

            if (!IsUsableRecoveryCandidate(
                    row.OriginalWindowStringsLang,
                    row.TranslationLang,
                    rowOriginalPayload,
                    rowTranslatedPayload,
                    sourceLanguageCode,
                    targetLanguageCode))
            {
                continue;
            }

            candidates.Add(
                new DbFirstPayloadRecoveryCandidate(
                    rowOriginalPayload,
                    rowTranslatedPayload));
        }

        if (DbFirstPayloadRecoveryHelper.TryRecoverOriginalPayload(
                livePayload,
                candidates,
                out originalPayload))
        {
            return true;
        }

        this.lastOriginalRecoveryWasUnstableTranslatedState =
            DbFirstPayloadRecoveryHelper.HasTranslatedSlotEvidence(
                livePayload,
                candidates);
        return false;
    }

    /// <summary>
    ///     Tries to find a matching <see cref="GameWindow" /> in cache or DB.
    /// </summary>
    /// <param name="originalJson">The serialized original payload.</param>
    /// <param name="gameWindow">The resolved row, if any.</param>
    /// <returns>True when a row was found.</returns>
    private bool TryFindGameWindow(
        DbFirstGameWindowPayload originalPayload,
        string originalJson,
        out GameWindow? gameWindow)
    {
        var language = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
            this.config.Lang);
        var gameVersion = GetGameVersion();
        var classJobId = this.GetLookupGameWindowClassJobId(originalPayload);

        gameWindow = GameWindowCacheManager.TryFindMatch(
            this.addonName,
            language,
            this.config.ChosenTransEngine,
            gameVersion,
            originalJson,
            classJobId);
        if (gameWindow != null)
        {
            return true;
        }

        if (GameWindowCacheManager.IsPreloaded)
        {
            return false;
        }

        if (classJobId.HasValue)
        {
            gameWindow = Echoglossian.FindEntity<GameWindow>(window =>
                window.WindowAddonName == this.addonName &&
                RuntimeLanguageHelper.LanguagesMatch(
                    window.TranslationLang,
                    language) &&
                window.TranslationEngine == this.config.ChosenTransEngine &&
                (window.GameVersion == null ||
                 window.GameVersion == gameVersion) &&
                window.ClassJobId == classJobId &&
                window.OriginalWindowStrings == originalJson);
            if (gameWindow != null)
            {
                GameWindowCacheManager.Update(gameWindow);
                return true;
            }
        }

        gameWindow = Echoglossian.FindEntity<GameWindow>(window =>
            window.WindowAddonName == this.addonName &&
            RuntimeLanguageHelper.LanguagesMatch(
                window.TranslationLang,
                language) &&
            window.TranslationEngine == this.config.ChosenTransEngine &&
            (window.GameVersion == null || window.GameVersion == gameVersion) &&
            window.ClassJobId == null &&
            window.OriginalWindowStrings == originalJson);
        if (gameWindow != null)
        {
            GameWindowCacheManager.Update(gameWindow);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one exact persisted <see cref="GameWindow" />
    ///     payload pair for the provided original payload.
    /// </summary>
    /// <param name="originalPayload">
    ///     The original-facing payload currently visible in the addon flow.
    /// </param>
    /// <param name="translatedPayload">
    ///     Receives the translated payload when one persisted exact match
    ///     exists.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when an exact persisted payload was found
    ///     and parsed successfully; otherwise <see langword="false" />.
    /// </returns>
    private protected bool TryResolveExactPersistedGameWindowPayload(
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;

        var originalJson = originalPayload.Serialize();
        return this.TryFindGameWindow(
                   originalPayload,
                   originalJson,
                   out var gameWindow) &&
               TryParseTranslatedPayload(
                   gameWindow?.TranslatedWindowStrings,
                   originalPayload,
                   out translatedPayload);
    }

    /// <summary>
    ///     Tries to resolve one compatible persisted <see cref="GameWindow" />
    ///     candidate when an exact original-payload match is not available.
    /// </summary>
    /// <param name="originalPayload">
    ///     The currently visible original-facing payload.
    /// </param>
    /// <param name="resolvedOriginalPayload">
    ///     The original payload of the compatible persisted candidate.
    /// </param>
    /// <param name="resolvedTranslatedPayload">
    ///     The translated payload of the compatible persisted candidate.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one unique compatible candidate was
    ///     found.
    /// </returns>
    private bool TryResolveCompatibleGameWindowPayload(
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload resolvedOriginalPayload,
        out DbFirstGameWindowPayload resolvedTranslatedPayload)
    {
        resolvedOriginalPayload = DbFirstGameWindowPayload.Empty;
        resolvedTranslatedPayload = DbFirstGameWindowPayload.Empty;

        var sourceLanguageCode =
            RuntimeLanguageHelper.GetCurrentGameLanguageCode();
        var targetLanguageCode =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang);
        var classJobId = this.GetLookupGameWindowClassJobId(originalPayload);
        var candidates = new List<DbFirstPayloadRecoveryCandidate>();

        foreach (var row in GameWindowCacheManager.GetCandidates(
                     this.addonName,
                     targetLanguageCode,
                     this.config.ChosenTransEngine,
                     GetGameVersion(),
                     classJobId))
        {
            if (!TryParseSerializedPayload(
                    row.OriginalWindowStrings,
                    out var rowOriginalPayload) ||
                !TryParseSerializedPayload(
                    row.TranslatedWindowStrings,
                    out var rowTranslatedPayload))
            {
                continue;
            }

            if (!IsUsableRecoveryCandidate(
                    row.OriginalWindowStringsLang,
                    row.TranslationLang,
                    rowOriginalPayload,
                    rowTranslatedPayload,
                    sourceLanguageCode,
                    targetLanguageCode))
            {
                continue;
            }

            candidates.Add(
                new DbFirstPayloadRecoveryCandidate(
                    rowOriginalPayload,
                    rowTranslatedPayload));
        }

        if (!DbFirstPayloadRecoveryHelper.TryResolveCompatibleCandidate(
                originalPayload,
                candidates,
                out var compatibleCandidate))
        {
            return false;
        }

        resolvedOriginalPayload = compatibleCandidate.OriginalPayload;
        resolvedTranslatedPayload = compatibleCandidate.TranslatedPayload;
        return true;
    }

    /// <summary>
    ///     Queues background translation and DB save when needed.
    /// </summary>
    /// <param name="payload">The original payload.</param>
    /// <param name="payloadKey">The stable payload key.</param>
    private void QueueTranslationIfNeeded(
        DbFirstGameWindowPayload payload,
        string payloadKey,
        StringArrayStructuredPayload? originalStructuredPayload = null)
    {
        if (!InFlightPayloads.TryAdd(payloadKey, 0))
        {
            return;
        }

        Task translationTask;
        if (originalStructuredPayload != null)
        {
            translationTask = DbFirstStructuredStringArrayHelper
                .TranslateAndPersistAsync(
                    originalStructuredPayload,
                    this.translationService,
                    ClientStateInterface.ClientLanguage.Humanize(),
                    RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                        this.config.Lang),
                    this.config.ChosenTransEngine,
                    GetGameVersion(),
                    ConfigDirectory)
                .ContinueWith(
                    task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion &&
                            task.Result != null)
                        {
                            StringArrayDataCacheManager.Update(task.Result);
                            ClearFailedPayloadRetry(payloadKey);
                            return;
                        }

                        MarkFailedPayloadRetry(payloadKey);
                    },
                    TaskScheduler.Default);
        }
        else
        {
            translationTask = this.TranslateAndPersistGameWindowPayloadAsync(
                    payload)
                .ContinueWith(
                    task =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion &&
                            task.Result)
                        {
                            ClearFailedPayloadRetry(payloadKey);
                            return;
                        }

                        MarkFailedPayloadRetry(payloadKey);
                    },
                    TaskScheduler.Default);
        }

        _ = translationTask.ContinueWith(
            completedTask => InFlightPayloads.TryRemove(payloadKey, out _),
            TaskScheduler.Default);
    }

    /// <summary>
    ///     Tries to resolve the next retry instant for one payload that
    ///     recently failed translation or persistence.
    /// </summary>
    /// <param name="payloadKey">The stable payload key.</param>
    /// <param name="retryUtc">The next retry instant.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload is still cooling down.
    /// </returns>
    private static bool TryGetFailedPayloadRetryUtc(
        string payloadKey,
        out DateTime retryUtc)
    {
        retryUtc = DateTime.MinValue;

        if (!FailedPayloadRetryUtc.TryGetValue(payloadKey, out retryUtc))
        {
            return false;
        }

        if (retryUtc > DateTime.UtcNow)
        {
            return true;
        }

        FailedPayloadRetryUtc.TryRemove(payloadKey, out _);
        retryUtc = DateTime.MinValue;
        return false;
    }

    /// <summary>
    ///     Marks one payload as temporarily failed so the runtime does not
    ///     hammer the translator or DB every refresh.
    /// </summary>
    /// <param name="payloadKey">The stable payload key.</param>
    private static void MarkFailedPayloadRetry(string payloadKey)
    {
        FailedPayloadRetryUtc[payloadKey] =
            DateTime.UtcNow + FailureRetryInterval;
    }

    /// <summary>
    ///     Clears any outstanding failure cooldown for one payload key.
    /// </summary>
    /// <param name="payloadKey">The stable payload key.</param>
    private static void ClearFailedPayloadRetry(string payloadKey)
    {
        FailedPayloadRetryUtc.TryRemove(payloadKey, out _);
    }

    /// <summary>
    ///     Applies a translated payload to the live addon.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <param name="payloadKey">The stable payload key.</param>
    private void ApplyPayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        string payloadKey,
        JournalTranslationDisplayMode displayMode)
    {
        if (TranslationDisplayModeHelper.WritesNativeTranslation(displayMode) &&
            this.useAtkValues &&
            addon->AtkValues != null)
        {
            foreach (var (index, translatedText) in translatedPayload.AtkValues)
            {
                if ((uint)index >= addon->AtkValuesCount)
                {
                    continue;
                }

                ref var currentValue = ref addon->AtkValues[index];
                if (currentValue.Type is not
                    (ValueType.String or
                     ValueType.String8 or
                     ValueType.ManagedString))
                {
                    continue;
                }

                var currentText = this.ReadAtkValueText(in currentValue);
                if (string.Equals(
                        currentText,
                        translatedText,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                currentValue.SetManagedString(translatedText);
            }
        }

        if (TranslationDisplayModeHelper.WritesNativeTranslation(displayMode) &&
            this.useTextNodes &&
            translatedPayload.TextNodes.Count > 0)
        {
            if (!this.TryApplyCustomTextNodePayload(
                    addon,
                    originalPayload,
                    translatedPayload))
            {
                this.ApplyTranslatedTextNodes(addon, translatedPayload.TextNodes);
            }
        }

        if (TranslationDisplayModeHelper.WritesNativeTranslation(displayMode) &&
            this.stringArrayDataType is { } arrayType)
        {
            var stringArrayData = AtkStage.Instance()->GetStringArrayData(
                arrayType);
            if (stringArrayData != null &&
                stringArrayData->StringArray != null &&
                this.ShouldWriteStringArrayValues(
                    stringArrayData->SubscribedAddonsCount))
            {
                foreach (var (index, translatedText) in translatedPayload
                             .StringArrayValues)
                {
                    if ((uint)index >= stringArrayData->Size)
                    {
                        continue;
                    }

                    var currentText = stringArrayData->StringArray[index]
                        .AsReadOnlySeStringSpan()
                        .ExtractText();
                    if (string.Equals(
                            currentText,
                            translatedText,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (this.ShouldRequestStringArrayUpdates())
                    {
                        stringArrayData->SetValueAndUpdate(
                            index,
                            translatedText,
                            readBeforeWrite: false,
                            managed: true);
                    }
                    else
                    {
                        stringArrayData->SetValue(
                            index,
                            translatedText,
                            suppressUpdates: true);
                    }
                }
            }
        }

        if (TranslationDisplayModeHelper.UsesHoverTooltips(displayMode))
        {
            this.RegisterHoverTooltips(
                addon,
                originalPayload,
                translatedPayload,
                displayMode);
        }
        else
        {
            this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);
        }

        this.lastResolvedState = new DbFirstGameWindowRuntimeState(
            payloadKey,
            originalPayload,
            translatedPayload);

        if (TranslationDisplayModeHelper.WritesNativeTranslation(displayMode))
        {
            this.runtimeState = new DbFirstGameWindowRuntimeState(
                payloadKey,
                originalPayload,
                translatedPayload);
        }
        else
        {
            this.runtimeState = null;
        }

        this.lastAppliedDisplayMode = displayMode;
    }

    /// <summary>
    ///     Restores the original payload if this runtime mutated the addon.
    /// </summary>
    private void RestoreOriginalPayloadIfNeeded()
    {
        if (this.runtimeState == null)
        {
            return;
        }

        if (!this.TryGetVisibleAddon(out var addon))
        {
            this.runtimeState = null;
            return;
        }

        this.RestorePayloadIfNeeded(
            addon,
            this.runtimeState.TranslatedPayload,
            this.runtimeState.OriginalPayload);
        this.runtimeState = null;
    }

    /// <summary>
    ///     Restores one native payload pair back to the original text for the
    ///     current live addon shape.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="translatedPayload">The translated payload currently represented in native UI.</param>
    /// <param name="originalPayload">The original payload to restore.</param>
    private void RestorePayloadIfNeeded(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload translatedPayload,
        DbFirstGameWindowPayload originalPayload)
    {
        if (this.useAtkValues && addon->AtkValues != null)
        {
            foreach (var (index, originalText) in originalPayload.AtkValues)
            {
                if ((uint)index >= addon->AtkValuesCount)
                {
                    continue;
                }

                ref var currentValue = ref addon->AtkValues[index];
                if (currentValue.Type is not
                    (ValueType.String or
                     ValueType.String8 or
                     ValueType.ManagedString))
                {
                    continue;
                }

                currentValue.SetManagedString(originalText);
            }
        }

        if (this.useTextNodes && originalPayload.TextNodes.Count > 0)
        {
            if (!this.TryApplyCustomTextNodePayload(
                    addon,
                    translatedPayload,
                    originalPayload))
            {
                this.ApplyTranslatedTextNodes(
                    addon,
                    originalPayload.TextNodes);
            }
        }

        if (this.stringArrayDataType is { } arrayType)
        {
            var stringArrayData = AtkStage.Instance()->GetStringArrayData(
                arrayType);
            if (stringArrayData != null &&
                stringArrayData->StringArray != null &&
                this.ShouldWriteStringArrayValues(
                    stringArrayData->SubscribedAddonsCount))
            {
                foreach (var (index, originalText) in originalPayload
                             .StringArrayValues)
                {
                    if ((uint)index >= stringArrayData->Size)
                    {
                        continue;
                    }

                    if (this.ShouldRequestStringArrayUpdates())
                    {
                        stringArrayData->SetValueAndUpdate(
                            index,
                            originalText,
                            readBeforeWrite: false,
                            managed: true);
                    }
                    else
                    {
                        stringArrayData->SetValue(
                            index,
                            originalText,
                            suppressUpdates: true);
                    }
                }
            }
        }

        this.AfterRestorePayload(
            addon,
            translatedPayload,
            originalPayload);
    }

    /// <summary>
    ///     Registers hover tooltips for visible text nodes in the addon.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <param name="displayMode">The active display mode.</param>
    private void RegisterHoverTooltips(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        JournalTranslationDisplayMode displayMode)
    {
        this.hoverTooltipManager.RemoveByPrefix(this.hoverTooltipKeyPrefix);

        if (this.TryRegisterCustomHoverTooltips(
                addon,
                originalPayload,
                translatedPayload,
                displayMode))
        {
            return;
        }

        var textNodeAddresses = AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(addon);
        if (textNodeAddresses.Count == 0)
        {
            return;
        }

        var originalToTranslated = BuildTooltipTextMap(
            originalPayload,
            translatedPayload,
            useTranslatedKeys: false);
        var translatedToOriginal = BuildTooltipTextMap(
            originalPayload,
            translatedPayload,
            useTranslatedKeys: true);
        var showOriginalTooltips =
            TranslationDisplayModeHelper.ShowsOriginalTooltips(displayMode);
        var registeredCount = 0;

        for (var i = 0; i < textNodeAddresses.Count; i++)
        {
            var textNode = (AtkTextNode*)textNodeAddresses[i];
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var visibleText = this.ReadTextNode(textNode);
            if (!ShouldCaptureText(visibleText))
            {
                continue;
            }

            string? tooltipBody;
            if (showOriginalTooltips)
            {
                translatedToOriginal.TryGetValue(visibleText, out tooltipBody);
            }
            else
            {
                originalToTranslated.TryGetValue(visibleText, out tooltipBody);
            }

            if (string.IsNullOrWhiteSpace(tooltipBody))
            {
                continue;
            }

            var width = Math.Max(1f, textNode->GetWidth());
            var height = Math.Max(1f, textNode->GetHeight());
            this.hoverTooltipManager.Register(
                $"{this.hoverTooltipKeyPrefix}{i}",
                new Vector2(textNode->ScreenX - 12f, textNode->ScreenY - 8f),
                new Vector2(
                    textNode->ScreenX + width + 12f,
                    textNode->ScreenY + height + 8f),
                string.Empty,
                tooltipBody,
                true);
            registeredCount++;
        }

    }

    /// <summary>
    ///     Reads the visible text of a live text node.
    /// </summary>
    /// <param name="textNode">The text node to read.</param>
    /// <returns>The visible text, or an empty string.</returns>
    private protected string ReadTextNode(AtkTextNode* textNode)
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
    ///     Reads the visible text of one live ATK value when it currently
    ///     stores a string.
    /// </summary>
    /// <param name="value">The ATK value to read.</param>
    /// <returns>The visible text, or an empty string.</returns>
    private protected string ReadAtkValueText(
        in AtkValue value)
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

    /// <summary>
    ///     Determines whether one node is effectively visible after accounting
    ///     for the visibility of its ancestor chain.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when the node and all ancestor nodes are
    ///     visible; otherwise <see langword="false" />.
    /// </returns>
    private protected bool IsEffectivelyVisible(
        AtkResNode* node)
    {
        for (var currentNode = node; currentNode != null; currentNode = currentNode->ParentNode)
        {
            if (!currentNode->IsVisible())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Applies translated text to visible text nodes by stable node key.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="translatedTextNodes">The translated text-node payload.</param>
    private void ApplyTranslatedTextNodes(
        AtkUnitBase* addon,
        IReadOnlyDictionary<string, string> translatedTextNodes)
    {
        var nodeAddresses = AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(
            addon);
        var ordinalsByNodeId = new Dictionary<uint, int>();

        foreach (var nodeAddress in nodeAddresses)
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var nodeId = textNode->AtkResNode.NodeId;
            ordinalsByNodeId.TryGetValue(nodeId, out var ordinal);
            ordinalsByNodeId[nodeId] = ordinal + 1;

            var textNodeKey = BuildTextNodeKey(nodeId, ordinal);
            if (!translatedTextNodes.TryGetValue(textNodeKey, out var translatedText) ||
                !ShouldCaptureText(translatedText))
            {
                continue;
            }

            if (this.ReadTextNode(textNode) == translatedText)
            {
                continue;
            }

            textNode->SetText(translatedText);
        }
    }

    /// <summary>
    ///     Restores only the text nodes that are still showing translated text
    ///     from the previously applied runtime state after the game has
    ///     already started populating a new context into the same addon.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="livePayload">
    ///     The payload captured immediately before the cleanup attempt.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one stale translated node was
    ///     restored to its original text; otherwise <see langword="false" />.
    /// </returns>
    private bool TryRestoreStaleTranslatedTextNodes(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload livePayload)
    {
        if (this.runtimeState == null ||
            this.runtimeState.OriginalPayload.TextNodes.Count == 0 ||
            this.runtimeState.TranslatedPayload.TextNodes.Count == 0)
        {
            return false;
        }

        var nodeAddresses = AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(
            addon);
        if (nodeAddresses.Count == 0)
        {
            return false;
        }

        var ordinalsByNodeId = new Dictionary<uint, int>();
        var restoredAny = false;

        foreach (var nodeAddress in nodeAddresses)
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var nodeId = textNode->AtkResNode.NodeId;
            ordinalsByNodeId.TryGetValue(nodeId, out var ordinal);
            ordinalsByNodeId[nodeId] = ordinal + 1;

            var textNodeKey = BuildTextNodeKey(nodeId, ordinal);
            if (!livePayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var currentText) ||
                !this.runtimeState.OriginalPayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var previousOriginalText) ||
                !this.runtimeState.TranslatedPayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var previousTranslatedText) ||
                !string.Equals(
                    currentText,
                    previousTranslatedText,
                    StringComparison.Ordinal) ||
                string.Equals(
                    currentText,
                    previousOriginalText,
                    StringComparison.Ordinal) ||
                !ShouldCaptureText(previousOriginalText))
            {
                continue;
            }

            textNode->SetText(previousOriginalText);
            restoredAny = true;
        }

        return restoredAny;
    }

    /// <summary>
    ///     Builds one stable text-node key from a node id and duplicate
    ///     ordinal in tree order.
    /// </summary>
    /// <param name="nodeId">The text node id.</param>
    /// <param name="ordinal">The zero-based ordinal for this node id.</param>
    /// <returns>The stable text-node key.</returns>
    private static string BuildTextNodeKey(uint nodeId, int ordinal)
    {
        return $"{nodeId}:{ordinal}";
    }

    /// <summary>
    ///     Builds a text map used by hover-tooltip registration.
    /// </summary>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <param name="useTranslatedKeys">
    ///     When set, translated text becomes the dictionary key and original
    ///     text becomes the value.
    /// </param>
    /// <returns>The tooltip lookup map.</returns>
    private static Dictionary<string, string> BuildTooltipTextMap(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        bool useTranslatedKeys)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        AppendTooltipTextMap(
            map,
            originalPayload.AtkValues,
            translatedPayload.AtkValues,
            useTranslatedKeys);
        AppendTooltipTextMap(
            map,
            originalPayload.StringArrayValues,
            translatedPayload.StringArrayValues,
            useTranslatedKeys);
        AppendTooltipTextMap(
            map,
            originalPayload.TextNodes,
            translatedPayload.TextNodes,
            useTranslatedKeys);

        return map;
    }

    /// <summary>
    ///     Appends one payload map into a tooltip text lookup.
    /// </summary>
    /// <param name="map">The lookup under construction.</param>
    /// <param name="originalValues">The original values.</param>
    /// <param name="translatedValues">The translated values.</param>
    /// <param name="useTranslatedKeys">
    ///     When set, translated text becomes the key.
    /// </param>
    private static void AppendTooltipTextMap(
        IDictionary<string, string> map,
        IReadOnlyDictionary<int, string> originalValues,
        IReadOnlyDictionary<int, string> translatedValues,
        bool useTranslatedKeys)
    {
        foreach (var (index, originalText) in originalValues)
        {
            if (!translatedValues.TryGetValue(index, out var translatedText) ||
                !ShouldCaptureText(originalText) ||
                !ShouldCaptureText(translatedText))
            {
                continue;
            }

            var key = useTranslatedKeys ? translatedText : originalText;
            var value = useTranslatedKeys ? originalText : translatedText;
            map.TryAdd(key, value);
        }
    }

    /// <summary>
    ///     Appends one text-node payload map into a tooltip text lookup.
    /// </summary>
    /// <param name="map">The lookup under construction.</param>
    /// <param name="originalValues">The original values.</param>
    /// <param name="translatedValues">The translated values.</param>
    /// <param name="useTranslatedKeys">
    ///     When set, translated text becomes the key.
    /// </param>
    private static void AppendTooltipTextMap(
        IDictionary<string, string> map,
        IReadOnlyDictionary<string, string> originalValues,
        IReadOnlyDictionary<string, string> translatedValues,
        bool useTranslatedKeys)
    {
        foreach (var (key, originalText) in originalValues)
        {
            if (!translatedValues.TryGetValue(key, out var translatedText) ||
                !ShouldCaptureText(originalText) ||
                !ShouldCaptureText(translatedText))
            {
                continue;
            }

            var lookupKey = useTranslatedKeys ? translatedText : originalText;
            var value = useTranslatedKeys ? originalText : translatedText;
            map.TryAdd(lookupKey, value);
        }
    }

    /// <summary>
    ///     Parses a translated payload and validates that all required keys are
    ///     present.
    /// </summary>
    /// <param name="translatedJson">The translated JSON payload.</param>
    /// <param name="originalPayload">The original payload.</param>
    /// <param name="translatedPayload">The parsed translated payload.</param>
    /// <returns>True when the payload is complete enough to apply.</returns>
    private static bool TryParseTranslatedPayload(
        string? translatedJson,
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;

        if (!TryParseSerializedPayload(
                translatedJson,
                out var parsedPayload))
        {
            return false;
        }

        foreach (var key in originalPayload.AtkValues.Keys)
        {
            if (!parsedPayload.AtkValues.ContainsKey(key))
            {
                return false;
            }
        }

        foreach (var key in originalPayload.StringArrayValues.Keys)
        {
            if (!parsedPayload.StringArrayValues.ContainsKey(key))
            {
                return false;
            }
        }

        foreach (var key in originalPayload.TextNodes.Keys)
        {
            if (!parsedPayload.TextNodes.ContainsKey(key))
            {
                return false;
            }
        }

        translatedPayload = parsedPayload.ProjectToShape(originalPayload);
        return true;
    }

    /// <summary>
    ///     Parses a serialized payload without requiring an external original
    ///     reference.
    /// </summary>
    /// <param name="serializedPayload">The serialized payload JSON.</param>
    /// <param name="payload">The parsed payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload could be parsed.
    /// </returns>
    private protected static bool TryParseSerializedPayload(
        string? serializedPayload,
        out DbFirstGameWindowPayload payload)
    {
        payload = DbFirstGameWindowPayload.Empty;

        if (string.IsNullOrWhiteSpace(serializedPayload))
        {
            return false;
        }

        if (ParsedPayloadCache.TryGetValue(serializedPayload, out payload))
        {
            return !payload.IsEmpty;
        }

        try
        {
            var combinedData =
                JsonConvert.DeserializeObject<CombinedTranslationData>(
                    serializedPayload);
            if (combinedData == null)
            {
                return false;
            }

            payload = new DbFirstGameWindowPayload(
                combinedData.AtkValues != null
                    ? new SortedDictionary<int, string>(
                        combinedData.AtkValues,
                        Comparer<int>.Default)
                    : new SortedDictionary<int, string>(),
                combinedData.StringArrayData != null
                    ? new SortedDictionary<int, string>(
                        combinedData.StringArrayData,
                        Comparer<int>.Default)
                    : new SortedDictionary<int, string>(),
                combinedData.TextNodes != null
                    ? new SortedDictionary<string, string>(
                        combinedData.TextNodes,
                        StringComparer.Ordinal)
                    : new SortedDictionary<string, string>(
                        StringComparer.Ordinal));
            if (payload.IsEmpty)
            {
                return false;
            }

            ParsedPayloadCache[serializedPayload] = payload;
            return true;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                $"[DbFirstGameWindowAddonHandler] Failed to parse serialized payload: {ex}");
            return false;
        }
    }

    /// <summary>
    ///     Determines whether a text value should be captured.
    /// </summary>
    /// <param name="text">The text to test.</param>
    /// <returns>True when the value should be part of the payload.</returns>
    private static bool ShouldCaptureText(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               !text.All(char.IsPunctuation) &&
               !NumericLikePattern.IsMatch(text);
    }

    /// <summary>
    ///     Determines whether one persisted candidate is safe to use for
    ///     recovery in the current source-language and target-language scope.
    /// </summary>
    /// <param name="originalLanguage">The row's stored original language.</param>
    /// <param name="translationLanguage">The row's stored translation language.</param>
    /// <param name="originalPayload">The persisted original payload.</param>
    /// <param name="translatedPayload">The persisted translated payload.</param>
    /// <param name="sourceLanguageCode">The current game language code.</param>
    /// <param name="targetLanguageCode">The configured target language code.</param>
    /// <returns>
    ///     <see langword="true" /> when the candidate matches the runtime
    ///     language pair and carries a meaningful translated state.
    /// </returns>
    private static bool IsUsableRecoveryCandidate(
        string? originalLanguage,
        string? translationLanguage,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        string sourceLanguageCode,
        string targetLanguageCode)
    {
        if (!RuntimeLanguageHelper.LanguagesMatch(
                translationLanguage,
                targetLanguageCode))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(originalLanguage) &&
            !RuntimeLanguageHelper.LanguagesMatch(
                originalLanguage,
                sourceLanguageCode))
        {
            return false;
        }

        if (!RuntimeLanguageHelper.LanguagesMatch(
                sourceLanguageCode,
                targetLanguageCode) &&
            originalPayload.StructurallyEquals(translatedPayload))
        {
            return false;
        }

        return true;
    }
}

/// <summary>
///     Represents one DB-first addon payload snapshot.
/// </summary>
/// <param name="AtkValues">The ATK string values.</param>
/// <param name="StringArrayValues">The string-array values.</param>
/// <param name="TextNodes">The visible text-node values.</param>
internal readonly record struct DbFirstGameWindowPayload(
    SortedDictionary<int, string> AtkValues,
    SortedDictionary<int, string> StringArrayValues,
    SortedDictionary<string, string> TextNodes)
{
    /// <summary>
    ///     Gets an empty payload.
    /// </summary>
    public static DbFirstGameWindowPayload Empty =>
        new(
            new SortedDictionary<int, string>(),
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    ///     Gets a value indicating whether the payload is empty.
    /// </summary>
    public bool IsEmpty =>
        this.AtkValues.Count == 0 &&
        this.StringArrayValues.Count == 0 &&
        this.TextNodes.Count == 0;

    /// <summary>
    ///     Serializes the payload using the stable JSON contract already used by
    ///     <see cref="GameWindow" /> rows.
    /// </summary>
    /// <returns>The serialized payload.</returns>
    public string Serialize()
    {
        return JsonConvert.SerializeObject(
            new
            {
                atkValues = this.AtkValues.Count > 0 ? this.AtkValues : null,
                stringArrayData =
                    this.StringArrayValues.Count > 0
                        ? this.StringArrayValues
                        : null,
                textNodes =
                    this.TextNodes.Count > 0
                        ? this.TextNodes
                        : null,
            });
    }

    /// <summary>
    ///     Determines whether the supplied live payload still matches this
    ///     translated payload.
    /// </summary>
    /// <param name="translatedPayload">The translated payload.</param>
    /// <returns>True when the visible payload matches the translated state.</returns>
    public bool MatchesTranslated(DbFirstGameWindowPayload translatedPayload)
    {
        return MatchesMap(this.AtkValues, translatedPayload.AtkValues) &&
               MatchesMap(this.StringArrayValues, translatedPayload.StringArrayValues) &&
               MatchesMap(this.TextNodes, translatedPayload.TextNodes);
    }

    /// <summary>
    ///     Determines whether the supplied live payload still matches this
    ///     original payload.
    /// </summary>
    /// <param name="originalPayload">The original payload.</param>
    /// <returns>True when the visible payload matches the original state.</returns>
    public bool MatchesOriginal(DbFirstGameWindowPayload originalPayload)
    {
        return MatchesMap(this.AtkValues, originalPayload.AtkValues) &&
               MatchesMap(this.StringArrayValues, originalPayload.StringArrayValues) &&
               MatchesMap(this.TextNodes, originalPayload.TextNodes);
    }

    /// <summary>
    ///     Determines whether two payload snapshots contain exactly the same
    ///     visible values.
    /// </summary>
    /// <param name="otherPayload">The payload to compare against.</param>
    /// <returns>
    ///     <see langword="true" /> when both payloads contain the same ATK and
    ///     string-array values.
    /// </returns>
    public bool StructurallyEquals(DbFirstGameWindowPayload otherPayload)
    {
        return MatchesMap(this.AtkValues, otherPayload.AtkValues) &&
               MatchesMap(this.StringArrayValues, otherPayload.StringArrayValues) &&
               MatchesMap(this.TextNodes, otherPayload.TextNodes);
    }

    /// <summary>
    ///     Projects this payload to the same ATK and string-array key shape as
    ///     one reference payload so compatible supersets do not write extra
    ///     slots into a reused addon context.
    /// </summary>
    /// <param name="referencePayload">
    ///     The payload whose visible key set should be preserved.
    /// </param>
    /// <returns>The projected payload.</returns>
    public DbFirstGameWindowPayload ProjectToShape(
        DbFirstGameWindowPayload referencePayload)
    {
        return new DbFirstGameWindowPayload(
            ProjectMap(this.AtkValues, referencePayload.AtkValues.Keys),
            ProjectMap(
                this.StringArrayValues,
                referencePayload.StringArrayValues.Keys),
            ProjectMap(
                this.TextNodes,
                referencePayload.TextNodes.Keys));
    }

    /// <summary>
    ///     Compares two payload maps.
    /// </summary>
    /// <param name="currentValues">The current visible values.</param>
    /// <param name="expectedValues">The expected values.</param>
    /// <returns>True when the maps match exactly.</returns>
    private static bool MatchesMap(
        SortedDictionary<int, string> currentValues,
        SortedDictionary<int, string> expectedValues)
    {
        if (currentValues.Count != expectedValues.Count)
        {
            return false;
        }

        foreach (var (index, expectedText) in expectedValues)
        {
            if (!currentValues.TryGetValue(index, out var currentText) ||
                !string.Equals(
                    currentText,
                    expectedText,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Compares two text-node payload maps.
    /// </summary>
    /// <param name="currentValues">The current visible values.</param>
    /// <param name="expectedValues">The expected values.</param>
    /// <returns>True when the maps match exactly.</returns>
    private static bool MatchesMap(
        SortedDictionary<string, string> currentValues,
        SortedDictionary<string, string> expectedValues)
    {
        if (currentValues.Count != expectedValues.Count)
        {
            return false;
        }

        foreach (var (index, expectedText) in expectedValues)
        {
            if (!currentValues.TryGetValue(index, out var currentText) ||
                !string.Equals(
                    currentText,
                    expectedText,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Projects one payload map down to a requested set of keys.
    /// </summary>
    /// <param name="sourceValues">The source values to project.</param>
    /// <param name="keys">The keys to keep.</param>
    /// <returns>The projected map.</returns>
    private static SortedDictionary<int, string> ProjectMap(
        SortedDictionary<int, string> sourceValues,
        IEnumerable<int> keys)
    {
        var projected = new SortedDictionary<int, string>();

        foreach (var key in keys)
        {
            if (sourceValues.TryGetValue(key, out var value))
            {
                projected[key] = value;
            }
        }

        return projected;
    }

    /// <summary>
    ///     Projects one text-node payload map down to a requested set of keys.
    /// </summary>
    /// <param name="sourceValues">The source values to project.</param>
    /// <param name="keys">The keys to keep.</param>
    /// <returns>The projected map.</returns>
    private static SortedDictionary<string, string> ProjectMap(
        SortedDictionary<string, string> sourceValues,
        IEnumerable<string> keys)
    {
        var projected = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var key in keys)
        {
            if (sourceValues.TryGetValue(key, out var value))
            {
                projected[key] = value;
            }
        }

        return projected;
    }
}

/// <summary>
///     Tracks the currently applied DB-first payload for one addon instance.
/// </summary>
/// <param name="PayloadKey">The stable payload key.</param>
/// <param name="OriginalPayload">The original payload.</param>
/// <param name="TranslatedPayload">The translated payload.</param>
internal sealed record DbFirstGameWindowRuntimeState(
    string PayloadKey,
    DbFirstGameWindowPayload OriginalPayload,
    DbFirstGameWindowPayload TranslatedPayload);


