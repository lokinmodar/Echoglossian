// <copyright file="RepositoryGuidanceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers repository-level guidance that must stay versioned with the code.
/// </summary>
public class RepositoryGuidanceTests
{
    /// <summary>
    /// Ensures the agent guidance is versioned and points runtime validation at
    /// the DalaMock-backed harness when deeper Dalamud behavior needs coverage.
    /// </summary>
    [Fact]
    public void AgentsGuidance_documents_mock_runtime_validation()
    {
        var root = FindRepositoryRoot();
        var agentsPath = Path.Combine(root.FullName, "AGENTS.md");
        var validationPath = Path.Combine(
            root.FullName,
            ".github",
            "instructions",
            "validation.instructions.md");

        Assert.True(File.Exists(agentsPath), "AGENTS.md must be committed.");
        Assert.True(
            File.Exists(validationPath),
            "validation instructions must be committed.");

        var agentsText = File.ReadAllText(agentsPath);
        var validationText = File.ReadAllText(validationPath);

        Assert.Contains("Echoglossian.Mock", agentsText);
        Assert.Contains("DalaMock", agentsText);
        Assert.Contains("Echoglossian.Mock.Tests", agentsText);
        Assert.Contains("Echoglossian.Mock", validationText);
        Assert.Contains("DalaMock", validationText);
        Assert.Contains("Echoglossian.Mock.Tests", validationText);
    }

    /// <summary>
    /// Ensures game-data and native addon behavior is explicitly routed through
    /// the Mock/DalaMock validation rail instead of being treated as unit-only.
    /// </summary>
    [Fact]
    public void AgentsGuidance_requires_mock_validation_for_game_data_behavior()
    {
        var root = FindRepositoryRoot();
        var agentsPath = Path.Combine(root.FullName, "AGENTS.md");
        var validationPath = Path.Combine(
            root.FullName,
            ".github",
            "instructions",
            "validation.instructions.md");

        var agentsText = File.ReadAllText(agentsPath);
        var validationText = File.ReadAllText(validationPath);

        Assert.Contains("real game data", agentsText);
        Assert.Contains("native UI payload", agentsText);
        Assert.Contains("extend `Echoglossian.Mock` or DalaMock", agentsText);
        Assert.Contains("real game data", validationText);
        Assert.Contains("native UI payload", validationText);
        Assert.Contains("extend `Echoglossian.Mock` or DalaMock", validationText);
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
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
