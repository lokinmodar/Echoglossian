// <copyright file="CharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text;
using Lumina.Text.ReadOnly;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian.NativeUI.Handlers;

/// <summary>
/// Responsible for extracting, translating, and applying translated AtkValues for the Character window.
/// </summary>
public unsafe class PrevCharacterWindowHandler
{
  private readonly TranslationService translationService;

  public bool GatheringComplete { get; private set; }

  public Dictionary<int, string?> RawAtkValues { get; private set; } = new();

  public Dictionary<int, string> RawStringArrayData { get; private set; } = new();

  public Dictionary<int, string> FilteredStringValues { get; private set; } = new();

  public Dictionary<int, string> FilteredStringArrayData { get; private set; } = new();

  public string TranslationInputString { get; private set; } = string.Empty;

  public string TranslationInputStringArray { get; private set; } = string.Empty;

  public Config Config { get; set; } = new();

  private AtkValue* atkValuesPtr = null;
  private int atkValuesCount = 0;

  private byte** stringArrayDataPtr = null;
  private int atkArrayDataSize = 0;

  private AtkUnitBase* characterWindowAddon = null;

  /// <summary>
  /// Initializes a new instance of the <see cref="CharacterWindowHandler"/> class.
  /// </summary>
  /// <param name="config">Configuration settings.</param>
  /// <param name="translationService">The translation service to use.</param>
  public CharacterWindowHandler(Config config, TranslationService translationService)
  {
    this.translationService = translationService;
    this.Config = config;
  }

  /// <summary>
  /// Extracts AtkValues from the Character addon and prepares the translated values.
  /// </summary>
  /// <param name="type">The type of addon event.</param>
  /// <param name="args">The arguments containing the addon event type and name.</param>
  public unsafe void ExtractAndTranslateValues(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"CharacterWindowHandler: ExtractAndTranslateValues called with type: {type}, addon: {args.AddonName}");

    if (type != AddonEvent.PreSetup || args.AddonName != "Character")
    {
      return;
    }

    var atkStg = AtkStage.Instance();
    this.characterWindowAddon = atkStg->RaptureAtkUnitManager->GetAddonByName(args.AddonName);

    var stringArray = atkStg->GetStringArrayData(StringArrayType.Character)->StringArray;
    this.stringArrayDataPtr = stringArray;

    var stringArraySize = atkStg->GetStringArrayData(StringArrayType.Character)->Size;
    this.atkArrayDataSize = stringArraySize;

    PluginLog.Debug($"Character window strings size {stringArraySize}");

    // Use LINQ to extract the texts with their positions in the stringArray into a Dictionary
    this.RawStringArrayData = Enumerable.Range(0, stringArraySize)
      .ToDictionary(i => i, i => new ReadOnlySeStringSpan(stringArray[i]).ExtractText());

    // For debugging purposes, we can log the RawStringArrayData to see if they were extracted correctly
    PluginLog.Debug($"RawStringArrayData: {JsonConvert.SerializeObject(this.RawStringArrayData, Formatting.Indented)}");

    // Filter the stringArrayData to only include strings that are not null or empty and are not just a punctuation symbol and are not only numerals or numeric representations like "1234" or "12.34" or fractions (texts like this "1. Text" should not be filtered out but "1234" or "12.34" or 99/99 should be filtered out)
    this.FilteredStringArrayData = this.RawStringArrayData
      .Where(kvp =>
        !string.IsNullOrWhiteSpace(kvp.Value) &&
        !kvp.Value.All(char.IsPunctuation) &&
        !NumericLikePattern.IsMatch(kvp.Value))
      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

    this.TranslationInputStringArray = string.Join("|", this.FilteredStringArrayData.Select(kvp => $"{kvp.Key}|{kvp.Value}"));

    PluginLog.Debug($"Character window strings: {this.TranslationInputStringArray}");

    string adTranslation = this.translationService.Translate(this.TranslationInputStringArray, ClientStateInterface.ClientLanguage.Humanize(), LangDict[LanguageInt].Code);

    var adParts = adTranslation.Split('|');

    for (int i = 0; i < adParts.Length - 1; i += 2)
    {
      if (int.TryParse(adParts[i], out int index))
      {
        this.FilteredStringArrayData[index] = adParts[i + 1];
      }
    }

    /* PluginLog.Debug($"FilteredStringArrayData: {JsonConvert.SerializeObject(this.FilteredStringArrayData, Formatting.Indented)}");*/

    if (this.characterWindowAddon == null || !this.characterWindowAddon->IsVisible)
    {
      return;
    }

    var cwAtkVals = this.characterWindowAddon->AtkValues;
    var cwAtkValsCount = this.characterWindowAddon->AtkValuesCount;

    if (cwAtkVals == null)
    {
      return;
    }

    this.atkValuesPtr = cwAtkVals;
    this.atkValuesCount = cwAtkValsCount;

