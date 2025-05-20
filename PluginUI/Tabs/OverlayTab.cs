using ImGuiNET;
using System;
using System.Numerics;

namespace Echoglossian.Tabs;

/// <summary>
/// Renders the configuration settings related to overlay-based translations using vertical tabs.
/// </summary>
public static class OverlayTab
{
    private static int selectedOverlayTab = 0;

    private static readonly string[] OverlayTabs =
    {
        Resources.TalkTabTitle,
        Resources.BattleTalkTabTitle,
        Resources.ToastTabTitle,
        Resources.SubtitleTabTitle
    };

    public static bool Draw(Config config)
    {
        bool changed = false;

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
        }

        ImGui.EndChild();
        return changed;
    }

    private static bool DrawTalkOverlay(Config config)
    {
        bool changed = false;

        changed |= ImGui.Checkbox(Resources.TranslateTalkToggleLabel, ref config.TranslateTalk);

        if (!config.TranslateTalk)
            return changed;

        if (config.OverlayOnlyLanguage)
        {
            changed |= AssignIfChanged(ref config.UseImGuiForTalk, true);
            changed |= AssignIfChanged(ref config.SwapTextsUsingImGui, false);
        }
        else
        {
            changed |= ImGui.Checkbox(Resources.OverlayToggleLabel, ref config.UseImGuiForTalk);
        }

        changed |= ImGui.Checkbox(Resources.TranslateNpcNamesToggle, ref config.TranslateNpcNames);

        if (config.UseImGuiForTalk)
        {
            changed |= DrawOverlaySettings(
                ref config.FontScale,
                ref config.ImGuiTalkWindowWidthMult,
                ref config.ImGuiTalkWindowHeightMult,
                ref config.ImGuiWindowPosCorrection,
                ref config.OverlayTalkTextColor,
                Resources.OverlayFontScaleLabel,
                ref config.FontChangeTime
            );
        }

        if (!config.OverlayOnlyLanguage && config.UseImGuiForTalk)
        {
            changed |= ImGui.Checkbox(Resources.SwapTranslationTextToggle, ref config.SwapTextsUsingImGui);

            if (config.SwapTextsUsingImGui && ShouldRemoveDiacritics(config))
            {
                changed |= ImGui.Checkbox(Resources.RemoveDiacriticsToggle, ref config.RemoveDiacriticsWhenUsingReplacementTalkBTalk);
            }
        }

        return changed;
    }

    private static bool DrawBattleTalkOverlay(Config config)
    {
        bool changed = false;

        changed |= ImGui.Checkbox(Resources.TransLateBattletalkToggle, ref config.TranslateBattleTalk);

        if (!config.TranslateBattleTalk)
            return changed;

        changed |= ImGui.Checkbox(Resources.OverlayToggleLabel, ref config.UseImGuiForBattleTalk);

        if (config.UseImGuiForBattleTalk)
        {
            changed |= DrawOverlaySettings(
                ref config.BattleTalkFontScale,
                ref config.ImGuiBattleTalkWindowWidthMult,
                ref config.ImGuiBattleTalkWindowHeightMult,
                ref config.ImGuiBattleTalkWindowPosCorrection,
                ref config.OverlayBattleTalkTextColor,
                Resources.OverlayFontScaleLabel,
                ref config.FontChangeTime
            );
        }

        return changed;
    }

    private static bool DrawToastOverlay(Config config)
    {
        bool changed = false;

        changed |= ImGui.Checkbox(Resources.TranslateToastToggle, ref config.TranslateToast);

        if (!config.TranslateToast)
            return changed;

        changed |= ImGui.Checkbox(Resources.OverlayToggleLabel, ref config.UseImGuiForToasts);

        if (config.UseImGuiForToasts)
        {
            changed |= DrawOverlaySettings(
                ref config.ToastFontScale,
                ref config.ImGuiToastWindowWidthMult,
                null,
                ref config.ImGuiToastWindowPosCorrection,
                ref config.OverlayToastTextColor,
                Resources.OverlayFontScaleLabel,
                ref config.FontChangeTime
            );
        }

        return changed;
    }

    private static bool DrawSubtitleOverlay(Config config)
    {
        bool changed = false;

        changed |= ImGui.Checkbox(Resources.TranslateTalkSubtitleToggleLabel, ref config.TranslateTalkSubtitle);

        if (!config.TranslateTalkSubtitle)
            return changed;

        changed |= ImGui.Checkbox(Resources.OverlayToggleLabel, ref config.UseImGuiForTalkSubtitle);

        if (config.UseImGuiForTalkSubtitle)
        {
            changed |= DrawOverlaySettings(
                ref config.TalkSubtitleFontScale,
                ref config.ImGuiTalkSubtitleWindowWidthMult,
                ref config.ImGuiTalkSubtitleWindowHeightMult,
                ref config.ImGuiTalkSubtitleWindowPosCorrection,
                ref config.OverlayTalkSubtitleTextColor,
                Resources.OverlayFontScaleLabel,
                ref config.FontChangeTime
            );
        }

        return changed;
    }

    private static bool DrawOverlaySettings(
        ref float fontScale,
        ref float widthMult,
        ref float? heightMult,
        ref Vector2 positionCorrection,
        ref Vector3 textColor,
        string fontScaleLabel,
        ref long fontChangeTime)
    {
        bool changed = false;

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
        changed |= ImGui.ColorEdit3(Resources.OverlayColorSelectName, ref textColor,
            ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayFontColorOrientations);
        }

        changed |= ImGui.DragFloat(Resources.OverlayWidthScrollLabel, ref widthMult, 0.001f, 0.01f, 3f);

        if (heightMult is not null)
        {
            changed |= ImGui.DragFloat(Resources.OverlayHeightScrollLabel, ref heightMult.Value, 0.001f, 0.01f, 3f);
        }

        changed |= ImGui.DragFloat2(Resources.OverlayPositionAdjustmentLabel, ref positionCorrection);
        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.OverlayAdjustmentOrientations);
        }

        return changed;
    }

    private static bool AssignIfChanged<T>(ref T field, T value) where T : notnull
    {
        if (!field.Equals(value))
        {
            field = value;
            return true;
        }

        return false;
    }

    private static bool ShouldRemoveDiacritics(Config config)
    {
        int lang = config.Lang;
        return lang is 24 or 25 or 44 or 60 or 61 or 80 or 83 or 87 or 91 or 104 or 105 or 109 or 110;
    }
}