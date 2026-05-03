// <copyright file="GameUIhelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Lumina.Excel.Sheets;

namespace Echoglossian;

public partial class Echoglossian
{
    public HashSet<string> UiElementsLabels = new();

    /// <summary>
    ///     Parses the UI elements from the Addon sheet in the game data.
    /// </summary>
    public void ParseUi()
    {
        var uiStuffz =
            DManager.GetExcelSheet<Addon>(ClientStateInterface.ClientLanguage);

        var addonList = uiStuffz?.ToList();

        PluginRuntimeLog.Debug($"Addon list: {uiStuffz?.Count.ToString()}");
        if (uiStuffz != null)
        {
            foreach (var a in uiStuffz)
            {
                this.UiElementsLabels.Add(a.Text.ToString());
                PluginRuntimeLog.Debug($"Sheet row: {a.RowId}: {a.Text.ToString()}");
            }
        }
    }
}

