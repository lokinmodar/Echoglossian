// <copyright file="PreviewScenarioCatalogTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Scenarios;
using Echoglossian.UIOverlays.TranslationOverlay;

using Xunit;

namespace Echoglossian.Previewer.Tests.Scenarios;

/// <summary>
/// Covers the deterministic preview scenario catalog.
/// </summary>
public sealed class PreviewScenarioCatalogTests
{
    /// <summary>
    /// Ensures every runtime overlay surface has at least one default preview scenario.
    /// </summary>
    [Fact]
    public void Defaults_ContainsScenarioForEachRuntimeOverlaySurface()
    {
        var expectedSurfaces = new[]
        {
            TranslationOverlaySurfaceId.Talk,
            TranslationOverlaySurfaceId.BattleTalk,
            TranslationOverlaySurfaceId.TalkSubtitle,
            TranslationOverlaySurfaceId.MiniTalk,
            TranslationOverlaySurfaceId.CutSceneSelectString,
            TranslationOverlaySurfaceId.TextGimmickHint,
            TranslationOverlaySurfaceId.WideTextToast,
            TranslationOverlaySurfaceId.ErrorToast,
            TranslationOverlaySurfaceId.AreaToast,
            TranslationOverlaySurfaceId.ClassChangeToast,
            TranslationOverlaySurfaceId.QuestToast,
            TranslationOverlaySurfaceId.ChatBubble,
        };

        var scenarios = PreviewScenarioCatalog.Defaults;

        foreach (var surface in expectedSurfaces)
        {
            var scenario = scenarios.FirstOrDefault(
                candidate => candidate.SurfaceId == surface);
            Assert.NotNull(scenario);
            Assert.False(string.IsNullOrWhiteSpace(scenario.Key));
            Assert.True(scenario.Visible);
            Assert.False(string.IsNullOrWhiteSpace(scenario.TranslatedText));
            Assert.True(scenario.AddonBounds.Width > 0f);
            Assert.True(scenario.AddonBounds.Height > 0f);
        }
    }

    /// <summary>
    /// Ensures the issue #274 Arabic Talk scenario remains available and uses
    /// stable preview bounds.
    /// </summary>
    [Fact]
    public void ResolveScenario_Issue274Arabic_ReturnsTalkScenarioWithArabicText()
    {
        var scenario = PreviewScenarioCatalog.ResolveScenario("talk-arabic-274");

        Assert.Equal("talk-arabic-274", scenario.Key);
        Assert.Equal(TranslationOverlaySurfaceId.Talk, scenario.SurfaceId);
        Assert.Contains('أ', scenario.TranslatedText);
        Assert.True(scenario.AddonBounds.Width > 0f);
        Assert.True(scenario.AddonBounds.Height > 0f);
    }

    /// <summary>
    /// Ensures viewport presets match the stable Task 6 logical resolutions.
    /// </summary>
    [Fact]
    public void ViewportPresets_ContainsRequiredLogicalResolutions()
    {
        var presets = PreviewScenarioCatalog.ViewportPresets;

        Assert.Collection(
            presets,
            preset => Assert.Equal((1280, 720), (preset.Width, preset.Height)),
            preset => Assert.Equal((1920, 1080), (preset.Width, preset.Height)),
            preset => Assert.Equal((2560, 1440), (preset.Width, preset.Height)),
            preset => Assert.Equal((3440, 1440), (preset.Width, preset.Height)));
    }

    /// <summary>
    /// Ensures scenario and viewport lookups are case-insensitive and stable.
    /// </summary>
    [Fact]
    public void Resolve_UsesCaseInsensitiveScenarioAndViewportKeys()
    {
        var scenario = PreviewScenarioCatalog.ResolveScenario("TALK");
        var viewport = PreviewScenarioCatalog.ResolveViewport("1920X1080");

        Assert.Equal("talk", scenario.Key);
        Assert.Equal(TranslationOverlaySurfaceId.Talk, scenario.SurfaceId);
        Assert.Equal((1920, 1080), (viewport.Width, viewport.Height));
    }
}
