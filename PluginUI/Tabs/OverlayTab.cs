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
        Resources.QuestWindowsTabTitle,
        Resources.SelectionDialogsTabTitle,
        Resources.GameWindowsTabTitle,
        Resources.TooltipTabTitle,
        Resources.NamePlateTabTitle,
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

    private static readonly string[] OverlayDisplayModes =
    {
        Resources.QuestDisplayModeNativeUiTranslation,
        Resources.OverlayDisplayModeOverlayTranslationOnly,
        Resources.OverlayDisplayModeNativeUiTranslationWithOriginalOverlay,
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

        ImGui.BeginChild("overlay_tab_left", new Vector2(185, 0), true);
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
                changed |= JournalTab.Draw(config);
                break;
            case 7:
                changed |= QuestWindowsTab.Draw(config);
                break;
            case 8:
                changed |= SelectionDialogsTab.Draw(config);
                break;
            case 9:
                changed |= GameWindowsTab.Draw(config);
                break;
            case 10:
                changed |= TooltipTab.Draw(config);
                break;
            case 11:
                changed |= DrawNamePlateOverlay(config);
                break;
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

        changed |= DrawOverlayDisplayModeCombo(
            config,
            "TalkDisplayMode",
            ref config.TalkTranslationDisplayMode);

        changed |= ImGui.Checkbox(
            Resources.TranslateNpcNamesToggle,
            ref config.TranslateTalkNpcNames);

        if (ShouldDrawOverlaySettings(
                config.TalkTranslationDisplayMode,
                config.OverlayOnlyLanguage))
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

        changed |= DrawOverlayDisplayModeCombo(
            config,
            "BattleTalkDisplayMode",
            ref config.BattleTalkTranslationDisplayMode);

        changed |= ImGui.Checkbox(
            Resources.TranslateNpcNamesToggle,
            ref config.TranslateBattleTalkNpcNames);

        if (ShouldDrawOverlaySettings(
                config.BattleTalkTranslationDisplayMode,
                config.OverlayOnlyLanguage))
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
                changed |= NativeUI.AddonHandlers.Toasts.ToastGuiSupportedToastPolicy
                    .UseSupportedNormalToastRuntime(config)
                    ? DrawSupportedNormalToastPlacementPage(config)
                    : DrawToastTypePage(
                        config,
                        Resources.ToastOverlayScreenInfoWideTextSectionTitle,
                        ref config.TranslateWideTextToast,
                        ref config.WideTextToastTranslationDisplayMode,
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
                    ref config.ErrorToastTranslationDisplayMode,
                    ref config.ErrorToastFontScale,
                    ref config.ImGuiErrorToastWindowWidthMult,
                    ref config.ImGuiErrorToastWindowPosCorrection,
                    ref config.OverlayErrorToastTextColor,
                    ref config.ErrorToastBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
            case 3:
                changed |= NativeUI.AddonHandlers.Toasts.ToastGuiSupportedToastPolicy
                    .UseSupportedNormalToastRuntime(config)
                    ? DrawSupportedNormalToastMemberPage(
                        Resources.ToastOverlayAreaSectionTitle,
                        ref config.TranslateAreaToast)
                    : DrawToastTypePage(
                        config,
                        Resources.ToastOverlayAreaSectionTitle,
                        ref config.TranslateAreaToast,
                        ref config.AreaToastTranslationDisplayMode,
                        ref config.AreaToastFontScale,
                        ref config.ImGuiAreaToastWindowWidthMult,
                        ref config.ImGuiAreaToastWindowPosCorrection,
                        ref config.OverlayAreaToastTextColor,
                        ref config.AreaToastBackgroundOpacity,
                        ref config.FontChangeTime);
                break;
            case 4:
                changed |= NativeUI.AddonHandlers.Toasts.ToastGuiSupportedToastPolicy
                    .UseSupportedNormalToastRuntime(config)
                    ? DrawSupportedNormalToastMemberPage(
                        Resources.ToastOverlayClassJobChangeSectionTitle,
                        ref config.TranslateClassChangeToast)
                    : DrawToastTypePage(
                        config,
                        Resources.ToastOverlayClassJobChangeSectionTitle,
                        ref config.TranslateClassChangeToast,
                        ref config.ClassChangeToastTranslationDisplayMode,
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
                    ref config.TextGimmickHintTranslationDisplayMode,
                    ref config.TextGimmickHintFontScale,
                    ref config.ImGuiTextGimmickHintWindowWidthMult,
                    ref config.ImGuiTextGimmickHintWindowPosCorrection,
                    ref config.OverlayTextGimmickHintTextColor,
                    ref config.TextGimmickHintBackgroundOpacity,
                    ref config.FontChangeTime);
                break;
            case 6:
                changed |= DrawQuestToastPlacementPage(config);
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
#if DEBUG
        var normalRouteState =
            NativeUI.AddonHandlers.Toasts.ToastGuiSupportedToastPolicy
                .GetSupportedNormalToastRouteState(config);
        var errorRouteState =
            NativeUI.AddonHandlers.Toasts.ToastGuiSupportedToastPolicy
                .GetSupportedErrorToastRouteState(config);
        ImGui.TextWrapped(
            Resources.ToastGuiNormalToastRouteStatusLabel + ": " +
            GetToastGuiRouteStateText(normalRouteState));
        ImGui.TextWrapped(
            Resources.ToastGuiErrorToastRouteStatusLabel + ": " +
            GetToastGuiRouteStateText(errorRouteState));
        ImGui.TextWrapped(
            Resources.ToastGuiQuestToastRouteStatusLabel + ": " +
            Resources.ToastGuiRouteStateFullRuntime);
        ImGui.TextWrapped(
            Resources.ToastGuiTextGimmickHintRouteStatusLabel + ": " +
            Resources.ToastGuiRouteStateAddonHandlerOnly);
        ImGui.Spacing();
#endif

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

    /// <summary>
    ///     Maps one effective toast route state to the localized UI label used
    ///     in the toast general page.
    /// </summary>
    /// <param name="routeState">The effective route state.</param>
    /// <returns>The localized UI label for the route state.</returns>
#if DEBUG
    private static string GetToastGuiRouteStateText(
        NativeUI.AddonHandlers.Toasts.ToastGuiRouteState routeState)
    {
        return routeState switch
        {
            NativeUI.AddonHandlers.Toasts.ToastGuiRouteState.ToastGuiFullRuntime =>
                Resources.ToastGuiRouteStateFullRuntime,
            NativeUI.AddonHandlers.Toasts.ToastGuiRouteState.ToastGuiCapturePrefetch =>
                Resources.ToastGuiRouteStateCapturePrefetch,
            _ => Resources.ToastGuiRouteStateLegacyAddonHandlers,
        };
    }
#endif

    private static bool DrawToastTypePage(
        Config config,
        string sectionTitle,
        ref bool isEnabled,
        ref JournalTranslationDisplayMode displayMode,
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

        changed |= DrawOverlayDisplayModeCombo(
            config,
            sectionTitle,
            ref displayMode);

        if (!ShouldDrawOverlaySettings(
                displayMode,
                config.OverlayOnlyLanguage))
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

    private static bool DrawSupportedNormalToastPlacementPage(Config config)
    {
        var changed = false;

        ImGui.TextUnformatted(Resources.ToastOverlayScreenInfoWideTextSectionTitle);
        ImGui.Separator();
        ImGui.TextWrapped(Resources.ToastOverlaySupportedNormalPlacementDescription);
        ImGui.Spacing();

        changed |= ImGui.Checkbox(
            Resources.TranslateScreenInfoToastToggleText,
            ref config.TranslateWideTextToast);

        if (ImGui.SliderFloat(
                Resources.OverlayFontScaleLabel,
                ref config.WideTextToastFontScale,
                0.25f,
                3f,
                "%.2f"))
        {
            changed = true;
            config.FontChangeTime = DateTime.Now.Ticks;
        }

        changed |= DrawToastPlacementBucket(
            config,
            Resources.ToastOverlayTopPlacementTitle,
            ref config.TopToastTranslationDisplayMode,
            ref config.ImGuiTopToastWindowWidthMult,
            ref config.ImGuiTopToastWindowPosCorrection,
            ref config.OverlayTopToastTextColor,
            ref config.TopToastBackgroundOpacity,
            config.WideTextToastFontScale,
            ref config.FontChangeTime);
        changed |= DrawToastPlacementBucket(
            config,
            Resources.ToastOverlayBottomPlacementTitle,
            ref config.BottomToastTranslationDisplayMode,
            ref config.ImGuiBottomToastWindowWidthMult,
            ref config.ImGuiBottomToastWindowPosCorrection,
            ref config.OverlayBottomToastTextColor,
            ref config.BottomToastBackgroundOpacity,
            config.WideTextToastFontScale,
            ref config.FontChangeTime);

        return changed;
    }

    private static bool DrawSupportedNormalToastMemberPage(
        string sectionTitle,
        ref bool isEnabled)
    {
        var changed = false;

        ImGui.TextUnformatted(sectionTitle);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            Resources.ToastOverlayEnableThisToastTypeLabel,
            ref isEnabled);

        if (isEnabled)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(
                Resources.ToastOverlaySupportedNormalSharedSettingsMessage);
        }

        return changed;
    }

    private static bool DrawQuestToastPlacementPage(Config config)
    {
        var changed = false;

        ImGui.TextUnformatted(Resources.ToastOverlayQuestSectionTitle);
        ImGui.Separator();

        changed |= ImGui.Checkbox(
            Resources.ToastOverlayEnableThisToastTypeLabel,
            ref config.TranslateQuestToast);

        if (!config.TranslateQuestToast)
        {
            return changed;
        }

        if (ImGui.SliderFloat(
                Resources.OverlayFontScaleLabel,
                ref config.QuestToastFontScale,
                0.25f,
                3f,
                "%.2f"))
        {
            changed = true;
            config.FontChangeTime = DateTime.Now.Ticks;
        }

        changed |= DrawToastPlacementBucket(
            config,
            Resources.ToastOverlayLeftPlacementTitle,
            ref config.QuestToastLeftTranslationDisplayMode,
            ref config.ImGuiQuestToastLeftWindowWidthMult,
            ref config.ImGuiQuestToastLeftWindowPosCorrection,
            ref config.OverlayQuestToastLeftTextColor,
            ref config.QuestToastLeftBackgroundOpacity,
            config.QuestToastFontScale,
            ref config.FontChangeTime);
        changed |= DrawToastPlacementBucket(
            config,
            Resources.ToastOverlayCentrePlacementTitle,
            ref config.QuestToastCentreTranslationDisplayMode,
            ref config.ImGuiQuestToastCentreWindowWidthMult,
            ref config.ImGuiQuestToastCentreWindowPosCorrection,
            ref config.OverlayQuestToastCentreTextColor,
            ref config.QuestToastCentreBackgroundOpacity,
            config.QuestToastFontScale,
            ref config.FontChangeTime);
        changed |= DrawToastPlacementBucket(
            config,
            Resources.ToastOverlayRightPlacementTitle,
            ref config.QuestToastRightTranslationDisplayMode,
            ref config.ImGuiQuestToastRightWindowWidthMult,
            ref config.ImGuiQuestToastRightWindowPosCorrection,
            ref config.OverlayQuestToastRightTextColor,
            ref config.QuestToastRightBackgroundOpacity,
            config.QuestToastFontScale,
            ref config.FontChangeTime);

        return changed;
    }

    private static bool DrawToastPlacementBucket(
        Config config,
        string sectionTitle,
        ref JournalTranslationDisplayMode displayMode,
        ref float widthMult,
        ref Vector2 positionCorrection,
        ref Vector3 textColor,
        ref float backgroundOpacity,
        float sharedFontScale,
        ref long fontChangeTime)
    {
        var changed = false;

        using var bucketId = ImRaii.PushId(
            BuildToastPlacementBucketId(sectionTitle));

        ImGui.Spacing();
        ImGui.TextUnformatted(sectionTitle);
        ImGui.Separator();

        changed |= DrawOverlayDisplayModeCombo(
            config,
            sectionTitle,
            ref displayMode);

        if (!ShouldDrawOverlaySettings(
                displayMode,
                config.OverlayOnlyLanguage))
        {
            ImGui.TextWrapped(
                Resources.NativeReplacementModeIsActiveForThisToastTypeOverlayStyleControlsAreNotUsedInThisMode);
            return changed;
        }

        var placementFontScale = sharedFontScale;
        changed |= DrawToastOverlaySettings(
            ref placementFontScale,
            ref widthMult,
            ref positionCorrection,
            ref textColor,
            ref backgroundOpacity,
            ref fontChangeTime);

        return changed;
    }

    /// <summary>
    ///     Builds a stable ImGui ID scope for one toast placement bucket.
    /// </summary>
    /// <param name="sectionTitle">The user-facing placement section title.</param>
    /// <returns>The unique ImGui ID scope for the placement bucket.</returns>
    internal static string BuildToastPlacementBucketId(string sectionTitle)
    {
        return $"ToastPlacementBucket:{sectionTitle}";
    }

    private static bool DrawNamePlateOverlay(Config config)
    {
        var changed = false;

        using var scrollingChildNamePlate = ImRaii.Child(
            "NamePlateOverlaySettings",
            new Vector2(-1, -1),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChildNamePlate)
        {
            return false;
        }

        changed |= ImGui.Checkbox(
            Resources.TranslateNamePlatesLabel,
            ref config.TranslateNamePlates);

        if (!config.TranslateNamePlates)
        {
            return changed;
        }

        ImGui.TextWrapped(Resources.NamePlateOverlayHelpText);

        changed |= DrawOverlayDisplayModeCombo(
            config,
            "NamePlateDisplayMode",
            ref config.NamePlateTranslationDisplayMode);

        ImGui.Spacing();
        changed |= ImGui.Checkbox(
            Resources.EnableDistanceAwareOverlaysLabel,
            ref config.EnableDistanceAwareOverlays);
        changed |= ImGui.SliderFloat(
            Resources.DistanceAwareOverlayFullScaleDistanceLabel,
            ref config.DistanceAwareOverlayFullScaleDistance,
            1f,
            40f,
            "%.1f");
        changed |= ImGui.SliderFloat(
            Resources.DistanceAwareOverlayFadeStartDistanceLabel,
            ref config.DistanceAwareOverlayFadeStartDistance,
            1f,
            60f,
            "%.1f");
        changed |= ImGui.SliderFloat(
            Resources.DistanceAwareOverlayMaxDistanceLabel,
            ref config.DistanceAwareOverlayMaxDistance,
            1f,
            80f,
            "%.1f");
        changed |= ImGui.SliderFloat(
            Resources.DistanceAwareOverlayMinScaleLabel,
            ref config.DistanceAwareOverlayMinScale,
            0.25f,
            1f,
            "%.2f");

        if (ShouldDrawOverlaySettings(
                config.NamePlateTranslationDisplayMode,
                config.OverlayOnlyLanguage))
        {
            changed |= DrawToastOverlaySettings(
                ref config.NamePlateFontScale,
                ref config.ImGuiNamePlateWindowWidthMult,
                ref config.ImGuiNamePlateWindowPosCorrection,
                ref config.OverlayNamePlateTextColor,
                ref config.NamePlateBackgroundOpacity,
                ref config.FontChangeTime);
        }

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

        changed |= DrawOverlayDisplayModeCombo(
            config,
            "TalkSubtitleDisplayMode",
            ref config.TalkSubtitleTranslationDisplayMode);

        if (ShouldDrawOverlaySettings(
                config.TalkSubtitleTranslationDisplayMode,
                config.OverlayOnlyLanguage))
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

        changed |= DrawOverlayDisplayModeCombo(
            config,
            "MiniTalkDisplayMode",
            ref config.MiniTalkTranslationDisplayMode);

        if (ShouldDrawOverlaySettings(
                config.MiniTalkTranslationDisplayMode,
                config.OverlayOnlyLanguage))
        {
            changed |= DrawToastOverlaySettings(
                ref config.MiniTalkFontScale,
                ref config.ImGuiMiniTalkWindowWidthMult,
                ref config.ImGuiMiniTalkWindowPosCorrection,
                ref config.OverlayMiniTalkTextColor,
                ref config.MiniTalkBackgroundOpacity,
                ref config.FontChangeTime);
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

        changed |= DrawOverlayDisplayModeCombo(
            config,
            "CutSceneSelectStringDisplayMode",
            ref config.CutSceneSelectStringTranslationDisplayMode);

        if (ShouldDrawOverlaySettings(
                config.CutSceneSelectStringTranslationDisplayMode,
                config.OverlayOnlyLanguage))
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

        if (ImGui.SliderFloat(fontScaleLabel, ref fontScale, 0.25f, 3f, "%.2f"))
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

        if (ImGui.SliderFloat(fontScaleLabel, ref fontScale, 0.25f, 3f, "%.2f"))
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
                0.25f,
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

    /// <summary>
    ///     Draws the shared display-mode selector used by legacy overlay-based
    ///     surfaces.
    /// </summary>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="comboId">The unique combo identifier.</param>
    /// <param name="displayMode">The display mode to edit.</param>
    /// <returns><see langword="true" /> when the selection changed.</returns>
    private static bool DrawOverlayDisplayModeCombo(
        Config config,
        string comboId,
        ref JournalTranslationDisplayMode displayMode)
    {
        return TranslationDisplayModeUiHelper.DrawDisplayModeCombo(
            comboId,
            ref displayMode,
            config.OverlayOnlyLanguage,
            description: Resources.OverlayDisplayModeDescription,
            modeLabels: OverlayDisplayModes);
    }

    /// <summary>
    ///     Determines whether overlay-specific style controls should be shown
    ///     for the provided display mode.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">
    ///     Whether the selected language only supports overlay rendering.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the overlay path is active.
    /// </returns>
    private static bool ShouldDrawOverlaySettings(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage)
    {
        return TranslationDisplayModeHelper.UsesOverlayPresentation(
            displayMode,
            overlayOnlyLanguage);
    }

}


