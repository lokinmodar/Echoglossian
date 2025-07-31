// <copyright file="StringArrayDataHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Lumina.Text.ReadOnly;

namespace Echoglossian.NativeUI.Handlers
{
  /// <summary>
  ///  Class to handle StringArrayData for the game.
  /// </summary>
  public class StringArrayDataHandler
  {
    /// <summary>
    ///     Gets the configuration settings for the addon handler.
    /// </summary>
    protected readonly Config Config;

    /// <summary>
    /// Translation service used for translating string array data.
    /// </summary>
    protected readonly TranslationService TranslationService;

    /// <summary>
    /// Stores the original string array data extracted from the game.
    /// </summary>
    private readonly List<StringArrayDataEntry> OriginalStringArrayDataBank = [];

    /// <summary>
    ///     Stores filtered string array data for translation, as a list of dictionaries.
    /// </summary>
    private readonly List<FilteredStringArrayDataEntry> FilteredStringArrayDataBank = [];

    /// <summary>
    /// List of prepared original string array data, paired with their types.
    /// </summary>
    private readonly List<(StringArrayType Type, string PreparedString)> PreparedStringArrayDataBank = [];

    /// <summary>
    /// List of translated string array data, paired with their types.
    /// </summary>
    private readonly List<(StringArrayType Type, string TranslatedPreparedString)> TranslatedPreparedStringArrayDataBank = [];

    /// <summary>
    ///  List of Arrays to avoid extraction and translations,
    /// </summary>
    private List<string> arraysToBlock = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StringArrayDataHandler"/> class.
    /// </summary>
    /// <param name="arraysToBlock">The arrays to block.</param>
    /// <param name="config">The configuration settings.</param>
    /// <param name="translationService">The translation service.</param>
    public StringArrayDataHandler(List<string> arraysToBlock, Config config, TranslationService translationService)
    {
      this.arraysToBlock = arraysToBlock ?? [];
      this.Config = config;
      this.TranslationService = translationService;

      PluginLog.Debug($"[StringArrayDataHandler ctor] Initializing StringArrayDataHandler with blocked arrays: {string.Join(", ", this.arraysToBlock)}");
    }

    // TODO: bring here the logics related to StringArrayData handling from GenericAddonHandler and GenericAddonHandlerHelper

