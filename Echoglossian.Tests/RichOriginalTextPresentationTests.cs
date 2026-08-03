// <copyright file="RichOriginalTextPresentationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers ownership and eligibility of original rich text presentation data.
/// </summary>
public class RichOriginalTextPresentationTests
{
    /// <summary>
    /// Ensures the presentation owns a copy of the captured SeString bytes.
    /// </summary>
    [Fact]
    public void Constructor_CopiesSeStringPayloadBytes()
    {
        var sourcePayload = new byte[] { 0x02, 0x1F, 0x01, 0x03 };
        var presentation = new RichOriginalTextPresentation("Formatted text", sourcePayload);

        sourcePayload[0] = 0xFF;

        Assert.True(presentation.TryGetSeStringPayload(out var payload));
        Assert.Equal(new byte[] { 0x02, 0x1F, 0x01, 0x03 }, payload.ToArray());
    }

    /// <summary>
    /// Ensures rich rendering is only enabled when swap displays original text
    /// in the normal ImGui backend.
    /// </summary>
    [Fact]
    public void CanUseFormattedSeString_OnlyAllowsPlainImGuiOriginalSwapPresentation()
    {
        var presentation = new RichOriginalTextPresentation("Formatted text", new byte[] { 0x02, 0x03 });

        Assert.True(
            RichOriginalTextPresentationPolicy.CanUseFormattedSeString(
                TextPresentationBackendKind.PlainImGui,
                true,
                presentation));
        Assert.False(
            RichOriginalTextPresentationPolicy.CanUseFormattedSeString(
                TextPresentationBackendKind.PlainImGui,
                false,
                presentation));
        Assert.False(
            RichOriginalTextPresentationPolicy.CanUseFormattedSeString(
                TextPresentationBackendKind.RtlTexture,
                true,
                presentation));
        Assert.False(
            RichOriginalTextPresentationPolicy.CanUseFormattedSeString(
                TextPresentationBackendKind.PlainImGui,
                true,
                null));
    }
}
