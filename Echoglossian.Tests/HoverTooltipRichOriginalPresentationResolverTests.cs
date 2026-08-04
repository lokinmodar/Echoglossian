// <copyright file="HoverTooltipRichOriginalPresentationResolverTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.UIOverlays.TextPresentation;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers cached rich-original capture for shared hover tooltips.
/// </summary>
public class HoverTooltipRichOriginalPresentationResolverTests
{
    /// <summary>
    /// Ensures an unchanged swap body retains the owned payload without
    /// reading the native text node again.
    /// </summary>
    [Fact]
    public void Resolve_UnchangedSwapBody_ReusesPriorCapture()
    {
        var priorPresentation = new RichOriginalTextPresentation(
            "Original",
            new byte[] { 0x02, 0x03 });
        var captureCalls = 0;

        var presentation = HoverTooltipRichOriginalPresentationResolver.Resolve(
            "Original",
            true,
            true,
            priorPresentation,
            "Original",
            true,
            new RichOriginalTextCaptureRequest((nint)1, "Original"),
            _ =>
            {
                captureCalls++;
                return null;
            },
            out var captureResolved);

        Assert.Same(priorPresentation, presentation);
        Assert.True(captureResolved);
        Assert.Equal(0, captureCalls);
    }

    /// <summary>
    /// Ensures non-swap tooltip presentation never requests a rich original
    /// payload.
    /// </summary>
    [Fact]
    public void Resolve_NonSwapPresentation_DoesNotCaptureOriginalPayload()
    {
        var captureCalls = 0;

        var presentation = HoverTooltipRichOriginalPresentationResolver.Resolve(
            previousBody: null,
            previousDisplaysOriginalSwapText: false,
            previousCaptureResolved: false,
            previousPresentation: null,
            currentBody: "Translated",
            displaysOriginalSwapText: false,
            captureRequest: new RichOriginalTextCaptureRequest((nint)1, "Original"),
            capture: _ =>
            {
                captureCalls++;
                return new RichOriginalTextPresentation(
                    "Original",
                    new byte[] { 0x02, 0x03 });
            },
            captureResolved: out var captureResolved);

        Assert.Null(presentation);
        Assert.False(captureResolved);
        Assert.Equal(0, captureCalls);
    }

    /// <summary>
    /// Ensures a failed capture is retained for an unchanged swap body instead
    /// of reading the native text node again on every frame.
    /// </summary>
    [Fact]
    public void Resolve_UnchangedSwapBodyAfterFailedCapture_DoesNotRetry()
    {
        var captureCalls = 0;

        var presentation = HoverTooltipRichOriginalPresentationResolver.Resolve(
            "Original",
            true,
            true,
            previousPresentation: null,
            "Original",
            true,
            new RichOriginalTextCaptureRequest((nint)1, "Original"),
            _ =>
            {
                captureCalls++;
                return new RichOriginalTextPresentation(
                    "Original",
                    new byte[] { 0x02, 0x03 });
            },
            out var captureResolved);

        Assert.Null(presentation);
        Assert.True(captureResolved);
        Assert.Equal(0, captureCalls);
    }
}
