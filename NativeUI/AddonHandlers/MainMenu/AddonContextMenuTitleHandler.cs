// <copyright file="AddonContextMenuTitleHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

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
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public AddonContextMenuTitleHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "AddonContextMenuTitle",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateGameMainMenu,
            useAtkValues: false,
            useTextNodes: true,
            displayModeSelector: static configuration =>
                configuration.GameMainMenuWindowTranslationDisplayMode)
    {
    }

    /// <summary>
    ///     The context-menu title addon reuses the same ATK value slots for
    ///     different submenu contexts, so compatible supersets are not stable
    ///     enough to reuse safely.
    /// </summary>
    /// <returns>Always <see langword="false" />.</returns>
    protected override bool ShouldReuseCompatiblePayloads()
    {
        return false;
    }

    /// <summary>
    ///     The context-menu title addon reuses the same visible text nodes
    ///     across multiple submenu contexts, so stale translated values from
    ///     the previous context must be restored before capturing a new
    ///     original-facing payload.
    /// </summary>
    /// <returns>Always <see langword="true" />.</returns>
    protected override bool ShouldRestoreStaleTranslatedTextNodesOnPayloadChange()
    {
        return true;
    }
}
