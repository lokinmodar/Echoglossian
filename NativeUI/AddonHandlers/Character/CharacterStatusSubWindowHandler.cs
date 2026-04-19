// <copyright file="CharacterStatusSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles DB-first translation for the CharacterStatus subwindow.
/// </summary>
public class CharacterStatusSubWindowHandler
    : CharacterTextNodeWindowHandlerBase
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterStatusSubWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterStatusSubWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "CharacterStatus",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService)
    {
    }
}
