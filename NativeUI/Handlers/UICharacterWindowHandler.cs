// <copyright file="UICharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Newtonsoft.Json;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool GatheringCharacterWindowAtkValuesComplete = false;
    public Dictionary<int, string> CharacterWindowAtkValues = new Dictionary<int, string>();
    public string CharacterWindowAtkValuesString = string.Empty; // New string to store the concatenated output

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
      var atkValuesSpan = characterWB->AtkValuesSpan;

      if (cwAtkVals == null)
      {
        return;
      }

      // Use LINQ to gather values into the dictionary
      this.CharacterWindowAtkValues = Enumerable.Range(0, cwAtkValsCount)
          .Where(i => cwAtkVals[i].Type == ValueType.String || cwAtkVals[i].Type == ValueType.String8)
          .Select(i => new
          {
            Index = i,
            Value = MemoryHelper.ReadSeStringAsString(out _, (nint)cwAtkVals[i].String.Value),
          })
          .Where(x => x.Value != null)
          .ToDictionary(x => x.Index, x => x.Value);

      if (this.CharacterWindowAtkValues.Count > 0)
      {
        string jsonOutput = JsonConvert.SerializeObject(this.CharacterWindowAtkValues, Formatting.Indented);
        PluginLog.Debug($"Character window AtkValues: {jsonOutput}");

        // Concatenate key-value pairs into a single string
        this.CharacterWindowAtkValuesString = string.Join("|", this.CharacterWindowAtkValues.Select(kvp => $"{kvp.Key}|{kvp.Value}"));
      }

      bool isGatheringComplete = this.CharacterWindowAtkValues.Count > 0;
      this.GatheringCharacterWindowAtkValuesComplete = isGatheringComplete;

      if (isGatheringComplete)
      {
        PluginLog.Debug("Finished gathering all Character window AtkValues.");
        PluginLog.Debug($"Character window AtkValues string: {this.CharacterWindowAtkValuesString}");

        // send the string to the translation service

        var translation = this.Translate(this.CharacterWindowAtkValuesString);

        if (string.IsNullOrEmpty(translation))
        {
          PluginLog.Error("Translation failed for Character window AtkValues.");
          return;
        }

        // Use LINQ to replace the original values in their positions in the dictionary with the translated values the translated values are on a string in the format key|value|next_key|next_value and so on
        var translatedValues = translation.Split('|')
            .Where((_, index) => index % 2 == 1) // Get only the values (odd indices)
            .ToArray();
        PluginLog.Debug($"Translated values count: {translatedValues.Length}");
        // Update the CharacterWindowAtkValues dictionary with the translated values in their indexes in the CharacterWindowAtkValues dictionary
        for (int i = 0; i < translatedValues.Length; i++)
        {
          if (this.CharacterWindowAtkValues.ContainsKey(i))
          {
            this.CharacterWindowAtkValues[i] = translatedValues[i];
          }
        }

        // Set the updated CharacterWindowAtkValues back to the addon
        var values = new Span<AtkValue>((void*)cwAtkVals, (int)cwAtkValsCount);

        for (int i = 0; i < cwAtkValsCount; i++)
        {
          if (values[i].Type == ValueType.String || values[i].Type == ValueType.String8)
          {
            // Update the value with the translated string
            values[i].SetManagedString(this.CharacterWindowAtkValues[i]);
          }
        }

        PluginLog.Debug($"CharacterWindow Translation result: {translation}");
      }
    }
  }
}