    /// <summary>
    /// Loads and translates StringArrayDatas from the ATK Stage.
    /// </summary>
    public unsafe void LoadAndTranslateStringArrayDatas()
    {
      PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Starting StringArrayDatas extraction and translation...");

      try
      {
        var atkStage = AtkStage.Instance();
        if (atkStage == null)
        {
          PluginLog.Error($"[LoadAndTranslateStringArrayDatas] Failed to get ATK Stage instance.");
          return;
        }

        var stirngArraysCount = atkStage->AtkArrayDataHolder->StringArrayCount;
        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Found {stirngArraysCount} StringArrayDatas in ATK Stage.");
        if (stirngArraysCount <= 0)
        {
          PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No StringArrayDatas found in ATK Stage.");
          return;
        }

        var stringArrayDataTypesAvailable = Enum.GetValues<StringArrayType>();

        // PluginLog.Debug($"[*****] Available StringArrayData types: {string.Join(", ", stringArrayDataTypesAvailable)}");

        // Filter out arrays to block
        stringArrayDataTypesAvailable = stringArrayDataTypesAvailable
            .Where(type => !this.arraysToBlock.Contains(type.ToString()))
            .ToArray();

        if (stringArrayDataTypesAvailable.Length == 0)
        {
          PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No StringArrayDatas to extract after filtering.");
          return;
        }

        for (var i = 0; i < stringArrayDataTypesAvailable.Length; i++)
        {
          try
          {
            var type = stringArrayDataTypesAvailable[i];
            var stringArrayData = atkStage->GetStringArrayData(type);
            var stringArray = stringArrayData->StringArray;

            var arraySize = stringArrayData->Size;

            if (stringArray == null || arraySize <= 0)
            {
              PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] StringArrayData of type {type} has no StringArray or its size is 0, skipping.");
              continue;
            }

            PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Extracting StringArrayData of type {type} with size {arraySize}...");

            var originalDict = new Dictionary<int, byte[]>();
            var filteredDict = new Dictionary<int, string>();

            for (var j = 0; j < arraySize; j++)
            {
              var span = new ReadOnlySeStringSpan(stringArray[j]);
              var text = span.ExtractText();

              if (!string.IsNullOrWhiteSpace(text) &&
                  !text.All(char.IsPunctuation) &&
                  !NumericLikePattern.IsMatch(text))
              {
                /*PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Extracted {type}[{j}] = '{text}'");*/
                originalDict[j] = span.Data.ToArray();
                filteredDict[j] = text;
              }
            }

            if (originalDict.Count > 0)
            {
              this.OriginalStringArrayDataBank.Add(new StringArrayDataEntry
              {
                Type = type,
                Entries = originalDict,
              });

              this.FilteredStringArrayDataBank.Add(new FilteredStringArrayDataEntry
              {
                Type = type,
                Entries = filteredDict,
              });
            }

            // Call the DB to check if the data is already translated and load the translated data from the database if available.
            var existingDbData = this.FindStringArrayData(type);

            if (existingDbData != null)
            {
              PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Found existing StringArrayData in DB for type {type}.");
              var translatedStrings = existingDbData.TranslatedStrings;

              if (!string.IsNullOrEmpty(translatedStrings))
              {
                PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Using existing translation for {type}: {translatedStrings}");

                // If the content is already translated, skip translation and apply values to the game.
                if (this.IsContentAlreadyTranslated(type, translatedStrings))
                {
                  PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Content for {type} is already translated, skipping translation.");
                  continue;
                }

                // apply translations to the game
                PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Applying existing translation for {type} to the game.");

                // Split the translated strings into individual entries
                var translationsDictionary = ParseStringArraySerializedText(existingDbData?.TranslatedStrings);

                for (var index = 0; index < translationsDictionary?.Count; index++)
                {
                  var translatedValue = translationsDictionary[index];
                  if (translatedValue is null)
                  {
                    PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No translated value found for {type}[{index}], skipping.");
                    continue;
                  }

                  // Apply the translated value to the game
                  PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Applying translated value for {type}[{index}]: {translatedValue}");
                  stringArrayData->SetValue(index, translatedValue, suppressUpdates: true);
                }
              }
            }
          }
          catch (Exception ex)
          {
            PluginLog.Error($"[LoadAndTranslateStringArrayDatas] Error extracting StringArrayData of type {stringArrayDataTypesAvailable[i]}: {ex}");
          }
        }

        // if the content is not already translated, prepare it for translation
        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Extracted {this.OriginalStringArrayDataBank.Count} StringArrayDatas.");
        if (this.OriginalStringArrayDataBank.Count == 0)
        {
          PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No StringArrayDatas extracted, skipping translation.");
          return;
        }

        var preparedData = this.PrepareStringArrayDataBankForTranslation();

        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Prepared {preparedData.Count} StringArrayDatas for translation.");

        this.PreparedStringArrayDataBank.AddRange(preparedData);

        if (preparedData.Count == 0)
        {
          PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No prepared StringArrayDatas to translate.");
          return;
        }

        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Prepared {preparedData.Count} StringArrayDatas for translation.");

        var translations = this.TranslateData(preparedData);

        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Is already translated? {translations.IsTranslationFinished}");

        for (var index = 0; index < translations.Item2.Count; index++)
        {
          var (type, translatedString) = translations.Item2[index];
          var translationsDictionary = ParseStringArraySerializedText(translatedString);
          if (translationsDictionary == null || translationsDictionary.Count == 0)
          {
            PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No translations found for {type}, skipping.");
            continue;
          }
          // read the current stringarraydata from the game

          var currentStringArrayData = atkStage->GetStringArrayData(type);

          PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Processing translation for {type}: {translatedString}");

          foreach (var (idx, value) in translationsDictionary)
          {
            PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Applying translated value for {type}[{idx}]: {value}");
            // apply the translated value to the game
            currentStringArrayData->SetValue(idx, value, suppressUpdates: true);
          }

          // Save or update the translated StringArrayData in the database
          if (!this.SaveOrUpdateSingleStringArrayData(type, translatedString))
          {
            PluginLog.Error($"[LoadAndTranslateStringArrayDatas] Failed to save or update StringArrayData of type {type}.");
          }
          else
          {
            PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Successfully saved or updated StringArrayData of type {type}.");
          }
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[LoadAndTranslateStringArrayDatas] Error during StringArrayDatas extraction and translation: {ex}");
        return;
      }
    }

