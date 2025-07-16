// <copyright file="GenericAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Lumina.Text.ReadOnly;
using static Dalamud.Plugin.Services.IAddonLifecycle;
using SeStringBuilder = Lumina.Text.SeStringBuilder;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.Handlers;

/// <summary>
///     Represents a delegate for handling local addon events.
/// </summary>
/// <param name="evt">The addon event being handled.</param>
/// <param name="args">The arguments associated with the addon event.</param>
public delegate void LocalAddonHandlerDelegate(AddonEvent evt, AddonArgs args);

/// <summary>
///     Represents a generic handler for addon translation.
/// </summary>
/// <typeparam name="TGenericEntity">
///     The database entity type implementing
///     IGenericEntity.
/// </typeparam>
public abstract unsafe class
    GenericAddonHandler<TGenericEntity> : IAddonTranslationHandler
    where TGenericEntity : class, IGenericEntity, new()
{
    /// <summary>
    ///     A regular expression pattern for identifying numeric-like strings.
    /// </summary>
    private static readonly Regex NumericLikePattern = new(
        @"^\s*([€£$¥]?\s*\d+([.,]\d+)?\s*[%€£$¥]?\s*|(\d+/\d+))\s*$",
        RegexOptions.Compiled);

    /// <summary>
    ///     Gets the name of the addon being handled.
    /// </summary>
    protected readonly string AddonName;

    /// <summary>
    ///     Gets the configuration settings for the addon handler.
    /// </summary>
    protected readonly Config Config;

    /// <summary>
    ///     Stores event handlers for addon events.
    /// </summary>
    private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>>
        eventHandlers = new();

    /// <summary>
    ///     Gets the type of string array data, if applicable.
    /// </summary>
    protected readonly StringArrayType? StringArrayDataType;

    /// <summary>
    ///     Gets the translation service used for translating addon content.
    /// </summary>
    protected readonly TranslationService TranslationService;

    /// <summary>
    ///     Gets a value indicating whether ATK values are used for translation.
    /// </summary>
    protected readonly bool UseAtkValues;

    /// <summary>
    ///     Gets a value indicating whether string arrays are used for translation.
    /// </summary>
    protected readonly bool UseStringArray;

    /// <summary>
    ///     Stores filtered ATK values for translation.
    /// </summary>
    protected Dictionary<int, string> FilteredAtkValues = new();

    /// <summary>
    ///     Stores filtered string array data for translation.
    /// </summary>
    protected Dictionary<int, string> FilteredStringArrayData = new();

    /// <summary>
    ///     Stores the original string array values to restore after translation.
    ///     Uses byte arrays to preserve exact original data.
    /// </summary>
    protected Dictionary<int, byte[]> OriginalStringArrayData = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenericAddonHandler" /> class.
    /// </summary>
    /// <param name="addonName">The name of the addon.</param>
    /// <param name="config">The configuration settings.</param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="useAtkValues">Indicates whether ATK values are used.</param>
    /// <param name="useStringArray">Indicates whether string arrays are used.</param>
    /// <param name="stringArrayDataType">The type of string array data, if applicable.</param>
    protected GenericAddonHandler(
        string addonName,
        Config config,
        TranslationService translationService,
        bool useAtkValues,
        bool useStringArray,
        StringArrayType? stringArrayDataType = null)
    {
        this.AddonName = addonName;
        this.Config = config;
        this.TranslationService = translationService;
        this.UseAtkValues = useAtkValues;
        this.UseStringArray = useStringArray;
        this.StringArrayDataType = useStringArray ? stringArrayDataType : null;
    }

    /// <summary>
    ///     Gets the registered event handlers for addon events.
    /// </summary>
    /// <returns>A dictionary of addon events and their corresponding delegates.</returns>
    public Dictionary<AddonEvent, AddonEventDelegate> GetEventHandlers()
    {
        return this.eventHandlers.ToDictionary(
            kvp => kvp.Key,
            kvp => new AddonEventDelegate((evt, args) =>
            {
                foreach (var handler in kvp.Value)
                {
                    handler(evt, args);
                }
            }));
    }

    /// <summary>
    ///     Registers a handler for a specific addon event.
    /// </summary>
    /// <param name="evt">The addon event to handle.</param>
    /// <param name="handler">The delegate to handle the event.</param>
    public void RegisterHandler(
        AddonEvent evt,
        LocalAddonHandlerDelegate handler)
    {
        if (!this.eventHandlers.TryGetValue(evt, out var list))
        {
            list = new List<LocalAddonHandlerDelegate>();
            this.eventHandlers[evt] = list;
        }

        list.Add(handler);
    }

    /// <summary>
    ///     Extracts and filters ATK values from the addon.
    /// </summary>
    private void ExtractAtkValues(AtkUnitBase* addon)
    {
        var atkValues = addon->AtkValues;
        var count = addon->AtkValuesCount;
        if (atkValues == null || count <= 0)
        {
            return;
        }

        var span = new Span<AtkValue>(atkValues, count);
        for (var i = 0; i < count; i++)
        {
            if (span[i].Type is ValueType.String or ValueType.String8
                or ValueType.ManagedString)
            {
                var value = MemoryHelper.ReadSeStringAsString(
                    out _,
                    (nint)span[i].String.Value);
                if (!string.IsNullOrWhiteSpace(value) &&
                    !value.All(char.IsPunctuation) &&
                    !NumericLikePattern.IsMatch(value))
                {
                    this.FilteredAtkValues[i] = value;
                }
            }
        }

        PluginLog.Debug(
            $"[{this.AddonName}] Extracted {this.FilteredAtkValues.Count} AtkValues.");
    }

    /// <summary>
    ///     Extracts and filters string array data from the ATK stage.
    /// </summary>
    private void ExtractStringArrayData(AtkStage* atkStage)
    {
        var data =
            atkStage->GetStringArrayData(this.StringArrayDataType!.Value);
        var array = data->StringArray;
        var size = data->Size;

        for (var i = 0; i < size; i++)
        {
            var span = new ReadOnlySeStringSpan(array[i]);
            var text = span.ExtractText();
            if (!string.IsNullOrWhiteSpace(text) &&
                !text.All(char.IsPunctuation) &&
                !NumericLikePattern.IsMatch(text))
            {
                this.OriginalStringArrayData[i] = span.Data.ToArray();
                this.FilteredStringArrayData[i] = text;
            }
        }

        PluginLog.Debug(
            $"[{this.AddonName}] Extracted {this.FilteredStringArrayData.Count} StringArray entries.");
    }

    /// <summary>
    ///     Attempts to load a cached translation from the database for this addon.
    /// </summary>
    /// <returns>True if translation was found and applied; otherwise, false.</returns>
    protected bool TryLoadTranslationFromDatabase()
    {
        var original = this.SerializeOriginalData();

        var entity = FindEntity<TGenericEntity>(e =>
            e.GetOriginalText() == original &&
            e.GetEntityKey() == this.AddonName &&
            e.GetTranslationLang() == LangDict[LanguageInt].Code &&
            (e.GetGameVersion() == null ||
             e.GetGameVersion() == GetGameVersion()));

        if (entity is null)
        {
            PluginLog.Debug($"[{this.AddonName}] No cached translation found.");
            return false;
        }

        this.ParseTranslatedData(entity.GetTranslatedText());
        PluginLog.Debug($"[{this.AddonName}] Loaded translated data from DB.");
        return true;
    }

    /// <summary>
    ///     Serializes the original extracted content (ATK and string array data) to a
    ///     combined string.
    ///     Used to compare against DB entries to avoid unnecessary translation.
    /// </summary>
    /// <returns>A single string representing all original content used as DB key.</returns>
    private string SerializeOriginalData()
    {
        var pairs = new List<string>();

        foreach (var pair in this.FilteredAtkValues)
        {
            pairs.Add($"a{pair.Key}|{pair.Value}");
        }

        foreach (var pair in this.FilteredStringArrayData)
        {
            pairs.Add($"s{pair.Key}|{pair.Value}");
        }

        var result = string.Join("|", pairs);
        PluginLog.Debug(
            $"[{this.AddonName}] SerializeOriginalData => \"{result}\"");
        return result;
    }

    /// <summary>
    ///     Parses JSON-formatted translated data retrieved from the DB and repopulates
    ///     FilteredAtkValues and FilteredStringArrayData.
    /// </summary>
    /// <param name="translatedJson">
    ///     The JSON string retrieved from the entity's
    ///     translated field.
    /// </param>
    private void ParseTranslatedData(string? translatedJson)
    {
        if (string.IsNullOrWhiteSpace(translatedJson))
        {
            PluginLog.Warning(
                $"[{this.AddonName}] ParseTranslatedData - JSON is null or empty.");
            return;
        }

        try
        {
            var parsed =
                JsonConvert.DeserializeObject<CombinedTranslationData>(
                    translatedJson);
            if (parsed == null)
            {
                PluginLog.Warning(
                    $"[{this.AddonName}] ParseTranslatedData - Failed to deserialize.");
                return;
            }

            if (parsed.atkValues != null)
            {
                this.FilteredAtkValues = parsed.atkValues;
                PluginLog.Debug(
                    $"[{this.AddonName}] Loaded {parsed.atkValues.Count} atkValues from DB.");
            }

            if (parsed.stringArrayData != null)
            {
                this.FilteredStringArrayData = parsed.stringArrayData;
                PluginLog.Debug(
                    $"[{this.AddonName}] Loaded {parsed.stringArrayData.Count} stringArrayData from DB.");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(
                $"[{this.AddonName}] ParseTranslatedData - Error parsing JSON: {ex.Message}");
        }
    }

    /// <summary>
    ///     Serializes the current translation data into a JSON string.
    /// </summary>
    /// <returns>
    ///     A JSON string representing translated ATK values and string array
    ///     data.
    /// </returns>
    private string SerializeTranslatedData()
    {
        var payload = new
        {
            atk = this.FilteredAtkValues,
            array = this.FilteredStringArrayData
        };

        var json = JsonConvert.SerializeObject(payload);
        PluginLog.Debug(
            $"[{this.AddonName}] Serialized translated payload: {json.Length} bytes");
        return json;
    }

    /// <summary>
    ///     Serializes combined translation result into JSON.
    /// </summary>
    private string SerializeTranslationResult()
    {
        var combined = new
        {
            atkValues = this.FilteredAtkValues,
            stringArrayData = this.FilteredStringArrayData
        };

        return JsonConvert.SerializeObject(combined);
    }

    /// <summary>
    ///     Extracts and translates addon content based on the specified event and
    ///     arguments.
    ///     Performs DB lookup before calling external translation.
    /// </summary>
    /// <typeparam name="TGenericEntity">The type of the generic DB entity.</typeparam>
    /// <param name="evt">The type of addon event.</param>
    /// <param name="args">The arguments associated with the addon event.</param>
    protected void ExtractAndTranslate<TGenericEntity>(
        AddonEvent evt,
        AddonArgs args) where TGenericEntity : class, IGenericEntity, new()
    {
        if (args.AddonName != this.AddonName)
        {
            return;
        }

        PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate - {evt}");

        var atkStage = AtkStage.Instance();
        var addon =
            atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);

        if (addon == null || !addon->IsVisible)
        {
            return;
        }

        // Reset state
        this.FilteredAtkValues.Clear();
        this.FilteredStringArrayData.Clear();

        if (this.UseAtkValues)
        {
            this.ExtractAtkValues(addon);
        }

        if (this.UseStringArray && this.StringArrayDataType.HasValue)
        {
            this.ExtractStringArrayData(atkStage);
        }

        // Early exit if nothing to translate
        if (this.FilteredAtkValues.Count == 0 &&
            this.FilteredStringArrayData.Count == 0)
        {
            PluginLog.Debug($"[{this.AddonName}] Nothing to translate.");
            return;
        }

        // Generate original payload as a key
        var originalJson = this.SerializeOriginalData();
        var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
        var targetLang = LangDict[LanguageInt].Code;

        var existing = FindEntity<TGenericEntity>(e =>
            e.GetEntityKey() == this.AddonName &&
            e.GetOriginalText() == originalJson &&
            e.GetTranslationLang() == targetLang &&
            e.GetGameVersion() == GetGameVersion());

        if (existing != null)
        {
            PluginLog.Debug(
                $"[{this.AddonName}] Translation retrieved from DB.");
            this.ParseTranslatedData(existing.GetTranslatedText());
            return;
        }

        // Prepare input for translation
        var merged = this.FilteredAtkValues.ToDictionary(
            kvp => $"a{kvp.Key}".GetHashCode(),
            kvp => kvp.Value);

        foreach (var kvp in this.FilteredStringArrayData)
        {
            merged[$"s{kvp.Key}".GetHashCode()] = kvp.Value;
        }

        var input = string.Join(
            "|",
            merged.Select(kvp => $"{kvp.Key}|{kvp.Value}"));

        PluginLog.Debug(
            $"[{this.AddonName}] Sending input to translation engine...");

        var translated = this.TranslationService.Translate(
            input,
            sourceLang,
            targetLang);

        if (string.IsNullOrWhiteSpace(translated))
        {
            PluginLog.Warning(
                $"[{this.AddonName}] Translation service returned empty result.");
            return;
        }

        // Parse result
        var parts = translated.Split('|');
        var parsed = new Dictionary<int, string>();
        for (var i = 0; i < parts.Length - 1; i += 2)
        {
            if (int.TryParse(parts[i], out var hash))
            {
                parsed[hash] = parts[i + 1];
            }
        }

        foreach (var key in this.FilteredAtkValues.Keys.ToList())
        {
            if (parsed.TryGetValue($"a{key}".GetHashCode(), out var val))
            {
                this.FilteredAtkValues[key] = val;
            }
        }

        foreach (var key in this.FilteredStringArrayData.Keys.ToList())
        {
            if (parsed.TryGetValue($"s{key}".GetHashCode(), out var val))
            {
                this.FilteredStringArrayData[key] = val;
            }
        }

        // Save to DB
        var translatedJson = this.SerializeTranslatedData();

        var entity = new TGenericEntity();
        if (entity is IGenericEntity generic)
        {
            generic.SetTranslatedText(translatedJson);
            if (generic is not IMultiTextEntity)
            {
                generic.SetOriginalText(originalJson);
            }

            generic.SetTranslationEngine(this.Config.ChosenTransEngine);
            generic.SetTranslationLang(targetLang);
            generic.SetEntityKey(this.AddonName);

            if (generic.GetGameVersion() is null &&
                generic is not IMultiTextEntity)
            {
                generic.SetGameVersion(GetGameVersion());
            }

            InsertEntity(generic);
            PluginLog.Debug($"[{this.AddonName}] Translation saved to DB.");
        }
        else
        {
            PluginLog.Error(
                $"[{this.AddonName}] TGenericEntity does not implement IGenericEntity properly.");
        }
    }

    /// <summary>
    ///     Applies translated content to the addon based on the specified event and
    ///     arguments.
    ///     Loads translated data from DB if available.
    /// </summary>
    /// <typeparam name="TGenericEntity">
    ///     The generic entity type implementing
    ///     IGenericEntity.
    /// </typeparam>
    /// <param name="type">The type of addon event.</param>
    /// <param name="args">The arguments associated with the addon event.</param>
    protected void ApplyTranslated<TGenericEntity>(
        AddonEvent type,
        AddonArgs args) where TGenericEntity : class, IGenericEntity, new()
    {
        PluginLog.Debug(
            $"[{this.AddonName}] Called ApplyTranslated<{typeof(TGenericEntity).Name}> - {type} - Args: {args.Type}");
        if (args.AddonName != this.AddonName)
        {
            PluginLog.Debug(
                $"[{this.AddonName}] ApplyTranslated - Addon name mismatch: {args.AddonName} != {this.AddonName}");
            return;
        }

        var atkStage = AtkStage.Instance();
        var addon =
            atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
        if (addon == null || !addon->IsVisible)
        {
            PluginLog.Debug(
                $"[{this.AddonName}] ApplyTranslated - Addon not found or not visible.");
            return;
        }

        // If translation data not present in memory, attempt to load from DB
        if (this.FilteredAtkValues.Count == 0 &&
            this.FilteredStringArrayData.Count == 0)
        {
            var originalData = this.SerializeOriginalData();
            var entity = FindEntity<TGenericEntity>(e =>
                e.GetEntityKey() == this.AddonName &&
                e.GetOriginalText() == originalData &&
                e.GetTranslationLang() == LangDict[LanguageInt].Code &&
                e.GetGameVersion() == GetGameVersion());

            if (entity != null)
            {
                this.ParseTranslatedData(entity.GetTranslatedText());
                PluginLog.Debug(
                    $"[{this.AddonName}] Loaded translated data from DB for ApplyTranslated.");
            }
            else
            {
                PluginLog.Debug(
                    $"[{this.AddonName}] No DB match found for ApplyTranslated.");
            }
        }

        switch (type)
        {
            case AddonEvent.PreSetup when this.UseAtkValues:
                this.ApplyTranslatedAtkValues(addon);
                this.ApplyTranslatedStringArray(atkStage);
                break;
            case AddonEvent.PreRequestedUpdate when this.UseStringArray &&
                                                    this.StringArrayDataType
                                                        .HasValue:
                this.ApplyTranslatedAtkValues(addon);
                this.ApplyTranslatedStringArray(atkStage);
                break;
            case AddonEvent.PostRequestedUpdate when this.UseStringArray &&
                this.StringArrayDataType.HasValue:
                this.RestoreOriginalStringArray(atkStage);
                break;
            case AddonEvent.PreRefresh when this.UseAtkValues:
                this.ApplyTranslatedAtkValues(addon);
                this.ApplyTranslatedStringArray(atkStage);
                break;
            default:
                PluginLog.Verbose(
                    $"[{this.AddonName}] ApplyTranslated skipped for event: {type}");
                break;
        }
    }

    /// <summary>
    ///     Applies translated ATK values to the addon, updating string values as
    ///     needed.
    /// </summary>
    /// <param name="addon">The ATK unit base addon to apply translations to.</param>
    private void ApplyTranslatedAtkValues(AtkUnitBase* addon)
    {
        PluginLog.Debug($"[{this.AddonName}] ApplyTranslatedAtkValues called");

        var atkValues = addon->AtkValues;
        var count = addon->AtkValuesCount;
        if (atkValues == null || count <= 0)
        {
            PluginLog.Warning(
                $"[{this.AddonName}] ApplyTranslatedAtkValues - atkValues is null or count is zero.");
            return;
        }

        var span = new Span<AtkValue>(atkValues, count);
        for (var i = 0; i < count; i++)
        {
            if (span[i].Type is ValueType.String or ValueType.String8
                or ValueType.ManagedString)
            {
                if (this.FilteredAtkValues.TryGetValue(i, out var translated))
                {
                    try
                    {
                        PluginLog.Debug(
                            $"[{this.AddonName}] Applying translation to index {i}: '{translated}'");
                        span[i].SetManagedString(translated);
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(
                            $"[{this.AddonName}] Error applying translation to index {i}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Applies translated strings from the string array data to the addon.
    /// </summary>
    /// <param name="atkStage">The ATK stage instance containing the string array data.</param>
    private void ApplyTranslatedStringArray(AtkStage* atkStage)
    {
        PluginLog.Debug(
            $"[{this.AddonName}] ApplyTranslatedStringArray called");

        if (!this.StringArrayDataType.HasValue)
        {
            PluginLog.Warning(
                $"[{this.AddonName}] StringArrayDataType is null in ApplyTranslatedStringArray");
            return;
        }

        try
        {
            var stringArrayData =
                atkStage->GetStringArrayData(this.StringArrayDataType.Value);
            if (stringArrayData == null)
            {
                PluginLog.Warning(
                    $"[{this.AddonName}] stringArrayData is null");
                return;
            }

            var size = stringArrayData->Size;

            for (var i = 0; i < size; i++)
            {
                if (this.FilteredStringArrayData.TryGetValue(
                        i,
                        out var translated))
                {
                    try
                    {
                        var builder = new SeStringBuilder();
                        builder.Append(translated);
                        var span = builder.GetViewAsSpan();
                        stringArrayData->SetValue(i, span, true, true, true);

                        PluginLog.Debug(
                            $"[{this.AddonName}] Applied translation to index {i}: '{translated}'");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(
                            $"[{this.AddonName}] Error applying translation to index {i}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(
                $"[{this.AddonName}] Error in ApplyTranslatedStringArray: {ex.Message}");
        }
    }

    /// <summary>
    ///     Restores the original string array data to the addon, if available.
    /// </summary>
    /// <param name="atkStage">The ATK stage instance containing the string array data.</param>
    private void RestoreOriginalStringArray(AtkStage* atkStage)
    {
        PluginLog.Debug(
            $"[{this.AddonName}] RestoreOriginalStringArray called");

        if (!this.StringArrayDataType.HasValue)
        {
            PluginLog.Warning(
                $"[{this.AddonName}] StringArrayDataType is null in RestoreOriginalStringArray");
            return;
        }

        try
        {
            var stringArrayData =
                atkStage->GetStringArrayData(this.StringArrayDataType.Value);
            if (stringArrayData == null)
            {
                PluginLog.Warning(
                    $"[{this.AddonName}] stringArrayData is null");
                return;
            }

            var size = stringArrayData->Size;

            for (var i = 0; i < size; i++)
            {
                if (this.OriginalStringArrayData.TryGetValue(
                        i,
                        out var originalBytes))
                {
                    try
                    {
                        var originalSpan =
                            new ReadOnlySpan<byte>(originalBytes);
                        stringArrayData->SetValue(
                            i,
                            originalSpan,
                            true,
                            true,
                            true);

                        PluginLog.Debug(
                            $"[{this.AddonName}] Restored original data to index {i} ({originalBytes.Length} bytes)");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(
                            $"[{this.AddonName}] Error restoring original data to index {i}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error(
                $"[{this.AddonName}] Error in RestoreOriginalStringArray: {ex.Message}");
        }
    }

    /// <summary>
    ///     JSON payload structure used for serialization of translated values.
    /// </summary>
    private class TranslatedPayload
    {
        public Dictionary<int, string>? atk { get; set; }

        public Dictionary<int, string>? array { get; set; }
    }

    /// <summary>
    ///     Structure used to deserialize combined translation data from the DB.
    /// </summary>
    private class CombinedTranslationData
    {
        public Dictionary<int, string>? atkValues { get; set; }

        public Dictionary<int, string>? stringArrayData { get; set; }
    }
}