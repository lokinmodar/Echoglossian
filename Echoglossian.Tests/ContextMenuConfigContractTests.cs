// <copyright file="ContextMenuConfigContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated ContextMenu configuration and UI contract.
/// </summary>
public sealed class ContextMenuConfigContractTests
{
    /// <summary>
    ///     Ensures the config declares dedicated ContextMenu settings.
    /// </summary>
    [Fact]
    public void Config_DefinesDedicatedContextMenuToggleAndDisplayMode()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root.FullName, "Config.cs"));

        Assert.Contains("TranslateContextMenu", source, StringComparison.Ordinal);
        Assert.Contains(
            "ContextMenuTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the game-windows tab draws a standalone ContextMenu section.
    /// </summary>
    [Fact]
    public void GameWindowsTab_DrawsStandaloneContextMenuSection()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "GameWindowsTab.cs"));

        Assert.Contains(
            "Resources.TranslateContextMenuWindow",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ref config.TranslateContextMenu",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ref config.ContextMenuTranslationDisplayMode",
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
