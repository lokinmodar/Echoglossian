// <copyright file="UiTooltipHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Linq;
using System.Threading.Tasks;

using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Echoglossian.Properties;
using XivCommon.Functions.Tooltips;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void TooltipsOnActionTooltip(ActionTooltip actionTooltip, HoveredAction action)
    {
      var tooltipDescription = actionTooltip[ActionTooltipString.Description];
#if DEBUG
      var list = tooltipDescription.Payloads.ToArray();
      var payload = list[0];

      var lines = tooltipDescription.Payloads.Where(p => p != NewLinePayload.Payload);

      foreach (var line in lines)
      {
        PluginLog.LogWarning(line.ToString() ?? string.Empty);
      }

      var payloadText = payload.ToString();

      var desc = tooltipDescription.TextValue;
      var status = TranslateAsync(desc);


      PluginLog.LogWarning($"Tooltip desc: {desc}");
      PluginLog.LogError($"Tooltip trans: {status.Result}");
#endif
    }

    private static async Task<string> TranslateAsync(string text)
    {
      string translation = await Task.Run(() => Translate(text));
      return translation;
    }
  }
}
