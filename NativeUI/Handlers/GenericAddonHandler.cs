// <copyright file="GenericAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Lumina.Text.ReadOnly;

using static Dalamud.Plugin.Services.IAddonLifecycle;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.Handlers;

/// <summary>
/// Represents a delegate for handling local addon events.
/// </summary>
/// <param name="evt">The addon event being handled.</param>
/// <param name="args">The arguments associated with the addon event.</param>
public delegate void LocalAddonHandlerDelegate(AddonEvent evt, AddonArgs args);

/// <summary>
/// Represents a generic handler for addon translation.
/// </summary>
public abstract unsafe class GenericAddonHandler : IAddonTranslationHandler
{
  /// <summary>
  /// Gets the translation service used for translating addon content.
  /// </summary>
  protected readonly TranslationService TranslationService;

  /// <summary>
  /// Gets the configuration settings for the addon handler.
  /// </summary>
  protected readonly Config Config;

  /// <summary>
  /// Gets the name of the addon being handled.
  /// </summary>
  protected readonly string AddonName;

  /// <summary>
  /// Gets a value indicating whether ATK values are used for translation.
  /// </summary>
  protected readonly bool UseAtkValues;

  /// <summary>
  /// Gets a value indicating whether string arrays are used for translation.
  /// </summary>
  protected readonly bool UseStringArray;

  /// <summary>
  /// Gets the type of string array data, if applicable.
  /// </summary>
  protected readonly StringArrayType? StringArrayDataType;

  /// <summary>
  /// Stores filtered ATK values for translation.
  /// </summary>
  protected Dictionary<int, string> FilteredAtkValues = new();

  /// <summary>
  /// Stores filtered string array data for translation.
  /// </summary>
  protected Dictionary<int, string> FilteredStringArrayData = new();

  /// <summary>
  /// Stores the original string array values to restore after translation.
  /// </summary>
  protected Dictionary<int, string> OriginalStringArrayData = new();

  /// <summary>
  /// A regular expression pattern for identifying numeric-like strings.
  /// </summary>
  private static readonly Regex NumericLikePattern = new(@"^\s*([€£$¥]?\s*\d+([.,]\d+)?\s*[%€£$¥]?\s*|(\d+/\d+))\s*$", RegexOptions.Compiled);

  /// <summary>
  /// Stores event handlers for addon events.
  /// </summary>
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();

  /// <summary>
  /// Initializes a new instance of the <see cref="GenericAddonHandler"/> class.
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
  /// Registers a handler for a specific addon event.
  /// </summary>
  /// <param name="evt">The addon event to handle.</param>
  /// <param name="handler">The delegate to handle the event.</param>
  public void RegisterHandler(AddonEvent evt, LocalAddonHandlerDelegate handler)
  {
    if (!this.eventHandlers.TryGetValue(evt, out var list))
    {
      list = new List<LocalAddonHandlerDelegate>();
      this.eventHandlers[evt] = list;
    }

    list.Add(handler);
  }

  /// <summary>
  /// Gets the registered event handlers for addon events.
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
  /// Extracts and translates addon content based on the specified event and arguments.
  /// </summary>
  /// <param name="type">The type of addon event.</param>
  /// <param name="args">The arguments associated with the addon event.</param>
  protected void ExtractAndTranslate(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"1 - [{this.AddonName}] Called ExtractAndTranslate - {type} - Args: {args}");
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate - {type}");

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    if (this.UseStringArray && this.StringArrayDataType.HasValue)
    {
      var stringArrayData = atkStage->GetStringArrayData(this.StringArrayDataType.Value);
      var stringArray = stringArrayData->StringArray;
      var size = stringArrayData->Size;

      this.OriginalStringArrayData = Enumerable.Range(0, size)
        .ToDictionary(i => i, i => new ReadOnlySeStringSpan(stringArray[i]).ExtractText());

      this.FilteredStringArrayData = this.OriginalStringArrayData
              .Where(kvp =>
                !string.IsNullOrWhiteSpace(kvp.Value) &&
                !kvp.Value.All(char.IsPunctuation) &&
                !NumericLikePattern.IsMatch(kvp.Value))
              .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

      string input = string.Join("|", this.FilteredStringArrayData.Select(kvp => $"{kvp.Key}|{kvp.Value}"));
      string translated = this.TranslationService.Translate(
        input,
        Echoglossian.ClientStateInterface.ClientLanguage.Humanize(),
        Echoglossian.LangDict[Echoglossian.LanguageInt].Code);

      var parts = translated.Split('|');
      for (int i = 0; i < parts.Length - 1; i += 2)
      {
        if (int.TryParse(parts[i], out int index))
        {
          this.FilteredStringArrayData[index] = parts[i + 1];
        }
      }
    }

