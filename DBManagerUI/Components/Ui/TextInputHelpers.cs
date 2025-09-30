// <copyright file="TextInputHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.DBManagerUI.Components.Ui
{
  /// <summary>
  /// Helper methods for wrapped, multiline ImGui text inputs.
  /// </summary>
  public static class TextInputHelpers
  {
    /// <summary>
    /// Draws a wrapped, multiline text box that auto-sizes by content within a min/max line clamp.
    /// Inspired by Wordsmith's approach, but simplified &amp; adapted for this project.
    /// </summary>
    /// <param name="id">ImGui ID (must be unique in scope).</param>
    /// <param name="text">Text reference to edit.</param>
    /// <param name="minLines">Minimum visible lines.</param>
    /// <param name="maxLines">Maximum visible lines (prevents runaway size).</param>
    /// <param name="flags">ImGui input flags.</param>
    /// <returns>True if value changed.</returns>
    public static bool DrawMultilineTextInput(string id, ref string text, int minLines = 4, int maxLines = 20, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
      if (minLines < 1)
      {
        minLines = 1;
      }

      if (maxLines < minLines)
      {
        maxLines = minLines;
      }

      // Rough line count by '\n'; add one to include the current line.
      int contentLines = 1;
      if (!string.IsNullOrEmpty(text))
      {
        for (int i = 0; i < text.Length; i++)
        {
          if (text[i] == '\n')
          {
            contentLines++;
          }
        }
      }

      contentLines = Math.Clamp(contentLines, minLines, maxLines);

      // Height: N lines plus a little padding. Width: full column width.
      float line = ImGui.GetTextLineHeightWithSpacing();
      float height = (line * contentLines) + (ImGui.GetStyle().FramePadding.Y * 2.0f);
      Vector2 size = new(ImGui.GetContentRegionAvail().X, height);

      // Wrap long soft lines at the available width (for display inside the box).
      ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + size.X);
      bool changed = ImGui.InputTextMultiline(id, ref text, 1 << 16, size, flags);
      ImGui.PopTextWrapPos();

      return changed;
    }
  }
}
