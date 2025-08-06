// <copyright file="GenericAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>



using Echoglossian.Cache;

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
  /// Handles loading of translated StringArrayData from the DB.
  /// </summary>
  private readonly StringArrayDataHandler stringArrayDataHandler;

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
  protected readonly bool UseStringArray; // kept for now, may remove later

  /// <summary>
  ///     Stores filtered ATK values for translation.
  /// </summary>
  protected Dictionary<int, string> FilteredAtkValues = new();

  /*  /// <summary>
    ///     Stores filtered string array data for translation.
    /// </summary>
    protected Dictionary<int, string> FilteredStringArrayData = new();*/

  /*  /// <summary>
    ///     Stores the original string array values to restore after translation.
    ///     Uses byte arrays to preserve exact original data.
    /// </summary>
    protected Dictionary<int, byte[]> OriginalStringArrayData = new();*/

  /// <summary>
  ///     Stores original ATK values extracted from the addon for change detection.
  /// </summary>
  protected Dictionary<int, string> OriginalAtkValues = new();

  /// <summary>
  ///  Stores a snapshot of the original ATK values.
  /// </summary>
  private Dictionary<int, string> SnapshotOriginalAtkValues = new();
  /*  /// <summary>
    ///  Stores a snapshot of the original string array data
    /// </summary>
    private Dictionary<int, string> SnapshotOriginalStringArrayData = new();*/



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
    this.stringArrayDataHandler = new StringArrayDataHandler(
      arraysToBlock: ArraysToBlock ?? new(),
      config: this.Config,
      translationService: this.TranslationService);

  }

  /// <summary>
  ///     Gets the registered event handlers for addon events.
  /// </summary>
  /// <returns>A dictionary of addon events and their corresponding delegates.</returns>
  public Dictionary<AddonEvent, AddonEventDelegate> GetEventHandlers()
  {
    PluginLog.Debug($"[{this.AddonName}] Getting event handlers...");
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
    PluginLog.Debug($"[{this.AddonName}] Registering handler for event: {evt}");
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
    PluginLog.Debug($"[{this.AddonName}] Handling PreSetup for {type} and {args.Type}...");
    if (args is not AddonSetupArgs setupArgs || args.AddonName != this.AddonName)
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping PreSetup for {type} - args mismatch.");
      return;
    }
    // ✅ Capture original values before any translation
    // 2
    this.SnapshotOriginalValues();
    // 3
    this.ExtractAndTranslate(type, args);

    // just ignore this for now
    // this.OnAtkValueUpdate(type, args);
  }

  /// <summary>
  ///     Handles translation and application of values during PreRefresh lifecycle.
  /// </summary>
  /// <param name="type">AddonEvent type.</param>
  /// <param name="args">AddonArgs containing refresh context.</param>
  protected void OnPreRefresh(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] Handling PreRefresh for {type} and {args.Type}...");
    if (args is not AddonRefreshArgs refreshArgs || args.AddonName != this.AddonName)
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping PreRefresh for {type} - args mismatch.");
      return;
    }
    // ✅ Capture original values before any translation
    // this.SnapshotOriginalValues();

    // this.ExtractAndTranslate(type, args);
    this.OnAtkValueUpdate(type, args);
  }

  /// <summary>
  ///     Handles translation and application of string array values during PreRequestedUpdate lifecycle.
  /// </summary>
  /// <param name="type">AddonEvent type.</param>
  /// <param name="args">AddonArgs containing update context.</param>
  protected void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] Handling PreRequestedUpdate for {type} and {args.Type}...");

    if (args is not AddonRequestedUpdateArgs requestedUpdateArgs || args.AddonName != this.AddonName)
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping PreRequestedUpdate for {type} - args mismatch.");
      return;
    }
    // ✅ Capture original values before any translation
    // this.SnapshotOriginalValues();
    // this.ExtractAndTranslate(type, args);

    this.OnArrayDataUpdate(type, args);
  }

  /// <summary>
  /// Handles the update of ATK values based on the translated results.
  /// Uses snapshot values to detect whether translation needs to be applied.
  /// </summary>
  /// <param name="type">AddonEvent type.</param>
  /// <param name="args">AddonArgs containing update context.</param>
  private unsafe void OnAtkValueUpdate(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] Handling ATK value update for {type}...");

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

      if (this.SnapshotOriginalAtkValues.TryGetValue(index, out var original) && original == translated)
      {
        PluginLog.Debug($"[{this.AddonName}] Skipping ATK[{index}] — already matches snapshot.");
        continue;
      }

      PluginLog.Debug($"[{this.AddonName}] Applying translated ATK[{index}]: {translated}");
      currentValue.SetManagedString(translated);
    }
  }

  /// <summary>
  /// Applies translated StringArrayData values loaded from the database.
  /// </summary>
  /// <param name="type">The AddonEvent that triggered the update.</param>
  /// <param name="args">The addon arguments containing the array pointer.</param>
  private void OnArrayDataUpdate(AddonEvent type, AddonArgs args)
  {
    PluginLog.Debug($"[{this.AddonName}] Handling StringArrayData update for {type}...");

    if (!this.UseStringArray || this.StringArrayDataType is not { } arrayType)
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping array update — not configured.");
      return;
    }

    if (args is not AddonRequestedUpdateArgs requestedUpdateArgs)
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping array update — invalid args.");
      return;
    }

    var stringArrayData = (StringArrayData**)requestedUpdateArgs.StringArrayData;
    var arrayIndex = this.GetStringArrayIndexForAddon(args.Addon);

    if (arrayIndex is -1)
    {
      PluginLog.Debug($"[{this.AddonName}] No matching string array index found.");
      return;
    }

    var addonArrayData = stringArrayData[arrayIndex];
    var stringArrayDataType = this.StringArrayDataType.Value.ToString();

    var dbData = FindStringArrayDatasByTypeAndLang(stringArrayDataType, LangDict[LanguageInt].Code);

    if (dbData is null)
    {
      PluginLog.Debug($"[{this.AddonName}] No DB data found for type {arrayType}.");
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] Found DB data for type {arrayType} with size {dbData.Count}.");

    for (var i = 0; i < dbData.Count; ++i)
    {
      PluginLog.Debug($"[{this.AddonName}] DB data[{i}]: {dbData[i].TranslatedStrings}");

      if (dbData[i]?.TranslatedStrings is not { Length: > 0 })
      {
        PluginLog.Debug($"[{this.AddonName}] No DB string array data found for type {arrayType}.");
        return;
      }

      var parsed = ParseStringArraySerializedText(dbData[i]?.TranslatedStrings);
      if (parsed.Count == 0)
      {
        PluginLog.Debug($"[{this.AddonName}] Parsed DB data is empty for type {arrayType}.");
        return;
      }

      foreach (var (index, value) in parsed)
      {
        PluginLog.Debug($"[{this.AddonName}] Applying DB-translated string at index {index}: {value}");
        addonArrayData->SetValue(index, value, suppressUpdates: true);
      }

    }

    PluginLog.Debug($"[{this.AddonName}] StringArrayData update completed for {arrayType}.");
  }

  /// <summary>
  ///  Gets the index of the string array associated with the specified addon.
  /// </summary>
  /// <param name="addon">Addon pointer.</param>
  /// <returns>The index of the string array if found; otherwise -1.</returns>
  private int GetStringArrayIndexForAddon(AtkUnitBasePtr addon)
  {
    PluginLog.Debug($"[{this.AddonName}] Getting StringArrayData index for addon {addon.Name}...");
    var arrayHolder = RaptureAtkModule.Instance()->AtkArrayDataHolder;

    for (var index = 0; index < arrayHolder.StringArrayCount; ++index)
    {
      var stringArray = arrayHolder.StringArrays[index];
      if (stringArray is null)
      {
        continue;
      }

      // PluginLog.Debug($"[{this.AddonName}] Checking if addon {addon->Name.ToString()} is subscribed to string array {index}...");

      if (stringArray->SubscribedAddons.Contains((byte)addon.Id))
      {
        PluginLog.Debug($"[{this.AddonName}] Addon {addon.Name} is subscribed to string array {index}.");
        return index;
      }
    }

    return -1;
  }

  /// <summary>
  /// Extracts and translates addon content based on the specified event and
  /// arguments. Performs DB lookup before calling external translation.
  /// Saves translated content into the database. Honors async/sync mode from config.
  /// </summary>
  /// <param name="evt">The type of addon event.</param>
  /// <param name="args">The arguments associated with the addon event.</param>
  private void ExtractAndTranslate(AddonEvent evt, AddonArgs args)
  {
    if (args.AddonName != this.AddonName)
    {
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] ExtractAndTranslate triggered - {evt}");

    var atkStage = AtkStage.Instance();
    var addon = atkStage->RaptureAtkUnitManager->GetAddonByName(this.AddonName);
    if (addon == null)
    {
      PluginLog.Debug($"[{this.AddonName}] Addon not found.");
      return;
    }

    this.FilteredAtkValues.Clear();
    this.OriginalAtkValues.Clear();

    if (this.UseAtkValues)
    {
      this.ExtractAtkValues(addon);
    }

    // Skip string array extraction – handled separately via StringArrayDataHandler
    PluginLog.Debug($"[{this.AddonName}] Skipping StringArrayData extraction — delegated to StringArrayDataHandler.");

    // ✅ Capture true original values before applying anything
    this.SnapshotOriginalAtkValues = new(this.OriginalAtkValues);

    if (this.FilteredAtkValues.Count == 0)
    {
      PluginLog.Debug($"[{this.AddonName}] No ATK values extracted; skipping translation.");
      return;
    }

    if (this.TryLoadTranslationFromDatabase())
    {
      PluginLog.Debug($"[{this.AddonName}] Translation found in DB; skipping.");
      return;
    }

    if (this.ContentIsAlreadyTranslated())
    {
      PluginLog.Debug($"[{this.AddonName}] Skipping translation — snapshot values match filtered values.");

      foreach (var kvp in this.SnapshotOriginalAtkValues)
      {
        if (!this.FilteredAtkValues.TryGetValue(kvp.Key, out var cur) || cur != kvp.Value)
        {
          PluginLog.Debug($"[ATK mismatch] Index {kvp.Key}: expected '{kvp.Value}', found '{cur}'");
        }
      }

      return;
    }

    if (this.Config.EnableAsyncTranslation)
    {
      PluginLog.Debug($"[{this.AddonName}] Launching async translation...");
      Task.Run(() => GenericAddonHandlerHelper.PerformTranslationAndSaveAsync<TGenericEntity>(
          this.AddonName,
          this.FilteredAtkValues,
          stringArray: new(), // no string array in addon context
          this.SnapshotOriginalAtkValues,
          originalArraySnapshot: new(),
          this.Config,
          this.TranslationService
      ));
      return;
    }

    PluginLog.Debug($"[{this.AddonName}] Performing sync translation...");

    var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
    var targetLang = LangDict[LanguageInt].Code;

    var inputParts = new List<string>();
    foreach (var kvp in this.FilteredAtkValues)
    {
      inputParts.Add($"a{kvp.Key}|{kvp.Value}");
    }

    var input = string.Join("|", inputParts);
    var translated = this.TranslationService.Translate(input, sourceLang, targetLang);
    if (string.IsNullOrWhiteSpace(translated))
    {
      PluginLog.Warning($"[{this.AddonName}] Translation returned empty.");
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
    }

    var entity = new TGenericEntity();

    if (entity is IMultiTextEntity multi)
    {
      PluginLog.Debug($"[{this.AddonName}] Saving as IMultiTextEntity");

      var messageEntry = this.FilteredAtkValues.FirstOrDefault(kvp => kvp.Key == 0);
      var senderEntry = this.FilteredAtkValues.FirstOrDefault(kvp => kvp.Key == 1);

      var originalMessage = this.OriginalAtkValues.TryGetValue(messageEntry.Key, out var origMsg)
          ? origMsg : messageEntry.Value;
      var originalSender = this.OriginalAtkValues.TryGetValue(senderEntry.Key, out var origSender)
          ? origSender : senderEntry.Value;

      multi.SetOriginalSecondaryText(originalMessage);
      multi.SetOriginalText(originalSender);
      multi.SetOriginalLang(sourceLang);

      multi.SetTranslatedSecondaryText(messageEntry.Value);
      multi.SetTranslatedText(senderEntry.Value);
      multi.SetTranslationLang(targetLang);
      multi.SetTranslationEngine(this.Config.ChosenTransEngine);
    }
    else
    {
      PluginLog.Debug($"[{this.AddonName}] Saving as IGenericEntity");

      var payload = new
      {
        atkValues = this.FilteredAtkValues.Count > 0 ? this.FilteredAtkValues : null,
      };

      var originalPayload = new
      {
        atkValues = this.SnapshotOriginalAtkValues.Count > 0 ? this.SnapshotOriginalAtkValues : null,
      };

      var translatedJson = JsonConvert.SerializeObject(payload);
      var originalJson = JsonConvert.SerializeObject(originalPayload);

      entity.SetOriginalText(originalJson);
      entity.SetTranslatedText(translatedJson);
      entity.SetOriginalLang(sourceLang);
      entity.SetTranslationLang(targetLang);
      entity.SetTranslationEngine(this.Config.ChosenTransEngine);
    }

    entity.SetEntityKey(this.AddonName);

    if (entity.GetGameVersion() is null && entity is not IMultiTextEntity)
    {
      entity.SetGameVersion(GetGameVersion());
    }

    if (this.FilteredAtkValues.Count == 0)
    {
      PluginLog.Debug($"[{this.AddonName}] No translated values; skipping DB insert.");
      return;
    }

    if (typeof(TGenericEntity) == typeof(GameWindow) && entity is GameWindow gw)
    {
      PluginLog.Debug($"[{this.AddonName}] Saving GameWindow to DB...");
      InsertGameWindow(gw);
      GameWindowCacheManager.Update(gw);
      PluginLog.Debug($"[{this.AddonName}] GameWindow saved and cached.");
    }
    else
    {
      InsertEntity(entity);
      PluginLog.Debug($"[{this.AddonName}] Translation saved to DB.");
    }
  }



  /// <summary>
  /// Extracts ATK values from the addon and populates the filtered and original dictionaries.
  /// </summary>
  /// <param name="addon">The addon's ATK unit base structure.</param>
  private unsafe void ExtractAtkValues(AtkUnitBase* addon)
  {
    PluginLog.Debug($"[{this.AddonName}] Extracting ATK values...");
    var atkValues = addon->AtkValues;
    var count = addon->AtkValuesCount;
    if (atkValues == null || count <= 0)
    {
      PluginLog.Debug($"[{this.AddonName}] No ATK values found or count is zero.");
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

  /// <summary>
  /// Captures the current extracted ATK values as immutable snapshots
  /// to preserve their original (pre-translation) state for DB and cache comparison.
  /// </summary>
  private void SnapshotOriginalValues()
  {
    try
    {
      this.SnapshotOriginalAtkValues = new(this.OriginalAtkValues);
      PluginLog.Debug($"[{this.AddonName}] Snapshot of original ATK values captured.");
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{this.AddonName}] Error during SnapshotOriginalValues: {ex}");
    }
  }


  /// <summary>
  /// Serializes the original ATK values for cache/DB matching.
  /// </summary>
  /// <returns>Returns the serialized data string.</returns>
  private string SerializeOriginalData()
  {
    PluginLog.Debug($"[{this.AddonName}] Serializing original ATK data...");
    var sb = new StringBuilder();

    foreach (var (index, val) in this.FilteredAtkValues)
      sb.Append($"a{index}|{val}|");

    return sb.ToString().TrimEnd('|');
  }


  /// <summary>
  /// Serializes the translation isMatch into a JSON string
  /// </summary>
  /// <returns>Returns the serialized translation isMatch.</returns>
  private string SerializeTranslationResult()
  {
    PluginLog.Debug($"[{this.AddonName}] Serializing translation isMatch to JSON...");
    var combined = new
    {
      atkValues = this.FilteredAtkValues,
    };

    return JsonConvert.SerializeObject(combined);
  }

  /// <summary>
  /// Attempts to load a translation from the database for the current addon.
  /// </summary>
  /// <remarks>This method tries to find the relevant entity in the database and retrieve its translated text.
  /// If the entity is not found or the translated text is empty or whitespace, the method returns <see
  /// langword="false"/>.</remarks>
  /// <returns><see langword="true"/> if the translation is successfully loaded and parsed; otherwise, <see langword="false"/>.</returns>
  private bool TryLoadTranslationFromDatabase() // TODO: revalidate the generic entity logic because data is not being properly saved in the Database and retrieval is not working as expected
  {
    PluginLog.Debug($"[{this.AddonName}] Attempting to load translation from DB...");
    var entity = this.TryFindEntityInDb();
    if (entity is null)
    {
      PluginLog.Debug($"[{this.AddonName}] No matching entity found in DB.");
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

  /// <summary>
  /// Parses the translated JSON data and populates the filtered ATK dictionary.
  /// </summary>
  /// <param name="translatedJson">Translated JSON data</param>
  private void ParseTranslatedData(string? translatedJson)
  {
    PluginLog.Debug($"[{this.AddonName}] Parsing translated data from JSON...");
    if (string.IsNullOrWhiteSpace(translatedJson))
    {
      PluginLog.Debug($"[{this.AddonName}] No translated data found in JSON.");
      return;
    }

    try
    {
      var parsed = JsonConvert.DeserializeObject<CombinedTranslationData>(translatedJson);
      if (parsed?.AtkValues != null)
      {
        this.FilteredAtkValues = parsed.AtkValues;
      }
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{this.AddonName}] Error parsing translated JSON: {ex}");
    }
  }


  /// <summary>
  ///     Attempts to find an entity in the database that matches the preserved snapshot of original data,
  ///     addon name, translation language, and game version. Uses in-memory GameWindow cache when applicable.
  /// </summary>
  /// <returns>
  ///     The entity of type <typeparamref name="TGenericEntity"/> if a matching entity is found;
  ///     otherwise, <see langword="null" />.
  /// </returns>
  private TGenericEntity? TryFindEntityInDb()
  {
    try
    {
      PluginLog.Debug($"[{this.AddonName}] Attempting to find entity in DB using snapshots...");

      var originalJson = JsonConvert.SerializeObject(new
      {
        atkValues = this.SnapshotOriginalAtkValues.Count > 0 ? this.SnapshotOriginalAtkValues : null,
      });

      var lang = LangDict[LanguageInt].Code;
      var version = GetGameVersion();

      if (typeof(TGenericEntity) == typeof(GameWindow))
      {
        var match = GameWindowCacheManager.TryFindMatch(
            addonName: this.AddonName,
            lang: lang,
            engine: this.Config.ChosenTransEngine,
            version: version,
            originalJson: originalJson);

        if (match is not null)
        {
          PluginLog.Debug($"[{this.AddonName}] Found GameWindow match in cache.");
          return match as TGenericEntity;
        }
      }

      var dbEntity = FindEntity<TGenericEntity>(e =>
          e.GetOriginalText() == originalJson &&
          e.GetEntityKey() == this.AddonName &&
          e.GetTranslationLang() == lang &&
          (e.GetGameVersion() == null || e.GetGameVersion() == version));

      PluginLog.Debug($"[{this.AddonName}] Entity found in DB: {dbEntity != null}");

      if (typeof(TGenericEntity) == typeof(GameWindow) && dbEntity is GameWindow gw)
      {
        PluginLog.Debug($"[{this.AddonName}] Updating GameWindow cache...");
        GameWindowCacheManager.Update(gw);
      }

      return dbEntity;
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{this.AddonName}] Error finding entity in DB: {ex}");
      return null;
    }
  }

  /// <summary>
  /// Determines if the currently extracted ATK values match the preserved original snapshot.
  /// Avoids re-translating or re-applying if the values are already translated.
  /// </summary>
  /// <returns><see langword="true"/> if the content was already translated; otherwise, <see langword="false"/>.</returns>
  private bool ContentIsAlreadyTranslated()
  {
    try
    {
      PluginLog.Debug($"[{this.AddonName}] Checking if ATK content is already translated...");

      bool atkMatch = this.SnapshotOriginalAtkValues.All(kvp =>
          this.FilteredAtkValues.TryGetValue(kvp.Key, out var val) && val == kvp.Value);

      if (!atkMatch)
      {
        foreach (var kvp in this.SnapshotOriginalAtkValues)
        {
          if (!this.FilteredAtkValues.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
          {
            PluginLog.Debug($"[ATK mismatch] Index {kvp.Key}: expected '{kvp.Value}', found '{val}'");
          }
        }
      }

      return atkMatch;
    }
    catch (Exception ex)
    {
      PluginLog.Error($"[{this.AddonName}] Error in ContentIsAlreadyTranslated: {ex}");
      return false;
    }
  }

}

/// <summary>
/// Represents a collection of translation data, combining attack values and string array data.
/// </summary>
/// <remarks>This class holds two dictionaries that map integer keys to string values, allowing for the
/// storage and retrieval of translation-related data. The dictionaries can be null, indicating the absence of
/// data.</remarks>
public class CombinedTranslationData
{
  /// <summary>
  /// Dictionary mapping ATK value indices to their translated strings.
  /// </summary>
  public Dictionary<int, string>? AtkValues { get; set; }

  /// <summary>
  /// Dictionary mapping string array indices to their translated strings.
  /// </summary>
  public Dictionary<int, string>? StringArrayData { get; set; }
}
