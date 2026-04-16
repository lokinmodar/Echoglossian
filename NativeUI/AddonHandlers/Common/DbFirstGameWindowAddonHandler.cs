// <copyright file="DbFirstGameWindowAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Handlers;
using Echoglossian.NativeUI.Helpers;
using Lumina.Text.ReadOnly;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.AddonHandlers.Common;

/// <summary>
///     Provides a DB-first runtime for addon-local surfaces backed by
///     <see cref="GameWindow" /> rows.
/// </summary>
public abstract unsafe class DbFirstGameWindowAddonHandler
    : IAddonTranslationHandler
{
    private static readonly Regex NumericLikePattern = new(
        @"^\s*([€£$¥]?\s*\d+([.,]\d+)?\s*[%€£$¥]?\s*|(\d+/\d+))\s*$",
        RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, byte>
        InFlightPayloads = new(StringComparer.Ordinal);

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private readonly string addonName;
    private readonly Config config;
    private readonly TranslationService translationService;
    private readonly bool useAtkValues;
    private readonly StringArrayType? stringArrayDataType;
    private readonly Func<Config, bool> enabledSelector;
    private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>>
        eventHandlers = [];

    private DbFirstGameWindowRuntimeState? runtimeState;

    private DateTime nextRetryUtc = DateTime.MinValue;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="DbFirstGameWindowAddonHandler" /> class.
    /// </summary>
    /// <param name="addonName">The target addon name.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="enabledSelector">
    ///     Resolves whether this addon should be active.
    /// </param>
    /// <param name="useAtkValues">
    ///     Indicates whether ATK string values are part of the payload.
    /// </param>
    /// <param name="stringArrayDataType">
    ///     The backing <see cref="StringArrayType" />, if any.
    /// </param>
    protected DbFirstGameWindowAddonHandler(
        string addonName,
        Config config,
        TranslationService translationService,
        Func<Config, bool> enabledSelector,
        bool useAtkValues,
        StringArrayType? stringArrayDataType = null)
    {
        this.addonName = addonName;
        this.config = config;
        this.translationService = translationService;
        this.enabledSelector = enabledSelector;
        this.useAtkValues = useAtkValues;
        this.stringArrayDataType = stringArrayDataType;

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
    ///     Handles setup/refresh/update events.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="args">The event args.</param>
    protected virtual void OnLifecycleEvent(AddonEvent evt, AddonArgs args)
    {
        this.RefreshOrQueue();
    }

    /// <summary>
    ///     Handles lightweight retries and config toggles.
    /// </summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="args">The event args.</param>
    protected virtual void OnPreDrawEvent(AddonEvent evt, AddonArgs args)
    {
        if (!this.enabledSelector(this.config))
        {
            this.RestoreOriginalPayloadIfNeeded();
            return;
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
        this.RestoreOriginalPayloadIfNeeded();
        this.runtimeState = null;
        this.nextRetryUtc = DateTime.MinValue;
    }

    /// <summary>
    ///     Performs the DB-first refresh path for the addon.
    /// </summary>
    private void RefreshOrQueue()
    {
        if (!this.enabledSelector(this.config))
        {
            this.RestoreOriginalPayloadIfNeeded();
            return;
        }

        if (!this.TryGetVisibleAddon(out var addon))
        {
            return;
        }

        var livePayload = this.CaptureLivePayload(addon);
        if (livePayload.IsEmpty)
        {
            return;
        }

        var originalPayload = this.ResolveOriginalPayload(livePayload);
        var originalJson = originalPayload.Serialize();
        var payloadKey = this.BuildPayloadKey(originalJson);

        if (!this.TryFindGameWindow(originalJson, out var gameWindow) ||
            !TryParseTranslatedPayload(
                gameWindow?.TranslatedWindowStrings,
                originalPayload,
                out var translatedPayload))
        {
            this.QueueTranslationIfNeeded(originalPayload, payloadKey);
            this.nextRetryUtc = DateTime.UtcNow + RetryInterval;
            return;
        }

        this.ApplyPayload(addon, originalPayload, translatedPayload, payloadKey);
        this.nextRetryUtc = DateTime.MinValue;
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

                var text = MemoryHelper.ReadSeStringAsString(
                    out _,
                    (nint)value.String.Value);
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
                stringArrayData->Size > 0)
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

        return new DbFirstGameWindowPayload(atkValues, stringArrayValues);
    }

    /// <summary>
    ///     Resolves the original payload to use for DB lookup.
    /// </summary>
    /// <param name="livePayload">The currently visible payload.</param>
    /// <returns>The original payload for DB lookup and restore.</returns>
    private DbFirstGameWindowPayload ResolveOriginalPayload(
        DbFirstGameWindowPayload livePayload)
    {
        if (this.runtimeState == null)
        {
            return livePayload;
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
        return livePayload;
    }

    /// <summary>
    ///     Builds a stable in-flight key for one payload.
    /// </summary>
    /// <param name="originalJson">The serialized original payload.</param>
    /// <returns>The stable payload key.</returns>
    private string BuildPayloadKey(string originalJson)
    {
        return $"{this.addonName}|{LangDict[LanguageInt].Code}|{this.config.ChosenTransEngine}|{GetGameVersion()}|{originalJson}";
    }

    /// <summary>
    ///     Tries to find a matching <see cref="GameWindow" /> in cache or DB.
    /// </summary>
    /// <param name="originalJson">The serialized original payload.</param>
    /// <param name="gameWindow">The resolved row, if any.</param>
    /// <returns>True when a row was found.</returns>
    private bool TryFindGameWindow(
        string originalJson,
        out GameWindow? gameWindow)
    {
        var language = LangDict[LanguageInt].Code;
        var gameVersion = GetGameVersion();

        gameWindow = GameWindowCacheManager.TryFindMatch(
            this.addonName,
            language,
            this.config.ChosenTransEngine,
            gameVersion,
            originalJson);
        if (gameWindow != null)
        {
            return true;
        }

        gameWindow = Echoglossian.FindEntity<GameWindow>(window =>
            window.WindowAddonName == this.addonName &&
            window.TranslationLang == language &&
            window.TranslationEngine == this.config.ChosenTransEngine &&
            (window.GameVersion == null || window.GameVersion == gameVersion) &&
            window.OriginalWindowStrings == originalJson);
        if (gameWindow != null)
        {
            GameWindowCacheManager.Update(gameWindow);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Queues background translation and DB save when needed.
    /// </summary>
    /// <param name="payload">The original payload.</param>
    /// <param name="payloadKey">The stable payload key.</param>
    private void QueueTranslationIfNeeded(
        DbFirstGameWindowPayload payload,
        string payloadKey)
    {
        if (!InFlightPayloads.TryAdd(payloadKey, 0))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                return GenericAddonHandlerHelper
                    .PerformTranslationAndSaveAsync<GameWindow>(
                        this.addonName,
                        new Dictionary<int, string>(payload.AtkValues),
                        new Dictionary<int, string>(payload.StringArrayValues),
                        new Dictionary<int, string>(payload.AtkValues),
                        new Dictionary<int, string>(payload.StringArrayValues),
                        this.config,
                        this.translationService);
            }
            finally
            {
                InFlightPayloads.TryRemove(payloadKey, out _);
            }
        });
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
        string payloadKey)
    {
        if (this.useAtkValues && addon->AtkValues != null)
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

                var currentText = MemoryHelper.ReadSeStringAsString(
                    out _,
                    (nint)currentValue.String.Value);
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

        if (this.stringArrayDataType is { } arrayType)
        {
            var stringArrayData = AtkStage.Instance()->GetStringArrayData(
                arrayType);
            if (stringArrayData != null && stringArrayData->StringArray != null)
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

                    stringArrayData->SetValue(
                        index,
                        translatedText,
                        suppressUpdates: true);
                }
            }
        }

        this.runtimeState = new DbFirstGameWindowRuntimeState(
            payloadKey,
            originalPayload,
            translatedPayload);
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

        if (this.useAtkValues && addon->AtkValues != null)
        {
            foreach (var (index, originalText) in this.runtimeState.OriginalPayload
                         .AtkValues)
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

        if (this.stringArrayDataType is { } arrayType)
        {
            var stringArrayData = AtkStage.Instance()->GetStringArrayData(
                arrayType);
            if (stringArrayData != null && stringArrayData->StringArray != null)
            {
                foreach (var (index, originalText) in this.runtimeState
                             .OriginalPayload.StringArrayValues)
                {
                    if ((uint)index >= stringArrayData->Size)
                    {
                        continue;
                    }

                    stringArrayData->SetValue(
                        index,
                        originalText,
                        suppressUpdates: true);
                }
            }
        }

        this.runtimeState = null;
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

        if (string.IsNullOrWhiteSpace(translatedJson))
        {
            return false;
        }

        try
        {
            var combinedData =
                JsonConvert.DeserializeObject<CombinedTranslationData>(
                    translatedJson);
            if (combinedData == null)
            {
                return false;
            }

            var translatedAtkValues =
                combinedData.AtkValues != null
                    ? new SortedDictionary<int, string>(
                        combinedData.AtkValues,
                        Comparer<int>.Default)
                    : new SortedDictionary<int, string>();
            var translatedStringArrayValues =
                combinedData.StringArrayData != null
                    ? new SortedDictionary<int, string>(
                        combinedData.StringArrayData,
                        Comparer<int>.Default)
                    : new SortedDictionary<int, string>();

            foreach (var key in originalPayload.AtkValues.Keys)
            {
                if (!translatedAtkValues.ContainsKey(key))
                {
                    return false;
                }
            }

            foreach (var key in originalPayload.StringArrayValues.Keys)
            {
                if (!translatedStringArrayValues.ContainsKey(key))
                {
                    return false;
                }
            }

            translatedPayload = new DbFirstGameWindowPayload(
                translatedAtkValues,
                translatedStringArrayValues);
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.Error(
                $"[DbFirstGameWindowAddonHandler] Failed to parse translated payload: {ex}");
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
}

/// <summary>
///     Represents one DB-first addon payload snapshot.
/// </summary>
/// <param name="AtkValues">The ATK string values.</param>
/// <param name="StringArrayValues">The string-array values.</param>
internal readonly record struct DbFirstGameWindowPayload(
    SortedDictionary<int, string> AtkValues,
    SortedDictionary<int, string> StringArrayValues)
{
    /// <summary>
    ///     Gets an empty payload.
    /// </summary>
    public static DbFirstGameWindowPayload Empty =>
        new(new SortedDictionary<int, string>(), new SortedDictionary<int, string>());

    /// <summary>
    ///     Gets a value indicating whether the payload is empty.
    /// </summary>
    public bool IsEmpty => this.AtkValues.Count == 0 && this.StringArrayValues.Count == 0;

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
               MatchesMap(this.StringArrayValues, translatedPayload.StringArrayValues);
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
               MatchesMap(this.StringArrayValues, originalPayload.StringArrayValues);
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
