// <copyright file="OverlayTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the configuration settings related to overlay-based translations
///     using vertical tabs.
/// </summary>
public static class OverlayTab
{
    private static int selectedOverlayTab;
    private static int selectedToastOverlayTab;

    private static readonly string[] OverlayTabs =
    {
        Resources.TalkTabTitle,
        Resources.BattleTalkTabTitle,
        Resources.ToastTabTitle,
        Resources.SubtitleTabTitle,
        Resources.OverlayTabMiniTalkText,
        Resources.OverlayTabCutSceneSelectStringText,
        Resources.ConfigTab4Name,
        Resources.OtherUIElementsTabTitle,
    };

    private static readonly string[] ToastOverlayTabs =
    {
        Resources.ToastOverlayTabGeneralText,
        Resources.ToastOverlayTabScreenInfoText,
        Resources.ToastOverlayTabErrorText,
        Resources.ToastOverlayTabAreaText,
        Resources.ToastOverlayTabClassJobText,
        Resources.ToastOverlayTabTextGimmickHintText,
        Resources.ToastOverlayTabQuestText,
    };

    /// <summary>
    ///     Draws the full Overlay tab with vertical sub-tabs.
    /// </summary>
    public static bool Draw(Config config)
    {
        var changed = false;

        using var scrollingChild = ImRaii.Child(
            "OvverlaysSettings",
            new Vector2(-1, -100),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChild)
        {
            return false;
        }

        ImGui.BeginChild("overlay_tab_left", new Vector2(150, 0), true);
        for (var i = 0; i < OverlayTabs.Length; i++)
        {
            if (ImGui.Selectable(OverlayTabs[i], selectedOverlayTab == i))
            {
                selectedOverlayTab = i;
            }
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("overlay_tab_right", new Vector2(0, 0), true);

        switch (selectedOverlayTab)
        {
            case 0:
                changed |= DrawTalkOverlay(config);
                break;
            case 1:
                changed |= DrawBattleTalkOverlay(config);
                break;
            case 2:
                changed |= DrawToastOverlay(config);
                break;
            case 3:
                changed |= DrawSubtitleOverlay(config);
                break;
            case 4:
                changed |= DrawMiniTalkOverlay(config);
                break;
            case 5:
                changed |= DrawCutSceneSelectStringOverlay(config);
                break;
            case 6:
                changed |= JournalTab.Draw(config, LangToRemoveDiacritics);
                break;
            case 7:
                changed |= OtherUIElementsSettingsTab.Draw(config);
                break;
        }

        if (ShouldRemoveDiacritics(config))
        {
            ImGui.Separator();
            changed |= DrawGlobalReplacementDiacriticsSetting(config);
        }

        ImGui.EndChild();

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }

    private static bool DrawTalkOverlay(Config config)
    {
        var changed = false;

        using var scrollingChildTalk = ImRaii.Child(
            "TalkOverlaySettings",
            new Vector2(-1, -1),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChildTalk)
        {
            return false;
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateTalkToggleLabel,
            ref config.TranslateTalk);

        if (!config.TranslateTalk)
        {
            return changed;
        }

        if (config.OverlayOnlyLanguage)
        {
            changed |= AssignIfChanged(ref config.UseImGuiForTalk, true);
            changed |= AssignIfChanged(ref config.SwapTextsUsingImGui, false);
        }
        else
        {
            changed |= ImGui.Checkbox(
                Resources.OverlayToggleLabel,
                ref config.UseImGuiForTalk);
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateNpcNamesToggle,
            ref config.TranslateTalkNpcNames);

        if (config.UseImGuiForTalk)
        {
            changed |= DrawOverlaySettings(
                ref config.TalkFontScale,
                ref config.ImGuiTalkWindowWidthMult,
                ref config.ImGuiWindowPosCorrection,
                ref config.OverlayTalkTextColor,
                Resources.OverlayFontScaleLabel,
                ref config.TalkForceShowTitle,
                ref config.FontChangeTime);
        }

        if (!config.OverlayOnlyLanguage && config.UseImGuiForTalk)
        {
            changed |= ImGui.Checkbox(
                Resources.SwapTranslationTextToggle,
                ref config.SwapTextsUsingImGui);
        }

        return changed;
    }

    private static bool DrawBattleTalkOverlay(Config config)
    {
        var changed = false;

        using var scrollingChildBattleTalk = ImRaii.Child(
            "BattleTalkOverlaySettings",
            new Vector2(-1, -1),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChildBattleTalk)
        {
            return false;
        }

        changed |= ImGui.Checkbox(
            Resources.TransLateBattletalkToggle,
            ref config.TranslateBattleTalk);

        if (!config.TranslateBattleTalk)
        {
            return changed;
        }

        if (config.OverlayOnlyLanguage)
        {
            changed |= AssignIfChanged(ref config.UseImGuiForBattleTalk, true);
            changed |= AssignIfChanged(ref config.SwapTextsUsingImGui, false);
        }
        else
        {
            changed |= ImGui.Checkbox(
                Resources.OverlayToggleLabel,
                ref config.UseImGuiForBattleTalk);
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateNpcNamesToggle,
            ref config.TranslateBattleTalkNpcNames);

        if (config.UseImGuiForBattleTalk)
        {
            changed |= DrawOverlaySettings(
                ref config.BattleTalkFontScale,
                ref config.ImGuiBattleTalkWindowWidthMult,
                ref config.ImGuiBattleTalkWindowPosCorrection,
                ref config.OverlayBattleTalkTextColor,
                Resources.OverlayFontScaleLabel,
                ref config.BattleTalkForceShowTitle,
                ref config.FontChangeTime);
        }

        if (!config.OverlayOnlyLanguage && config.UseImGuiForBattleTalk)
        {
            changed |= ImGui.Checkbox(
                Resources.SwapTranslationTextToggle,
                ref config.SwapTextsUsingImGui);
        }

        return changed;
    }

    private static bool DrawToastOverlay(Config config)
    {
        var changed = false;

        using var scrollingChildToast = ImRaii.Child(
            "ToastOverlaySettings",
            new Vector2(-1, -1),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChildToast)
        {
            return false;
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateToastToggleText,
            ref config.TranslateToast);

        if (!config.TranslateToast)
        {
            return changed;
        }

        ImGui.Separator();

        ImGui.BeginChild("toast_overlay_tab_left", new Vector2(170, 0), true);
        for (var i = 0; i < ToastOverlayTabs.Length; i++)
        {
            if (ImGui.Selectable(
                    ToastOverlayTabs[i],
                    selectedToastOverlayTab == i))
            {
                selectedToastOverlayTab = i;
            }
        }

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("toast_overlay_tab_right", new Vector2(0, 0), true);

        switch (selectedToastOverlayTab)
        {
            case 0:
                changed |= DrawToastGeneralPage(config);
                break;
            case 1:
                changed |= DrawToastTypePage(
                    config,
                    Resources.ToastOverlayScreenInfoWideTextSectionTitle,
                    ref config.TranslateWideTextToast,
                    ref config.UseImGuiForWideTextToast,
                    ref config.WideTextToastFontScale,
                    ref config.ImGuiWideTextToastWindowWidthMult,
                    ref config.ImGuiWideTextToastWindowPosCorrection,
                    ref config.OverlayWideTextToastTextColor,
                    ref config.WideTextToastBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
            case 2:
                changed |= DrawToastTypePage(
                    config,
                    Resources.ToastOverlayErrorSectionTitle,
                    ref config.TranslateErrorToast,
                    ref config.UseImGuiForErrorToast,
                    ref config.ErrorToastFontScale,
                    ref config.ImGuiErrorToastWindowWidthMult,
                    ref config.ImGuiErrorToastWindowPosCorrection,
                    ref config.OverlayErrorToastTextColor,
                    ref config.ErrorToastBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
            case 3:
                changed |= DrawToastTypePage(
                    config,
                    Resources.ToastOverlayAreaSectionTitle,
                    ref config.TranslateAreaToast,
                    ref config.UseImGuiForAreaToast,
                    ref config.AreaToastFontScale,
                    ref config.ImGuiAreaToastWindowWidthMult,
                    ref config.ImGuiAreaToastWindowPosCorrection,
                    ref config.OverlayAreaToastTextColor,
                    ref config.AreaToastBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
            case 4:
                changed |= DrawToastTypePage(
                    config,
                    Resources.ToastOverlayClassJobChangeSectionTitle,
                    ref config.TranslateClassChangeToast,
                    ref config.UseImGuiForClassChangeToast,
                    ref config.ClassChangeToastFontScale,
                    ref config.ImGuiClassChangeToastWindowWidthMult,
                    ref config.ImGuiClassChangeToastWindowPosCorrection,
                    ref config.OverlayClassChangeToastTextColor,
                    ref config.ClassChangeToastBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
            case 5:
                changed |= DrawToastTypePage(
                    config,
                    Resources.ToastOverlayTextGimmickHintSectionTitle,
                    ref config.TranslateTextGimmickHint,
                    ref config.UseImGuiForTextGimmickHint,
                    ref config.TextGimmickHintFontScale,
                    ref config.ImGuiTextGimmickHintWindowWidthMult,
                    ref config.ImGuiTextGimmickHintWindowPosCorrection,
                    ref config.OverlayTextGimmickHintTextColor,
                    ref config.TextGimmickHintBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
            case 6:
                changed |= DrawToastTypePage(
                    config,
                    Resources.ToastOverlayQuestSectionTitle,
                    ref config.TranslateQuestToast,
                    ref config.UseImGuiForQuestToast,
                    ref config.QuestToastFontScale,
                    ref config.ImGuiQuestToastWindowWidthMult,
                    ref config.ImGuiQuestToastWindowPosCorrection,
                    ref config.OverlayQuestToastTextColor,
                    ref config.QuestToastBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
        }

        ImGui.EndChild();

        return changed;
    }

    private static bool DrawToastGeneralPage(Config config)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.WhichToastsToTranslate);
        ImGui.Spacing();

        changed |= ImGui.Checkbox(
            Resources.TranslateScreenInfoToastToggleText,
            ref config.TranslateWideTextToast);
        changed |= ImGui.Checkbox(
            Resources.TranslateErrorToastToggleText,
            ref config.TranslateErrorToast);
        changed |= ImGui.Checkbox(
            Resources.TranslateAreaToastToggleText,
            ref config.TranslateAreaToast);
        changed |= ImGui.Checkbox(
            Resources.TranslateClassChangeToastToggleText,
            ref config.TranslateClassChangeToast);
        changed |= ImGui.Checkbox(
            Resources.TranslateTextGimmickHintToggleText,
            ref config.TranslateTextGimmickHint);
        changed |= ImGui.Checkbox(
            Resources.TranslateQuestToastToggleText,
            ref config.TranslateQuestToast);

        ImGui.Spacing();
        ImGui.TextWrapped(
            Resources.ToastModeDescription);
        ImGui.TextWrapped(
            Resources.ToastModeSwapDescription);

        if (config.OverlayOnlyLanguage)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(
                Resources.OverlayOnlyLanguageActiveAllToastTypesWillRenderThroughOverlays);
        }

        return changed;
    }

    private static bool DrawToastTypePage(
        Config config,
        string sectionTitle,
        ref bool isEnabled,
        ref bool useOverlay,
        ref float fontScale,
        ref float widthMult,
        ref Vector2 positionCorrection,
        ref Vector3 textColor,
        ref float backgroundOpacity,
        ref long fontChangeTime)
    {
        var changed = false;

        ImGui.TextUnformatted(sectionTitle);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            Resources.ToastOverlayEnableThisToastTypeLabel,
            ref isEnabled);

        if (!isEnabled)
        {
            return changed;
        }

        if (config.OverlayOnlyLanguage)
        {
            changed |= AssignIfChanged(ref useOverlay, true);
            changed |= AssignIfChanged(ref config.SwapTextsUsingImGui, false);
            ImGui.TextWrapped(
                Resources.OverlayOnlyLanguageActiveThisToastTypeWillRenderThroughAnOverlay);
        }
        else
        {
            changed |= ImGui.Checkbox(
                Resources.ToastOverlayUseOverlayForThisToastTypeLabel,
                ref useOverlay);
        }

        if (!config.OverlayOnlyLanguage && useOverlay)
        {
            changed |= ImGui.Checkbox(
                Resources.SwapTranslationTextToggle,
                ref config.SwapTextsUsingImGui);
        }

        if (!useOverlay)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(
                Resources.NativeReplacementModeIsActiveForThisToastTypeOverlayStyleControlsAreNotUsedInThisMode);
            return changed;
        }

        changed |= DrawToastOverlaySettings(
            ref fontScale,
            ref widthMult,
            ref positionCorrection,
            ref textColor,
            ref backgroundOpacity,
            ref fontChangeTime);

        return changed;
    }

    private static bool DrawSubtitleOverlay(Config config)
    {
        var changed = false;

        using var scrollingChildSubtitle = ImRaii.Child(
            "SubtitleOverlaySettings",
            new Vector2(-1, -1),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChildSubtitle)
        {
            return false;
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateTalkSubtitleToggleLabel,
            ref config.TranslateTalkSubtitle);

        if (!config.TranslateTalkSubtitle)
        {
            return changed;
        }

        if (config.OverlayOnlyLanguage)
        {
            changed |= AssignIfChanged(
                ref config.UseImGuiForTalkSubtitle,
                true);
        }
        else
        {
            changed |= ImGui.Checkbox(
                Resources.OverlayToggleLabel,
                ref config.UseImGuiForTalkSubtitle);
        }

        if (config.UseImGuiForTalkSubtitle)
        {
            changed |= DrawSubtitleOverlaySettings(
                ref config.TalkSubtitleFontScale,
                ref config.ImGuiTalkSubtitleWindowWidthMult,
                ref config.ImGuiTalkSubtitleWindowPosCorrection,
                ref config.OverlayTalkSubtitleTextColor,
                Resources.OverlayFontScaleLabel,
                ref config.FontChangeTime);
        }

        return changed;
    }

    /// <summary>
    ///     Draws the MiniTalk overlay settings without exposing a title bar
    ///     toggle. MiniTalk windows are intentionally titleless so the
    ///     configuration only controls style and placement.
    /// </summary>
    private static bool DrawMiniTalkOverlay(Config config)
    {
        var changed = false;

        using var scrollingChildMiniTalk = ImRaii.Child(
            "MiniTalkOverlaySettings",
            new Vector2(-1, -1),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChildMiniTalk)
        {
            return false;
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateMiniTalkLabel,
            ref config.TranslateMiniTalk);

        if (!config.TranslateMiniTalk)
        {
            return changed;
        }

        if (config.OverlayOnlyLanguage)
        {
            changed |= AssignIfChanged(
                ref config.UseImGuiForMiniTalk,
                true);
            changed |= AssignIfChanged(
                ref config.SwapTextsUsingImGui,
                false);
        }
        else
        {
            changed |= ImGui.Checkbox(
                Resources.OverlayToggleLabel,
                ref config.UseImGuiForMiniTalk);
        }

        if (config.UseImGuiForMiniTalk)
        {
            changed |= DrawToastOverlaySettings(
                ref config.MiniTalkFontScale,
                ref config.ImGuiMiniTalkWindowWidthMult,
                ref config.ImGuiMiniTalkWindowPosCorrection,
                ref config.OverlayMiniTalkTextColor,
                ref config.MiniTalkBackgroundOpacity,
                ref config.FontChangeTime);
        }

        if (!config.OverlayOnlyLanguage && config.UseImGuiForMiniTalk)
        {
            changed |= ImGui.Checkbox(
                Resources.SwapTranslationTextToggle,
                ref config.SwapTextsUsingImGui);
        }

        return changed;
    }

    /// <summary>
    ///     Draws the CutSceneSelectString overlay settings. The question becomes
    ///     the title bar and the options are rendered as a multiline body.
    /// </summary>
    private static bool DrawCutSceneSelectStringOverlay(Config config)
    {
        var changed = false;

        using var scrollingChildCutSceneSelectString = ImRaii.Child(
            "CutSceneSelectStringOverlaySettings",
            new Vector2(-1, -1),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChildCutSceneSelectString)
        {
            return false;
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateCutSceneSelectStringLabel,
            ref config.TranslateCutSceneSelectString);

        if (!config.TranslateCutSceneSelectString)
        {
            return changed;
        }

        if (config.OverlayOnlyLanguage)
        {
            changed |= AssignIfChanged(
                ref config.UseImGuiForCutSceneSelectString,
                true);
            changed |= AssignIfChanged(ref config.SwapTextsUsingImGui, false);
        }
        else
        {
            changed |= ImGui.Checkbox(
                Resources.OverlayToggleLabel,
                ref config.UseImGuiForCutSceneSelectString);
        }

        if (config.UseImGuiForCutSceneSelectString)
        {
            changed |= DrawToastOverlaySettings(
                ref config.CutSceneSelectStringFontScale,
                ref config.ImGuiCutSceneSelectStringWindowWidthMult,
                ref config.ImGuiCutSceneSelectStringWindowPosCorrection,
                ref config.OverlayCutSceneSelectStringTextColor,
                ref config.CutSceneSelectStringBackgroundOpacity,
                ref config.FontChangeTime);
            ImGui.TextWrapped(
                Resources.CutSceneSelectStringOverlayHelpText);
        }

        if (!config.OverlayOnlyLanguage &&
            config.UseImGuiForCutSceneSelectString)
        {
            changed |= ImGui.Checkbox(
                Resources.SwapTranslationTextToggle,
                ref config.SwapTextsUsingImGui);
        }

        return changed;
    }

    /// <summary>
    ///     Draws the TalkSubtitle overlay settings without exposing a title bar
    ///     toggle. TalkSubtitle windows are intentionally titleless so the
    ///     configuration only controls style and placement.
    /// </summary>
    private static bool DrawSubtitleOverlaySettings(
        ref float fontScale,
        ref float widthMult,
        ref Vector2 positionCorrection,
        ref Vector3 textColor,
        string fontScaleLabel,
        ref long fontChangeTime)
    {
        var changed = false;

        if (ImGui.SliderFloat(fontScaleLabel, ref fontScale, -3f, 3f, "%.2f"))
        {
            changed = true;
            fontChangeTime = DateTime.Now.Ticks;
        }

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
        }

        ImGui.Text(Resources.FontColorSelectLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            Resources.OverlayColorSelectName,
            ref textColor,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
        }

        changed |= ImGui.DragFloat(
            Resources.OverlayWidthScrollLabel,
            ref widthMult,
            0.001f,
            0.01f,
            3f);
        changed |= ImGui.DragFloat2(
            Resources.OverlayPositionAdjustmentLabel,
            ref positionCorrection);
        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
        }

        return changed;
    }

