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
    /// Ensures the Issue 258 performance protocol remains versioned and includes
    /// comparable frame-time, SQLite, queue, and log evidence.
    /// </summary>
    [Fact]
    public void Issue258Baseline_documents_reproducible_persistence_evidence()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(
            root.FullName,
            "docs",
            "issue-258-async-persistence-baseline.md");

        Assert.True(File.Exists(path), "Issue 258 baseline protocol must be committed.");
        var text = File.ReadAllText(path);
        Assert.Contains("https://github.com/lokinmodar/Echoglossian/issues/258", text);
        Assert.Contains("median", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p95", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p99", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WAL", text, StringComparison.Ordinal);
        Assert.Contains("Echoglossian.log", text, StringComparison.Ordinal);
        Assert.Contains("accepted-quest-prefetch-activity.log", text, StringComparison.Ordinal);
        Assert.Contains("DB-2", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the local validation rail audits synchronous database work before
    /// running the .NET build and test commands.
    /// </summary>
    [Fact]
    public void LocalValidation_runs_sync_database_audit_before_dotnet()
    {
        var root = FindRepositoryRoot();
        var validationPath = Path.Combine(
            root.FullName,
            "scripts",
            "validate-local-tests.ps1");
        var text = File.ReadAllText(validationPath);
        var auditIndex = text.IndexOf(
            "audit-sync-db-hotpaths.ps1",
            StringComparison.Ordinal);
        var dotnetIndex = text.IndexOf("dotnet restore", StringComparison.Ordinal);

        Assert.True(auditIndex >= 0, "Validation must invoke the sync DB audit.");
        Assert.True(dotnetIndex > auditIndex, "Audit must run before dotnet restore.");
    }

    /// <summary>
    /// Ensures pull requests run the synchronous database hot-path audit on Windows.
    /// </summary>
    [Fact]
    public void ContinuousIntegration_runs_sync_database_audit_without_baseline_updates()
    {
        var root = FindRepositoryRoot();
        var workflowPath = Path.Combine(
            root.FullName,
            ".github",
            "workflows",
            "audit-sync-db-hotpaths.yml");

        Assert.True(File.Exists(workflowPath), "The sync DB audit workflow must be committed.");
        var text = File.ReadAllText(workflowPath);

        Assert.Contains("windows-latest", text, StringComparison.Ordinal);
        Assert.Contains("audit-sync-db-hotpaths.ps1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateBaseline", text, StringComparison.Ordinal);
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
