// <copyright file="CharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles DB-first translation for the main character window.
/// </summary>
public unsafe class CharacterWindowHandler : CharacterTextNodeWindowHandlerBase
{
    private static readonly HashSet<string> StableHeaderTexts =
    [
        "Character",
        "Attributes",
        "Profile",
        "Classes/Jobs",
        "Reputation",
    ];

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "Character",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            stringArrayType: StringArrayType.Character,
            useAtkValues: true)
    {
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return base.ShouldCaptureTextNode(textNode, visibleText) ||
               this.CanCaptureSupplementalCharacterText(visibleText) ||
               IsStableCharacterHeaderText(visibleText);
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureStringArrayValues(
        byte subscribedAddonsCount)
    {
        return !this.AreDynamicCharacterSubwindowsVisible();
    }

    /// <inheritdoc />
    protected override bool ShouldWriteStringArrayValues(
        byte subscribedAddonsCount)
    {
        return !this.AreDynamicCharacterSubwindowsVisible();
    }

    /// <inheritdoc />
    protected override bool ShouldReuseCompatiblePayloads()
    {
        return false;
    }

    /// <inheritdoc />
    protected override bool ShouldRequestStringArrayUpdates()
    {
        return true;
    }

    /// <inheritdoc />
    private protected override bool TryApplyCustomTextNodePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        return this.ApplyVisibleTextNodesByValue(
            addon,
            sourcePayload,
            targetPayload);
    }

    /// <summary>
    ///     Determines whether one Character-family tab addon that owns its own
    ///     visible text-node surface is currently visible.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when a dynamic Character-family subwindow is
    ///     visible; otherwise <see langword="false" />.
    /// </returns>
    private bool AreDynamicCharacterSubwindowsVisible()
    {
        return this.IsAddonVisible("CharacterClass") ||
               this.IsAddonVisible("CharacterStatus") ||
               this.IsAddonVisible("CharacterProfile") ||
               this.IsAddonVisible("CharacterRepute");
    }

    /// <summary>
    ///     Determines whether one visible text belongs to the stable root
    ///     header of the Character window and should therefore be captured
    ///     even when it is not yet part of the canonical lookup.
    /// </summary>
    /// <param name="visibleText">The currently visible text.</param>
    /// <returns>
    ///     <see langword="true" /> when the text is one stable root-header
    ///     label; otherwise <see langword="false" />.
    /// </returns>
    internal static bool IsStableCharacterHeaderText(string visibleText)
    {
        return StableHeaderTexts.Contains(visibleText);
    }
}
