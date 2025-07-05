// <copyright file="UICharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool GatheringCharacterWindowAtkValuesComplete = false;

    // Holds string values or null for all AtkValues to maintain consistent indexing
    public Dictionary<int, string?> CharacterWindowAtkValues = new Dictionary<int, string?>();

    // Holds only non-null string values for translation
    public Dictionary<int, string> FilteredCharacterWindowStringAtkValues = new Dictionary<int, string>();

    // Pipe-separated string for sending to translation service
    public string CharacterWindowAtkValuesString = string.Empty;

    private unsafe void TranslateCharacterWindow()
    {
      var atkStg = AtkStage.Instance();
      var characterWB = atkStg->RaptureAtkUnitManager->GetAddonByName("Character");

      if (characterWB == null || !characterWB->IsVisible)
      {
        return;
      }

      var cwAtkVals = characterWB->AtkValues;
      var cwAtkValsCount = characterWB->AtkValuesCount;

      if (cwAtkVals == null)
      {
        return;
      }

      // Store all values with their index; string values are parsed, others are set to null
      this.CharacterWindowAtkValues = Enumerable.Range(0, cwAtkValsCount)
        .ToDictionary(
          i => i,
          i =>
          {
            var type = cwAtkVals[i].Type;
            return type == ValueType.String || type == ValueType.String8 || type == ValueType.ManagedString
              ? MemoryHelper.ReadSeStringAsString(out _, (nint)cwAtkVals[i].String.Value)
              : null;
          });

      // Build a filtered dictionary with only valid string entries for translation
      this.FilteredCharacterWindowStringAtkValues = Enumerable.Range(0, cwAtkValsCount)
        .Where(i => cwAtkVals[i].Type == ValueType.String || cwAtkVals[i].Type == ValueType.String8 || cwAtkVals[i].Type == ValueType.ManagedString)
        .Select(i => new
        {
          Index = i,
          Value = MemoryHelper.ReadSeStringAsString(out _, (nint)cwAtkVals[i].String.Value),
        })
        .Where(x => x.Value != null)
        .ToDictionary(x => x.Index, x => x.Value!); // use ! to assure compiler that nulls are filtered

      // If we have any strings to translate
      if (this.FilteredCharacterWindowStringAtkValues.Count > 0)
      {
        // Log the values we're about to translate
        string jsonOutput = JsonConvert.SerializeObject(this.FilteredCharacterWindowStringAtkValues, Formatting.Indented);
        PluginLog.Debug($"Character window AtkValues: {jsonOutput}");

        // Build the translation string in the format: key|value|key|value|...
        this.CharacterWindowAtkValuesString = string.Join("|", this.FilteredCharacterWindowStringAtkValues.Select(kvp => $"{kvp.Key}|{kvp.Value}"));
      }

      bool isGatheringComplete = this.FilteredCharacterWindowStringAtkValues.Count > 0;
      this.GatheringCharacterWindowAtkValuesComplete = isGatheringComplete;

      if (!isGatheringComplete)
      {
        return;
      }

      PluginLog.Debug("Finished gathering all Character window AtkValues.");
      PluginLog.Debug($"Character window AtkValues string: {this.CharacterWindowAtkValuesString}");

      // Call the translation service
      var translation = this.Translate(this.CharacterWindowAtkValuesString);

      if (string.IsNullOrEmpty(translation))
      {
        PluginLog.Error("Translation failed for Character window AtkValues.");
        return;
      }

      // Parse translation result: expected format is "index|value|index|value|..."
      var parts = translation.Split('|');

      for (int i = 0; i < parts.Length - 1; i += 2)
      {
        if (int.TryParse(parts[i], out int index))
        {
          this.FilteredCharacterWindowStringAtkValues[index] = parts[i + 1];
        }
      }

      PluginLog.Debug($"Translated values count: {this.FilteredCharacterWindowStringAtkValues.Count}");

      // Replace original addon text values with their translated counterparts
      var values = new Span<AtkValue>((void*)cwAtkVals, (int)cwAtkValsCount);

      for (int i = 0; i < cwAtkValsCount; i++)
      {
        if (values[i].Type == ValueType.String || values[i].Type == ValueType.String8 || values[i].Type == ValueType.ManagedString)
        {
          if (this.FilteredCharacterWindowStringAtkValues.TryGetValue(i, out var translated))
          {
            values[i].SetManagedString(translated);
          }
        }
      }

      PluginLog.Debug($"CharacterWindow Translation result: {translation}");
    }
  }
}
