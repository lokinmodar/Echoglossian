// <copyright file="TooltipTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the settings tab for tooltip presentation and appearance.
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

        ImGui.TextUnformatted(Resources.TooltipAddonOverlayAppearanceSectionLabel);
        ImGui.Separator();
        ImGui.TextWrapped(Resources.TooltipAddonOverlayAppearanceDescription);

        changed |= ImGui.Checkbox(
            Resources.TooltipAddonHideNativeTooltipWhenOverlayActiveLabel,
            ref config.TooltipAddonHideNativeTooltipWhenOverlayActive);

        changed |= ImGui.SliderFloat(
            Resources.TooltipAddonOverlayFontScaleAdjustmentLabel,
            ref config.TooltipAddonOverlayFontScaleAdjustment,
            0.25f,
            3f,
            "%.2f");

        changed |= ImGui.SliderFloat(
            Resources.TooltipAddonOverlayLineHeightScaleLabel,
            ref config.TooltipAddonOverlayLineHeightScale,
            0.8f,
            1.2f,
            "%.2f");

        changed |= ImGui.SliderFloat(
            Resources.TooltipAddonOverlayPaddingLabel,
            ref config.TooltipAddonOverlayPadding,
            0f,
            32f,
            "%.0f px");

        var tooltipAddonTextColorLabel = Resources.TooltipAddonOverlayTextColorLabel;
        ImGui.Text(tooltipAddonTextColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{tooltipAddonTextColorLabel}##Color",
            ref config.TooltipAddonOverlayTextColor,
            ImGuiColorEditFlags.NoInputs);

        var tooltipAddonBackgroundColorLabel =
            Resources.TooltipAddonOverlayBackgroundColorLabel;
        ImGui.Text(tooltipAddonBackgroundColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{tooltipAddonBackgroundColorLabel}##Color",
            ref config.TooltipAddonOverlayBackgroundColor,
            ImGuiColorEditFlags.NoInputs);

        changed |= ImGui.SliderFloat(
            Resources.TooltipAddonOverlayBackgroundOpacityLabel,
            ref config.TooltipAddonOverlayBackgroundOpacity,
            0f,
            1f,
            "%.2f");

        var maxWidthMode = (int)config.TooltipAddonOverlayMaxWidthMode;
        if (ImGui.Combo(
                Resources.TooltipAddonOverlayMaxWidthModeLabel,
                ref maxWidthMode,
                [
                    Resources.TooltipAddonOverlayMaxWidthMatchNativeLabel,
                    Resources.TooltipAddonOverlayMaxWidthManualCapLabel,
                ],
                2))
        {
            config.TooltipAddonOverlayMaxWidthMode =
                (TooltipAddonOverlayMaxWidthMode)maxWidthMode;
            changed = true;
        }

        if (config.TooltipAddonOverlayMaxWidthMode ==
            TooltipAddonOverlayMaxWidthMode.ManualCap)
        {
            changed |= ImGui.SliderFloat(
                Resources.TooltipAddonOverlayManualMaxWidthLabel,
                ref config.TooltipAddonOverlayManualMaxWidth,
                240f,
                1920f,
                "%.0f px");
        }

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