    if (this.UseAtkValues)
    {
      var atkValues = addon->AtkValues;
      var count = addon->AtkValuesCount;
      if (atkValues == null || count <= 0)
      {
        return;
      }

      var raw = Enumerable.Range(0, count)
        .ToDictionary(i => i, i =>
        {
          var type = atkValues[i].Type;
          return type is ValueType.String or ValueType.String8 or ValueType.ManagedString
            ? MemoryHelper.ReadSeStringAsString(out _, (nint)atkValues[i].String.Value)
            : null;
        });

      this.FilteredAtkValues = raw
        .Where(kvp =>
          !string.IsNullOrWhiteSpace(kvp.Value) &&
          !kvp.Value.All(char.IsPunctuation) &&
          !NumericLikePattern.IsMatch(kvp.Value))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

      string input = string.Join("|", this.FilteredAtkValues.Select(kvp => $"{kvp.Key}|{kvp.Value}"));

      // TODO: Add logic to retrieve from DB if available
      string translated = this.TranslationService.Translate(
        input,
        Echoglossian.ClientStateInterface.ClientLanguage.Humanize(),
        Echoglossian.LangDict[Echoglossian.LanguageInt].Code);

      // TODO: Add logic to handle translation errors or empty results and logic to save to DB if it is a new translation

      var parts = translated.Split('|');
      // trim starting and ending whitespace from each part
      parts = parts.Select(p => p.Trim()).ToArray();

      for (int i = 0; i < parts.Length - 1; i += 2)
      {
        if (int.TryParse(parts[i], out int index))
        {
          this.FilteredAtkValues[index] = parts[i + 1];
        }
      }
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate - Completed with {this.FilteredAtkValues.Count} ATK values and {this.FilteredStringArrayData.Count} string array entries filtered.");

    // Apply the translated content to the addon

    // addon->OnRefresh((uint)addon->AtkValuesCount, addon->AtkValues);
    // this.ApplyTranslated(type, args);
    // addon->OnSetup((uint)addon->AtkValuesCount, addon->AtkValues); // Ensure the addon is set up with the updated values
  }

  /// <summary>
  /// Applies translated content to the addon based on the specified event and arguments.
  /// </summary>
  /// <param name="type">The type of addon event.</param>
  /// <param name="args">The arguments associated with the addon event.</param>
  protected void ApplyTranslated(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] Called ApplyTranslated - {type} - Args: {args}");
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ApplyTranslated - {type}");

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
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
      case AddonEvent.PreRefresh when this.UseAtkValues:
        this.ApplyTranslatedAtkValues(addon);
        break;

      case AddonEvent.PreRequestedUpdate when this.UseStringArray && this.StringArrayDataType.HasValue:
        this.ApplyTranslatedStringArray(atkStage);
        break;
      case AddonEvent.PostRequestedUpdate when this.UseStringArray && this.StringArrayDataType.HasValue:
        this.RestoreOriginalStringArray(atkStage);
        break;

      default:
        PluginLog.Verbose($"[{this.AddonName}] ApplyTranslated skipped for event: {type}");
        break;
    }
  }

  /// <summary>
  /// Applies translated ATK values to the addon, updating string values as needed.
  /// </summary>
  /// <param name="addon">The ATK unit base addon to apply translations to.</param>
  private unsafe void ApplyTranslatedAtkValues(AtkUnitBase* addon)
  {
    PluginLog.Debug($"2 - [{this.AddonName}] ApplyTranslatedAtkValues called");
    var atkValues = addon->AtkValues;
    var count = addon->AtkValuesCount;
    if (atkValues == null || count <= 0)
    {
      return;
    }

    var span = new Span<AtkValue>(atkValues, count);
    for (int i = 0; i < count; i++)
    {
      if (span[i].Type is ValueType.String or ValueType.String8 or ValueType.ManagedString)
      {
        if (this.FilteredAtkValues.TryGetValue(i, out var translated))
        {
          span[i].SetManagedString(translated);
        }
      }
    }

    // addon->OnRefresh((uint)count, atkValues); // Uncomment if refresh is required
    // addon->OnSetup((uint)count, atkValues); // Ensure the addon is set up with the updated values
  }

  /// <summary>
  /// Applies translated strings from the string array data to the addon.
  /// </summary>
  /// <param name="atkStage">The ATK stage instance containing the string array data.</param>
  private unsafe void ApplyTranslatedStringArray(AtkStage* atkStage)
  {
    PluginLog.Debug($"3 - [{this.AddonName}] ApplyTranslatedStringArray called");
    var stringArrayData = atkStage->GetStringArrayData(this.StringArrayDataType!.Value);
    var size = stringArrayData->Size;

    for (int i = 0; i < size; i++)
    {
      if (this.FilteredStringArrayData.TryGetValue(i, out var translated))
      {
        var ro = new ReadOnlySeString(translated);
        stringArrayData->SetValue(i, new Lumina.Text.SeStringBuilder().Append(ro).GetViewAsSpan(), readBeforeWrite: true, managed: true, suppressUpdates: true);
      }
    }
  }

  /// <summary>
  /// Restores the original string array data to the addon, if available.
  /// </summary>
  /// <param name="atkStage">The ATK stage instance containing the string array data.</param>
  private unsafe void RestoreOriginalStringArray(AtkStage* atkStage)
  {
    PluginLog.Debug($"[{this.AddonName}] RestoreOriginalStringArray called");

    var stringArrayData = atkStage->GetStringArrayData(this.StringArrayDataType!.Value);
    var size = stringArrayData->Size;

    for (int i = 0; i < size; i++)
    {
      if (this.OriginalStringArrayData.TryGetValue(i, out var original))
      {
        var ro = new ReadOnlySeString(original);
        stringArrayData->SetValue(i, new Lumina.Text.SeStringBuilder().Append(ro).GetViewAsSpan(), readBeforeWrite: true, managed: true, suppressUpdates: false);
      }
    }
  }
}
