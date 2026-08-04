// <copyright file="TooltipAddonRichOriginalTextPresentationFactoryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Dalamud.Game.Text.SeStringHandling;
using Lumina.Text.ReadOnly;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers rich original-text payload assembly for Tooltip addon swap overlays.
/// </summary>
public sealed class TooltipAddonRichOriginalTextPresentationFactoryTests
{
    /// <summary>
    /// Ensures the factory preserves rich payload bytes across multiple tooltip
    /// text nodes while rebuilding the combined overlay body.
    /// </summary>
    [Fact]
    public void Create_CombinesStructuredTooltipSegmentsIntoOnePresentation()
    {
        const string title = "One-star Clan Mark Bills";
        const string body = "Several parchment bills notarized by the Clan Centurio.";
        var titlePayload = new SeStringBuilder()
            .AddUiForeground(500)
            .AddText(title)
            .AddUiForegroundOff()
            .Build()
            .Encode();
        var bodyPayload = new SeStringBuilder()
            .AddUiGlow(7)
            .AddText(body)
            .AddUiGlowOff()
            .Build()
            .Encode();

        var presentation = TooltipAddonRichOriginalTextPresentationFactory.Create(
            $"{title}\n{body}",
            [titlePayload, bodyPayload]);

        Assert.NotNull(presentation);
        Assert.True(presentation!.TryGetSeStringPayload(out var payload));
        Assert.Equal($"{title}\n{body}", new ReadOnlySeString(payload.ToArray()).ExtractText());
        Assert.NotEqual(
            ReadOnlySeString.FromText($"{title}\n{body}").Data.ToArray(),
            payload.ToArray());
    }

    /// <summary>
    /// Ensures the factory declines swap rich rendering when any tooltip
    /// source segment is unavailable.
    /// </summary>
    [Fact]
    public void Create_ReturnsNullWhenAnySegmentPayloadIsMissing()
    {
        var presentation = TooltipAddonRichOriginalTextPresentationFactory.Create(
            "Title\nBody",
            [new byte[] { 0x02, 0x03 }, null]);

        Assert.Null(presentation);
    }
}