    this.RawAtkValues = Enumerable.Range(0, cwAtkValsCount)
      .ToDictionary(
        i => i,
        i =>
        {
          var type = cwAtkVals[i].Type;
          return type == ValueType.String || type == ValueType.String8 || type == ValueType.ManagedString
            ? MemoryHelper.ReadSeStringAsString(out _, (nint)cwAtkVals[i].String.Value)
            : null;
        });

    this.FilteredStringValues = Enumerable.Range(0, this.atkValuesCount)
      .Where(i => cwAtkVals[i].Type == ValueType.String || cwAtkVals[i].Type == ValueType.String8 || cwAtkVals[i].Type == ValueType.ManagedString)
      .Select(i => new
      {
        Index = i,
        Value = MemoryHelper.ReadSeStringAsString(out _, (nint)cwAtkVals[i].String.Value),
      })
      .Where(x => x.Value != null)
      .ToDictionary(x => x.Index, x => x.Value!);

    if (this.FilteredStringValues.Count == 0)
    {
      this.GatheringComplete = false;
      return;
    }

    this.TranslationInputString = string.Join("|", this.FilteredStringValues.Select(kvp => $"{kvp.Key}|{kvp.Value}"));

    PluginLog.Debug($"Character window AtkValues: {JsonConvert.SerializeObject(this.FilteredStringValues, Formatting.Indented)}");
    PluginLog.Debug($"Character window AtkValues string: {this.TranslationInputString}");

    string translation = this.translationService.Translate(this.TranslationInputString, ClientStateInterface.ClientLanguage.Humanize(), LangDict[LanguageInt].Code);

    if (string.IsNullOrEmpty(translation))
    {
      PluginLog.Error("Translation failed for Character window AtkValues.");
      this.GatheringComplete = false;
      return;
    }

    var parts = translation.Split('|');
    for (int i = 0; i < parts.Length - 1; i += 2)
    {
      if (int.TryParse(parts[i], out int index))
      {
        this.FilteredStringValues[index] = parts[i + 1];
      }
    }

    PluginLog.Debug($"Translated values count: {this.FilteredStringValues.Count}");
    this.GatheringComplete = true;

    this.ApplyTranslatedArrayDataValues(type, args);
    this.ApplyTranslatedValues(type, args);


    this.characterWindowAddon->OnRefresh((uint)this.atkValuesCount, this.atkValuesPtr);

    return;
  }

  /// <summary>
  /// Applies the translated AtkValues to the Character window.
  /// </summary>
  /// <param name="type">The type of addon event.</param>
  /// <param name="args">The arguments containing the addon event type and name.</param>
  public unsafe void ApplyTranslatedValues(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"CharacterWindowHandler: ApplyTranslatedValues called with type: {type}, addon: {args.AddonName}");
    if (type is not AddonEvent.PreSetup or AddonEvent.PreRefresh or AddonEvent.PreRequestedUpdate && args.AddonName != "Character")
    {
      return;
    }

    if (!this.GatheringComplete || this.atkValuesPtr == null || this.atkValuesCount <= 0)
    {
      return;
    }

    var values = new Span<AtkValue>(this.atkValuesPtr, this.atkValuesCount);

    for (int i = 0; i < this.atkValuesCount; i++)
    {
      if (values[i].Type == ValueType.String || values[i].Type == ValueType.String8 || values[i].Type == ValueType.ManagedString)
      {
        if (this.FilteredStringValues.TryGetValue(i, out var translated))
        {
          values[i].SetManagedString(translated);
        }
      }
    }

    PluginLog.Debug("Applied translated Character window AtkValues.");
    return;
  }

  /// <summary>
  /// Applies the translated string array data values to the Character window.
  /// </summary>
  /// <param name="type">The type of addon event.</param>
  /// <param name="args">The arguments containing the addon event type and name.</param>
  public unsafe void ApplyTranslatedArrayDataValues(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"CharacterWindowHandler: ApplyTranslatedArrayDataValues called with type: {type}, addon: {args.AddonName}");
    if (type is not AddonEvent.PreSetup or AddonEvent.PreRefresh or AddonEvent.PreRequestedUpdate && args.AddonName != "Character")
    {
      return;
    }

    if (!this.GatheringComplete || this.stringArrayDataPtr == null || this.atkArrayDataSize <= 0)
    {
      return;
    }

    for (int i = 0; i < this.atkArrayDataSize; i++)
    {
      if (this.FilteredStringArrayData.TryGetValue(i, out var translated))
      {
        var readOnlySeString = new ReadOnlySeString(translated);

        AtkStage.Instance()->GetStringArrayData(StringArrayType.Character)->SetValue(i, new Lumina.Text.SeStringBuilder()
            .Append(readOnlySeString)
            .GetViewAsSpan(), true, true);
      }
    }

    PluginLog.Debug("Applied translated Character window string array data.");
    return;
  }
}