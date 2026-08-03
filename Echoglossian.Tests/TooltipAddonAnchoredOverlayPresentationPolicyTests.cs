// <copyright file="TooltipAddonAnchoredOverlayPresentationPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the Tooltip addon anchored-overlay presentation policy.
/// </summary>
public sealed class TooltipAddonAnchoredOverlayPresentationPolicyTests
{
    /// <summary>
    ///     Ensures Tooltip and swap modes use the anchored overlay.
    /// </summary>
    [Fact]
    public void TooltipAddonAnchoredOverlayPresentationPolicy_UsesAnchoredOverlayForTooltipAndSwapModes()
    {
        Assert.False(TooltipAddonAnchoredOverlayPresentationPolicy.UsesAnchoredOverlay(
            JournalTranslationDisplayMode.NativeUiTranslation,
            overlayOnlyLanguage: false));
        Assert.True(TooltipAddonAnchoredOverlayPresentationPolicy.UsesAnchoredOverlay(
            JournalTranslationDisplayMode.TooltipTranslation,
            overlayOnlyLanguage: false));
        Assert.True(TooltipAddonAnchoredOverlayPresentationPolicy.UsesAnchoredOverlay(
            JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
            overlayOnlyLanguage: false));
    }

    /// <summary>
    ///     Ensures swap mode selects original overlay text and tooltip mode
    ///     selects translated overlay text.
    /// </summary>
    [Fact]
    public void TooltipAddonAnchoredOverlayPresentationPolicy_SelectsOverlayBodyForEffectiveMode()
    {
        Assert.Equal(
            "original",
            TooltipAddonAnchoredOverlayPresentationPolicy.SelectOverlayBody(
                JournalTranslationDisplayMode.NativeUiTranslationWithOriginalTooltips,
                overlayOnlyLanguage: false,
                "original",
                "translated"));
        Assert.Equal(
            "translated",
            TooltipAddonAnchoredOverlayPresentationPolicy.SelectOverlayBody(
                JournalTranslationDisplayMode.NativeUiTranslation,
                overlayOnlyLanguage: true,
                "original",
                "translated"));
    }
}
