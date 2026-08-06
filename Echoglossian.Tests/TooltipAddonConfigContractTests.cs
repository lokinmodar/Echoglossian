// <copyright file="TooltipAddonConfigContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated Tooltip addon configuration contract.
/// </summary>
public sealed class TooltipAddonConfigContractTests
{
    /// <summary>
    ///     Ensures the config declares a dedicated toggle and display mode for
    ///     the Tooltip addon runtime.
    /// </summary>
    [Fact]
    public void Config_DefinesDedicatedTooltipAddonSettings()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "Config.cs"));

        Assert.Contains("TranslateTooltipAddon", source, StringComparison.Ordinal);
        Assert.Contains("TooltipAddonTranslationDisplayMode", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the Tooltip addon anchored overlay has its own persisted
    ///     appearance and presentation settings.
    /// </summary>
    [Fact]
    public void Config_DefinesDedicatedTooltipAddonOverlaySettings()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "Config.cs"));

        Assert.Contains("TooltipAddonOverlayTextColor", source, StringComparison.Ordinal);
        Assert.Contains(
            "TooltipAddonHideNativeTooltipWhenOverlayActive",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TooltipAddonOverlayFontScaleAdjustment",
            source,
            StringComparison.Ordinal);
        Assert.Contains("TooltipAddonOverlayMaxWidthMode", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures ActionDetail and ItemDetail overlays keep their own shared
    ///     persisted appearance settings instead of reusing the Tooltip addon
    ///     overlay bucket.
    /// </summary>
    [Fact]
    public void Config_DefinesDedicatedActionItemDetailOverlaySettings()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "Config.cs"));

        Assert.Contains(
            "ActionItemDetailOverlayTextColor",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ActionItemDetailOverlayFontScaleAdjustment",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ActionItemDetailOverlayLineHeightScale",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ActionItemDetailOverlayMaxWidthMode",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the tooltip settings UI exposes the dedicated Tooltip addon
    ///     controls separately from ActionDetail and ItemDetail.
    /// </summary>
    [Fact]
    public void TooltipTab_RendersDedicatedTooltipAddonControls()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "TooltipTab.cs"));

        Assert.Contains("config.TranslateTooltipAddon", source, StringComparison.Ordinal);
        Assert.Contains("config.TooltipAddonTranslationDisplayMode", source, StringComparison.Ordinal);
        Assert.Contains("Resources.TranslateTooltipAddonLabel", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the tooltip settings UI exposes a dedicated ActionDetail /
    ///     ItemDetail overlay appearance bucket instead of relying on the
    ///     hover-tooltip controls.
    /// </summary>
    [Fact]
    public void TooltipTab_RendersDedicatedActionItemDetailOverlayControls()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "TooltipTab.cs"));

        Assert.Contains(
            "Resources.ActionItemDetailOverlayAppearanceSectionLabel",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "config.ActionItemDetailOverlayFontScaleAdjustment",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "config.ActionItemDetailOverlayMaxWidthMode",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
