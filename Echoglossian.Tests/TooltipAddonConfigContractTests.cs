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