    private bool SaveOrUpdateStringArrayDatas(List<(StringArrayType Type, string TranslatedString)> translatedData)
    {
      try
      {
        PluginLog.Debug($"[SaveOrUpdateStringArrayDatas] Saving or updating StringArrayDatas...");
        foreach (var (type, translatedString) in translatedData)
        {
          var currentOriginalData = this.OriginalStringArrayDataBank
            .FirstOrDefault(entry => entry.Type == type);

          PluginLog.Debug($"[SaveOrUpdateStringArrayDatas] Processing StringArrayData of type {type} with translated string: {translatedString}");

          var reconstructedDictionary = new Dictionary<int, string>(currentOriginalData?.Entries
            .ToDictionary(kvp => kvp.Key, kvp => new ReadOnlySeStringSpan(kvp.Value).ExtractText()) ?? []);

          var formattedData = FormatStringArrayDatas(
          type: type.ToString(),
          size: reconstructedDictionary.Count,
          rawData: SerializeDictionary(this.OriginalStringArrayDataBank
        .FirstOrDefault(entry => entry.Type == currentOriginalData.Type).Entries),
          formattedRawData: null,
          originalLang: ClientStateInterface.ClientLanguage.Humanize(),
          originalStrings: this.PreparedStringArrayDataBank
            .Where(d => d.Type == type)
            .Select(d => d.PreparedString)
            .FirstOrDefault() ?? string.Empty,
          translationLang: LangDict[this.Config.Lang].Code,
          translatedStrings: translatedString,
          translatedStringsWithPayloads: null,
          translationEngine: this.Config.ChosenTransEngine,
          gameVersion: GetGameVersion());
          this.SaveStringArrayData(formattedData);
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[SaveOrUpdateStringArrayDatas] Error saving or updating StringArrayData: {ex}");
        return false;
      }

      PluginLog.Debug($"[SaveOrUpdateStringArrayDatas] Successfully saved or updated StringArrayData.");
      return true;
    }

    private bool SaveOrUpdateSingleStringArrayData(
      StringArrayType type,
      string translatedString)
    {
      PluginLog.Debug($"[SaveOrUpdateSingleStringArrayData] Saving or updating StringArrayData of type {type}...");
      try
      {
        var currentOriginalData = this.OriginalStringArrayDataBank
          .FirstOrDefault(entry => entry.Type == type);
        if (currentOriginalData == null)
        {
          PluginLog.Error($"[SaveOrUpdateSingleStringArrayData] No original data found for type {type}.");
          return false;
        }
        var reconstructedDictionary = new Dictionary<int, string>(currentOriginalData.Entries
          .ToDictionary(kvp => kvp.Key, kvp => new ReadOnlySeStringSpan(kvp.Value).ExtractText()));
        var formattedData = FormatStringArrayDatas(
          type: type.ToString(),
          size: reconstructedDictionary.Count,
          rawData: SerializeDictionary(currentOriginalData.Entries),
          formattedRawData: null,
          originalLang: ClientStateInterface.ClientLanguage.Humanize(),
          originalStrings: this.PreparedStringArrayDataBank
            .Where(d => d.Type == type)
            .Select(d => d.PreparedString)
            .FirstOrDefault() ?? string.Empty,
          translationLang: LangDict[this.Config.Lang].Code,
          translatedStrings: translatedString,
          translatedStringsWithPayloads: null,
          translationEngine: this.Config.ChosenTransEngine,
          gameVersion: GetGameVersion());
        return this.SaveStringArrayData(formattedData);
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[SaveOrUpdateSingleStringArrayData] Error saving or updating StringArrayData of type {type}: {ex}");
        return false;
      }
    }

    /// <summary>
    /// Checks if the given StringArrayData has already been translated by comparing it with fresh translation results.
    /// </summary>
    /// <param name="data">A list of (StringArrayType, string) pairs representing the original stringified entries.</param>
    /// <returns>A tuple: whether it's already translated, and the newly translated result.</returns>
    private (bool IsTranslationFinished, List<(StringArrayType Type, string TranslatedString)>) TranslateData(
      List<(StringArrayType Type, string StringsToTranslate)> data)
    {
      // Kick off the async translation task
      var translatedStrings = this.TranslateStringArrayDatas(data.Select(d => d.StringsToTranslate).ToList())
        .GetAwaiter()
        .GetResult();

      if (translatedStrings == null || translatedStrings.Count != data.Count)
      {
        PluginLog.Warning("[TranslateData] Translation returned null or count mismatch.");
        return (false, []);
      }

      // Pair translated strings back with their types
      var result = data
        .Select((original, i) => (original.Type, TranslatedString: translatedStrings[i]))
        .ToList();

      // Check if original strings and translated results are identical
      var isIdentical = data.Select(d => d.StringsToTranslate)
                            .SequenceEqual(translatedStrings);

      return (isIdentical, result);
    }

    /// <summary>
    /// Extracts each StringArrayData from the bank and returns a list of tuples,
    /// where each tuple contains the StringArrayType and a string in the format "s0|value0|s1|value1|..." for translation.
    /// </summary>
    /// <returns>A list of (StringArrayType, prepared string) tuples.</returns>
    private List<(StringArrayType Type, string PreparedString)> PrepareStringArrayDataBankForTranslation()
    {
      try
      {
        PluginLog.Debug("[PrepareStringArrayDataBankForTranslation] Preparing StringArrayData for translation...");
        var preparedData = new List<(StringArrayType, string)>();

        if (this.OriginalStringArrayDataBank.Count == 0)
        {
          PluginLog.Debug("[PrepareStringArrayDataBankForTranslation] No StringArrayData found in the bank.");
          return preparedData;
        }

        PluginLog.Debug($"[PrepareStringArrayDataBankForTranslation] Found {this.OriginalStringArrayDataBank.Count} StringArrayDatas in the bank.");

        foreach (var entry in this.OriginalStringArrayDataBank)
        {
          var sb = new StringBuilder();

          foreach (var (index, bytes) in entry.Entries)
          {
            if (bytes is null || bytes.Length == 0)
            {
              continue;
            }

            var extracted = new ReadOnlySeStringSpan(bytes).ExtractText();
            sb.Append($"s{index}|{extracted}|");
          }

          var final = sb.ToString().TrimEnd('|');

          if (!string.IsNullOrEmpty(final))
          {
            PluginLog.Debug($"[PrepareStringArrayDataBankForTranslation] [{entry.Type}] Prepared: {final}");
            preparedData.Add((entry.Type, final));
          }
        }

        PluginLog.Debug($"[PrepareStringArrayDataBankForTranslation] Prepared {preparedData.Count} StringArrayData entries.");
        return preparedData;
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[PrepareStringArrayDataBankForTranslation] Error: {ex}");
        return [];
      }
    }

    /// <summary>
    /// Async translate each prepared StringArrayData.
    /// </summary>
    /// <param name="preparedData">The prepared StringArrayData to translate.</param>
    /// <returns>A list of translated StringArrayData.</returns>
    private async Task<List<string>> TranslateStringArrayDatas(List<string> preparedData)
    {
      PluginLog.Debug($"[TranslateStringArrayDatas] Starting translation of {preparedData.Count} StringArrayDatas...");

      var sourceLang = ClientStateInterface.ClientLanguage.Humanize();
      var targetLang = LangDict[this.Config.Lang].Code;

      List<string> translatedResults = [];

      try
      {
        // for each prepared data, translate asynchronously and store the result
        foreach (var data in preparedData)
        {
          PluginLog.Debug($"[TranslateStringArrayDatas] Translating StringArrayData: {data}");
          var translated = await this.TranslationService.TranslateAsync(data, sourceLang, targetLang);

          if (string.IsNullOrWhiteSpace(translated))
          {
            PluginLog.Error($"[TranslateStringArrayDatas] Translation failed for data: {data}");
            continue;
          }

          translatedResults.Add(translated);
        }

        PluginLog.Debug($"[TranslateStringArrayDatas] Translated {translatedResults.Count} StringArrayDatas successfully.");
        return translatedResults;
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[TranslateStringArrayDatas] Error during StringArrayData translation: {ex}");
        return null;
      }
    }

    /// <summary>
    ///     Determines if the currently extracted values match the preserved original snapshot.
    ///     This avoids re-translating or re-applying if the values are already translated.
    /// </summary>
    /// <returns><see langword="true"/> if the content was already translated; otherwise, <see langword="false"/>.</returns>
    private bool IsContentAlreadyTranslated(StringArrayType type, string stringArrayDataContent)
    {
      try
      {
        PluginLog.Debug($"[IsContentAlreadyTranslated] Checking if content is already translated (using OriginalStringArrayDataBank)...");

        var originalContent = this.OriginalStringArrayDataBank.FirstOrDefault(entry => entry.Type == type)?.Entries
          .Select(kvp => $"s{kvp.Key}:{new ReadOnlySeStringSpan(kvp.Value).ExtractText()}")
          .Aggregate((current, next) => $"{current}|{next}").TrimEnd('|');

        PluginLog.Debug($"[IsContentAlreadyTranslated] Comparing original content with provided stringArrayDataContent...");

        PluginLog.Debug($"[IsContentAlreadyTranslated] Original content: {originalContent}");
        if (string.IsNullOrEmpty(originalContent))
        {
          PluginLog.Debug($"[IsContentAlreadyTranslated] No original content found for type {type}.");
          return false;
        }

        PluginLog.Debug($"[IsContentAlreadyTranslated] Provided content: {stringArrayDataContent}");

        return originalContent == stringArrayDataContent;
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[IsContentAlreadyTranslated] Error in IsContentAlreadyTranslated (snapshot mode): {ex}");
        return false;
      }
    }

    /// <summary>
    /// Checks the database for the required StringArrayData and returns the translated values.
    /// </summary>
    /// <param name="stringArrayType">The StringArrayType to search for.</param>
    /// <returns>The found StringArrayDatas, or an empty array if not found.</returns>
    public StringArrayDatas FindStringArrayData(StringArrayType stringArrayType)
    {
      PluginLog.Debug($"[FindStringArrayData] Finding StringArrayData...");

      try
      {
        var type = stringArrayType.ToString();

        PluginLog.Debug($"[FindStringArrayData] Searching for StringArrayData of type: {type}");

        var rawData = this.OriginalStringArrayDataBank
          .FirstOrDefault(entry => entry.Type.ToString() == stringArrayType.ToString())?.Entries;

        if (rawData == null)
        {
          PluginLog.Debug($"[FindStringArrayData] No data found for StringArrayType: {stringArrayType}");
          return null;
        }

        var formattedData = FormatStringArrayDatas(
           type: type,
           size: rawData?.Count ?? 0,
           rawData: SerializeDictionary(rawData),
           formattedRawData: null,
           originalLang: ClientStateInterface.ClientLanguage.Humanize(),
           originalStrings: string.Join("|", rawData.Select(kvp => $"{kvp.Key}:{new ReadOnlySeStringSpan(kvp.Value).ExtractText()}") ?? []),
           translationLang: LangDict[this.Config.Lang].Code,
           translatedStrings: null,
           translatedStringsWithPayloads: null,
           translationEngine: this.Config.ChosenTransEngine,
           gameVersion: GetGameVersion());

        var foundData = FindAndReturnStringArrayData(formattedData);

        if (foundData == null)
        {
          PluginLog.Debug($"[FindStringArrayData] No StringArrayData found for type: {type}");
          return null;
        }

        PluginLog.Debug($"[FindStringArrayData] Found StringArrayData for type: {type}, ID: {foundData.Id}");

        return foundData;
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[FindStringArrayData] Error finding StringArrayData: {ex}");
        return null;
      }
    }

    /// <summary>
    /// Saves the StringArrayData to the database.
    /// </summary>
    /// <param name="data">The StringArrayData to save.</param>
    /// <returns><see langword="true"/> if the save was successful; otherwise, <see langword="false"/>.</returns>
    protected bool SaveStringArrayData(StringArrayDatas stringArrayDatas)
    {
      PluginLog.Debug($"[SaveStringArrayData] Saving StringArrayData...");

      try
      {
        var isInserted = InsertOrUpdateStringArrayData(stringArrayDatas).GetAwaiter().GetResult();

        PluginLog.Debug($"[SaveStringArrayData] Successfully saved StringArrayData.");
        return isInserted == Resources.DataInsertedUpdatedInStringArrayDatasTable ? true : false;
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[SaveStringArrayData] Error saving StringArrayData: {ex}");
        return false;
      }
    }
  }

  /// <summary>
  /// Represents a string array data block, paired with its associated type.
  /// </summary>
  public class StringArrayDataEntry
  {
    /// <summary>
    /// The type of the StringArrayData (from the game).
    /// </summary>
    public StringArrayType Type { get; set; }

    /// <summary>
    /// The map of index to string bytes extracted from the array.
    /// </summary>
    public Dictionary<int, byte[]> Entries { get; set; } = [];
  }

  public class FilteredStringArrayDataEntry
  {
    public StringArrayType Type { get; set; }
    public Dictionary<int, string> Entries { get; set; } = [];
  }
}
