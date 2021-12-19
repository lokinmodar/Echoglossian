// <copyright file="UiTooltipHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Linq;
using System.Threading.Tasks;

using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using XivCommon.Functions.Tooltips;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void TooltipsOnActionTooltip(ActionTooltip actionTooltip, HoveredAction action)
    {
      Dalamud.Game.Text.SeStringHandling.SeString tooltipDescription = actionTooltip[ActionTooltipString.Description];
#if DEBUG
      Dalamud.Game.Text.SeStringHandling.Payload[] list = tooltipDescription.Payloads.ToArray();
      Dalamud.Game.Text.SeStringHandling.Payload payload = list[0];

      System.Collections.Generic.IEnumerable<Dalamud.Game.Text.SeStringHandling.Payload> lines = tooltipDescription.Payloads.Where(p => p != NewLinePayload.Payload);

      foreach (Dalamud.Game.Text.SeStringHandling.Payload line in lines)
      {
        PluginLog.LogWarning(line.ToString() ?? string.Empty);
      }

      string payloadText = payload.ToString();

      string desc = tooltipDescription.TextValue;
      Task<string> status = TranslateAsync(desc);

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
