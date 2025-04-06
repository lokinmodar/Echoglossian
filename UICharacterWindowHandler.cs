// <copyright file="UICharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Generic;
using System.Linq;

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
      }
    }
  }
}
