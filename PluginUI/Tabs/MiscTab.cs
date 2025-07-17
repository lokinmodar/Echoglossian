// <copyright file="MiscTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the miscellaneous settings tab in the plugin configuration UI.
/// </summary>
public static class MiscTab
{
    /// <summary>
    ///     Draws the miscellaneous settings UI.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>True if any value has changed, otherwise false.</returns>
    public static bool Draw(Config config)
    {
        var changed = false;

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}