// <copyright file="TooltipAddonAnchoredOverlayRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TranslationOverlay;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Retains Tooltip addon overlay state while the native tooltip is visible or
/// intentionally hidden.
/// </summary>
internal sealed class TooltipAddonAnchoredOverlayRuntime
{
    private TooltipAddonOverlayFrame? lastVisibleFrame;
    private float renderScaleAdjustment = 1f;

    /// <summary>
    /// Publishes the current Tooltip addon overlay content and anchor state.
    /// </summary>
    /// <param name="overlay">The shared Tooltip addon overlay.</param>
    /// <param name="frame">The current live Tooltip addon frame.</param>
    /// <param name="text">The overlay body text.</param>
    /// <param name="displaysOriginalSwapText">
    /// Whether the overlay is presenting original swap text.
    /// </param>
    /// <param name="renderScaleAdjustment">
    /// The additional render-scale adjustment requested by configuration.
    /// </param>
    internal void Publish(
        TranslationOverlay overlay,
        TooltipAddonOverlayFrame frame,
        string text,
        bool displaysOriginalSwapText,
        float renderScaleAdjustment)
    {
        if (frame.NativeVisible || this.lastVisibleFrame == null)
        {
            this.lastVisibleFrame = frame;
        }

        this.renderScaleAdjustment = Math.Clamp(
            renderScaleAdjustment,
            0.25f,
            3f);
        var activeFrame = this.ResolveActiveFrame(frame) ?? frame;
        overlay.Position = activeFrame.Position;
        overlay.Dimensions = activeFrame.Size;
        overlay.UpdateRuntimePresentation(
            activeFrame.NativeScale * this.renderScaleAdjustment,
            1f);
        overlay.CurrentName = string.Empty;
        overlay.OriginalName = string.Empty;
        overlay.CurrentText = text ?? string.Empty;
        overlay.UpdateContentPresentation(displaysOriginalSwapText, presentation: null);
        overlay.Display = !string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// Synchronizes the shared Tooltip addon overlay to the newest live frame,
    /// or the last visible frame when the native tooltip is hidden.
    /// </summary>
    /// <param name="overlay">The shared Tooltip addon overlay.</param>
    /// <param name="currentFrame">The live Tooltip addon frame, if any.</param>
    /// <returns>
    /// <see langword="true"/> when the overlay remains displayable; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    internal bool TrySync(
        TranslationOverlay overlay,
        TooltipAddonOverlayFrame? currentFrame)
    {
        var resolvedFrame = this.ResolveActiveFrame(currentFrame);
        if (resolvedFrame == null)
        {
            this.Clear(overlay);
            return false;
        }

        overlay.Position = resolvedFrame.Value.Position;
        overlay.Dimensions = resolvedFrame.Value.Size;
        overlay.UpdateRuntimePresentation(
            resolvedFrame.Value.NativeScale * this.renderScaleAdjustment,
            1f);
        return overlay.Display;
    }

    /// <summary>
    /// Clears retained Tooltip addon overlay state.
    /// </summary>
    /// <param name="overlay">The shared Tooltip addon overlay.</param>
    internal void Clear(TranslationOverlay overlay)
    {
        this.lastVisibleFrame = null;
        this.renderScaleAdjustment = 1f;
        overlay.Display = false;
        overlay.CurrentText = string.Empty;
        overlay.CurrentName = string.Empty;
        overlay.OriginalName = string.Empty;
        overlay.ClearContentPresentation();
        overlay.ClearRuntimePresentation();
    }

    /// <summary>
    /// Resolves the best frame for the active Tooltip addon overlay.
    /// </summary>
    /// <param name="currentFrame">The current live frame, if any.</param>
    /// <returns>The best available frame, if any.</returns>
    private TooltipAddonOverlayFrame? ResolveActiveFrame(
        TooltipAddonOverlayFrame? currentFrame)
    {
        if (currentFrame.HasValue && currentFrame.Value.NativeVisible)
        {
            this.lastVisibleFrame = currentFrame.Value;
            return currentFrame.Value;
        }

        return this.lastVisibleFrame ?? currentFrame;
    }
}