    /// <summary>
    ///     Draw settings for overlays that do not have a height adjustment.
    /// </summary>
    private static bool DrawOverlaySettings(
        ref float fontScale,
        ref float widthMult,
        ref Vector2 positionCorrection,
        ref Vector3 textColor,
        string fontScaleLabel,
        ref bool forceShowTitle,
        ref long fontChangeTime)
    {
        var changed = false;

        if (ImGui.SliderFloat(fontScaleLabel, ref fontScale, -3f, 3f, "%.2f"))
        {
            changed = true;
            fontChangeTime = DateTime.Now.Ticks;
        }

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
        }

        ImGui.Text(Resources.FontColorSelectLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            Resources.OverlayColorSelectName,
            ref textColor,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
        }

        changed |= ImGui.DragFloat(
            Resources.OverlayWidthScrollLabel,
            ref widthMult,
            0.001f,
            0.01f,
            3f);
        changed |= ImGui.DragFloat2(
            Resources.OverlayPositionAdjustmentLabel,
            ref positionCorrection);
        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
        }

        changed |= ImGui.Checkbox(
            Resources.OverlayForceShowTitleToggleLabel,
            ref forceShowTitle);

        return changed;
    }

    /// <summary>
    ///     Draws the configurable visual settings shared by toast overlays while
    ///     keeping their style independent per toast type.
    /// </summary>
    /// <param name="fontScale">The font scale used by the toast overlay.</param>
    /// <param name="widthMult">The width multiplier used by the toast overlay.</param>
    /// <param name="positionCorrection">
    ///     The X/Y position correction applied to the toast overlay.
    /// </param>
    /// <param name="textColor">The text color used by the toast overlay.</param>
    /// <param name="backgroundOpacity">
    ///     The background opacity used by the toast overlay.
    /// </param>
    /// <param name="fontChangeTime">
    ///     Timestamp used to invalidate/rebuild runtime font state when needed.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when any toast overlay setting changed;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    private static bool DrawToastOverlaySettings(
        ref float fontScale,
        ref float widthMult,
        ref Vector2 positionCorrection,
        ref Vector3 textColor,
        ref float backgroundOpacity,
        ref long fontChangeTime)
    {
        var changed = false;

        if (ImGui.SliderFloat(
                Resources.OverlayFontScaleLabel,
                ref fontScale,
                -3f,
                3f,
                "%.2f"))
        {
            changed = true;
            fontChangeTime = DateTime.Now.Ticks;
        }

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayFontSizeOrientations);
        }

        ImGui.Text(Resources.FontColorSelectLabel);
        ImGui.SameLine();
        changed |= ImGui.ColorEdit3(
            Resources.OverlayColorSelectName,
            ref textColor,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
        }

        changed |= ImGui.DragFloat(
            Resources.OverlayWidthScrollLabel,
            ref widthMult,
            0.001f,
            0.01f,
            3f);
        changed |= ImGui.DragFloat2(
            Resources.OverlayPositionAdjustmentLabel,
            ref positionCorrection);
        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
        }

        changed |= ImGui.SliderFloat(
            Resources.ToastOverlayBackgroundOpacityLabel,
            ref backgroundOpacity,
            0f,
            1f,
            "%.2f");

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                Resources.ToastOverlayBackgroundOpacityTooltip);
        }

        return changed;
    }

    private static bool AssignIfChanged<T>(ref T field, T value)
        where T : notnull
    {
        if (!field.Equals(value))
        {
            field = value;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Draws the global diacritics-removal toggle used by native replacement
    ///     flows across Talk, BattleTalk, Toasts, subtitles, and other handlers
    ///     that must fit within the game font limitations.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <returns>
    ///     <see langword="true" /> when the toggle value changed; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool DrawGlobalReplacementDiacriticsSetting(Config config)
    {
        return ImGui.Checkbox(
            Resources.RemoveDiacriticsToggle,
            ref config.RemoveDiacriticsWhenUsingReplacementTalkBTalk);
    }

    private static bool ShouldRemoveDiacritics(Config config)
    {
        var lang = config.Lang;
        return lang is 24 or 25 or 44 or 60 or 61 or 80 or 83 or 87 or 91 or 104
            or 105 or 109 or 110;
    }
}
