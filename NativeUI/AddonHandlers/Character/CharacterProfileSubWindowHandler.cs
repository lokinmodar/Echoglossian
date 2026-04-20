// <copyright file="CharacterProfileSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles translation for the "CharacterProfile" addon using visible text
///     nodes only.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public unsafe class CharacterProfileSubWindowHandler
    : CharacterTextNodeWindowHandlerBase
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterProfileSubWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterProfileSubWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "CharacterProfile",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService)
    {
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return base.ShouldCaptureTextNode(textNode, visibleText) ||
               this.CanCaptureSupplementalCharacterText(visibleText);
    }
}
