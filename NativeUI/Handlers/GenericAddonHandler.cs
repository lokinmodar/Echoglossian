// <copyright file="GenericAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Lumina.Text.ReadOnly;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

using static Dalamud.Plugin.Services.IAddonLifecycle;

namespace Echoglossian.NativeUI.Handlers;

/// <summary>
/// Local delegate used internally for event dispatching.
/// </summary>
/// <param name="evt">The addon event being handled.</param>
/// <param name="args">The arguments associated with the addon event.</param>
public delegate void LocalAddonHandlerDelegate(AddonEvent evt, AddonArgs args);

/// <summary>
/// Generic, reusable base class for translating interface addons using AtkValues and/or StringArrayData.
/// </summary>
public abstract unsafe class GenericAddonHandler : IAddonTranslationHandler
{
  protected readonly TranslationService TranslationService;
  protected readonly Config Config;
  protected readonly string AddonName;
  protected readonly bool UseAtkValues;
  protected readonly bool UseStringArray;
  protected readonly StringArrayType? StringArrayDataType;

  protected AtkUnitBase* AddonPtr = null;
  protected AtkValue* AtkValuesPtr = null;
  protected int AtkValuesCount = 0;

  protected byte** StringArrayDataPtr = null;
  protected int StringArraySize = 0;

  protected Dictionary<int, string?> RawAtkValues = new();
  protected Dictionary<int, string> FilteredAtkValues = new();

  protected Dictionary<int, string> RawStringArrayData = new();
  protected Dictionary<int, string> FilteredStringArrayData = new();

  private static readonly Regex NumericLikePattern = new(@"^\s*([€£$¥]?\s*\d+([.,]\d+)?\s*[%€£$¥]?\s*|(\d+/\d+))\s*$", RegexOptions.Compiled);

  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();

  /// <summary>
  /// Initializes a new instance of the <see cref="GenericAddonHandler"/> class.
  /// </summary>
  /// <param name="addonName">The name of the addon being handled.</param>
  /// <param name="config">The configuration settings for the addon handler.</param>
  /// <param name="translationService">The translation service used for translating addon data.</param>
  /// <param name="useAtkValues">Indicates whether AtkValues should be used for translation.</param>
  /// <param name="useStringArray">Indicates whether StringArrayData should be used for translation.</param>
  /// <param name="stringArrayDataType">The type of StringArrayData to be used, if applicable.</param>
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
  /// Registers a local handler for a given Dalamud AddonEvent.
  /// </summary>
  /// <param name="evt">The addon event to register the handler for.</param>
  /// <param name="handler">The delegate to handle the specified addon event.</param>
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
  /// Returns a combined dictionary of Dalamud event delegates for AddonLifecycle.
  /// </summary>
  /// <returns>A dictionary mapping addon events to their respective delegates.</returns>
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
  /// Default logic for extracting and translating values from an addon.
  /// Subclasses may call this in their custom handler.
  /// </summary>
  /// <param name="type">Type of the addon event being handled.</param>
  /// <param name="args">Arguments containing the addon event type and name.</param>
  protected void ExtractAndTranslate(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate - {type}");

    var atkStage = AtkStage.Instance();
    this.AddonPtr = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);

    if (this.AddonPtr == null || !this.AddonPtr->IsVisible)
    {
      return;
    }

    // StringArrayData translation
    if (this.UseStringArray && this.StringArrayDataType.HasValue)
    {
      var stringArray = atkStage->GetStringArrayData(this.StringArrayDataType.Value)->StringArray;
      this.StringArrayDataPtr = stringArray;
      this.StringArraySize = atkStage->GetStringArrayData(this.StringArrayDataType.Value)->Size;

      this.RawStringArrayData = Enumerable.Range(0, this.StringArraySize)
        .ToDictionary(i => i, i => new ReadOnlySeStringSpan(stringArray[i]).ExtractText());

      this.FilteredStringArrayData = this.RawStringArrayData
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

    // AtkValues translation
    if (this.UseAtkValues)
    {
      this.AtkValuesPtr = this.AddonPtr->AtkValues;
      this.AtkValuesCount = this.AddonPtr->AtkValuesCount;

      if (this.AtkValuesPtr == null || this.AtkValuesCount <= 0)
      {
        return;
      }

      this.RawAtkValues = Enumerable.Range(0, this.AtkValuesCount)
        .ToDictionary(i => i, i =>
        {
          var type = this.AtkValuesPtr[i].Type;
          return type is ValueType.String or ValueType.String8 or ValueType.ManagedString
            ? MemoryHelper.ReadSeStringAsString(out _, (nint)this.AtkValuesPtr[i].String.Value)
            : null;
        });

      this.FilteredAtkValues = this.RawAtkValues
        .Where(kvp =>
          !string.IsNullOrWhiteSpace(kvp.Value) &&
          !kvp.Value.All(char.IsPunctuation) &&
          !NumericLikePattern.IsMatch(kvp.Value))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

      string input = string.Join("|", this.FilteredAtkValues.Select(kvp => $"{kvp.Key}|{kvp.Value}"));
      string translated = this.TranslationService.Translate(
        input,
        Echoglossian.ClientStateInterface.ClientLanguage.Humanize(),
        Echoglossian.LangDict[Echoglossian.LanguageInt].Code);

      var parts = translated.Split('|');
      for (int i = 0; i < parts.Length - 1; i += 2)
      {
        if (int.TryParse(parts[i], out int index))
        {
          this.FilteredAtkValues[index] = parts[i + 1];
        }
      }
    }
  }

  /// <summary>
  /// Default logic for applying previously translated values back into the addon.
  /// Subclasses may call this in their custom handler.
  /// </summary>
  /// <param name="type">Type of the addon event being handled.</param>
  /// <param name="args">Arguments containing the addon event type and name.</param>
  protected void ApplyTranslated(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ApplyTranslated - {type}");

    if (this.UseStringArray && this.StringArrayDataPtr != null && this.StringArraySize > 0)
    {
      for (int i = 0; i < this.StringArraySize; i++)
      {
        if (this.FilteredStringArrayData.TryGetValue(i, out var translated))
        {
          var ro = new ReadOnlySeString(translated);
          AtkStage.Instance()->GetStringArrayData(this.StringArrayDataType!.Value)->SetValueAndUpdate(
            i,
            new Lumina.Text.SeStringBuilder().Append(ro).GetViewAsSpan(),
            true, true);
        }
      }
    }

    if (this.UseAtkValues && this.AtkValuesPtr != null && this.AtkValuesCount > 0)
    {
      var span = new Span<AtkValue>(this.AtkValuesPtr, this.AtkValuesCount);
      for (int i = 0; i < this.AtkValuesCount; i++)
      {
        if (span[i].Type is ValueType.String or ValueType.String8 or ValueType.ManagedString)
        {
          if (this.FilteredAtkValues.TryGetValue(i, out var translated))
          {
            span[i].SetManagedString(translated);
          }
        }
      }

      this.AddonPtr->OnRefresh((uint)this.AtkValuesCount, this.AtkValuesPtr);
    }
  }
}
