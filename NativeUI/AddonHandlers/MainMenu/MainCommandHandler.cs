// <copyright file="MainCommandHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.MainMenu;

/// <summary>
///     Handles DB-first translation for the main command menu.
/// </summary>
public class MainCommandHandler : DbFirstGameWindowAddonHandler
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="MainCommandHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public MainCommandHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "_MainCommand",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateMainCommandWindow,
            useAtkValues: true,
            displayModeSelector: static configuration =>
                configuration.MainCommandWindowTranslationDisplayMode)
    {
    }
}
