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
    private static readonly Vector4 SectionHeaderColor =
        new(0.78f, 0.86f, 0.97f, 1f);

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

        DrawSectionHeader(
            Resources.ActionAndItemTooltipsSectionLabel,
            null);

        changed |= ImGui.Checkbox(
            Resources.ActionAndItemTooltipsToggleLabel,
            ref config.TranslateTooltips);

        if (config.TranslateTooltips)
        {
            ImGui.TextWrapped(Resources.ActionAndItemTooltipsOverlayOnlyDescription);
        }

        DrawSectionHeader(
            Resources.ActionItemDetailOverlayAppearanceSectionLabel,
            Resources.ActionItemDetailOverlayAppearanceDescription);

        changed |= DrawSharedOverlayAppearanceControls(
            fontScaleLabel: Resources.TooltipAddonOverlayFontScaleAdjustmentLabel,
            ref config.ActionItemDetailOverlayFontScaleAdjustment,
            lineHeightLabel: Resources.TooltipAddonOverlayLineHeightScaleLabel,
            ref config.ActionItemDetailOverlayLineHeightScale,
            paddingLabel: Resources.TooltipAddonOverlayPaddingLabel,
            ref config.ActionItemDetailOverlayPadding,
            textColorLabel: Resources.TooltipAddonOverlayTextColorLabel,
            ref config.ActionItemDetailOverlayTextColor,
            backgroundColorLabel: Resources.TooltipAddonOverlayBackgroundColorLabel,
            ref config.ActionItemDetailOverlayBackgroundColor,
            backgroundOpacityLabel: Resources.TooltipAddonOverlayBackgroundOpacityLabel,
            ref config.ActionItemDetailOverlayBackgroundOpacity,
            maxWidthModeLabel: Resources.TooltipAddonOverlayMaxWidthModeLabel,
            matchNativeLabel: Resources.ActionItemDetailOverlayMaxWidthMatchNativeLabel,
            ref config.ActionItemDetailOverlayMaxWidthMode,
            manualMaxWidthLabel: Resources.TooltipAddonOverlayManualMaxWidthLabel,
            ref config.ActionItemDetailOverlayManualMaxWidth);

        DrawSectionHeader(
            Resources.HoverTooltipAppearanceSectionLabel,
            Resources.HoverTooltipAppearanceDescription);

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

        DrawSectionHeader(
            Resources.TranslateTooltipAddonLabel,
            null);

        changed |= ImGui.Checkbox(
            Resources.TranslateTooltipAddonLabel,
            ref config.TranslateTooltipAddon);
        changed |= TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            Resources.TranslateTooltipAddonLabel,
            ref config.TooltipAddonTranslationDisplayMode,
            config.OverlayOnlyLanguage);

        DrawSectionHeader(
            Resources.TooltipAddonOverlayAppearanceSectionLabel,
            Resources.TooltipAddonOverlayAppearanceDescription);

        changed |= ImGui.Checkbox(
            Resources.TooltipAddonHideNativeTooltipWhenOverlayActiveLabel,
            ref config.TooltipAddonHideNativeTooltipWhenOverlayActive);

        changed |= DrawSharedOverlayAppearanceControls(
            fontScaleLabel: Resources.TooltipAddonOverlayFontScaleAdjustmentLabel,
            ref config.TooltipAddonOverlayFontScaleAdjustment,
            lineHeightLabel: Resources.TooltipAddonOverlayLineHeightScaleLabel,
            ref config.TooltipAddonOverlayLineHeightScale,
            paddingLabel: Resources.TooltipAddonOverlayPaddingLabel,
            ref config.TooltipAddonOverlayPadding,
            textColorLabel: Resources.TooltipAddonOverlayTextColorLabel,
            ref config.TooltipAddonOverlayTextColor,
            backgroundColorLabel: Resources.TooltipAddonOverlayBackgroundColorLabel,
            ref config.TooltipAddonOverlayBackgroundColor,
            backgroundOpacityLabel: Resources.TooltipAddonOverlayBackgroundOpacityLabel,
            ref config.TooltipAddonOverlayBackgroundOpacity,
            maxWidthModeLabel: Resources.TooltipAddonOverlayMaxWidthModeLabel,
            matchNativeLabel: Resources.TooltipAddonOverlayMaxWidthMatchNativeLabel,
            ref config.TooltipAddonOverlayMaxWidthMode,
            manualMaxWidthLabel: Resources.TooltipAddonOverlayManualMaxWidthLabel,
            ref config.TooltipAddonOverlayManualMaxWidth);

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    /// <summary>
    ///     Draws one visually separated section heading for the tooltip tab.
    /// </summary>
    /// <param name="title">The section title.</param>
    /// <param name="description">The optional section description.</param>
    private static void DrawSectionHeader(
        string title,
        string? description)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(SectionHeaderColor, title);

        if (!string.IsNullOrWhiteSpace(description))
        {
            ImGui.TextWrapped(description);
        }

        ImGui.Spacing();
    }

    /// <summary>
    ///     Draws one shared overlay-appearance control bucket.
    /// </summary>
    /// <param name="fontScaleLabel">The font-scale slider label.</param>
    /// <param name="fontScale">The persisted font-scale value.</param>
    /// <param name="lineHeightLabel">The line-height slider label.</param>
    /// <param name="lineHeight">The persisted line-height value.</param>
    /// <param name="paddingLabel">The padding slider label.</param>
    /// <param name="padding">The persisted padding value.</param>
    /// <param name="textColorLabel">The text-color label.</param>
    /// <param name="textColor">The persisted text color.</param>
    /// <param name="backgroundColorLabel">The background-color label.</param>
    /// <param name="backgroundColor">The persisted background color.</param>
    /// <param name="backgroundOpacityLabel">The opacity slider label.</param>
    /// <param name="backgroundOpacity">The persisted background opacity.</param>
    /// <param name="maxWidthModeLabel">The width-mode combo label.</param>
    /// <param name="matchNativeLabel">The native-width option label.</param>
    /// <param name="maxWidthMode">The persisted width mode.</param>
    /// <param name="manualMaxWidthLabel">The manual-width slider label.</param>
    /// <param name="manualMaxWidth">The persisted manual width.</param>
    /// <returns><c>true</c> when one or more controls changed.</returns>
    private static bool DrawSharedOverlayAppearanceControls(
        string fontScaleLabel,
        ref float fontScale,
        string lineHeightLabel,
        ref float lineHeight,
        string paddingLabel,
        ref float padding,
        string textColorLabel,
        ref Vector3 textColor,
        string backgroundColorLabel,
        ref Vector3 backgroundColor,
        string backgroundOpacityLabel,
        ref float backgroundOpacity,
        string maxWidthModeLabel,
        string matchNativeLabel,
        ref TooltipAddonOverlayMaxWidthMode maxWidthMode,
        string manualMaxWidthLabel,
        ref float manualMaxWidth)
    {
        var changed = false;

        changed |= ImGui.SliderFloat(
            fontScaleLabel,
            ref fontScale,
            0.25f,
            3f,
            "%.2f");

        changed |= ImGui.SliderFloat(
            lineHeightLabel,
            ref lineHeight,
            0.8f,
            1.2f,
            "%.2f");

        changed |= ImGui.SliderFloat(
            paddingLabel,
            ref padding,
            0f,
            32f,
            "%.0f px");

        ImGui.Text(textColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{textColorLabel}##Color",
            ref textColor,
            ImGuiColorEditFlags.NoInputs);

        ImGui.Text(backgroundColorLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            $"{backgroundColorLabel}##Color",
            ref backgroundColor,
            ImGuiColorEditFlags.NoInputs);

        changed |= ImGui.SliderFloat(
            backgroundOpacityLabel,
            ref backgroundOpacity,
            0f,
            1f,
            "%.2f");

        var maxWidthModeIndex = (int)maxWidthMode;
        if (ImGui.Combo(
                maxWidthModeLabel,
                ref maxWidthModeIndex,
                [
                    matchNativeLabel,
                    Resources.TooltipAddonOverlayMaxWidthManualCapLabel,
                ],
                2))
        {
            maxWidthMode = (TooltipAddonOverlayMaxWidthMode)maxWidthModeIndex;
            changed = true;
        }

        if (maxWidthMode == TooltipAddonOverlayMaxWidthMode.ManualCap)
        {
            changed |= ImGui.SliderFloat(
                manualMaxWidthLabel,
                ref manualMaxWidth,
                240f,
                1920f,
                "%.0f px");
        }

        return changed;
    }
}
