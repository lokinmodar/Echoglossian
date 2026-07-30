// <copyright file="ContextMenuHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated ContextMenu runtime wiring contract.
/// </summary>
public sealed class ContextMenuHandlerContractTests
{
    /// <summary>
    ///     Ensures addon wiring registers the dedicated ContextMenu handler.
    /// </summary>
    [Fact]
    public void AddonHandlerWiring_RegistersContextMenuHandler()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));

        Assert.Contains("(AddonName: \"ContextMenu\"", source, StringComparison.Ordinal);
        Assert.Contains("new ContextMenuHandler(", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the dedicated ContextMenu handler uses its own config and
    ///     text-node DB-first runtime.
    /// </summary>
    [Fact]
    public void ContextMenuHandler_UsesDedicatedConfigAndTextNodeRuntime()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "MainMenu",
            "ContextMenuHandler.cs"));

        Assert.Contains("addonName: \"ContextMenu\"", source, StringComparison.Ordinal);
        Assert.Contains("configuration.TranslateContextMenu", source, StringComparison.Ordinal);
        Assert.Contains("configuration.ContextMenuTranslationDisplayMode", source, StringComparison.Ordinal);
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
