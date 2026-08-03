// <copyright file="TranslationWindowConfigTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;

using Dalamud.Game.Gui.Toast;

using Echoglossian.UIOverlays.TranslationOverlay;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers translation overlay configuration selection by surface identifier.
/// </summary>
public class TranslationWindowConfigTests
{
    /// <summary>
    /// Ensures each active overlay surface delegates to its existing named
    /// configuration factory.
    /// </summary>
    /// <param name="surfaceId">The surface to resolve.</param>
    /// <param name="expectedFactory">The existing factory expected to be used.</param>
    [Theory]
    [MemberData(nameof(ActiveSurfaceFactories))]
    public void ForSurface_ActiveSurface_DelegatesToExistingFactory(
        object surfaceId,
        object expectedFactory)
    {
        var config = new Config();
        var typedSurfaceId = Assert.IsType<TranslationOverlaySurfaceId>(surfaceId);
        var typedExpectedFactory = Assert.IsType<Func<Config, TranslationWindowConfig>>(
            expectedFactory);

        var actual = TranslationWindowConfig.ForSurface(config, typedSurfaceId);

        Assert.Equal(typedExpectedFactory(config), actual);
    }

    /// <summary>
    /// Ensures the tooltip-only surfaces remain explicitly unavailable to the
    /// overlay renderer until their later preview phases.
    /// </summary>
    /// <param name="surfaceId">The unsupported tooltip surface.</param>
    [Theory]
    [InlineData((int)TranslationOverlaySurfaceId.ActionDetail)]
    [InlineData((int)TranslationOverlaySurfaceId.ItemDetail)]
    public void ForSurface_TooltipOnlySurface_ThrowsNotSupportedException(
        int surfaceId)
    {
        Assert.Throws<NotSupportedException>(
            () => TranslationWindowConfig.ForSurface(
                new Config(),
                (TranslationOverlaySurfaceId)surfaceId));
    }

    /// <summary>
    /// Ensures callers get an immediate argument error for a missing config.
    /// </summary>
    [Fact]
    public void ForSurface_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => TranslationWindowConfig.ForSurface(
                null!,
                TranslationOverlaySurfaceId.Talk));
    }

    /// <summary>
    ///     Ensures the Tooltip addon anchored-overlay surface resolves through
    ///     its dedicated configuration factory.
    /// </summary>
    [Fact]
    public void TranslationWindowConfig_ForSurfaceSupportsTooltipAddon()
    {
        var config = new Config();

        var windowConfig = TranslationWindowConfig.ForSurface(
            config,
            TranslationOverlaySurfaceId.TooltipAddon);

        Assert.Equal(TranslationOverlaySurfaceId.TooltipAddon, windowConfig.SurfaceId);
    }

    /// <summary>
    ///     Ensures the callback-owned normal-toast placement factories use the
    ///     placement-specific overrides instead of the legacy shared family
    ///     config.
    /// </summary>
    [Fact]
    public void FromConfigForSupportedNormalToastPlacement_UsesBottomPlacementOverrides()
    {
        var config = new Config
        {
            WideTextToastFontScale = 1.5f,
            ImGuiBottomToastWindowWidthMult = 1.8f,
            ImGuiBottomToastWindowPosCorrection = new Vector2(12f, -8f),
            OverlayBottomToastTextColor = new Vector3(0.2f, 0.4f, 0.6f),
            BottomToastBackgroundOpacity = 0.35f,
        };

        var actual = TranslationWindowConfig.FromConfigForSupportedNormalToastPlacement(
            config,
            ToastPosition.Bottom);

        Assert.Equal(TranslationOverlaySurfaceId.WideTextToast, actual.SurfaceId);
        Assert.Equal(1.5f, actual.FontScale);
        Assert.Equal(1.8f, actual.WidthMultiplier);
        Assert.Equal(new Vector2(12f, -8f), actual.PosCorrection);
        Assert.Equal(new Vector4(0.2f, 0.4f, 0.6f, 1.0f), actual.TextColor);
        Assert.Equal(0.35f, actual.BackgroundOpacity);
    }

    /// <summary>
    ///     Ensures the quest-toast placement factories use the per-placement
    ///     overrides for the active quest alignment bucket.
    /// </summary>
    [Fact]
    public void FromConfigForQuestToastPlacement_UsesRightPlacementOverrides()
    {
        var config = new Config
        {
            QuestToastFontScale = 1.25f,
            ImGuiQuestToastRightWindowWidthMult = 1.6f,
            ImGuiQuestToastRightWindowPosCorrection = new Vector2(-16f, 4f),
            OverlayQuestToastRightTextColor = new Vector3(0.9f, 0.8f, 0.1f),
            QuestToastRightBackgroundOpacity = 0.6f,
        };

        var actual = TranslationWindowConfig.FromConfigForQuestToastPlacement(
            config,
            QuestToastPosition.Right);

        Assert.Equal(TranslationOverlaySurfaceId.QuestToast, actual.SurfaceId);
        Assert.Equal(1.25f, actual.FontScale);
        Assert.Equal(1.6f, actual.WidthMultiplier);
        Assert.Equal(new Vector2(-16f, 4f), actual.PosCorrection);
        Assert.Equal(new Vector4(0.9f, 0.8f, 0.1f, 1.0f), actual.TextColor);
        Assert.Equal(0.6f, actual.BackgroundOpacity);
    }

    /// <summary>
    /// Gets the existing factory expected for each runtime overlay surface.
    /// </summary>
    public static IEnumerable<object[]> ActiveSurfaceFactories()
    {
        yield return
        [
            TranslationOverlaySurfaceId.Talk,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForTalk,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.BattleTalk,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForBattleTalk,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.TalkSubtitle,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigTalkSubtitle,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.MiniTalk,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForMiniTalk,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.CutSceneSelectString,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForCutSceneSelectString,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.SelectYesNo,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForSelectYesNo,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.SelectOk,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForSelectOk,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.SelectString,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForSelectString,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.TextGimmickHint,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForTextGimmickHint,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.WideTextToast,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForWideTextToast,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.ErrorToast,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForErrorToast,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.AreaToast,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForAreaToast,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.ClassChangeToast,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForClassChangeToast,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.QuestToast,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForQuestToast,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.ChatBubble,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForChatBubble,
        ];
        yield return
        [
            TranslationOverlaySurfaceId.TooltipAddon,
            (Func<Config, TranslationWindowConfig>)TranslationWindowConfig.FromConfigForTooltipAddon,
        ];
    }
}
