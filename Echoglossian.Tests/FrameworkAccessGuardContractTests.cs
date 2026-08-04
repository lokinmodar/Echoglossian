// <copyright file="FrameworkAccessGuardContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies that framework access guards remain conservative during login
///     and zone transition races.
/// </summary>
public sealed class FrameworkAccessGuardContractTests
{
    /// <summary>
    ///     Ensures player-scoped framework access requires a stable readiness
    ///     gate, territory, and a valid object-table local player.
    /// </summary>
    [Fact]
    public void PlayerScopedFrameworkAccess_uses_stable_local_player_gate()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "FrameworkAccessGuard.cs"));

        Assert.Contains(
            "PlayerScopedFrameworkReadinessGate",
            source);
        Assert.Contains(
            "TimeSpan.FromSeconds(10)",
            source);
        Assert.Contains(
            "ClientStateInterface.TerritoryType,",
            source);
        Assert.Contains(
            "localPlayer?.IsValid() == true",
            source);
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
