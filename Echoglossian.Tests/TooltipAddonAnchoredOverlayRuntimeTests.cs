// <copyright file="TooltipAddonAnchoredOverlayRuntimeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the Tooltip addon anchored-overlay runtime state transitions.
/// </summary>
public sealed class TooltipAddonAnchoredOverlayRuntimeTests
{
    /// <summary>
    /// Ensures publish updates the visible overlay text and anchored bounds.
    /// </summary>
    [Fact]
    public void Publish_UpdatesBoundsTextAndRuntimeScale()
    {
        using var overlay = new TranslationOverlay();
        var runtime = new TooltipAddonAnchoredOverlayRuntime();
        var frame = new TooltipAddonOverlayFrame(
            Position: new Vector2(320f, 180f),
            Size: new Vector2(420f, 132f),
            NativeScale: 0.85f,
            NativeVisible: true);

        runtime.Publish(
            overlay,
            frame,
            "مرحبا",
            displaysOriginalSwapText: false,
            richOriginalTextPresentation: null,
            renderScaleAdjustment: 1.1f);

        Assert.True(overlay.Display);
        Assert.Equal("مرحبا", overlay.CurrentText);
        Assert.Equal(frame.Position, overlay.Position);
        Assert.Equal(frame.Size, overlay.Dimensions);
        Assert.InRange(overlay.RenderScale, 0.934f, 0.936f);
        Assert.Equal(1f, overlay.RenderAlpha);
    }

    /// <summary>
    /// Ensures sync reuses the last visible frame when the native tooltip is
    /// hidden after publish.
    /// </summary>
    [Fact]
    public void TrySync_ReusesLastVisibleFrameWhenCurrentFrameIsHidden()
    {
        using var overlay = new TranslationOverlay();
        var runtime = new TooltipAddonAnchoredOverlayRuntime();
        var visibleFrame = new TooltipAddonOverlayFrame(
            Position: new Vector2(200f, 140f),
            Size: new Vector2(360f, 96f),
            NativeScale: 0.9f,
            NativeVisible: true);
        var hiddenFrame = new TooltipAddonOverlayFrame(
            Position: new Vector2(999f, 999f),
            Size: new Vector2(1f, 1f),
            NativeScale: 0.3f,
            NativeVisible: false);

        runtime.Publish(
            overlay,
            visibleFrame,
            "Travel",
            displaysOriginalSwapText: true,
            richOriginalTextPresentation: null,
            renderScaleAdjustment: 1f);

        var synced = runtime.TrySync(overlay, hiddenFrame);

        Assert.True(synced);
        Assert.Equal(visibleFrame.Position, overlay.Position);
        Assert.Equal(visibleFrame.Size, overlay.Dimensions);
        Assert.True(overlay.DisplaysOriginalSwapText);
    }

    /// <summary>
    /// Ensures publish retains a rich original payload when swap presentation
    /// should render the original Tooltip text through the overlay.
    /// </summary>
    [Fact]
    public void Publish_PreservesRichOriginalPresentationForSwapContent()
    {
        using var overlay = new TranslationOverlay();
        var runtime = new TooltipAddonAnchoredOverlayRuntime();
        var frame = new TooltipAddonOverlayFrame(
            Position: new Vector2(280f, 160f),
            Size: new Vector2(400f, 120f),
            NativeScale: 1f,
            NativeVisible: true);
        var presentation = new RichOriginalTextPresentation(
            "Original title\nOriginal body",
            new byte[] { 0x02, 0x03 });

        runtime.Publish(
            overlay,
            frame,
            "Original title\nOriginal body",
            displaysOriginalSwapText: true,
            richOriginalTextPresentation: presentation,
            renderScaleAdjustment: 1f);

        Assert.True(overlay.Display);
        Assert.True(overlay.DisplaysOriginalSwapText);
        Assert.Same(presentation, overlay.RichOriginalTextPresentation);
    }

    /// <summary>
    /// Ensures clear removes content and resets runtime presentation state.
    /// </summary>
    [Fact]
    public void Clear_ResetsOverlayState()
    {
        using var overlay = new TranslationOverlay
        {
            Display = true,
            CurrentText = "Travel",
            CurrentName = "Title",
            OriginalName = "Original",
            Position = new Vector2(12f, 34f),
            Dimensions = new Vector2(56f, 78f),
            RenderScale = 1.5f,
            RenderAlpha = 0.4f,
        };

        var runtime = new TooltipAddonAnchoredOverlayRuntime();
        runtime.Clear(overlay);

        Assert.False(overlay.Display);
        Assert.Equal(string.Empty, overlay.CurrentText);
        Assert.Equal(string.Empty, overlay.CurrentName);
        Assert.Equal(string.Empty, overlay.OriginalName);
        Assert.Equal(1f, overlay.RenderScale);
        Assert.Equal(1f, overlay.RenderAlpha);
        Assert.False(overlay.DisplaysOriginalSwapText);
    }
}
