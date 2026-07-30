// <copyright file="TooltipAddonNativePresentationContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards native Tooltip addon presentation behavior that depends on text
///     reflow and config layout.
/// </summary>
public sealed class TooltipAddonNativePresentationContractTests
{
    /// <summary>
    ///     Ensures the dedicated Tooltip handler owns a custom text-node apply
    ///     path that reflows the text node and its background instead of
    ///     relying on raw <c>SetText</c> only.
    /// </summary>
    [Fact]
    public void TooltipHandler_UsesNativeTextReflowHelpers()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Common",
            "TooltipHandler.cs"));

        Assert.Contains(
            "TryApplyCustomTextNodePayload",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ApplyTextReplacementWithInferredReflow",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RestoreLayoutSnapshot",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated Tooltip addon toggle is rendered after the
    ///     shared hover-tooltip appearance controls so addon-specific toggles
    ///     do not mix with formatting sliders from other flows.
    /// </summary>
    [Fact]
    public void TooltipTab_RendersDedicatedTooltipControlsAfterAppearanceSection()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "TooltipTab.cs"));

        var appearanceIndex = source.IndexOf(
            "Resources.HoverTooltipAppearanceSectionLabel",
            StringComparison.Ordinal);
        var tooltipAddonIndex = source.IndexOf(
            "Resources.TranslateTooltipAddonLabel",
            StringComparison.Ordinal);

        Assert.True(appearanceIndex >= 0);
        Assert.True(tooltipAddonIndex >= 0);
        Assert.True(
            appearanceIndex < tooltipAddonIndex,
            "Tooltip addon controls should render after hover appearance settings.");
    }

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
