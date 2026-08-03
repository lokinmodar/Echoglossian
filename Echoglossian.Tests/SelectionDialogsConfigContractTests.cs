// <copyright file="SelectionDialogsConfigContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the config contract required to re-enable selection-dialog
///     translation modes.
/// </summary>
public sealed class SelectionDialogsConfigContractTests
{
    /// <summary>
    ///     Ensures the config declares explicit display-mode fields for the
    ///     selection-dialog surfaces.
    /// </summary>
    [Fact]
    public void Config_DefinesSelectionDialogDisplayModes()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "Config.cs"));

        Assert.Contains(
            "SelectYesNoTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectOkTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectStringTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SelectIconStringTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the config declares an independent toggle for the
    ///     icon-bearing dialog rather than sharing the SelectString toggle.
    /// </summary>
    [Fact]
    public void Config_DefinesDedicatedSelectIconStringToggle()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "Config.cs"));

        Assert.Contains(
            "TranslateSelectIconString",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the configuration UI uses the shared native or tooltip
    ///     display-mode helper and exposes SelectIconString independently.
    /// </summary>
    [Fact]
    public void SelectionDialogsTab_UsesSharedTooltipDisplayModesForAllDialogs()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "SelectionDialogsTab.cs"));

        Assert.Contains(
            "TranslationDisplayModeUiHelper.DrawDisplayModeCombo",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "config.TranslateSelectIconString",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "config.SelectIconStringTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Resources.TranslateSelectIconStringLabel",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "OverlayDisplayModeOverlayTranslationOnly",
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
