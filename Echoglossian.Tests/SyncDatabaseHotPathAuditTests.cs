// <copyright file="SyncDatabaseHotPathAuditTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Verifies the repository synchronous-database audit command contract.
/// </summary>
public sealed class SyncDatabaseHotPathAuditTests
{
    /// <summary>
    /// Ensures an untracked synchronous save in a runtime path fails the audit.
    /// </summary>
    [Fact]
    public void AuditScript_NewRuntimeSaveChanges_ReturnsFailure()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            "NativeUI/Helpers/Fixture.cs",
            "public void Tick() { context.SaveChanges(); }");

        var result = this.RunAudit(fixture, updateBaseline: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("sync-ef-save", result.Output, StringComparison.Ordinal);
        Assert.Contains("NativeUI/Helpers/Fixture.cs", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures synchronous set-based update and delete operations fail the audit.
    /// </summary>
    /// <param name="methodName">The synchronous bulk operation method name.</param>
    [Theory]
    [InlineData("ExecuteUpdate")]
    [InlineData("ExecuteDelete")]
    public void AuditScript_SynchronousBulkWrite_ReturnsFailure(string methodName)
    {
        using var fixture = this.CreateFixture();
        var statement = methodName == "ExecuteUpdate"
            ? "query.ExecuteUpdate(setters => setters);"
            : "query.ExecuteDelete();";
        fixture.WriteSource("DBHelpers/BulkWriteFixture.cs", statement);

        var result = this.RunAudit(fixture, updateBaseline: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("sync-ef-bulk-write", result.Output, StringComparison.Ordinal);
        Assert.Contains(methodName, result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a persistence-helper invocation split across source lines fails
    /// the audit.
    /// </summary>
    [Fact]
    public void AuditScript_WrappedPersistenceHelperCall_ReturnsFailure()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            "NativeUI/WrappedHelperFixture.cs",
            string.Join(
                Environment.NewLine,
                "StringArrayDataPersistenceHelper",
                "    .InsertStringArrayData(config, row);"));

        var result = this.RunAudit(fixture, updateBaseline: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("persistence-helper-call", result.Output, StringComparison.Ordinal);
        Assert.Contains("InsertStringArrayData", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures an untracked synchronous database context construction in the
    /// startup cache path fails the audit and is assigned an approved stage.
    /// </summary>
    [Fact]
    public void AuditScript_NewCacheDbContext_ReturnsFailureWithApprovedStage()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            "Cache/Fixture.cs",
            "public void Load() { using var context = new EchoglossianDbContext(configDir); }");

        var result = this.RunAudit(fixture, updateBaseline: false);
        var report = File.ReadAllText(fixture.ReportPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("direct-db-context", result.Output, StringComparison.Ordinal);
        Assert.Contains("Cache/Fixture.cs", result.Output, StringComparison.Ordinal);
        Assert.Contains("## DB-7", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures every production source directory is audited without requiring
    /// an explicit scanner allowlist entry.
    /// </summary>
    /// <param name="sourceDirectory">The production source directory.</param>
    [Theory]
    [InlineData("PluginRuntime")]
    [InlineData("Services")]
    public void AuditScript_ProductionSourceDirectory_ReturnsFailure(
        string sourceDirectory)
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            $"{sourceDirectory}/Fixture.cs",
            "public void Save() { context.SaveChanges(); }");

        var result = this.RunAudit(fixture, updateBaseline: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("sync-ef-save", result.Output, StringComparison.Ordinal);
        Assert.Contains($"{sourceDirectory}/Fixture.cs", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures synchronous queries through the plugin database context fail the
    /// audit outside the legacy database directories.
    /// </summary>
    /// <param name="sourceDirectory">The production source directory.</param>
    [Theory]
    [InlineData("Cache")]
    [InlineData("NativeUI/Helpers")]
    public void AuditScript_DbContextQueryOutsideDatabaseDirectories_ReturnsFailure(
        string sourceDirectory)
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            $"{sourceDirectory}/Fixture.cs",
            "public void Load(EchoglossianDbContext context) { _ = context.Rows.ToList(); }");

        var result = this.RunAudit(fixture, updateBaseline: false);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("sync-ef-query", result.Output, StringComparison.Ordinal);
        Assert.Contains($"{sourceDirectory}/Fixture.cs", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures an in-memory LINQ terminal in a database-context source file is
    /// not misclassified as synchronous database debt.
    /// </summary>
    [Fact]
    public void AuditScript_InMemoryQueryInDbContextFile_IsNotDatabaseDebt()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            "Cache/Fixture.cs",
            string.Join(
                Environment.NewLine,
                "public void Load(EchoglossianDbContext context) { _ = context.Rows.ToList().Select(row => row.Id).ToList(); }",
                "public void Normalize(List<int> rows) { _ = rows.ToList(); }"));

        var result = this.RunAudit(fixture, updateBaseline: true);
        using var document = JsonDocument.Parse(File.ReadAllText(fixture.BaselinePath));
        var queryFindings = document.RootElement
            .GetProperty("allowedFindings")
            .EnumerateArray()
            .Where(finding => finding.GetProperty("category").GetString() == "sync-ef-query")
            .ToArray();

        Assert.Equal(0, result.ExitCode);
        Assert.Single(queryFindings);
    }

    /// <summary>
    /// Ensures an explicitly captured baseline passes unchanged and fails after
    /// the finding is removed without refreshing the baseline.
    /// </summary>
    [Fact]
    public void AuditScript_BaselineLifecycle_DetectsResolvedDebt()
    {
        using var fixture = this.CreateFixture();
        const string relativePath = "DBHelpers/Fixture.cs";
        fixture.WriteSource(relativePath, "public void Save() { context.SaveChanges(); }");

        var update = this.RunAudit(fixture, updateBaseline: true);
        var unchanged = this.RunAudit(fixture, updateBaseline: false);
        fixture.DeleteSource(relativePath);
        var resolved = this.RunAudit(fixture, updateBaseline: false);

        Assert.Equal(0, update.ExitCode);
        Assert.Equal(0, unchanged.ExitCode);
        Assert.NotEqual(0, resolved.ExitCode);
        Assert.Contains(
            "resolved baseline finding",
            resolved.Output,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures changing baseline metadata without regenerating the baseline
    /// fails the exact-contract comparison.
    /// </summary>
    [Fact]
    public void AuditScript_ChangedBaselineStage_ReturnsFailure()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            "DBHelpers/ActionFixture.cs",
            "public void Save() { context.SaveChanges(); }");
        var update = this.RunAudit(fixture, updateBaseline: true);
        var baseline = File.ReadAllText(fixture.BaselinePath);
        Assert.Contains("\"stage\": \"DB-4\"", baseline, StringComparison.Ordinal);
        File.WriteAllText(
            fixture.BaselinePath,
            baseline.Replace(
                "\"stage\": \"DB-4\"",
                "\"stage\": \"DB-7\"",
                StringComparison.Ordinal));

        var changed = this.RunAudit(fixture, updateBaseline: false);

        Assert.Equal(0, update.ExitCode);
        Assert.NotEqual(0, changed.ExitCode);
        Assert.Contains(
            "changed baseline finding",
            changed.Output,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures tests, output, vendor code, and migration snapshots are excluded.
    /// </summary>
    [Fact]
    public void AuditScript_NonRuntimePaths_AreExcluded()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource("Echoglossian.Tests/Fixture.cs", "context.SaveChanges();");
        fixture.WriteSource("EFCoreSqlite/Migrations/Fixture.cs", "context.SaveChanges();");
        fixture.WriteSource("vendor/Fixture.cs", "context.SaveChanges();");
        fixture.WriteSource("NativeUI/obj/Fixture.cs", "context.SaveChanges();");

        var result = this.RunAudit(fixture, updateBaseline: false);

        Assert.Equal(0, result.ExitCode);
    }

    /// <summary>
    /// Ensures generated findings receive an approved migration stage.
    /// </summary>
    [Fact]
    public void AuditScript_UpdateBaseline_AssignsApprovedStage()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            "NativeUI/Helpers/ReferenceTextFixture.cs",
            "ReferenceTextPersistenceHelper.FindReferenceText(config, probe);");

        var result = this.RunAudit(fixture, updateBaseline: true);
        using var document = JsonDocument.Parse(File.ReadAllText(fixture.BaselinePath));
        var finding = document.RootElement
            .GetProperty("allowedFindings")
            .EnumerateArray()
            .Single();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("DB-2", finding.GetProperty("stage").GetString());
        Assert.False(string.IsNullOrWhiteSpace(finding.GetProperty("id").GetString()));
    }

    /// <summary>
    /// Ensures removing one repeated synchronous call fails without refreshing
    /// the baseline, even when the remaining call is in the same source file.
    /// </summary>
    [Fact]
    public void AuditScript_RepeatedCallLifecycle_DetectsOneResolvedOccurrence()
    {
        using var fixture = this.CreateFixture();
        const string relativePath = "DBHelpers/RepeatedSaveFixture.cs";
        fixture.WriteSource(
            relativePath,
            "public void Save() { context.SaveChanges(); context.SaveChanges(); }");

        var update = this.RunAudit(fixture, updateBaseline: true);
        using var baseline = JsonDocument.Parse(File.ReadAllText(fixture.BaselinePath));
        var ids = baseline.RootElement
            .GetProperty("allowedFindings")
            .EnumerateArray()
            .Select(finding => finding.GetProperty("id").GetString())
            .ToArray();
        fixture.WriteSource(
            relativePath,
            "public void Save() { context.SaveChanges(); }");
        var resolved = this.RunAudit(fixture, updateBaseline: false);

        Assert.Equal(0, update.ExitCode);
        Assert.Equal(2, ids.Length);
        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Equal(2, id!.Count(character => character == '|')));
        Assert.All(ids, id => Assert.DoesNotContain("line", id!, StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(0, resolved.ExitCode);
        Assert.Contains(
            "resolved baseline finding",
            resolved.Output,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures baseline and Markdown report bytes are identical when generated
    /// by Windows PowerShell and PowerShell 7.
    /// </summary>
    [Fact]
    public void AuditScript_UpdateBaseline_IsByteStableAcrossPowerShellEngines()
    {
        using var fixture = this.CreateFixture();
        fixture.WriteSource(
            "NativeUI/Helpers/Fixture.cs",
            "public void Tick() { context.SaveChanges(); }");

        var windowsPowerShell = this.RunAudit(
            fixture,
            updateBaseline: true,
            shellFileName: "powershell.exe");
        var windowsBaseline = File.ReadAllBytes(fixture.BaselinePath);
        var windowsReport = File.ReadAllBytes(fixture.ReportPath);
        var powerShell7 = this.RunAudit(
            fixture,
            updateBaseline: true,
            shellFileName: "pwsh.exe");

        Assert.Equal(0, windowsPowerShell.ExitCode);
        Assert.Equal(0, powerShell7.ExitCode);
        Assert.Equal(windowsBaseline, File.ReadAllBytes(fixture.BaselinePath));
        Assert.Equal(windowsReport, File.ReadAllBytes(fixture.ReportPath));
    }

    /// <summary>
    /// Creates an isolated audit fixture repository.
    /// </summary>
    /// <returns>The isolated audit fixture repository.</returns>
    private AuditFixture CreateFixture()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "EchoglossianSyncDbAuditTests",
            Guid.NewGuid().ToString("N"));
        return new AuditFixture(root);
    }

    /// <summary>
    /// Runs the audit command against an isolated fixture repository.
    /// </summary>
    /// <param name="fixture">The fixture repository to audit.</param>
    /// <param name="updateBaseline">Whether the audit should refresh its baseline.</param>
    /// <returns>The process result.</returns>
    private AuditProcessResult RunAudit(
        AuditFixture fixture,
        bool updateBaseline,
        string shellFileName = "powershell.exe")
    {
        var scriptPath = Path.Combine(
            this.FindRepositoryRoot().FullName,
            "scripts",
            "audit-sync-db-hotpaths.ps1");
        var startInfo = new ProcessStartInfo
        {
            FileName = shellFileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
                 {
                     "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                     "-File", scriptPath,
                     "-RepositoryRoot", fixture.RootPath,
                     "-BaselinePath", fixture.BaselinePath,
                     "-ReportPath", fixture.ReportPath,
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (updateBaseline)
        {
            startInfo.ArgumentList.Add("-UpdateBaseline");
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var standardOutput = process!.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30_000), "Audit process timed out.");
        return new AuditProcessResult(
            process.ExitCode,
            standardOutput + Environment.NewLine + standardError);
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private DirectoryInfo FindRepositoryRoot()
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

    /// <summary>
    /// Represents the result of an audit process invocation.
    /// </summary>
    /// <param name="ExitCode">The audit process exit code.</param>
    /// <param name="Output">The audit process combined output.</param>
    private sealed record AuditProcessResult(int ExitCode, string Output);

    /// <summary>
    /// Provides an isolated file-system fixture for audit process tests.
    /// </summary>
    private sealed class AuditFixture : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuditFixture" /> class.
        /// </summary>
        /// <param name="rootPath">The root path for the fixture repository.</param>
        internal AuditFixture(string rootPath)
        {
            this.RootPath = rootPath;
            this.BaselinePath = Path.Combine(rootPath, "baseline.json");
            this.ReportPath = Path.Combine(rootPath, "report.md");
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(this.BaselinePath, "{\"schemaVersion\":1,\"allowedFindings\":[]}");
        }

        /// <summary>
        /// Gets the root path for the fixture repository.
        /// </summary>
        internal string RootPath { get; }

        /// <summary>
        /// Gets the fixture baseline path.
        /// </summary>
        internal string BaselinePath { get; }

        /// <summary>
        /// Gets the fixture report path.
        /// </summary>
        internal string ReportPath { get; }

        /// <summary>
        /// Writes a source file into the fixture repository.
        /// </summary>
        /// <param name="relativePath">The source path relative to the fixture root.</param>
        /// <param name="content">The source content.</param>
        internal void WriteSource(string relativePath, string content)
        {
            var path = Path.Combine(
                this.RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        /// <summary>
        /// Deletes a source file from the fixture repository.
        /// </summary>
        /// <param name="relativePath">The source path relative to the fixture root.</param>
        internal void DeleteSource(string relativePath)
        {
            var path = Path.Combine(
                this.RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Delete(path);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Directory.Exists(this.RootPath))
            {
                Directory.Delete(this.RootPath, recursive: true);
            }
        }
    }
}
