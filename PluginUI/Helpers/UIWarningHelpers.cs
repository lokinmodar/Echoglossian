// <copyright file="UIWarningHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
///     Utility methods for drawing UI warnings.
/// </summary>
public static class UIWarningHelpers
{
    /// <summary>
    ///     Shows a styled warning text to indicate a required field.
    /// </summary>
    /// <param name="fieldName">The name of the required field.</param>
    public static void ShowFieldRequiredWarningIfEmpty(string fieldName)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
        var format = Resources.ResourceManager.GetString(
                         "RequiredFieldMessageFormat",
                         Resources.Culture) ??
                     "{0} is required.";
        ImGui.TextWrapped(string.Format(format, fieldName));
        ImGui.PopStyleColor();
    }
}
