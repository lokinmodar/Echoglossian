// <copyright file="StringArrayDataHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Lumina.Text.ReadOnly;

namespace Echoglossian.NativeUI.Handlers
{
  /// <summary>
  ///  Class to handle StringArrayData for the game.
  /// </summary>
  public unsafe class StringArrayDataHandler
  {
    /// <summary>
    ///     Gets the configuration settings for the addon handler.
    /// </summary>
    protected readonly Config Config;

    /// <summary>
    ///     Stores the original string array values to restore after translation.
    ///     Uses byte arrays to preserve exact original data.
    /// </summary>
    protected Dictionary<int, byte[]> OriginalStringArrayData = new();

    /// <summary>
    ///  Stores a snapshot of the original string array data
    /// </summary>
    private Dictionary<int, string> SnapshotOriginalStringArrayData = new();

    /// <summary>
    ///     Stores filtered string array data for translation.
    /// </summary>
    protected Dictionary<int, string> FilteredStringArrayData = new();

    /// <summary>
    ///  List of Arrays to avoid extraction and translations
    /// </summary>
    private List<string> arraysToBlock = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StringArrayDataHandler"/> class.
    /// </summary>
    /// <param name="arraysToBlock">The arrays to block.</param>
    public StringArrayDataHandler(List<string> arraysToBlock)
    {
      this.arraysToBlock = arraysToBlock ?? new List<string>();
    }

    // TODO: bring here the logics related to StringArrayData handling from GenericAddonHandler and GenericAddonHandlerHelper

    /// <summary>
    ///     Captures the current extracted ATK and StringArrayData values as immutable snapshots
    ///     to preserve their original (pre-translation) state for DB and cache comparison.
    /// </summary>
    private void SnapshotOriginalValues()
    {
      try
      {


        this.SnapshotOriginalStringArrayData = this.OriginalStringArrayData
            .ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                  var span = kvp.Value;
                  return new ReadOnlySeStringSpan(span).ExtractText();
                });

        PluginLog.Debug($"[{this.AddonName}] Snapshots of original ATK and StringArrayData captured.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[{this.AddonName}] Error during SnapshotOriginalValues: {ex}");
      }
    }

    /// <summary>
    ///  Serializes the original data from ATK values and string array data
    /// </summary>
    /// <returns>Returns the serialized data string.</returns>
    private string SerializeOriginalData()
    {
      PluginLog.Debug($"[{this.AddonName}] Serializing original data...");
      var sb = new StringBuilder();


      foreach (var (index, val) in this.FilteredStringArrayData)
        sb.Append($"s{index}|{val}|");

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

        stringArrayData = this.FilteredStringArrayData,
      };

      return JsonConvert.SerializeObject(combined);
    }

    /// <summary>
    /// Parses the translated JSON data and populates the filtered dictionaries.
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
        if (parsed == null)
        {
          return;
        }

        if (parsed.AtkValues != null)
        {
          this.FilteredAtkValues = parsed.AtkValues;
        }

        if (parsed.StringArrayData != null)
        {
          this.FilteredStringArrayData = parsed.StringArrayData;
        }
      }
      catch
      {
        // ignored
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
        PluginLog.Debug($"[{this.AddonName}] Checking if content is already translated (snapshot mode)...");

        bool atkMatch = this.SnapshotOriginalAtkValues.All(kvp =>
            this.FilteredAtkValues.TryGetValue(kvp.Key, out var val) && val == kvp.Value);

        bool strMatch = this.SnapshotOriginalStringArrayData.All(kvp =>
            this.FilteredStringArrayData.TryGetValue(kvp.Key, out var val) && val == kvp.Value);

        bool isMatch = atkMatch && strMatch;

        PluginLog.Debug($"[{this.AddonName}] Snapshot retranslation check: ATK = {atkMatch}, Array = {strMatch}, Result = {isMatch}");

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

        if (!strMatch)
        {
          foreach (var kvp in this.SnapshotOriginalStringArrayData)
          {
            if (!this.FilteredStringArrayData.TryGetValue(kvp.Key, out var val) || val != kvp.Value)
            {
              PluginLog.Debug($"[STR mismatch] Index {kvp.Key}: expected '{kvp.Value}', found '{val}'");
            }
          }
        }

        return isMatch;
      }
      catch (Exception ex)
      {
        PluginLog.Error($"[{this.AddonName}] Error in ContentIsAlreadyTranslated (snapshot mode): {ex}");
        return false;
      }
    }

    /// <summary>
    /// Extracts string array data from the ATK stage and populates the filtered and original dictionaries.
    /// </summary>
    /// <param name="atkStage">The ATK stage containing the string array data.</param>
    private unsafe void ExtractStringArrayData(AtkStage* atkStage)
    {
      PluginLog.Debug($"[{this.AddonName}] Extracting StringArrayData of type {this.StringArrayDataType.Value}...");
      var data = atkStage->GetStringArrayData(this.StringArrayDataType!.Value);
      var array = data->StringArray;
      var size = data->Size;

      if (array == null || size <= 0)
      {
        PluginLog.Debug($"[{this.AddonName}] No StringArrayData found or size is zero.");
        return;
      }

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

  }
}
