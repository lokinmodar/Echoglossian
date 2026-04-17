// <copyright file="TranslationDisplayModeUiHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the shared translation display-mode dropdown used by DB-first UI
///     surfaces.
/// </summary>
internal static class TranslationDisplayModeUiHelper
{
    private static readonly string[] DisplayModes =
    [
        Resources.QuestDisplayModeNativeUiTranslation,
        Resources.QuestDisplayModeTooltipTranslationOnly,
        Resources.QuestDisplayModeNativeUiTranslationWithOriginalTooltips,
    ];

    private const string GenericDisplayModeLabel = "Display mode";
    private const string GenericDisplayModeDescription =
        "This mode controls how this translated UI is presented. Native UI writes the translation into the addon, tooltip-only keeps the addon intact, and native-with-original-tooltips writes translation natively while showing the original in tooltips.";

    /// <summary>
    ///     Draws a shared display-mode combo.
    /// </summary>
    /// <param name="comboId">The unique ImGui id suffix for this combo.</param>
    /// <param name="displayMode">The display mode to edit.</param>
    /// <returns><c>true</c> when the selection changed.</returns>
    public static bool DrawDisplayModeCombo(
        string comboId,
        ref JournalTranslationDisplayMode displayMode)
    {
        var changed = false;
        var modeValue = (int)displayMode;
        ImGui.PushID(comboId);
        if (ImGui.Combo(
                GenericDisplayModeLabel,
                ref modeValue,
                DisplayModes,
                DisplayModes.Length))
        {
            displayMode = (JournalTranslationDisplayMode)modeValue;
            changed = true;
        }

        ImGui.TextWrapped(GenericDisplayModeDescription);
        ImGui.PopID();
        return changed;
    }
}
