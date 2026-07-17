// <copyright file="PreviewRuntimeActionUiHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
/// Draws preview-safe UI for actions that are unavailable in the standalone
/// preview runtime.
/// </summary>
public static class PreviewRuntimeActionUiHelper
{
    /// <summary>
    /// Draws one action button that becomes unavailable in preview-only mode.
    /// </summary>
    /// <param name="label">The button label.</param>
    /// <param name="runtimeActionsAvailable">
    /// Whether live runtime-owned actions are available.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the button was clicked while available.
    /// </returns>
    public static bool DrawButton(
        string label,
        bool runtimeActionsAvailable)
    {
        ImGui.BeginDisabled(!runtimeActionsAvailable);
        var clicked = ImGui.Button(label);
        ImGui.EndDisabled();
        if (!runtimeActionsAvailable &&
            ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(Resources.PreviewImageryUnavailableText);
        }

        return runtimeActionsAvailable && clicked;
    }
}
