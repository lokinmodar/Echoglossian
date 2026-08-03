// <copyright file="GameMainMenuHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards wiring contracts for the shared game-main-menu handler family.
/// </summary>
public sealed class GameMainMenuHandlerContractTests
{
    /// <summary>
    ///     Ensures addon wiring includes the dedicated SystemMenu handler in
    ///     the shared game-main-menu scope.
    /// </summary>
    [Fact]
    public void AddonHandlerWiring_RegistersSystemMenuHandler()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));

        Assert.Contains("SystemMenu", source, StringComparison.Ordinal);
        Assert.Contains("SystemMenuHandler", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated SystemMenu handler source file exists.
    /// </summary>
    [Fact]
    public void SystemMenuHandler_SourceFileExists()
    {
        var root = FindRepositoryRoot();
        Assert.True(
            File.Exists(Path.Combine(
                root.FullName,
                "NativeUI",
                "AddonHandlers",
                "MainMenu",
                "SystemMenuHandler.cs")),
            "Expected SystemMenu handler source file to exist.");
    }

    /// <summary>
    ///     Ensures the SystemMenu handler stays on the shared game-main-menu
    ///     DB-first path with text-node capture.
    /// </summary>
    [Fact]
    public void SystemMenuHandler_UsesGameMainMenuTextNodeRuntime()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "MainMenu",
            "SystemMenuHandler.cs"));

        Assert.Contains("addonName: \"SystemMenu\"", source, StringComparison.Ordinal);
        Assert.Contains("configuration.TranslateGameMainMenu", source, StringComparison.Ordinal);
        Assert.Contains(
            "configuration.GameMainMenuWindowTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
        Assert.Contains("useAtkValues: false", source, StringComparison.Ordinal);
        Assert.Contains("useTextNodes: true", source, StringComparison.Ordinal);
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
