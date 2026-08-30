// <copyright file="PreviewScenarioCatalog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TranslationOverlay;

namespace Echoglossian.Previewer.Scenarios;

/// <summary>
/// Provides deterministic built-in preview scenarios and viewport presets.
/// </summary>
internal static class PreviewScenarioCatalog
{
    /// <summary>
    /// Gets the deterministic default viewport presets.
    /// </summary>
    internal static IReadOnlyList<PreviewViewportPreset> ViewportPresets { get; } =
    [
        new("1280x720", 1280, 720),
        new("1920x1080", 1920, 1080),
        new("2560x1440", 2560, 1440),
        new("3440x1440", 3440, 1440),
    ];

    /// <summary>
    /// Gets the deterministic default scenario list.
    /// </summary>
    internal static IReadOnlyList<PreviewScenario> Defaults { get; } =
    [
        Create("talk", "Talk", TranslationOverlaySurfaceId.Talk, 440, 690, 1040, 240, "The road opens before us. Keep your eyes on the horizon.", "Alphinaud"),
        Create("talk-arabic-274", "Talk Arabic #274", TranslationOverlaySurfaceId.Talk, 440, 690, 1040, 240, "أعتذر، ولكن أخشى أنني يجب أن أبعدك في الوقت الحالي. يرجى العودة في وقت لاحق.", "Alisaie"),
        Create("battle-talk", "Battle Talk", TranslationOverlaySurfaceId.BattleTalk, 360, 620, 1200, 210, "Steel yourself. The next strike will decide the field.", "Thancred"),
        Create("talk-subtitle", "Talk Subtitle", TranslationOverlaySurfaceId.TalkSubtitle, 300, 780, 1320, 120, "A gentle wind carries the sound of distant bells.", null),
        Create("mini-talk", "Mini Talk", TranslationOverlaySurfaceId.MiniTalk, 1260, 180, 420, 150, "This device is older than it looks.", "Y'shtola"),
        Create("cutscene-select-string", "Cutscene Select String", TranslationOverlaySurfaceId.CutSceneSelectString, 600, 410, 720, 260, "Choose a response that keeps the conversation moving.", "Select String"),
        Create("text-gimmick-hint", "Text Gimmick Hint", TranslationOverlaySurfaceId.TextGimmickHint, 660, 250, 600, 120, "Inspect the glowing mechanism.", null),
        Create("wide-text-toast", "Wide Text Toast", TranslationOverlaySurfaceId.WideTextToast, 560, 190, 800, 120, "A new path has opened nearby.", null),
        Create("error-toast", "Error Toast", TranslationOverlaySurfaceId.ErrorToast, 610, 150, 700, 110, "Unable to execute command at this time.", null),
        Create("area-toast", "Area Toast", TranslationOverlaySurfaceId.AreaToast, 540, 130, 840, 140, "The Lavender Beds", null),
        Create("class-change-toast", "Class Change Toast", TranslationOverlaySurfaceId.ClassChangeToast, 600, 145, 720, 130, "Changed to paladin.", null),
        Create("quest-toast", "Quest Toast", TranslationOverlaySurfaceId.QuestToast, 560, 170, 800, 150, "Quest accepted: Echoes Across the Rift", null),
        Create("chat-bubble", "Chat Bubble", TranslationOverlaySurfaceId.ChatBubble, 820, 360, 330, 110, "Over here, before the patrol returns.", "Scout"),
    ];

    /// <summary>
    /// Resolves a scenario by key, falling back to the first default.
    /// </summary>
    /// <param name="key">The requested scenario key.</param>
    /// <returns>The matching scenario.</returns>
    internal static PreviewScenario ResolveScenario(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var scenario in Defaults)
            {
                if (string.Equals(scenario.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return scenario;
                }
            }
        }

        return Defaults[0];
    }

    /// <summary>
    /// Resolves a viewport by key, falling back to 1920x1080.
    /// </summary>
    /// <param name="key">The requested viewport key.</param>
    /// <returns>The matching viewport preset.</returns>
    internal static PreviewViewportPreset ResolveViewport(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            foreach (var preset in ViewportPresets)
            {
                if (string.Equals(preset.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }
            }
        }

        return ViewportPresets[1];
    }

    /// <summary>
    /// Resolves a viewport by explicit dimensions, falling back to key lookup.
    /// </summary>
    /// <param name="width">The requested width.</param>
    /// <param name="height">The requested height.</param>
    /// <returns>The matching or ad hoc viewport preset.</returns>
    internal static PreviewViewportPreset ResolveViewport(int? width, int? height)
    {
        if (width is > 0 && height is > 0)
        {
            foreach (var preset in ViewportPresets)
            {
                if (preset.Width == width.Value && preset.Height == height.Value)
                {
                    return preset;
                }
            }

            return new PreviewViewportPreset($"{width.Value}x{height.Value}", width.Value, height.Value);
        }

        return ResolveViewport(null);
    }

    private static PreviewScenario Create(
        string key,
        string displayName,
        TranslationOverlaySurfaceId surfaceId,
        float x,
        float y,
        float width,
        float height,
        string translatedText,
        string? title)
    {
        return new PreviewScenario(
            key,
            displayName,
            surfaceId,
            new PreviewAddonBounds(x, y, width, height),
            translatedText,
            title,
            Visible: true,
            ShowsSimulatedAddonBounds: false);
    }
}
