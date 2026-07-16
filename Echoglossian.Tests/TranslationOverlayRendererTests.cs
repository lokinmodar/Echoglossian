// <copyright file="TranslationOverlayRendererTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers renderer decisions that do not require a live ImGui runtime.
/// </summary>
public sealed class TranslationOverlayRendererTests
{
    /// <summary>
    /// Ensures the inline RTL title path only activates after the title block is available.
    /// </summary>
    /// <param name="hasTitleBlock">Whether the RTL title texture is ready.</param>
    /// <param name="expected">The expected helper result.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ShouldUseInlineRtlTitle_RequiresReadyTitleBlock(
        bool hasTitleBlock,
        bool expected)
    {
        var actual = TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
            TextPresentationBackendKind.RtlTexture,
            forceShowTitle: true,
            resolvedTitle: "Battle Talk",
            hasTitleBlock);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Ensures non-RTL or untitled paths do not hide the normal title bar.
    /// </summary>
    [Fact]
    public void ShouldUseInlineRtlTitle_NonRtlOrMissingTitle_ReturnsFalse()
    {
        Assert.False(
            TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
                TextPresentationBackendKind.PlainImGui,
                forceShowTitle: true,
                resolvedTitle: "Talk",
                hasTitleBlock: true));
        Assert.False(
            TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
                TextPresentationBackendKind.RtlTexture,
                forceShowTitle: true,
                resolvedTitle: string.Empty,
                hasTitleBlock: true));
        Assert.False(
            TranslationOverlayRenderer.ShouldUseInlineRtlTitle(
                TextPresentationBackendKind.RtlTexture,
                forceShowTitle: false,
                resolvedTitle: "Talk",
                hasTitleBlock: true));
    }
}
