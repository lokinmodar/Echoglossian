// <copyright file="PreviewCanvas.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.Previewer.Scenarios;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;

namespace Echoglossian.Previewer.UI;

/// <summary>
/// Draws the preview canvas and routes overlay drawing through the shared renderer.
/// </summary>
internal sealed class PreviewCanvas : IDisposable
{
    private readonly TranslationOverlayRenderer renderer;
    private readonly TranslationOverlay overlay = new();
    private string lastBodyText = string.Empty;
    private string lastTitle = string.Empty;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewCanvas" /> class.
    /// </summary>
    /// <param name="renderer">The shared overlay renderer.</param>
    internal PreviewCanvas(TranslationOverlayRenderer renderer)
    {
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>
    /// Calculates a uniformly scaled logical viewport within the available space.
    /// </summary>
    /// <param name="availableWidth">The available host width.</param>
    /// <param name="availableHeight">The available host height.</param>
    /// <param name="logicalWidth">The logical viewport width.</param>
    /// <param name="logicalHeight">The logical viewport height.</param>
    /// <returns>The scaled viewport layout.</returns>
    internal static PreviewCanvasLayout CalculateScaledViewport(
        float availableWidth,
        float availableHeight,
        int logicalWidth,
        int logicalHeight)
    {
        var safeWidth = Math.Max(availableWidth, 1f);
        var safeHeight = Math.Max(availableHeight, 1f);
        var scale = Math.Min(safeWidth / logicalWidth, safeHeight / logicalHeight);
        var size = new Vector2(logicalWidth * scale, logicalHeight * scale);
        return new PreviewCanvasLayout(
            new Vector2(
                (safeWidth - size.X) / 2f,
                (safeHeight - size.Y) / 2f),
            size,
            scale);
    }

    /// <summary>
    /// Draws the canvas and active overlay scenario.
    /// </summary>
    /// <param name="state">The preview shell state.</param>
    /// <param name="configuration">The editable preview configuration.</param>
    /// <param name="availableSize">The available canvas host size.</param>
    /// <returns>The most recent overlay render result.</returns>
    internal TranslationOverlayRenderResult Draw(
        PreviewShellState state,
        Config configuration,
        Vector2 availableSize)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(configuration);

        var layout = CalculateScaledViewport(
            availableSize.X,
            availableSize.Y,
            state.Viewport.Width,
            state.Viewport.Height);
        var cursor = ImGui.GetCursorScreenPos();
        var viewportPosition = cursor + layout.Offset;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            viewportPosition,
            viewportPosition + layout.Size,
            ImGui.GetColorU32(new Vector4(0.04f, 0.05f, 0.06f, 1f)));
        drawList.AddRect(
            viewportPosition,
            viewportPosition + layout.Size,
            ImGui.GetColorU32(new Vector4(0.28f, 0.32f, 0.36f, 1f)));

        if (state.ShowSimulatedAddonBounds)
        {
            DrawAddonGuide(drawList, viewportPosition, layout.Scale, state);
        }

        this.UpdateOverlay(state);
        var scaledBounds = ScaleBounds(state.AddonBounds, viewportPosition, layout.Scale);
        var request = new TranslationOverlayRenderRequest(
            this.overlay,
            TranslationWindowConfig.ForSurface(configuration, state.SurfaceId),
            viewportPosition,
            layout.Size,
            scaledBounds.Position,
            scaledBounds.Size,
            IsPreview: true);
        return this.renderer.Draw(request, state.Title);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.overlay.Dispose();
        this.disposed = true;
    }

    /// <summary>
    /// Updates the persistent preview overlay with the current shell state.
    /// </summary>
    /// <param name="state">The current preview shell state.</param>
    private void UpdateOverlay(PreviewShellState state)
    {
        this.overlay.Display = state.Visible;
        if (!string.Equals(this.lastBodyText, state.BodyText, StringComparison.Ordinal))
        {
            this.overlay.CurrentTextId++;
            this.lastBodyText = state.BodyText;
        }

        if (!string.Equals(this.lastTitle, state.Title, StringComparison.Ordinal))
        {
            this.overlay.CurrentNameId++;
            this.lastTitle = state.Title;
        }

        this.overlay.CurrentText = state.BodyText;
        this.overlay.CurrentName = state.Title;
        this.overlay.OriginalName = state.Title;
    }

    private static void DrawAddonGuide(
        ImDrawListPtr drawList,
        Vector2 viewportPosition,
        float scale,
        PreviewShellState state)
    {
        var scaledBounds = ScaleBounds(state.AddonBounds, viewportPosition, scale);
        var color = ImGui.GetColorU32(new Vector4(0.95f, 0.72f, 0.18f, 0.85f));
        drawList.AddRect(
            scaledBounds.Position,
            scaledBounds.Position + scaledBounds.Size,
            color,
            0f,
            ImDrawFlags.None,
            2f);
        drawList.AddText(
            scaledBounds.Position + new Vector2(6f, 6f),
            color,
            $"Simulated bounds: {state.SurfaceId}");
    }

    private static (Vector2 Position, Vector2 Size) ScaleBounds(
        PreviewAddonBounds bounds,
        Vector2 viewportPosition,
        float scale)
    {
        return (
            viewportPosition + new Vector2(bounds.X * scale, bounds.Y * scale),
            new Vector2(bounds.Width * scale, bounds.Height * scale));
    }
}

/// <summary>
/// Describes the scaled canvas viewport layout.
/// </summary>
/// <param name="Offset">The offset from the host canvas origin.</param>
/// <param name="Size">The scaled viewport size.</param>
/// <param name="Scale">The uniform scale factor.</param>
internal sealed record PreviewCanvasLayout(Vector2 Offset, Vector2 Size, float Scale);
