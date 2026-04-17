// <copyright file="JournalTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the settings tab for journal and quest-family translation.
/// </summary>
public static class JournalTab
{
    private const string ActionAndItemTooltipSectionLabel =
        "Action and item tooltips";
    private const string ActionAndItemTooltipDisplayModeLabel =
        "Tooltip display mode";
    private const string ActionAndItemTooltipDisplayModeDescription =
        "This mode controls action and item tooltips. Native UI writes the translated tooltip into the game tooltip, tooltip-only keeps the native tooltip intact and uses Echoglossian overlays, and native-with-original-tooltips writes translation natively while showing the original in our overlay.";
    private const string HoverTooltipAppearanceSectionLabel =
        "Hover tooltip appearance";
    private const string HoverTooltipTextColorLabel =
        "Tooltip text color";
    private const string HoverTooltipBackgroundColorLabel =
        "Tooltip background color";
    private const string HoverTooltipBackgroundOpacityLabel =
        "Tooltip background opacity";
    private const string HoverTooltipAppearanceDescription =
        "These colors apply to Echoglossian hover tooltips used by quest and DB-first UI surfaces.";

    /// <summary>
    ///     Draws the Journal settings tab.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <param name="langToRemoveDiacritics">Whether the selected language supports diacritic removal.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    public static bool Draw(Config config, bool langToRemoveDiacritics)
    {
        var changed = false;

        if (config.Translate)
        {
            changed |= DrawJournalSection(config);
            changed |= DrawTooltipSection(config);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateJournalDetailToggle,
                ref config.TranslateJournalDetail,
                ref config.JournalDetailTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateJournalAcceptToggle,
                ref config.TranslateJournalAccept,
                ref config.JournalAcceptTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateJournalResultToggle,
                ref config.TranslateJournalResult,
                ref config.JournalResultTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateToDoListToggle,
                ref config.TranslateToDoList,
                ref config.ToDoListTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateScenarioTreeToggle,
                ref config.TranslateScenarioTree,
                ref config.ScenarioTreeTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateRecommendListToggle,
                ref config.TranslateRecommendList,
                ref config.RecommendListTranslationDisplayMode);
            changed |= DrawQuestFamilySection(
                config,
                Resources.TranslateAreaMapToggle,
                ref config.TranslateAreaMap,
                ref config.AreaMapTranslationDisplayMode);
        }

        if (langToRemoveDiacritics)
        {
            changed |= ImGui.Checkbox(
                Resources.RemoveDiacriticsToggle,
                ref config.RemoveDiacriticsWhenUsingReplacementQuest);
        }

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    /// <summary>
    ///     Draws the Journal-specific quest translation controls.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawJournalSection(Config config)
    {
        var changed = false;
        ImGui.TextUnformatted(Resources.TranslateJournalToggle);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            Resources.TranslateJournalToggle,
            ref config.TranslateJournal);

        changed |= DrawQuestDisplayModeCombo(
            nameof(config.JournalTranslationDisplayMode),
            ref config.JournalTranslationDisplayMode,
            config.OverlayOnlyLanguage,
            Resources.JournalQuestDisplayModeLabel,
            Resources.JournalQuestDisplayModeDescription);

        changed |= ImGui.Checkbox(
            Resources.JournalGlobalHoverTooltipsLabel,
            ref config.TranslateTooltips);

        return changed;
    }

    /// <summary>
    ///     Draws the action/item tooltip mode and the shared hover-tooltip
    ///     appearance settings.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawTooltipSection(Config config)
    {
        var changed = false;

        ImGui.Spacing();
        ImGui.TextUnformatted(ActionAndItemTooltipSectionLabel);
        ImGui.Separator();

        changed |= DrawQuestDisplayModeCombo(
            nameof(config.TooltipTranslationDisplayMode),
            ref config.TooltipTranslationDisplayMode,
            config.OverlayOnlyLanguage,
            ActionAndItemTooltipDisplayModeLabel,
            ActionAndItemTooltipDisplayModeDescription);

        ImGui.Spacing();
        ImGui.TextUnformatted(HoverTooltipAppearanceSectionLabel);
        ImGui.Separator();
        ImGui.TextWrapped(HoverTooltipAppearanceDescription);

        ImGui.Text(HoverTooltipTextColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{HoverTooltipTextColorLabel}##Color",
            ref config.HoverTooltipTextColor,
            ImGuiColorEditFlags.NoInputs);

        ImGui.Text(HoverTooltipBackgroundColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{HoverTooltipBackgroundColorLabel}##Color",
            ref config.HoverTooltipBackgroundColor,
            ImGuiColorEditFlags.NoInputs);

        changed |= ImGui.SliderFloat(
            HoverTooltipBackgroundOpacityLabel,
            ref config.HoverTooltipBackgroundOpacity,
            0f,
            1f,
            "%.2f");

        return changed;
    }

    /// <summary>
    ///     Draws a quest family settings section with an enable toggle and
    ///     display mode combo.
    /// </summary>
    /// <param name="sectionLabel">The label used for the section.</param>
    /// <param name="enabled">The toggle that enables translation for the family.</param>
    /// <param name="displayMode">The configured display mode for the family.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    private static bool DrawQuestFamilySection(
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
        changed |= DrawQuestDisplayModeCombo(
            sectionLabel,
            ref displayMode,
            config.OverlayOnlyLanguage);

        return changed;
    }

    /// <summary>
    ///     Draws the generic quest display mode combo and description text.
    /// </summary>
    /// <param name="comboId">A unique ImGui id suffix for this combo.</param>
    /// <param name="displayMode">The configured display mode to edit.</param>
    /// <returns><c>true</c> when the combo selection changed.</returns>
    private static bool DrawQuestDisplayModeCombo(
        string comboId,
        ref JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage,
        string? label = null,
        string? description = null)
    {
        return TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            comboId,
            ref displayMode,
            overlayOnlyLanguage,
            label,
            description);
    }
}
