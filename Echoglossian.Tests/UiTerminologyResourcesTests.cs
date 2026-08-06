// <copyright file="UiTerminologyResourcesTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Properties;

using System;
using System.Globalization;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers user-facing terminology for detail windows versus hover tooltips.
/// </summary>
public class UiTerminologyResourcesTests
{
    private sealed class ResourceCultureScope : IDisposable
    {
        private readonly CultureInfo? previousCulture;

        public ResourceCultureScope(CultureInfo culture)
        {
            this.previousCulture = Resources.Culture;
            Resources.Culture = culture;
        }

        public void Dispose()
        {
            Resources.Culture = this.previousCulture;
        }
    }

    /// <summary>
    ///     Ensures the detail-window toggle no longer implies that the same
    ///     switch owns all hover tooltip behavior.
    /// </summary>
    [Fact]
    public void ActionAndItemDetailToggleLabel_UsesDetailTerminology()
    {
        using var cultureScope = new ResourceCultureScope(
            CultureInfo.InvariantCulture);
        Assert.Equal(
            "Enable action/item detail translation",
            Resources.ActionAndItemTooltipsToggleLabel);
    }

    /// <summary>
    ///     Ensures the hover-tooltip appearance section is explicitly labeled
    ///     as hover-only so it is not confused with ActionDetail and
    ///     ItemDetail.
    /// </summary>
    [Fact]
    public void HoverTooltipAppearanceSectionLabel_UsesHoverTerminology()
    {
        using var cultureScope = new ResourceCultureScope(
            CultureInfo.InvariantCulture);
        Assert.Equal(
            "Hover Plugin Tooltip appearance",
            Resources.HoverTooltipAppearanceSectionLabel);
    }

    /// <summary>
    ///     Ensures the shared description explicitly calls out hover tooltips
    ///     instead of generic tooltip wording.
    /// </summary>
    [Fact]
    public void HoverTooltipAppearanceDescription_UsesHoverTerminology()
    {
        using var cultureScope = new ResourceCultureScope(
            CultureInfo.InvariantCulture);
        Assert.Equal(
            "These settings apply to hover Plugin Tooltips used by quest and DB-first UI surfaces.",
            Resources.HoverTooltipAppearanceDescription);
    }

    /// <summary>
    ///     Ensures ActionDetail and ItemDetail overlay settings are labeled as
    ///     detail overlays so they are not confused with hover tooltips or the
    ///     Tooltip addon.
    /// </summary>
    [Fact]
    public void ActionItemDetailOverlayAppearanceSectionLabel_UsesDetailOverlayTerminology()
    {
        using var cultureScope = new ResourceCultureScope(
            CultureInfo.InvariantCulture);
        Assert.Equal(
            "Action/item detail overlay appearance",
            Resources.ActionItemDetailOverlayAppearanceSectionLabel);
    }

    /// <summary>
    ///     Ensures the tab title reflects that this area now combines detail
    ///     windows with hover-tooltip controls.
    /// </summary>
    [Fact]
    public void TooltipTabTitle_UsesCombinedDetailsAndHoverTerminology()
    {
        using var cultureScope = new ResourceCultureScope(
            CultureInfo.InvariantCulture);
        Assert.Equal(
            "Details & hover",
            Resources.TooltipTabTitle);
    }
}
