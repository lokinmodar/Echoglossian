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
    /// <param name="richOriginalTextPresentation">
    /// The optional copied original SeString payload used for swap
    /// presentation.
    /// </param>
    /// <param name="renderScaleAdjustment">
    /// The additional render-scale adjustment requested by configuration.
    /// </param>
    internal void Publish(
        TranslationOverlay overlay,
        TooltipAddonOverlayFrame frame,
        string text,
        bool displaysOriginalSwapText,
        RichOriginalTextPresentation? richOriginalTextPresentation,
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
        OverlayPublicationDiagnostics.Log(
            "TooltipAddonOverlayDiag",
            "runtime-publish",
            OverlayPublicationDiagnostics.BuildPreview(text),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{OverlayPublicationDiagnostics.BuildPreview(text)}|" +
                $"{frame.NativeVisible}|{activeFrame.NativeVisible}|" +
                $"{activeFrame.NativeScale:0.##}|{this.renderScaleAdjustment:0.##}|" +
                $"{OverlayPublicationDiagnostics.RoundVector(activeFrame.Position).X:0}," +
                $"{OverlayPublicationDiagnostics.RoundVector(activeFrame.Position).Y:0}|" +
                $"{OverlayPublicationDiagnostics.RoundVector(activeFrame.Size).X:0}," +
                $"{OverlayPublicationDiagnostics.RoundVector(activeFrame.Size).Y:0}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"currentPos={OverlayPublicationDiagnostics.FormatVector(frame.Position)} " +
                $"currentSize={OverlayPublicationDiagnostics.FormatVector(frame.Size)} " +
                $"currentNativeScale={frame.NativeScale:0.##} currentNativeVisible={frame.NativeVisible} " +
                $"activePos={OverlayPublicationDiagnostics.FormatVector(activeFrame.Position)} " +
                $"activeSize={OverlayPublicationDiagnostics.FormatVector(activeFrame.Size)} " +
                $"activeNativeScale={activeFrame.NativeScale:0.##} activeNativeVisible={activeFrame.NativeVisible} " +
                $"renderScaleAdjustment={this.renderScaleAdjustment:0.##} displaysOriginal={displaysOriginalSwapText} " +
                $"textLen={text.Length} preview='{OverlayPublicationDiagnostics.BuildPreview(text)}'"));
        overlay.Position = activeFrame.Position;
        overlay.Dimensions = activeFrame.Size;
        overlay.UpdateRuntimePresentation(
            activeFrame.NativeScale * this.renderScaleAdjustment,
            1f);
        overlay.CurrentName = string.Empty;
        overlay.OriginalName = string.Empty;
        overlay.CurrentText = text ?? string.Empty;
        overlay.UpdateContentPresentation(
            displaysOriginalSwapText,
            richOriginalTextPresentation);
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
            OverlayPublicationDiagnostics.Log(
                "TooltipAddonOverlayDiag",
                "runtime-sync-clear",
                "no-frame",
                "no-frame",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"overlayDisplay={overlay.Display} lastVisibleFrame={this.lastVisibleFrame.HasValue}"));
            this.Clear(overlay);
            return false;
        }

        overlay.Position = resolvedFrame.Value.Position;
        overlay.Dimensions = resolvedFrame.Value.Size;
        overlay.UpdateRuntimePresentation(
            resolvedFrame.Value.NativeScale * this.renderScaleAdjustment,
            1f);
        OverlayPublicationDiagnostics.Log(
            "TooltipAddonOverlayDiag",
            "runtime-sync",
            OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}|" +
                $"{currentFrame.HasValue}|{resolvedFrame.Value.NativeVisible}|" +
                $"{resolvedFrame.Value.NativeScale:0.##}|{this.renderScaleAdjustment:0.##}|" +
                $"{OverlayPublicationDiagnostics.RoundVector(resolvedFrame.Value.Position).X:0}," +
                $"{OverlayPublicationDiagnostics.RoundVector(resolvedFrame.Value.Position).Y:0}|" +
                $"{OverlayPublicationDiagnostics.RoundVector(resolvedFrame.Value.Size).X:0}," +
                $"{OverlayPublicationDiagnostics.RoundVector(resolvedFrame.Value.Size).Y:0}|" +
                $"{overlay.Display}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"currentFramePresent={currentFrame.HasValue} resolvedPos={OverlayPublicationDiagnostics.FormatVector(resolvedFrame.Value.Position)} " +
                $"resolvedSize={OverlayPublicationDiagnostics.FormatVector(resolvedFrame.Value.Size)} " +
                $"resolvedNativeScale={resolvedFrame.Value.NativeScale:0.##} resolvedNativeVisible={resolvedFrame.Value.NativeVisible} " +
                $"renderScaleAdjustment={this.renderScaleAdjustment:0.##} overlayDisplay={overlay.Display} " +
                $"textLen={overlay.CurrentText.Length} preview='{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}'"));
        return overlay.Display;
    }

    /// <summary>
    /// Clears retained Tooltip addon overlay state.
    /// </summary>
    /// <param name="overlay">The shared Tooltip addon overlay.</param>
    internal void Clear(TranslationOverlay overlay)
    {
        OverlayPublicationDiagnostics.Log(
            "TooltipAddonOverlayDiag",
            "runtime-clear",
            OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}|{overlay.Display}|{this.lastVisibleFrame.HasValue}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"overlayDisplay={overlay.Display} lastVisibleFrame={this.lastVisibleFrame.HasValue} " +
                $"textLen={overlay.CurrentText.Length} preview='{OverlayPublicationDiagnostics.BuildPreview(overlay.CurrentText)}'"));
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
