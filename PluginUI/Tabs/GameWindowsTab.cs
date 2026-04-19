// <copyright file="GameWindowsTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders DB-first game-window settings that were previously grouped under
///     the generic "other UI elements" tab.
/// </summary>
public static class GameWindowsTab
{
    /// <summary>
    ///     Draws the game-windows settings tab.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    public static bool Draw(Config config)
    {
        var changed = false;

        changed |= DrawWindowSection(
            config,
            Resources.TranslateCharacterWindow,
            ref config.TranslateCharacterWindow,
            ref config.CharacterWindowTranslationDisplayMode);
        changed |= DrawWindowSection(
            config,
            Resources.TranslateMainCommandWindow,
            ref config.TranslateMainCommandWindow,
            ref config.MainCommandWindowTranslationDisplayMode);
        changed |= DrawWindowSection(
            config,
            Resources.TranslateActionMenuWindow,
            ref config.TranslateActionMenuWindow,
            ref config.ActionMenuWindowTranslationDisplayMode);
        changed |= DrawWindowSection(
            config,
            Resources.TranslateHudWindows,
            ref config.TranslateHudWindow,
            ref config.HudWindowTranslationDisplayMode);
        changed |= DrawWindowSection(
            config,
            Resources.TranslateOperationGuideWindow,
            ref config.TranslateOperationGuideWindow,
            ref config.OperationGuideTranslationDisplayMode);
        changed |= DrawWindowSection(
            config,
            Resources.TranslateAddonContextMenuTitleWindow,
            ref config.TranslateAddonContextMenuTitle,
            ref config.AddonContextMenuTitleTranslationDisplayMode);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    /// <summary>
    ///     Draws one DB-first game-window section with an enable toggle and
    ///     display-mode combo.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <param name="sectionLabel">The section label.</param>
    /// <param name="enabled">The toggle that enables translation.</param>
    /// <param name="displayMode">The configured display mode.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawWindowSection(
        Config config,
        string sectionLabel,
        ref bool enabled,
        ref JournalTranslationDisplayMode displayMode)
    {
        var changed = false;

        ImGui.Spacing();
        ImGui.TextUnformatted(sectionLabel);
        ImGui.Separator();

        changed |= ImGui.Checkbox(sectionLabel, ref enabled);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            sectionLabel,
            ref displayMode,
            config.OverlayOnlyLanguage);

        return changed;
    }
}
