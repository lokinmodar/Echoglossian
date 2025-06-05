// <copyright file="FieldValidationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
namespace Echoglossian.Helpers;

/// <summary>
/// Provides UI helpers to display field validation warnings.
/// </summary>
public static class FieldValidationHelper
{
  /// <summary>
  /// Displays a warning message if the field value is null or whitespace.
  /// </summary>
  /// <param name="fieldLabel">The display name of the field (for the warning text).</param>
  /// <param name="fieldValue">The current value of the field.</param>
  public static void ShowFieldRequiredWarningIfEmpty(string fieldLabel, string? fieldValue)
  {
    if (string.IsNullOrWhiteSpace(fieldValue))
    {
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
      ImGui.TextWrapped($"{fieldLabel} is required.");
      ImGui.PopStyleColor();
    }
  }
}
