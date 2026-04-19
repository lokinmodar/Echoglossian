// <copyright file="CharacterClassSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles translation for the "CharacterClass" addon using visible text
///     nodes only.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public unsafe class CharacterClassSubWindowHandler
    : CharacterTextNodeWindowHandlerBase
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterClassSubWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterClassSubWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "CharacterClass",
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
        return textNode != null &&
               !string.IsNullOrWhiteSpace(visibleText);
    }
}
