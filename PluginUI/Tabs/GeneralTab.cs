// <copyright file="GeneralTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Provides methods to render the "Whats, Whens and Hows" general
///     configuration tab.
/// </summary>
public static class GeneralTab
{
    /// <summary>
    ///     Renders the general configuration tab and handles user interactions.
    /// </summary>
    /// <param name="config">
    ///     The configuration object to be modified based on user
    ///     input.
    /// </param>
    /// <returns>True if any configuration value was changed; otherwise, false.</returns>
    public static bool Draw(Config config)
    {
        var changed = false;
        ImGui.Text(Resources.ConfigTab9Text);
        ImGui.Spacing();
        changed |= ImGui.Checkbox(
            Resources.ShowInCutscenesLabel,
            ref config.ShowInCutscenes);
        ImGui.Spacing();

        if (ImGui.Checkbox(
                Resources.ConfigTab9CheckboxClipboardText,
                ref config.CopyTranslationToClipboard))
        {
            changed = true;
        }

        ImGui.SameLine();
        ImGui.Text(Resources.HoverTooltipIndicator);

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Resources.ConfigTab9CheckboxClipboardTooltipText);
        }

        return changed;
    }
}
