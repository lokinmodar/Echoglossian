// <copyright file="SelectionDialogsTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders selection and confirmation dialog settings.
/// </summary>
public static class SelectionDialogsTab
{
    /// <summary>
    ///     Draws the selection-dialog settings tab.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    public static bool Draw(Config config)
    {
        var changed = false;

        changed |= ImGui.Checkbox(
            Resources.TranslateYesNoScreenLabel,
            ref config.TranslateYesNoScreen);
        changed |= ImGui.Checkbox(
            Resources.TranslateSelectStringLabel,
            ref config.TranslateSelectString);
        changed |= ImGui.Checkbox(
            Resources.TranslateSelectOkLabel,
            ref config.TranslateSelectOk);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
