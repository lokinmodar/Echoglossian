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
    private const string TranslateMainCommandLabel = "Translate Main Command";
    private const string TranslateHudWindowsLabel = "Translate HUD Windows";
    private const string TranslateOperationGuideLabel = "Translate Operation Guide";
    private const string TranslateAddonContextMenuTitleLabel = "Translate Addon Context Menu Title";

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
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            nameof(config.CharacterWindowTranslationDisplayMode),
            ref config.CharacterWindowTranslationDisplayMode,
            config.OverlayOnlyLanguage);

        ImGui.Spacing();
        changed |= ImGui.Checkbox(
            TranslateMainCommandLabel,
            ref config.TranslateMainCommandWindow);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            nameof(config.MainCommandWindowTranslationDisplayMode),
            ref config.MainCommandWindowTranslationDisplayMode,
            config.OverlayOnlyLanguage);

        ImGui.Spacing();
        changed |= ImGui.Checkbox(
            TranslateHudWindowsLabel,
            ref config.TranslateHudWindow);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            nameof(config.HudWindowTranslationDisplayMode),
            ref config.HudWindowTranslationDisplayMode,
            config.OverlayOnlyLanguage);

        ImGui.Spacing();
        changed |= ImGui.Checkbox(
            TranslateOperationGuideLabel,
            ref config.TranslateOperationGuideWindow);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            nameof(config.OperationGuideTranslationDisplayMode),
            ref config.OperationGuideTranslationDisplayMode,
            config.OverlayOnlyLanguage);

        ImGui.Spacing();
        changed |= ImGui.Checkbox(
            TranslateAddonContextMenuTitleLabel,
            ref config.TranslateAddonContextMenuTitle);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            nameof(config.AddonContextMenuTitleTranslationDisplayMode),
            ref config.AddonContextMenuTitleTranslationDisplayMode,
            config.OverlayOnlyLanguage);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
