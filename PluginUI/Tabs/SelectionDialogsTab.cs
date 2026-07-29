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
    private static readonly string[] OverlayDisplayModes =
    {
        Resources.QuestDisplayModeNativeUiTranslation,
        Resources.OverlayDisplayModeOverlayTranslationOnly,
        Resources.OverlayDisplayModeNativeUiTranslationWithOriginalOverlay,
    };

    /// <summary>
    ///     Draws the selection-dialog settings tab.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    public static bool Draw(Config config)
    {
        var changed = false;

        changed |= DrawSelectionDialogSection(
            "SelectYesNoDisplayMode",
            Resources.TranslateYesNoScreenLabel,
            ref config.TranslateYesNoScreen,
            ref config.SelectYesNoTranslationDisplayMode);
        changed |= DrawSelectionDialogSection(
            "SelectStringDisplayMode",
            Resources.TranslateSelectStringLabel,
            ref config.TranslateSelectString,
            ref config.SelectStringTranslationDisplayMode);
        changed |= DrawSelectionDialogSection(
            "SelectOkDisplayMode",
            Resources.TranslateSelectOkLabel,
            ref config.TranslateSelectOk,
            ref config.SelectOkTranslationDisplayMode);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    /// <summary>
    ///     Draws one selection-dialog enable toggle and display-mode combo.
    /// </summary>
    /// <param name="comboId">The stable ImGui ID for the display-mode combo.</param>
    /// <param name="sectionLabel">The translated checkbox label.</param>
    /// <param name="enabled">Whether the surface is enabled.</param>
    /// <param name="displayMode">The selected translation display mode.</param>
    /// <returns><c>true</c> when any setting changed.</returns>
    private static bool DrawSelectionDialogSection(
        string comboId,
        string sectionLabel,
        ref bool enabled,
        ref JournalTranslationDisplayMode displayMode)
    {
        var changed = false;

        changed |= ImGui.Checkbox(sectionLabel, ref enabled);
        if (!enabled)
        {
            return changed;
        }

        changed |= DrawDisplayModeCombo(comboId, ref displayMode);
        ImGui.Separator();
        return changed;
    }

    /// <summary>
    ///     Draws the shared selection-dialog display-mode combo.
    /// </summary>
    /// <param name="comboId">The stable ImGui ID for the combo.</param>
    /// <param name="displayMode">The selected display mode.</param>
    /// <returns><c>true</c> when the mode changed.</returns>
    private static bool DrawDisplayModeCombo(
        string comboId,
        ref JournalTranslationDisplayMode displayMode)
    {
        var selectedIndex = (int)displayMode;
        if (!ImGui.Combo(
                $"##{comboId}",
                ref selectedIndex,
                OverlayDisplayModes,
                OverlayDisplayModes.Length))
        {
            return false;
        }

        displayMode = (JournalTranslationDisplayMode)selectedIndex;
        return true;
    }
}
