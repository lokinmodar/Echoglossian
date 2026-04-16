// <copyright file="HudWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Hud;

/// <summary>
///     Handles DB-first translation for the main HUD window.
/// </summary>
public class HudWindowHandler : DbFirstGameWindowAddonHandler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HudWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public HudWindowHandler(
        Config config,
        TranslationService translationService)
        : base(
            addonName: "Hud",
            config: config,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateHudWindow,
            useAtkValues: true,
            stringArrayDataType: StringArrayType.Hud)
    {
    }
}
