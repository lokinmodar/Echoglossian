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

        changed |= config.NormalizeStructuredTooltipPresentationSettings();

        ImGui.TextUnformatted(Resources.ActionAndItemTooltipsSectionLabel);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            Resources.ActionAndItemTooltipsToggleLabel,
            ref config.TranslateTooltips);

        if (config.TranslateTooltips)
        {
            ImGui.TextWrapped(Resources.ActionAndItemTooltipsOverlayOnlyDescription);
        }

        ImGui.TextUnformatted(Resources.HoverTooltipAppearanceSectionLabel);
        ImGui.Separator();
        ImGui.TextWrapped(Resources.HoverTooltipAppearanceDescription);

        changed |= ImGui.SliderFloat(
            Resources.HoverTooltipFontScaleLabel,
            ref config.HoverTooltipFontScale,
            0.25f,
            3f,
            "%.2f");

        changed |= ImGui.SliderFloat(
            Resources.HoverTooltipMaxWidthLabel,
            ref config.HoverTooltipMaxWidth,
            240f,
            960f,
            "%.0f px");

        changed |= ImGui.SliderFloat(
            Resources.TexturePresentationLineHeightScaleLabel,
            ref config.TexturePresentationLineHeightScale,
            0.8f,
            1.2f,
            "%.2f");
        ImGui.TextWrapped(Resources.TexturePresentationLineHeightScaleDescription);

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

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted(Resources.TranslateTooltipAddonLabel);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            Resources.TranslateTooltipAddonLabel,
            ref config.TranslateTooltipAddon);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            Resources.TranslateTooltipAddonLabel,
            ref config.TooltipAddonTranslationDisplayMode,
            config.OverlayOnlyLanguage);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
