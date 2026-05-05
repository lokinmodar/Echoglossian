// <copyright file="TooltipTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the settings tab for action/item detail presentation plus shared
///     hover-tooltip appearance.
/// </summary>
public static class TooltipTab
{
    private static readonly string[] DetailDisplayModes =
    [
        Resources.QuestDisplayModeNativeUiTranslation,
        Resources.OverlayDisplayModeOverlayTranslationOnly,
        Resources.OverlayDisplayModeNativeUiTranslationWithOriginalOverlay,
    ];

    /// <summary>
    ///     Draws the tooltip settings tab.
    /// </summary>
    /// <param name="config">The current plugin configuration.</param>
    /// <returns><c>true</c> when a setting changed.</returns>
    public static bool Draw(Config config)
    {
        var changed = false;

        if (!config.Translate)
        {
            return false;
        }

        ImGui.TextUnformatted(Resources.ActionAndItemTooltipsSectionLabel);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            GetText(
                "ActionAndItemTooltipsToggleLabel",
                "Enable action/item detail translation and shared hover tooltips"),
            ref config.TranslateTooltips);

        if (!config.TranslateTooltips)
        {
            if (changed)
            {
                FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
                Echoglossian.SaveConfig(config);
            }

            return changed;
        }

        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            nameof(config.TooltipTranslationDisplayMode),
            ref config.TooltipTranslationDisplayMode,
            config.OverlayOnlyLanguage,
            Resources.ActionAndItemTooltipsDisplayModeLabel,
            Resources.ActionAndItemTooltipsDisplayModeDescription,
            DetailDisplayModes);

        ImGui.Spacing();
        ImGui.TextUnformatted(Resources.HoverTooltipAppearanceSectionLabel);
        ImGui.Separator();
        ImGui.TextWrapped(Resources.HoverTooltipAppearanceDescription);

        var textColorLabel = Resources.HoverTooltipTextColorLabel;
        ImGui.Text(textColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{textColorLabel}##Color",
            ref config.HoverTooltipTextColor,
            ImGuiColorEditFlags.NoInputs);

        var backgroundColorLabel = Resources.HoverTooltipBackgroundColorLabel;
        ImGui.Text(backgroundColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{backgroundColorLabel}##Color",
            ref config.HoverTooltipBackgroundColor,
            ImGuiColorEditFlags.NoInputs);

        changed |= ImGui.SliderFloat(
            Resources.HoverTooltipBackgroundOpacityLabel,
            ref config.HoverTooltipBackgroundOpacity,
            0f,
            1f,
            "%.2f");

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    private static string GetText(string key, string fallback)
    {
        return Resources.ResourceManager.GetString(key, Resources.Culture) ??
               fallback;
    }
}
