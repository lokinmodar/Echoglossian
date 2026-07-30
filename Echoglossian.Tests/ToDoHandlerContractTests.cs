// <copyright file="ToDoHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated ToDo runtime wiring contract.
/// </summary>
public sealed class ToDoHandlerContractTests
{
    /// <summary>
    ///     Ensures addon wiring registers the dedicated ToDo handler.
    /// </summary>
    [Fact]
    public void AddonHandlerWiring_RegistersDedicatedToDoHandler()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));

        Assert.Contains("(AddonName: \"ToDo\"", source, StringComparison.Ordinal);
        Assert.Contains("new ToDoHandler(", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures countdown ticks are excluded from ToDo translation identity
    ///     and can reuse the current presentation.
    /// </summary>
    [Fact]
    public void ToDoHandler_ExcludesTimerNodeAndReusesSnapshotOnCountdownTicks()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "ToDoHandler.cs"));

        Assert.Contains("IsTimerNode", source, StringComparison.Ordinal);
        Assert.Contains("ComputeSourceContentHash", source, StringComparison.Ordinal);
        Assert.Contains("TryReuseCurrentToDoPresentation", source, StringComparison.Ordinal);
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
