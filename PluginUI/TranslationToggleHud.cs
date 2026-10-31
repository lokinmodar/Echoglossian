// <copyright file="TranslationToggleHud.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
/// Draws the optional draggable global translation HUD toggle.
/// </summary>
public partial class Echoglossian
{
  private const float TranslationToggleButtonSize = 42f;
  private const float TranslationToggleDragThreshold = 5f;
  private readonly TranslationToggleHudInteraction translationToggleHudInteraction = new();

  /// <summary>
  /// Draws and handles the optional global translation HUD toggle.
  /// </summary>
  private void DrawTranslationToggleHud()
  {
    if (!this.configuration.ShowTranslationToggleButton)
    {
      return;
    }

    var viewport = ImGui.GetMainViewport();
    var buttonSize = new Vector2(
        TranslationToggleButtonSize,
        TranslationToggleButtonSize);
    var clampedPosition = TranslationToggleHudInteraction.ClampToViewport(
        this.configuration.TranslationToggleButtonPosition,
        buttonSize,
        viewport.WorkPos,
        viewport.WorkSize);
    if (clampedPosition != this.configuration.TranslationToggleButtonPosition)
    {
      this.configuration.TranslationToggleButtonPosition = clampedPosition;
      SaveConfig(this.configuration);
    }

    ImGui.SetNextWindowPos(clampedPosition, ImGuiCond.Always);
    ImGui.SetNextWindowSize(buttonSize, ImGuiCond.Always);
    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
    var visible = ImGui.Begin(
        "##EchoglossianTranslationToggleHud",
        ImGuiWindowFlags.NoDecoration |
        ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoMove |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoNavFocus |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse);
    ImGui.PopStyleVar(2);

    if (!visible)
    {
      ImGui.End();
      return;
    }

    this.DrawTranslationToggleHudButton(buttonSize);
    ImGui.End();
  }

  /// <summary>
  /// Draws the toggle button and handles one frame of pointer interaction.
  /// </summary>
  /// <param name="buttonSize">The square HUD button size.</param>
  private void DrawTranslationToggleHudButton(Vector2 buttonSize)
  {
    var state = this.translationActivationCoordinator.State;
    var position = ImGui.GetCursorScreenPos();
    var maximum = position + buttonSize;
    var drawList = ImGui.GetWindowDrawList();
    var bodyColor = state switch
    {
      TranslationActivationState.Enabled => new Vector4(0.24f, 0.16f, 0.55f, 0.88f),
      TranslationActivationState.Transitioning => new Vector4(0.65f, 0.36f, 0.05f, 0.90f),
      _ => new Vector4(0.20f, 0.20f, 0.22f, 0.42f),
    };
    var iconColor = state switch
    {
      TranslationActivationState.Enabled => new Vector4(0.25f, 0.95f, 1f, 1f),
      TranslationActivationState.Transitioning => new Vector4(1f, 0.72f, 0.20f, 1f),
      _ => new Vector4(0.65f, 0.65f, 0.68f, 0.62f),
    };

    if (state == TranslationActivationState.Enabled)
    {
      drawList.AddRectFilled(
          position - new Vector2(4f),
          maximum + new Vector2(4f),
          ImGui.GetColorU32(new Vector4(0.28f, 0.55f, 1f, 0.10f)),
          12f);
      drawList.AddRectFilled(
          position - new Vector2(2f),
          maximum + new Vector2(2f),
          ImGui.GetColorU32(new Vector4(0.65f, 0.25f, 1f, 0.16f)),
          10f);
    }

    drawList.AddRectFilled(
        position,
        maximum,
        ImGui.GetColorU32(bodyColor),
        9f);
    ImGui.InvisibleButton("##TranslationToggleButton", buttonSize);

    using (PluginInterface.UiBuilder.IconFontHandle.Push())
    {
      var icon = FontAwesomeIcon.Language.ToIconString();
      var iconSize = ImGui.CalcTextSize(icon);
      drawList.AddText(
          position + ((buttonSize - iconSize) / 2f),
          ImGui.GetColorU32(iconColor),
          icon);
    }

    var mousePosition = ImGui.GetMousePos();
    if (ImGui.IsItemActivated())
    {
      this.translationToggleHudInteraction.Begin(
          mousePosition,
          this.configuration.TranslationToggleButtonPosition);
    }

    if (ImGui.IsItemActive())
    {
      var update = this.translationToggleHudInteraction.Update(
          mousePosition,
          released: false,
          TranslationToggleDragThreshold);
      if (update.Action == TranslationToggleHudInteractionAction.Dragging)
      {
        this.configuration.TranslationToggleButtonPosition =
            TranslationToggleHudInteraction.ClampToViewport(
                update.Position,
                buttonSize,
                ImGui.GetMainViewport().WorkPos,
                ImGui.GetMainViewport().WorkSize);
      }
    }

    if (ImGui.IsItemDeactivated())
    {
      var update = this.translationToggleHudInteraction.Update(
          mousePosition,
          released: true,
          TranslationToggleDragThreshold);
      if (update.Action == TranslationToggleHudInteractionAction.DragCompleted)
      {
        this.configuration.TranslationToggleButtonPosition =
            TranslationToggleHudInteraction.ClampToViewport(
                update.Position,
                buttonSize,
                ImGui.GetMainViewport().WorkPos,
                ImGui.GetMainViewport().WorkSize);
        SaveConfig(this.configuration);
      }
      else if (update.Action == TranslationToggleHudInteractionAction.Click &&
               state != TranslationActivationState.Transitioning)
      {
        _ = this.translationActivationCoordinator.RequestAsync(
            TranslationActivationRequest.Toggle);
      }
    }

    if (ImGui.IsItemHovered())
    {
      ImGui.BeginTooltip();
      ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40f);
      ImGui.TextUnformatted(state switch
      {
        TranslationActivationState.Enabled =>
            Resources.TranslationToggleEnabledTooltip,
        TranslationActivationState.Transitioning =>
            Resources.TranslationToggleTransitioningTooltip,
        _ => Resources.TranslationToggleDisabledTooltip,
      });
      ImGui.PopTextWrapPos();
      ImGui.EndTooltip();
    }
  }
}
