// <copyright file="TranslationWindowConfigTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

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
    }
}
