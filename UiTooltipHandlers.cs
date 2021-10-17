// <copyright file="UiTooltipHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.Gui;
using Dalamud.Logging;
using XivCommon.Functions.Tooltips;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private void TooltipsOnActionTooltip(ActionTooltip actionTooltip, HoveredAction action)
    {
      PluginLog.LogWarning($"{((actionTooltip.Fields & ActionTooltipFields.Description) != 0 ? ActionTooltipFields.Description.ToString() : string.Empty)}");
    }
  }
}
