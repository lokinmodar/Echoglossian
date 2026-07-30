// <copyright file="ContextMenuRuntimeContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards ContextMenu runtime capture and hover registration contracts.
/// </summary>
public sealed class ContextMenuRuntimeContractTests
{
    /// <summary>
    ///     Ensures ContextMenu captures the full visible row chain rather than
    ///     a fixed number of menu slots.
    /// </summary>
    [Fact]
    public void ContextMenu_CapturesVariableVisibleRowChain()
    {
        var source = ReadContextMenuHandlerSource();

        Assert.Contains("ResolveContextMenuRowTextNodes", source);
        Assert.Contains("ComponentNodes[3]", source);
        Assert.Contains("Next", source);
        Assert.DoesNotContain("for (var i = 0; i < 5;", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures ContextMenu registers each hover target from its visible
    ///     row collision bounds.
    /// </summary>
    [Fact]
    public void ContextMenu_RegistersHoverTargetsFromRowCollisionBounds()
    {
        var source = ReadContextMenuHandlerSource();

        Assert.Contains("TryRegisterCustomHoverTooltips", source);
        Assert.Contains("RegisterTranslatedHoverTooltip", source);
        Assert.Contains("ComponentRoot", source);
    }

    /// <summary>
    ///     Reads the ContextMenu handler source.
    /// </summary>
    /// <returns>The handler source text.</returns>
    private static string ReadContextMenuHandlerSource()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "MainMenu",
            "ContextMenuHandler.cs"));
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
