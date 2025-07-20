// <copyright file="GenericAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;

using FFXIVClientStructs.FFXIV.Client.UI;

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
///     The database entity type implementing IGenericEntity.
/// </typeparam>
public abstract unsafe class GenericAddonHandler<TGenericEntity> : IAddonTranslationHandler
    where TGenericEntity : class, IGenericEntity, new()
{
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
  private readonly Dictionary<AddonEvent, List<LocalAddonHandlerDelegate>> eventHandlers = new();

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
  ///     Stores original ATK values extracted from the addon for change detection.
  /// </summary>
  protected Dictionary<int, string> OriginalAtkValues = new();

  /// <summary>
  ///     Initializes a new instance of the <see cref="GenericAddonHandler{TGenericEntity}" /> class.
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
  ///     Handles translation and application of values during PreSetup lifecycle.
  /// </summary>
  /// <param name="type">AddonEvent type.</param>
  /// <param name="args">AddonArgs containing setup context.</param>
  protected void OnPreSetup(AddonEvent type, AddonArgs args)
  {
    if (args is not AddonSetupArgs setupArgs || args.AddonName != this.AddonName)
    {
      return;
    }

    this.ExtractAndTranslate(type, args);
    this.OnAtkValueUpdate(type, args);
  }

  /// <summary>
  ///     Handles translation and application of values during PreRefresh lifecycle.
  /// </summary>
  /// <param name="type">AddonEvent type.</param>
  /// <param name="args">AddonArgs containing refresh context.</param>
  protected void OnPreRefresh(AddonEvent type, AddonArgs args)
  {
    if (args is not AddonRefreshArgs refreshArgs || args.AddonName != this.AddonName)
    {
      return;
    }

    this.ExtractAndTranslate(type, args);
    this.OnAtkValueUpdate(type, args);
  }

  /// <summary>
  ///     Handles translation and application of string array values during PreRequestedUpdate lifecycle.
  /// </summary>
  /// <param name="type">AddonEvent type.</param>
  /// <param name="args">AddonArgs containing update context.</param>
  protected void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
  {
    if (args is not AddonRequestedUpdateArgs requestedUpdateArgs || args.AddonName != this.AddonName)
    {
      return;
    }

    this.ExtractAndTranslate(type, args);
    this.OnArrayDataUpdate(type, args);
  }

  private unsafe void OnAtkValueUpdate(AddonEvent type, AddonArgs args)
  {
    var atkValues = args switch
    {
      AddonRefreshArgs refreshArgs => (AtkValue*)refreshArgs.AtkValues,
      AddonSetupArgs setupArgs => (AtkValue*)setupArgs.AtkValues,
      _ => throw new Exception("Unsupported AddonEvent"),
    };

    var valueCount = args switch
    {
      AddonRefreshArgs refreshArgs => refreshArgs.AtkValueCount,
      AddonSetupArgs setupArgs => setupArgs.AtkValueCount,
      _ => throw new Exception("Unsupported AddonEvent"),
    };

    for (var index = 0; index < valueCount; ++index)
    {
      ref var currentValue = ref atkValues[index];

      if (currentValue.Type is not (ValueType.String or ValueType.String8 or ValueType.ManagedString))
      {
        continue;
      }

      if (!this.FilteredAtkValues.TryGetValue(index, out var translated))
      {
        continue;
      }

      var current = MemoryHelper.ReadSeStringAsString(out _, (nint)currentValue.String.Value);
      if (current == translated)
      {
        continue;
      }

      currentValue.SetManagedString(translated);
    }
  }

  private unsafe void OnArrayDataUpdate(AddonEvent type, AddonArgs args)
  {
    if (args is AddonRequestedUpdateArgs requestedUpdateArgs)
    {
      var stringArrayData = (StringArrayData**)requestedUpdateArgs.StringArrayData;
      var arrayIndex = this.GetStringArrayIndexForAddon((AtkUnitBase*)args.Addon);

      if (arrayIndex is -1)
      {
        return;
      }

      var addonArrayData = stringArrayData[arrayIndex];

      for (var index = 0; index < addonArrayData->Size; ++index)
      {
        ref var currentValue = ref addonArrayData->StringArray[index];

        if (currentValue is null)
        {
          continue;
        }

        if (!this.FilteredStringArrayData.TryGetValue(index, out var translated))
        {
          continue;
        }

        var span = new ReadOnlySeStringSpan(currentValue);
        var current = span.ExtractText();
        if (current == translated)
        {
          continue;
        }

        addonArrayData->SetValue(index, translated, suppressUpdates: true);
      }
    }
  }

  private unsafe int GetStringArrayIndexForAddon(AtkUnitBase* addon)
  {
    var arrayHolder = RaptureAtkModule.Instance()->AtkArrayDataHolder;

    for (var index = 0; index < arrayHolder.StringArrayCount; ++index)
    {
      var stringArray = arrayHolder.StringArrays[index];
      if (stringArray is null)
      {
        continue;
      }

      if (stringArray->SubscribedAddons.Contains((byte)addon->Id))
      {
        return index;
      }
    }

    return -1;
  }

  /// <summary>
  ///     Extracts and translates addon content based on the specified event and
  ///     arguments. Performs DB lookup before calling external translation. Runs asynchronously if configured.
  /// </summary>
  /// <param name="evt">The type of addon event.</param>
  /// <param name="args">The arguments associated with the addon event.</param>
  protected void ExtractAndTranslate(AddonEvent evt, AddonArgs args)
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    this.FilteredAtkValues.Clear();
    this.FilteredStringArrayData.Clear();
    this.OriginalAtkValues.Clear();
    this.OriginalStringArrayData.Clear();

    if (this.UseAtkValues)
    {
      PluginLog.Debug($"[{this.AddonName}] Extracting ATK values...");
      this.ExtractAtkValues(addon);
    }

    if (this.UseStringArray && this.StringArrayDataType.HasValue)
    {
      PluginLog.Debug($"[{this.AddonName}] Extracting StringArrayData for {this.StringArrayDataType.Value}...");
      this.ExtractStringArrayData(atkStage);
    }

    if (this.FilteredAtkValues.Count == 0 && this.FilteredStringArrayData.Count == 0)
    {
      PluginLog.Debug($"[{this.AddonName}] No translatable content found in addon.");
      return;
    }

    if (this.TryLoadTranslationFromDatabase() || this.ContentIsAlreadyTranslated())
    {
      PluginLog.Debug($"[{this.AddonName}] Content already translated or loaded from DB.");
      return;
    }

    var enableAsyncTranslationOverride = true; // Force async translation for testing purposes

    if (enableAsyncTranslationOverride || this.Config.EnableAsyncTranslation)
    {
      PluginLog.Debug($"[{this.AddonName}] Launching async translation task...");

      Task.Run(() => GenericAddonHandlerHelper.PerformTranslationAndSaveAsync<TGenericEntity>(
          this.AddonName,
          this.FilteredAtkValues,
          this.FilteredStringArrayData,
          this.Config,
          this.TranslationService,
          typeof(TGenericEntity)));

      return;
    }

    PluginLog.Debug($"[{this.AddonName}] Performing synchronous translation...");

    var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
    var targetLang = LangDict[LanguageInt].Code;

    var inputParts = new List<string>();
    foreach (var kvp in this.FilteredAtkValues)
    {
      inputParts.Add($"a{kvp.Key}|{kvp.Value}");
    }

    foreach (var kvp in this.FilteredStringArrayData)
    {
      inputParts.Add($"s{kvp.Key}|{kvp.Value}");
    }

    var input = string.Join("|", inputParts);
    var translated = this.TranslationService.Translate(input, sourceLang, targetLang);
    if (string.IsNullOrWhiteSpace(translated))
    {
      return;
    }

    var parsed = translated.Split('|');
    for (int i = 0; i < parsed.Length - 1; i += 2)
    {
      var key = parsed[i];
      var value = parsed[i + 1];

      if (key.StartsWith("a") && int.TryParse(key[1..], out var atkIndex))
      {
        this.FilteredAtkValues[atkIndex] = value;
      }
      else if (key.StartsWith("s") && int.TryParse(key[1..], out var strIndex))
      {
        this.FilteredStringArrayData[strIndex] = value;
      }
    }

    var translatedJson = this.SerializeTranslationResult();
    var originalJson = this.SerializeOriginalData();

    var entity = new TGenericEntity();
    entity.SetTranslatedText(translatedJson);

    if (entity is not IMultiTextEntity)
    {
      entity.SetOriginalText(originalJson);
      entity.SetOriginalLang(sourceLang);
    }

    entity.SetTranslationLang(targetLang);
    entity.SetTranslationEngine(this.Config.ChosenTransEngine);
    entity.SetEntityKey(this.AddonName);

    if (entity.GetGameVersion() is null && entity is not IMultiTextEntity)
    {
      entity.SetGameVersion(GetGameVersion());
    }

    InsertEntity<TGenericEntity>(entity);
    PluginLog.Debug($"[{this.AddonName}] Synchronous translation saved to DB.");
  }


  private string SerializeOriginalData()
  {
    var sb = new StringBuilder();
    foreach (var (index, val) in this.FilteredAtkValues)
      sb.Append($"a{index}|{val}|");

    foreach (var (index, val) in this.FilteredStringArrayData)
      sb.Append($"s{index}|{val}|");

    return sb.ToString().TrimEnd('|');
  }

  private string SerializeTranslationResult()
  {
    var combined = new
    {
      atkValues = this.FilteredAtkValues,
      stringArrayData = this.FilteredStringArrayData,
    };

    return JsonConvert.SerializeObject(combined);
  }

  private bool TryLoadTranslationFromDatabase()
  {
    PluginLog.Debug($"[{this.AddonName}] Attempting to load translation from DB...");
    var entity = this.TryFindEntityInDb();
    if (entity is null)
    {
      return false;
    }

    var translated = entity.GetTranslatedText();
    if (string.IsNullOrWhiteSpace(translated))
    {
      return false;
    }

    this.ParseTranslatedData(translated);
    return true;
  }

  private void ParseTranslatedData(string? translatedJson)
  {
    if (string.IsNullOrWhiteSpace(translatedJson))
    {
      return;
    }

    try
    {
      var parsed = JsonConvert.DeserializeObject<CombinedTranslationData>(translatedJson);
      if (parsed == null)
      {
        return;
      }

      if (parsed.atkValues != null)
      {
        this.FilteredAtkValues = parsed.atkValues;
      }

      if (parsed.stringArrayData != null)
      {
        this.FilteredStringArrayData = parsed.stringArrayData;
      }
    }
    catch
    {
      // ignored
    }
  }

  private TGenericEntity? TryFindEntityInDb()
  {
    var original = this.SerializeOriginalData();
    var lang = LangDict[LanguageInt].Code;
    var version = GetGameVersion();

    return FindEntity<TGenericEntity>(e =>
        e.GetOriginalText() == original &&
        e.GetEntityKey() == this.AddonName &&
        e.GetTranslationLang() == lang &&
        (e.GetGameVersion() == null || e.GetGameVersion() == version));
  }

  private bool ContentIsAlreadyTranslated()
  {
    var entity = this.TryFindEntityInDb();
    if (entity is null || string.IsNullOrWhiteSpace(entity.GetTranslatedText()))
    {
      return false;
    }

    try
    {
      var parsed = JsonConvert.DeserializeObject<CombinedTranslationData>(entity.GetTranslatedText());
      if (parsed is null)
      {
        return false;
      }

      var atkTranslated = parsed.atkValues ?? new();
      var strTranslated = parsed.stringArrayData ?? new();

      bool atkMatch = this.FilteredAtkValues.All(kvp =>
          atkTranslated.TryGetValue(kvp.Key, out var val) && val == kvp.Value);

      bool strMatch = this.FilteredStringArrayData.All(kvp =>
          strTranslated.TryGetValue(kvp.Key, out var val) && val == kvp.Value);

      var ismatch = atkMatch && strMatch;
      PluginLog.Debug(
          $"[{this.AddonName}] Content already translated: ATK match: {atkMatch}, StringArray match: {strMatch}, Overall match: {ismatch}");
      return ismatch;
    }
    catch
    {
      return false;
    }
  }

  private unsafe void ExtractAtkValues(AtkUnitBase* addon)
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
      if (span[i].Type is ValueType.String or ValueType.String8 or ValueType.ManagedString)
      {
        var value = MemoryHelper.ReadSeStringAsString(out _, (nint)span[i].String.Value);
        if (!string.IsNullOrWhiteSpace(value) &&
            !value.All(char.IsPunctuation) &&
            !NumericLikePattern.IsMatch(value))
        {
          this.FilteredAtkValues[i] = value;
          this.OriginalAtkValues[i] = value;
        }
      }
    }
  }

  private unsafe void ExtractStringArrayData(AtkStage* atkStage)
  {
    var data = atkStage->GetStringArrayData(this.StringArrayDataType!.Value);
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
  }

  private class CombinedTranslationData
  {
    public Dictionary<int, string>? atkValues { get; set; }
    public Dictionary<int, string>? stringArrayData { get; set; }
  }
}
