// <copyright file="CharacterStatusSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles DB-first translation for the CharacterStatus subwindow.
/// </summary>
public class CharacterStatusSubWindowHandler : DbFirstGameWindowAddonHandler
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterStatusSubWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterStatusSubWindowHandler(
        Config config,
        TranslationService translationService)
        : base(
            addonName: "CharacterStatus",
            config: config,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateCharacterWindow,
            useAtkValues: true,
            stringArrayDataType: StringArrayType.Character)
    {
    }
}
