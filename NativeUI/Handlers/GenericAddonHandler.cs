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
  ///     Loads translated content from the database into FilteredAtkValues and FilteredStringArrayData.
  /// </summary>
  private void LoadFromDatabaseIfNeeded() // unused now
  {
    if (this.FilteredAtkValues.Count > 0 || this.FilteredStringArrayData.Count > 0)
    {
      return;
    }

    var original = this.SerializeOriginalData();
    var lang = LangDict[this.Config.Lang].Code;
    var version = GetGameVersion();

    var entity = FindEntity<TGenericEntity>(e =>
      e.GetOriginalText() == original &&
      e.GetEntityKey() == this.AddonName &&
      e.GetTranslationLang() == lang &&
      (e.GetGameVersion() == null || e.GetGameVersion() == version));

    if (entity is null)
    {
      return;
    }

    var json = entity.GetTranslatedText();
    if (string.IsNullOrWhiteSpace(json))
    {
      return;
    }

    try
    {
      var parsed = JsonConvert.DeserializeObject<CombinedTranslationData>(json);
      if (parsed is null)
      {
        return;
      }

      this.FilteredAtkValues = parsed.atkValues ?? new();
      this.FilteredStringArrayData = parsed.stringArrayData ?? new();
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{this.AddonName}] Failed to load translation from DB: {ex.Message}");
    }
  }

  /// <summary>
  ///     Applies ATK translations before the UI is set up.
  /// </summary>
  protected void OnPreSetup(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] OnPreSetup called");
    if (args is not AddonSetupArgs setupArgs || args.AddonName != this.AddonName)
    {
      PluginLog.Debug($"[{this.AddonName}] OnPreSetup - args.AddonName mismatch");
      return;
    }

    if (!this.UseAtkValues || !this.UseStringArray)
    {
      PluginLog.Debug($"[{this.AddonName}] OnPreSetup - UseAtkValues or UseStringArray is false");
      return;
    }

    this.OnAtkValueUpdate(type, args);
  }

  private unsafe void OnAtkValueUpdate(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug("Refreshing Addon AtkValues");

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
      switch (currentValue.Type)
      {
        case ValueType.ManagedString:
        case ValueType.String:
        case ValueType.String8:
          /* if (this.FilteredAtkValues.TryGetValue(index, out var value))
           {
             PluginLog.Debug($"[{this.AddonName}] Setting AtkValue {index} to '{value}'");
             currentValue.SetManagedString(value);
             this.OriginalAtkValues[index] = value;
           }
           else
           {
             PluginLog.Debug($"[{this.AddonName}] No translation found for AtkValue {index}");
           }*/
          currentValue.SetManagedString("coc boc gog"); // to test the implementation and it works to set the string i passed
          break;
      }
    }
  }

  /// <summary>
  ///     Applies ATK translations on refresh (e.g., tab switch).
  /// </summary>
  protected void OnPreRefresh(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] OnPreRefresh called");
    if (args is not AddonRefreshArgs refreshArgs || args.AddonName != this.AddonName)
    {
      PluginLog.Debug($"[{this.AddonName}] OnPreRefresh - args.AddonName mismatch");
      return;
    }

    if (!this.UseAtkValues || !this.UseStringArray)
    {
      PluginLog.Debug($"[{this.AddonName}] OnPreRefresh - UseAtkValues or UseStringArray is false");
      return;
    }

    this.OnPreSetup(type, args);
  }

  /// <summary>
  ///     Applies StringArray translations immediately before update.
  /// </summary>
  protected void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] OnPreRequestedUpdate called");
    if (args is not AddonRequestedUpdateArgs requestedUpdateArgs || args.AddonName != this.AddonName)
    {
      PluginLog.Debug($"[{this.AddonName}] OnPreRequestedUpdate - args.AddonName mismatch");
      return;
    }

    if (!this.UseStringArray || !this.StringArrayDataType.HasValue)
    {
      PluginLog.Debug($"[{this.AddonName}] OnPreRequestedUpdate - UseStringArray is false or StringArrayDataType is null");
      return;
    }
    this.OnArrayDataUpdate(type, args);
  }

  private unsafe void OnArrayDataUpdate(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug("Refreshing Addon StringArrayData");

    if (args is AddonRequestedUpdateArgs requestedUpdateArgs)
    {
      var stringArrayData = (StringArrayData**)requestedUpdateArgs.StringArrayData;
      var arrayIndex = this.GetStringArrayIndexForAddon((AtkUnitBase*)args.Addon);

      if (arrayIndex is -1) return;

      var addonArrayData = stringArrayData[arrayIndex];

      for (var index = 0; index < addonArrayData->Size; ++index)
      {
        ref var stringValue = ref addonArrayData->StringArray[index];
        if (stringValue is not null)
        {
          addonArrayData->SetValue(index, "Brasil!", suppressUpdates: true);
          /*if (this.FilteredStringArrayData.TryGetValue(index, out var value))
{
  addonArrayData->SetValue(index, value, suppressUpdates: true);

}*/
        }
      }
    }
  }

  private unsafe int GetStringArrayIndexForAddon(AtkUnitBase* addon)
  {
    PluginLog.Debug($"[{this.AddonName}] GetStringArrayIndexForAddon called");
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
  ///     Applies filtered translations to ATK values.
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
    foreach (var (index, value) in this.FilteredAtkValues)
    {
      if (index < 0 || index >= count)
      {
        continue;
      }

      var atk = span[index];
      if (atk.Type is not (ValueType.String or ValueType.String8 or ValueType.ManagedString))
      {
        continue;
      }

      atk.SetManagedString(value);
    }
  }

  /// <summary>
  ///     Converts original ATK and string array values into a single string for DB matching.
  /// </summary>
  private string SerializeOriginalData()
  {
    var sb = new StringBuilder();
    foreach (var (index, val) in this.FilteredAtkValues)
      sb.Append($"a{index}|{val}|");

    foreach (var (index, val) in this.FilteredStringArrayData)
      sb.Append($"s{index}|{val}|");

    return sb.ToString().TrimEnd('|');
  }

  /// <summary>
  ///     Translates values and inserts them into DB using chunked async helper.
  /// </summary>
  protected void BeginAsyncTranslation()  // unused now
  {
    Task.Run(() => GenericAddonHandlerHelper.PerformTranslationAndSaveAsync<TGenericEntity>(
      this.AddonName,
      this.FilteredAtkValues,
      this.FilteredStringArrayData,
      this.Config,
      this.TranslationService,
      typeof(TGenericEntity)));
  }


  /// <summary>
  ///     Extracts addon data, checks for DB translation, and triggers async translation if needed.
  ///     Should be invoked on a safe lifecycle event like PreRefresh.
  /// </summary>
  /// <param name="evt">The addon event triggering this logic.</param>
  /// <param name="args">The associated addon arguments.</param>
  protected void ExtractAndTranslateIfNeeded(AddonEvent evt, AddonArgs args)
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslateIfNeeded triggered at {evt}");

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
    if (addon == null || !addon->IsVisible)
    {
      PluginLog.Debug($"[{this.AddonName}] Addon not visible or not found");
      return;
    }

    this.FilteredAtkValues.Clear();
    this.FilteredStringArrayData.Clear();
    this.OriginalAtkValues.Clear();
    this.OriginalStringArrayData.Clear();

    if (this.UseAtkValues)
    {
      this.ExtractAtkValues(addon);
    }

    if (this.UseStringArray && this.StringArrayDataType.HasValue)
    {
      this.ExtractStringArrayData(atkStage);
    }

    if (this.FilteredAtkValues.Count == 0 && this.FilteredStringArrayData.Count == 0)
    {
      PluginLog.Debug($"[{this.AddonName}] Nothing extracted, skipping translation.");
      return;
    }

    if (this.TryLoadTranslationFromDatabase())
    {
      PluginLog.Debug($"[{this.AddonName}] Translation loaded from DB; skipping translation.");
      return;
    }

    if (this.ContentIsAlreadyTranslated())
    {
      PluginLog.Debug($"[{this.AddonName}] Extracted content already translated; skipping.");
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] Scheduling async translation...");

    Task.Run(() => GenericAddonHandlerHelper.PerformTranslationAndSaveAsync<TGenericEntity>(
      this.AddonName,
      this.FilteredAtkValues,
      this.FilteredStringArrayData,
      this.Config,
      this.TranslationService,
      typeof(TGenericEntity)));
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
  /// <param name="addon">Pointer to the target native Game UI addon.</param>
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

    PluginLog.Debug($"[{this.AddonName}] Extracted {this.FilteredAtkValues.Count} AtkValues.");
  }

  /// <summary>
  ///     Extracts and filters string array data from the ATK stage.
  /// </summary>
  /// <param name="atkStage"></param>
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
    var entity = this.TryFindEntityInDb();

    if (entity is null)
    {
      PluginLog.Debug($"[{this.AddonName}] No cached translation found.");
      return false;
    }

    var translated = entity.GetTranslatedText();
    if (string.IsNullOrWhiteSpace(translated))
    {
      PluginLog.Warning(
          $"[{this.AddonName}] Found entity but translated text was null or empty.");
      return false;
    }

    this.ParseTranslatedData(translated);
    PluginLog.Debug($"[{this.AddonName}] Loaded translated data from DB.");
    return true;
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
  ///     Serializes a combined translation result into JSON.
  /// </summary>
  private string SerializeTranslationResult()
  {
    var combined = new
    {
      atkValues = this.FilteredAtkValues,
      stringArrayData = this.FilteredStringArrayData,
    };

    return JsonConvert.SerializeObject(combined);
  }

  /// <summary>
  ///     Attempts to find a translated entity in the database based on the original
  ///     serialized content.
  /// </summary>
  /// <returns>
  ///     The matching <see cref="TGenericEntity" /> from the database, or null
  ///     if not found.
  /// </returns>
  private TGenericEntity? TryFindEntityInDb()
  {
    var original = this.SerializeOriginalData();
    var lang = LangDict[LanguageInt].Code;
    var version = GetGameVersion();

    return FindEntity<TGenericEntity>(e =>
        e.GetOriginalText() == original &&
        e.GetEntityKey() == this.AddonName &&
        e.GetTranslationLang() == lang &&

        // Only compare GameVersion if it is relevant to the entity
        (e.GetGameVersion() == null || e.GetGameVersion() == version));
  }

  /// <summary>
  ///     Checks whether the currently extracted data is already translated.
  /// </summary>
  /// <returns>True if both ATK and StringArray values are already translated; otherwise, false.</returns>
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

      if (atkMatch && strMatch)
      {
        PluginLog.Debug($"[{this.AddonName}] Content is already translated. Skipping translation.");
        return true;
      }

      return false;
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{this.AddonName}] Error checking translated state: {ex.Message}");
      return false;
    }
  }

  /// <summary>
  ///     Extracts and translates addon content based on the specified event and
  ///     arguments. Performs DB lookup before calling external translation.
  /// </summary>
  /// <param name="evt">The type of addon event.</param>
  /// <param name="args">The arguments associated with the addon event.</param>
  protected void ExtractAndTranslate(AddonEvent evt, AddonArgs args)
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate - {evt}");

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
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

    if (this.FilteredAtkValues.Count == 0 && this.FilteredStringArrayData.Count == 0)
    {
      PluginLog.Debug($"[{this.AddonName}] Nothing to translate.");
      return;
    }

    // If all extracted values match the originals, skip
    var allAlreadyTranslated = true;
    foreach (var (i, value) in this.FilteredAtkValues)
    {
      var originalValue = this.OriginalStringArrayData.TryGetValue(i, out var bytes);
      var extractedValue = new ReadOnlySeStringSpan(bytes).ExtractText();

      PluginLog.Debug(
          $"[{this.AddonName}] Checking ATK value {i}: Original: {originalValue}, Extracted: {extractedValue}, Current: {value}");
      if (!originalValue || value == extractedValue) // TODO: check if this is ok
      {
        allAlreadyTranslated = false;
        PluginLog.Debug($"[{this.AddonName}] ATK value {i} is not translated.");
        break;
      }
    }

    if (allAlreadyTranslated)
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping translation - content already translated.");
      return;
    }

    if (this.TryLoadTranslationFromDatabase())
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping translation; loaded from DB.");
      return;
    }

    if (this.ContentIsAlreadyTranslated())
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping translation; content is already translated.");
      return;
    }


    var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
    var targetLang = LangDict[LanguageInt].Code;

    // Build translation input using raw keys (e.g., a0|value|s1|value)
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
    PluginLog.Debug($"[{this.AddonName}] Sending input to translation engine: {input}");

    var translated = this.TranslationService.Translate(input, sourceLang, targetLang);
    if (string.IsNullOrWhiteSpace(translated))
    {
      PluginLog.Warning($"[{this.AddonName}] Translation service returned empty result.");
      return;
    }

    // Parse response in a0|translated|s1|translated format
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

    // Save translation result
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
    PluginLog.Debug($"[{this.AddonName}] Translation saved to DB.");
  }

  /// <summary>
  ///     Extracts and translates addon content asynchronously. Performs DB lookup before translation.
  ///     Uses GenericAddonHandlerHelper for chunked translation and DB save.
  /// </summary>
  /// <param name="evt"> The type of addon event.</param>
  /// <param name="args">The addon arguments.</param>
  protected void ExtractAndTranslateAsync(AddonEvent evt, AddonArgs args)  // unused now
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslateAsync");

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
    if (addon == null || !addon->IsVisible)
    {
      return;
    }

    // Reset
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

    if (this.FilteredAtkValues.Count == 0 && this.FilteredStringArrayData.Count == 0)
    {
      PluginLog.Debug($"[{this.AddonName}] Nothing to translate.");
      return;
    }

    // Check if the current values already match the originals
    var alreadyTranslated = true;

    foreach (var (index, value) in this.FilteredStringArrayData)
    {
      if (!this.OriginalStringArrayData.TryGetValue(index, out var originalBytes) ||
          value == new ReadOnlySeStringSpan(originalBytes).ExtractText())
      {
        continue;
      }

      // Detected mismatch, so this needs translation
      alreadyTranslated = false;
      break;
    }

    if (alreadyTranslated)
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping async translation - content already translated.");
      return;
    }

    if (this.TryLoadTranslationFromDatabase())
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping translation; loaded from DB.");
      return;
    }

    if (this.ContentIsAlreadyTranslated())
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping async translation; content is already translated.");
      return;
    }


    // Begin background translation
    Task.Run(() => GenericAddonHandlerHelper.PerformTranslationAndSaveAsync<TGenericEntity>(
      this.AddonName,
      this.FilteredAtkValues,
      this.FilteredStringArrayData,
      this.Config,
      this.TranslationService,
      typeof(TGenericEntity)));
  }

  /// <summary>
  ///     Applies translated content to the addon based on the specified event and
  ///     arguments. Loads translated data from DB if available.
  /// </summary>
  /// <param name="type">The type of addon event.</param>
  /// <param name="args">The arguments associated with the addon event.</param>
  protected void ApplyTranslated(AddonEvent type, AddonArgs args)
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
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
    if (addon == null || !addon->IsVisible)
    {
      PluginLog.Debug(
          $"[{this.AddonName}] ApplyTranslated - Addon not found or not visible.");
      return;
    }

    // Load translation if not yet populated
    if (this.FilteredAtkValues.Count == 0 && this.FilteredStringArrayData.Count == 0)
    {
      if (!this.TryLoadTranslationFromDatabase())
      {
        PluginLog.Debug($"[{this.AddonName}] No DB match found for ApplyTranslated.");
        return;
      }
    }

    // Only apply if the current in-game values are not already translated
    bool shouldApplyAtk = this.UseAtkValues && ShouldApplyAtkValues(addon);
    bool shouldApplyStr = this.UseStringArray && this.StringArrayDataType.HasValue && ShouldApplyStringArray(atkStage);

    switch (type)
    {
      case AddonEvent.PreSetup:
      case AddonEvent.PreRefresh:
        if (shouldApplyAtk)
        {
          this.ApplyTranslatedAtkValues(addon);
        }

        if (shouldApplyStr)
        {
          this.ApplyTranslatedStringArray(atkStage);
        }

        break;

      case AddonEvent.PreRequestedUpdate:
        if (shouldApplyAtk)
        {
          this.ApplyTranslatedAtkValues(addon);
        }

        if (shouldApplyStr)
        {
          this.ApplyTranslatedStringArray(atkStage);
        }

        break;

      case AddonEvent.PostRequestedUpdate:
        if (this.UseStringArray && this.StringArrayDataType.HasValue)
        {
          this.RestoreOriginalStringArray(atkStage);
        }

        break;

      default:
        PluginLog.Verbose($"[{this.AddonName}] ApplyTranslated skipped for event: {type}");
        break;
    }

    /// <summary>
    /// Determines whether ATK values need reapplying.
    /// </summary>
    bool ShouldApplyAtkValues(AtkUnitBase* addon)
    {
      var atkValues = addon->AtkValues;
      var count = addon->AtkValuesCount;
      if (atkValues == null || count <= 0)
      {
        return false;
      }

      var span = new Span<AtkValue>(atkValues, count);
      foreach (var (i, expected) in this.FilteredAtkValues)
      {
        if (i < 0 || i >= count)
        {
          continue;
        }

        var atk = span[i];
        if (atk.Type is not (ValueType.String or ValueType.String8 or ValueType.ManagedString))
        {
          continue;
        }

        var current = MemoryHelper.ReadSeStringAsString(out _, (nint)atk.String.Value);
        if (!string.Equals(current, expected, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }

    /// <summary>
    /// Determines whether string array data needs reapplying.
    /// </summary>
    bool ShouldApplyStringArray(AtkStage* atkStage)
    {
      var data = atkStage->GetStringArrayData(this.StringArrayDataType!.Value);
      if (data == null)
      {
        return false;
      }

      for (var i = 0; i < data->Size; i++)
      {
        if (!this.FilteredStringArrayData.TryGetValue(i, out var expected))
        {
          continue;
        }

        var span = new ReadOnlySeStringSpan(data->StringArray[i]);
        var current = span.ExtractText();
        if (!string.Equals(current, expected, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
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

            /* PluginLog.Debug(
                 $"[{this.AddonName}] Applied translation to index {index}: '{translated}'");*/
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

            /* PluginLog.Debug(
                 $"[{this.AddonName}] Restored original data to index {index} ({originalBytes.Length} bytes)");*/
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
  ///     Structure used to deserialize combined translation data from the DB.
  /// </summary>
  private class CombinedTranslationData
  {
    public Dictionary<int, string>? atkValues { get; set; }
    public Dictionary<int, string>? stringArrayData { get; set; }
  }
}