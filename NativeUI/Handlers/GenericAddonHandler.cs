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
/// Represents a generic handler for translating UI content from addon lifecycle events.
/// </summary>
public abstract unsafe class GenericAddonHandler : IAddonTranslationHandler
{
  protected readonly TranslationService TranslationService;
  protected readonly Config Config;
  protected readonly string AddonName;
  protected readonly bool UseAtkValues;
  protected readonly bool UseStringArray;
  protected readonly StringArrayType? StringArrayDataType;

  protected Dictionary<int, string> FilteredAtkValues = new();
  protected Dictionary<int, string> FilteredStringArrayData = new();
  protected Dictionary<int, string> OriginalStringArrayData = new();

  private static readonly Regex NumericLikePattern = new(@"^\s*([€£$¥]?\s*\d+([.,]\d+)?\s*[%€£$¥]?\s*|(\d+/\d+))\s*$", RegexOptions.Compiled);
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();

  /// <summary>
  /// Initializes a new instance of the <see cref="GenericAddonHandler"/> class.
  /// </summary>
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
  /// Registers a translation handler for the specified addon event.
  /// </summary>
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
  /// Returns the complete list of registered addon event handlers.
  /// </summary>
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
  /// Extracts and translates addon string array and ATK values.
  /// </summary>
  protected void ExtractAndTranslate(AddonEvent type, AddonArgs args)
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate - {type}");

    if (this.UseStringArray && this.StringArrayDataType.HasValue && args is AddonRequestedUpdateArgs updateArgs && updateArgs.StringArrayData != IntPtr.Zero)
    {
      var data = (StringArrayData*)updateArgs.StringArrayData;
      var size = data->Size;
      var strings = data->StringArray;

      this.OriginalStringArrayData = Enumerable.Range(0, size)
        .ToDictionary(i => i, i => new ReadOnlySeStringSpan(strings[i]).ExtractText());

      this.FilteredStringArrayData = this.OriginalStringArrayData
        .Where(kvp =>
          !string.IsNullOrWhiteSpace(kvp.Value) &&
          !kvp.Value.All(char.IsPunctuation) &&
          !NumericLikePattern.IsMatch(kvp.Value))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

      var input = string.Join("|", this.FilteredStringArrayData.Select(kvp => $"{kvp.Key}|{kvp.Value}"));
      var translated = this.TranslationService.Translate(
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

    if (!this.UseAtkValues)
    {
      return;
    }

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

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

    var atkInput = string.Join("|", this.FilteredAtkValues.Select(kvp => $"{kvp.Key}|{kvp.Value}"));
    var atkTranslated = this.TranslationService.Translate(
      atkInput,
      Echoglossian.ClientStateInterface.ClientLanguage.Humanize(),
      Echoglossian.LangDict[Echoglossian.LanguageInt].Code);

    var parts2 = atkTranslated.Split('|').Select(p => p.Trim()).ToArray();
    for (int i = 0; i < parts2.Length - 1; i += 2)
    {
      if (int.TryParse(parts2[i], out int index))
      {
        this.FilteredAtkValues[index] = parts2[i + 1];
      }
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate completed with {this.FilteredAtkValues.Count} AtkValues and {this.FilteredStringArrayData.Count} StringArray entries.");
  }

  /// <summary>
  /// Applies translated values to the addon or restores original string data.
  /// </summary>
  protected void ApplyTranslated(AddonEvent type, AddonArgs args)
  {
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
      case AddonEvent.PreRefresh when this.UseAtkValues:
        this.ApplyTranslatedAtkValues(addon);
        break;

      case AddonEvent.PreRequestedUpdate when this.UseStringArray:
        this.ApplyTranslatedStringArray(args);
        break;

      case AddonEvent.PostRequestedUpdate when this.UseStringArray:
        this.RestoreOriginalStringArray(args);
        break;

      default:
        PluginLog.Verbose($"[{this.AddonName}] ApplyTranslated skipped for event: {type}");
        break;
    }
  }

  /// <summary>
  /// Writes translated ATK values into the addon memory.
  /// </summary>
  private void ApplyTranslatedAtkValues(AtkUnitBase* addon)
  {
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
  }

  /// <summary>
  /// Writes translated string array data to the string array memory location.
  /// </summary>
  private void ApplyTranslatedStringArray(AddonArgs args)
  {
    if (args is not AddonRequestedUpdateArgs updateArgs || updateArgs.StringArrayData == IntPtr.Zero)
    {
      return;
    }

    var data = (StringArrayData*)updateArgs.StringArrayData;
    var size = data->Size;

    for (int i = 0; i < size; i++)
    {
      if (this.FilteredStringArrayData.TryGetValue(i, out var translated))
      {
        var ro = new ReadOnlySeString(translated);
        data->SetValue(i, new Lumina.Text.SeStringBuilder().Append(ro).GetViewAsSpan(), readBeforeWrite: true, managed: true, suppressUpdates: true);
      }
    }
  }

  /// <summary>
  /// Restores original string array values to prevent UI desync.
  /// </summary>
  private void RestoreOriginalStringArray(AddonArgs args)
  {
    if (args is not AddonRequestedUpdateArgs updateArgs || updateArgs.StringArrayData == IntPtr.Zero)
    {
      return;
    }

    var data = (StringArrayData*)updateArgs.StringArrayData;
    var size = data->Size;

    for (int i = 0; i < size; i++)
    {
      if (this.OriginalStringArrayData.TryGetValue(i, out var original))
      {
        var ro = new ReadOnlySeString(original);
        data->SetValue(i, new Lumina.Text.SeStringBuilder().Append(ro).GetViewAsSpan(), readBeforeWrite: true, managed: true, suppressUpdates: false);
      }
    }
  }
}
