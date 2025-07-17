// <copyright file="OtherUIElementsSettingsTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders additional settings for select strings, confirmations, and UI
///     elements.
/// </summary>
public static class OtherUIElementsSettingsTab
{
    public static bool Draw(Config config)
    {
        var changed = false;

        changed |= ImGui.Checkbox(
            Resources.TranslateYesNoScreenLabel,
            ref config.TranslateYesNoScreen);
        changed |= ImGui.Checkbox(
            Resources.TranslateCutSceneSelectStringLabel,
            ref config.TranslateCutSceneSelectString);
        changed |= ImGui.Checkbox(
            Resources.TranslateSelectStringLabel,
            ref config.TranslateSelectString);
        changed |= ImGui.Checkbox(
            Resources.TranslateSelectOkLabel,
            ref config.TranslateSelectOk);
        changed |= ImGui.Checkbox(
            Resources.TranslateCharacterWindow,
            ref config.TranslateCharacterWindow);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}