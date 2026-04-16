// <copyright file="AddonContextMenuTitleHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.MainMenu;

/// <summary>
///     Handles DB-first translation for the addon context menu title.
/// </summary>
public class AddonContextMenuTitleHandler : DbFirstGameWindowAddonHandler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AddonContextMenuTitleHandler" />
    ///     class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public AddonContextMenuTitleHandler(
        Config config,
        TranslationService translationService)
        : base(
            addonName: "AddonContextMenuTitle",
            config: config,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateAddonContextMenuTitle,
            useAtkValues: true)
    {
    }
}
