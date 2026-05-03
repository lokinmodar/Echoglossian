// <copyright file="UiActionDetailHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    /*private void TooltipsOnActionDetail(ActionTooltip actionTooltip, HoveredAction action)
    {
      Dalamud.Game.Text.SeStringHandling.SeString tooltipDescription = actionTooltip[ActionTooltipString.Description];
#if DEBUG
      Dalamud.Game.Text.SeStringHandling.Payload[] list = tooltipDescription.Payloads.ToArray();
      Dalamud.Game.Text.SeStringHandling.Payload payload = list[0];

      System.Collections.Generic.IEnumerable<Dalamud.Game.Text.SeStringHandling.Payload> lines = tooltipDescription.Payloads.Where(p => p != NewLinePayload.Payload);

      foreach (Dalamud.Game.Text.SeStringHandling.Payload line in lines)
      {
        PluginRuntimeLog.Debug(line.ToString() ?? string.Empty);
      }

      string payloadText = payload.ToString();

      string desc = tooltipDescription.TextValue;
      Task<string> status = this.TranslateAsync(desc);

      PluginRuntimeLog.Debug($"Tooltip desc: {desc}");
      PluginRuntimeLog.Debug($"Tooltip trans: {status.Result}");
#endif
    }*/
  }
}


