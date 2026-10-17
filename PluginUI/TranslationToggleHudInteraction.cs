// <copyright file="TranslationToggleHudInteraction.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI;

/// <summary>
/// Tracks drag versus click interaction for the translation HUD toggle.
/// </summary>
internal sealed class TranslationToggleHudInteraction
{
    private Vector2 pressMousePosition;
    private Vector2 pressWindowPosition;
    private bool active;
    private bool dragged;

    /// <summary>
    /// Starts one pointer interaction.
    /// </summary>
    /// <param name="mousePosition">The pointer position at press time.</param>
    /// <param name="windowPosition">The HUD position at press time.</param>
    public void Begin(Vector2 mousePosition, Vector2 windowPosition)
    {
        this.pressMousePosition = mousePosition;
        this.pressWindowPosition = windowPosition;
        this.active = true;
        this.dragged = false;
    }

    /// <summary>
    /// Updates the active pointer interaction.
    /// </summary>
    /// <param name="mousePosition">The current pointer position.</param>
    /// <param name="released">Whether the pointer was released this frame.</param>
    /// <param name="dragThreshold">The minimum movement treated as a drag.</param>
    /// <returns>The current interaction result.</returns>
    public TranslationToggleHudInteractionResult Update(
        Vector2 mousePosition,
        bool released,
        float dragThreshold)
    {
        if (!this.active)
        {
            return new TranslationToggleHudInteractionResult(
                TranslationToggleHudInteractionAction.None,
                this.pressWindowPosition);
        }

        var delta = mousePosition - this.pressMousePosition;
        this.dragged |= delta.LengthSquared() >= dragThreshold * dragThreshold;
        var position = this.dragged
            ? this.pressWindowPosition + delta
            : this.pressWindowPosition;
        if (!released)
        {
            return new TranslationToggleHudInteractionResult(
                this.dragged
                    ? TranslationToggleHudInteractionAction.Dragging
                    : TranslationToggleHudInteractionAction.None,
                position);
        }

        this.active = false;
        return new TranslationToggleHudInteractionResult(
            this.dragged
                ? TranslationToggleHudInteractionAction.DragCompleted
                : TranslationToggleHudInteractionAction.Click,
            position);
    }

    /// <summary>
    /// Clamps a HUD position to the active viewport work area.
    /// </summary>
    /// <param name="position">The requested HUD position.</param>
    /// <param name="size">The HUD size.</param>
    /// <param name="viewportPosition">The viewport work-area origin.</param>
    /// <param name="viewportSize">The viewport work-area size.</param>
    /// <returns>The clamped HUD position.</returns>
    public static Vector2 ClampToViewport(
        Vector2 position,
        Vector2 size,
        Vector2 viewportPosition,
        Vector2 viewportSize)
    {
        var maximum = viewportPosition + Vector2.Max(viewportSize - size, Vector2.Zero);
        return Vector2.Clamp(position, viewportPosition, maximum);
    }
}

/// <summary>
/// Describes the current translation HUD interaction action.
/// </summary>
internal enum TranslationToggleHudInteractionAction
{
    None,
    Dragging,
    DragCompleted,
    Click,
}

/// <summary>
/// Carries a translation HUD interaction action and resulting position.
/// </summary>
/// <param name="Action">The current interaction action.</param>
/// <param name="Position">The resulting HUD position.</param>
internal sealed record TranslationToggleHudInteractionResult(
    TranslationToggleHudInteractionAction Action,
    Vector2 Position);
