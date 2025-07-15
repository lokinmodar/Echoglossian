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
public abstract unsafe class GenericAddonHandler : IAddonTranslationHandler
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
        var data = atkStage->GetStringArrayData(this.StringArrayDataType.Value);
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
    /// </summary>
    /// <param name="evt">The type of addon event.</param>
    /// <param name="args">The arguments associated with the addon event.</param>
    /// <summary>
    ///     Extracts and filters both AtkValues and StringArrayData for translation.
    ///     Combines extracted strings into the translation dictionary and calls
    ///     Translate.
    /// </summary>
    protected void ExtractAndTranslate(AddonEvent evt, AddonArgs args)
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

        // Merge values for combined translation
        var combined = new Dictionary<int, string>();
        foreach (var pair in this.FilteredAtkValues)
        {
            combined[$"a{pair.Key}".GetHashCode()] = pair.Value;
        }

        foreach (var pair in this.FilteredStringArrayData)
        {
            combined[$"s{pair.Key}".GetHashCode()] = pair.Value;
        }

        if (combined.Count == 0)
        {
            PluginLog.Debug($"[{this.AddonName}] Nothing to translate.");
            return;
        }

        var input = string.Join(
            "|",
            combined.Select(kvp => $"{kvp.Key}|{kvp.Value}"));

        var translated = this.TranslationService.Translate(
            input,
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code);

        var parts = translated.Split('|');
        var parsed = new Dictionary<int, string>();
        for (var i = 0; i < parts.Length - 1; i += 2)
        {
            if (int.TryParse(parts[i], out var key))
            {
                parsed[key] = parts[i + 1];
            }
        }

        foreach (var pair in this.FilteredAtkValues.Keys.ToList())
        {
            var hash = $"a{pair}".GetHashCode();
            if (parsed.TryGetValue(hash, out var value))
            {
                this.FilteredAtkValues[pair] = value;
            }
        }

        foreach (var pair in this.FilteredStringArrayData.Keys.ToList())
        {
            var hash = $"s{pair}".GetHashCode();
            if (parsed.TryGetValue(hash, out var value))
            {
                this.FilteredStringArrayData[pair] = value;
            }
        }

        // Save translated result to DB
        var entity = Echoglossian.FormatGameWindow(
            this.AddonName,
            "[combined]", // You can replace this with raw-extracted data if needed
            ClientStateInterface.ClientLanguage.Humanize(),
            this.SerializeTranslationResult(),
            LangDict[LanguageInt].Code,
            this.Config.ChosenTransEngine);

        Echoglossian.InsertGameWindow(entity);
    }

    /// <summary>
    ///     Applies translated content to the addon based on the specified event and
    ///     arguments.
    /// </summary>
    /// <param name="type">The type of addon event.</param>
    /// <param name="args">The arguments associated with the addon event.</param>
    protected void ApplyTranslated(AddonEvent type, AddonArgs args)
    {
        PluginLog.Debug(
            $"[{this.AddonName}] Called ApplyTranslated - {type} - Args: {args.Type}");
        if (args.AddonName != this.AddonName)
        {
            PluginLog.Debug(
                $"[{this.AddonName}] ApplyTranslated - Addon name mismatch: {args.AddonName} != {this.AddonName}");
            return;
        }

        PluginLog.Debug($"[{this.AddonName}] ApplyTranslated - {type}");

        var atkStage = AtkStage.Instance();
        var addon =
            atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
        if (addon == null || !addon->IsVisible)
        {
            return;
        }

        switch (type)
        {
            case AddonEvent.PreSetup when this.UseAtkValues:
                this.ApplyTranslatedAtkValues(addon);
                this.ApplyTranslatedStringArray(atkStage);
                break;
            case AddonEvent.PostSetup:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PostSetup event.");
                break;
            case AddonEvent.PreUpdate when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PreUpdate event.");
                break;
            case AddonEvent.PostUpdate when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PostUpdate event.");
                break;
            case AddonEvent.PreDraw when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PreDraw event.");
                break;
            case AddonEvent.PostDraw when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PostDraw event.");
                break;
            case AddonEvent.PreFinalize when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PreFinalize event.");
                break;
            case AddonEvent.PreRequestedUpdate when this.UseStringArray &&
                                                    this.StringArrayDataType
                                                        .HasValue:
                this.ApplyTranslatedAtkValues(addon);
                this.ApplyTranslatedStringArray(atkStage);
                break;
            case AddonEvent.PostRequestedUpdate when this.UseStringArray &&
                this.StringArrayDataType.HasValue:
                // this.ApplyTranslatedStringArray(atkStage);
                this.RestoreOriginalStringArray(atkStage);
                break;
            case AddonEvent.PreRefresh when this.UseAtkValues:
                this.ApplyTranslatedAtkValues(addon);
                this.ApplyTranslatedStringArray(atkStage);
                break;
            case AddonEvent.PostRefresh when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PostRefresh event.");
                break;
            case AddonEvent.PreReceiveEvent when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PreReceiveEvent event.");
                break;
            case AddonEvent.PostReceiveEvent when this.UseAtkValues:
                PluginLog.Debug(
                    $"[{this.AddonName}] ApplyTranslated skipped for PostReceiveEvent event.");
                break;
            default:
                PluginLog.Debug(
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
        PluginLog.Debug(
            $"2 - [{this.AddonName}] ApplyTranslatedAtkValues called");
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

        // addon->OnRefresh((uint)count, atkValues); // Uncomment if refresh is required
        // addon->OnSetup((uint)count, atkValues); // Ensure the addon is set up with the updated values
    }

    /// <summary>
    ///     Applies translated strings from the string array data to the addon.
    /// </summary>
    /// <param name="atkStage">The ATK stage instance containing the string array data.</param>
    private void ApplyTranslatedStringArray(AtkStage* atkStage)
    {
        PluginLog.Debug(
            $"3 - [{this.AddonName}] ApplyTranslatedStringArray called");

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
                        // Use SeStringBuilder for proper encoding
                        var seStringBuilder = new SeStringBuilder();
                        seStringBuilder.Append(translated);
                        var translatedSpan = seStringBuilder.GetViewAsSpan();

                        stringArrayData->SetValue(
                            i,
                            translatedSpan,
                            true,
                            true,
                            true);

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
                        // Restore using the exact original byte data
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
}