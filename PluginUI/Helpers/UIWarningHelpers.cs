using ImGuiNET;
using System.Numerics;

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
/// Utility methods for drawing UI warnings.
/// </summary>
public static class UIWarningHelpers
{
  /// <summary>
  /// Shows a styled warning text to indicate a required field.
  /// </summary>
  /// <param name="fieldName">The name of the required field.</param>
  public static void ShowFieldRequiredWarning(string fieldName)
  {
    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
    ImGui.TextWrapped($"{fieldName} is required.");
    ImGui.PopStyleColor();
  }
}
