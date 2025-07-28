// <copyright file="StringArrayDataHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Client.UI;

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
    private readonly List<StringArrayDataEntry> OriginalStringArrayDataBank = new();

    /// <summary>
    ///     Stores filtered string array data for translation, as a list of dictionaries.
    /// </summary>
    private readonly List<FilteredStringArrayDataEntry> FilteredStringArrayDataBank = new();


    /// <summary>
    ///     Gets a value indicating whether string arrays are used for translation.
    /// </summary>
    protected readonly bool UseStringArray; // kept for now, may remove later

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
      this.arraysToBlock = arraysToBlock ?? new List<string>();
      this.Config = config;
      this.TranslationService = translationService;

      PluginLog.Debug($"[*****] Initializing StringArrayDataHandler with blocked arrays: {string.Join(", ", this.arraysToBlock)}");
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
                PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Extracted {type}[{j}] = '{text}'");
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
          }
          catch (Exception ex)
          {
            PluginLog.Error($"[LoadAndTranslateStringArrayDatas] Error extracting StringArrayData of type {stringArrayDataTypesAvailable[i]}: {ex}");
          }
        }


        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Extracted {this.OriginalStringArrayDataBank.Count} StringArrayDatas.");
        if (this.OriginalStringArrayDataBank.Count == 0)
        {
          PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No StringArrayDatas extracted, skipping translation.");
          return;
        }

        PluginLog.Debug(
          $"[LoadAndTranslateStringArrayDatas] Extracted StringArrayDatas: " +
          string.Join("; ", this.OriginalStringArrayDataBank.Select(entry =>
            $"[{entry.Type}] " +
            string.Join(", ", entry.Entries.Select(kvp =>
              $"{kvp.Key}: {new ReadOnlySeStringSpan(kvp.Value).ExtractText()}"))))
        );


        var preparedData = this.PrepareStringArrayDataBankForTranslation();
        if (preparedData.Count == 0)
        {
          PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] No prepared StringArrayDatas to translate.");
          return;
        }

        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Prepared {preparedData.Count} StringArrayDatas for translation.");

        var isAlreadyTranslated = this.IsTranslated(preparedData);

        PluginLog.Debug($"[LoadAndTranslateStringArrayDatas] Is already translated? {isAlreadyTranslated.Item1}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[LoadAndTranslateStringArrayDatas] Error during StringArrayDatas extraction and translation: {ex}");
        return;
      }
    }

    /// <summary>
    /// Checks if the given StringArrayData has already been translated by comparing it with fresh translation results.
    /// </summary>
    /// <param name="data">A list of (StringArrayType, string) pairs representing the original stringified entries.</param>
    /// <returns>A tuple: whether it's already translated, and the newly translated result.</returns>
    private (bool IsTranslated, List<(StringArrayType Type, string TranslatedString)>) IsTranslated(
      List<(StringArrayType Type, string StringsToTranslate)> data)
    {
      // Kick off the async translation task
      var translatedStrings = this.TranslateStringArrayDatas(data.Select(d => d.StringsToTranslate).ToList())
        .GetAwaiter()
        .GetResult();

      if (translatedStrings == null || translatedStrings.Count != data.Count)
      {
        PluginLog.Warning("[IsTranslated] Translation returned null or count mismatch.");
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
              continue;

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

      List<string> translatedResults = new List<string>();

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
    private bool ContentIsAlreadyTranslated()
    {
      try
      {
        PluginLog.Debug($"[*****] Checking if content is already translated (snapshot mode)...");

        /* bool atkMatch = this.SnapshotOriginalAtkValues.All(kvp =>
             this.FilteredAtkValues.TryGetValue(kvp.Key, out var val) && val == kvp.Value);*/

        /* bool strMatch = this.SnapshotOriginalStringArrayData.All(kvp =>
             this.FilteredStringArrayData.TryGetValue(kvp.Key, out var val) && val == kvp.Value);

         bool isMatch = *//*atkMatch &&*//* strMatch;

         PluginLog.Debug($"[*****] Snapshot retranslation check: Array = {strMatch}, Result = {isMatch}");*/

        /*if (!atkMatch)
        {
          foreach (var kvp in this.SnapshotOriginalAtkValues)
          {
            if (!this.FilteredAtkValues.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
            {
              PluginLog.Debug($"[ATK mismatch] Index {kvp.Key}: expected '{kvp.Value}', found '{val}'");
            }
          }
        }*/

        /*if (!strMatch)
        {
          foreach (var kvp in this.SnapshotOriginalStringArrayData)
          {
            if (!this.FilteredStringArrayData.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
            {
              PluginLog.Debug($"[STR mismatch] Index {kvp.Key}: expected '{kvp.Value}', found '{val}'");
            }
          }
        }

        return isMatch;*/
        return true;
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[*****] Error in ContentIsAlreadyTranslated (snapshot mode): {ex}");
        return false;
      }
    }

    /// <summary>
    /// Checks the database for the required StringArrayData and returns the translated values.
    /// </summary>
    /// <param name="dataToSearch">The StringArrayDatas to search for.</param>
    /// <returns>The found StringArrayDatas, or an empty array if not found.</returns>
    protected StringArrayDatas FindStringArrayData(StringArrayType stringArrayType)
    {
      PluginLog.Debug($"[FindStringArrayData] Finding StringArrayData...");


      Echoglossian.FormatStringArrayDatas(
        type: stringArrayType.ToString,
        size: this.OriginalStringArrayDataBank.FirstOrDefault(entry => entry.Type == stringArrayType)?.Entries.Count ?? 0,
        rawData: this.OriginalStringArrayDataBank.FirstOrDefault(entry => entry.Type == stringArrayType)?.Entries.Select(kvp => kvp.Value).ToList() ?? new List<byte[]>(),
        formattedRawData: null,
        originalLang: ClientStateInterface.ClientLanguage.Humanize(),
        originalStrings: string.Join("|", this.OriginalStringArrayDataBank.FirstOrDefault(entry => entry.Type == stringArrayType)?.Entries.Select(kvp => $"{kvp.Key}:{new ReadOnlySeStringSpan(kvp.Value).ExtractText()}") ?? []),
        translationLang: LangDict[this.Config.Lang].Code,
        translatedStrings: null,
        translatedStringsWithPayloads: null,
        translationEngine: this.Config.ChosenTransEngine,
        gameVersion: GetGameVersion());








        );



    }


    /*/// <summary>
    /// Applies translated StringArrayData values to the game, using the original snapshot for validation.
    /// </summary>
    /// <param name="type">The AddonEvent that triggered the update.</param>
    /// <param name="args">The addon arguments containing the array pointer.</param>
    private unsafe void OnArrayDataUpdate(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"[*****] Handling StringArrayData update for {type}...");

      if (!this.UseStringArray || this.StringArrayDataType is null)
      {
        PluginLog.Debug($"[*****] Skipping array update — not configured.");
        return;
      }

      if (args is not AddonRequestedUpdateArgs requestedUpdateArgs)
      {
        PluginLog.Debug($"[*****] Skipping array update — invalid args.");
        return;
      }

      var stringArrayData = (StringArrayData**)requestedUpdateArgs.StringArrayData;
      var arrayIndex = this.GetStringArrayIndexForAddon((AtkUnitBase*)args.Addon);

      if (arrayIndex is -1)
      {
        PluginLog.Debug($"[*****] No matching string array index found.");
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

        if (this.SnapshotOriginalStringArrayData.TryGetValue(index, out var originalBytes))
        {
          var snapshot = originalBytes; // fixed this from var snapshot = new ReadOnlySeStringSpan(originalBytes).ExtractText();
          if (snapshot == translated)
          {
            PluginLog.Debug($"[*****] Snapshot value {snapshot}, translated value {translated}, current value {new ReadOnlySeStringSpan(currentValue).ExtractText()}");
            PluginLog.Debug($"[*****] Skipping STR[{index}] — already matches snapshot.");
            continue;
          }
        }

        PluginLog.Debug($"[*****] Applying translated array string at index {index}: {translated}");
        addonArrayData->SetValue(index, translated, suppressUpdates: true);
      }
    }*/

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
    public Dictionary<int, byte[]> Entries { get; set; } = new();
  }

  public class FilteredStringArrayDataEntry
  {
    public StringArrayType Type { get; set; }
    public Dictionary<int, string> Entries { get; set; } = new();
  }
}
