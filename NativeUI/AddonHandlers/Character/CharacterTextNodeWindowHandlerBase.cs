// <copyright file="CharacterTextNodeWindowHandlerBase.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Provides the shared DB-first runtime for Character-family windows that
///     should only capture stable sheet-backed text nodes.
/// </summary>
public abstract unsafe class CharacterTextNodeWindowHandlerBase
    : DbFirstGameWindowAddonHandler
{
    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterTextNodeWindowHandlerBase" /> class.
    /// </summary>
    /// <param name="addonName">The target addon name.</param>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    protected CharacterTextNodeWindowHandlerBase(
        string addonName,
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService,
        StringArrayType? stringArrayType = null)
        : base(
            addonName: addonName,
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateCharacterWindow,
            useAtkValues: false,
            useTextNodes: true,
            stringArrayDataType: stringArrayType,
            displayModeSelector: static configuration =>
                configuration.CharacterWindowTranslationDisplayMode)
    {
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return textNode != null &&
               textNode->TextId != 0 &&
               !string.IsNullOrWhiteSpace(visibleText);
    }

    /// <inheritdoc />
    protected override bool ShouldRefreshAppliedStateOnPreDraw()
    {
        return false;
    }
}
