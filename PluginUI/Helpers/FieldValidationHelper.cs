// <copyright file="FieldValidationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using ImGuiNET;
using System.Numerics;
using System.Collections.Generic;

namespace Echoglossian.Helpers;

/// <summary>
/// Provides UI helpers to display field validation warnings.
/// </summary>
public static class FieldValidationHelper
{
  private static readonly Dictionary<string, bool> fieldTouched = new();

  /// <summary>
  /// Displays a warning message if the field value is null or whitespace.
  /// </summary>
  public static void ShowFieldRequiredWarningIfEmpty(string fieldLabel, string? fieldValue)
  {
    if (string.IsNullOrWhiteSpace(fieldValue))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
      ImGui.TextWrapped($"{fieldLabel} is required.");
      ImGui.PopStyleColor();
    }
  }

  /// <summary>
  /// Draws a validated input text field. Highlights red and shows warning only after first interaction.
  /// </summary>
  public static bool ValidatedInputText(string label, ref string value, int maxLength, out bool isInvalid)
  {
    if (!fieldTouched.ContainsKey(label))
    {
      fieldTouched[label] = false;
    }

    bool changed = ImGui.InputText(label, ref value, (uint)maxLength);

    if (ImGui.IsItemActive() || changed)
    {
      fieldTouched[label] = true;
    }

    isInvalid = string.IsNullOrWhiteSpace(value);

    if (fieldTouched[label] && isInvalid)
    {
      ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.5f, 0.1f, 0.1f, 1f));
      ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 0.2f, 0.2f, 1f));
      ImGui.PopStyleColor(2);

      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
      ImGui.TextWrapped($"{label} is required.");
      ImGui.PopStyleColor();
    }

    return changed;
  }

  /// <summary>
  /// Forces a field to be considered "touched", useful for triggering validation manually (e.g., on submit).
  /// </summary>
  public static void MarkFieldAsTouched(string label)
  {
    fieldTouched[label] = true;
  }
}
