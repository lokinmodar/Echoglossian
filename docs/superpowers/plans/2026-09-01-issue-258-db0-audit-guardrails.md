# Issue 258 DB-0 Audit and Guardrails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish a reproducible, versioned inventory of synchronous database work and the evidence protocol that every later Issue 258 migration stage must reduce.

**Architecture:** A Windows-safe PowerShell scanner inspects production runtime C# paths for direct synchronous EF operations, blocking waits, context construction, and calls to known synchronous persistence helpers. A checked-in JSON baseline records the current debt with its planned migration stage; normal runs fail for new findings or stale resolved entries. Focused xUnit process tests exercise the scanner against isolated fixture repositories, and the standard validation script runs the audit before compiling.

**Tech Stack:** PowerShell 5.1+, C# 14, .NET 10, xUnit 2.9.3, Git.

**Spec:** `docs/superpowers/specs/2026-09-01-issue-258-async-persistence-and-translation-toggle-design.md`

## Global Constraints

- Execute only DB-0 on branch `issue-258-00-db-audit`; do not begin DB-1 infrastructure.
- Preserve the current database schema, canonical identities, lookup semantics, and save compatibility.
- Do not change plugin runtime behavior in DB-0.
- The audit must be safe to rerun and must not require network access.
- Use PowerShell and Windows-safe process invocation.
- Scan production runtime code; exclude tests, generated output, vendor code, and EF migration snapshots.
- Treat the checked-in baseline as debt, not permission to add equivalent violations.
- Every baseline finding must map to one of `DB-2` through `DB-8`; no unclassified stage value is allowed.
- Do not add hot-path logs or another runtime queue.
- Follow repository headers, XML documentation, braces, and `this.` call rules.
- Use short commits with `#258` and push every validated stable commit.
- Never stage `Echoglossian.xml` in DB-0 because this stage changes no production C# XML documentation.
- Do not create, merge, or release a pull request from the execution task; report the pushed branch for human review.
- Never publish a release without first providing the user a build to test, receiving explicit confirmation that the tested build passed, and then receiving an express request for that specific release. Test approval, merge approval, or release eligibility alone is not authorization.

## File Structure

- Create `scripts/audit-sync-db-hotpaths.ps1`: deterministic scanner, baseline comparison, stage assignment, and Markdown report generation.
- Create `scripts/sync-db-hotpaths-baseline.json`: versioned set of known findings at DB-0.
- Create `Echoglossian.Tests/SyncDatabaseHotPathAuditTests.cs`: process-level fixture tests for detection, exclusions, baseline update, and stale-baseline failure.
- Create `docs/issue-258-sync-db-hotpath-inventory.md`: generated human-readable inventory grouped by migration stage.
- Create `docs/issue-258-async-persistence-baseline.md`: reproducible in-game scenario and evidence contract.
- Modify `Echoglossian.Tests/RepositoryGuidanceTests.cs`: contract coverage for the baseline protocol and validation wiring.
- Modify `scripts/validate-local-tests.ps1`: run the audit before restore/build/test.
- Modify this plan during execution: check completed steps and append final evidence.

---

### Task 1: Add the deterministic synchronous-database audit

**Files:**
- Create: `scripts/audit-sync-db-hotpaths.ps1`
- Create: `scripts/sync-db-hotpaths-baseline.json`
- Create: `Echoglossian.Tests/SyncDatabaseHotPathAuditTests.cs`
- Create: `docs/issue-258-sync-db-hotpath-inventory.md`

**Interfaces:**
- Consumes: a repository root containing production C# source and an optional existing JSON baseline.
- Produces: `audit-sync-db-hotpaths.ps1` with explicit `-RepositoryRoot`, `-BaselinePath`, `-ReportPath`, and optional `-UpdateBaseline` arguments; a standalone process exits successfully only when current findings exactly match the baseline.
- Produces baseline schema `{ "schemaVersion": 1, "allowedFindings": [{ "id", "category", "stage", "path", "evidence" }] }` sorted by `id`.

- [x] **Step 1: Write failing process-level audit tests**

Create `SyncDatabaseHotPathAuditTests.cs` with the normal repository copyright header and XML documentation. The tests run the real script against disposable fixture roots rather than scanning or modifying the working repository:

```csharp
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
        Assert.Contains("resolved baseline finding", resolved.Output, StringComparison.OrdinalIgnoreCase);
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

    private AuditFixture CreateFixture()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "EchoglossianSyncDbAuditTests",
            Guid.NewGuid().ToString("N"));
        return new AuditFixture(root);
    }

    private AuditProcessResult RunAudit(AuditFixture fixture, bool updateBaseline)
    {
        var scriptPath = Path.Combine(
            this.FindRepositoryRoot().FullName,
            "scripts",
            "audit-sync-db-hotpaths.ps1");
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
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

    private sealed record AuditProcessResult(int ExitCode, string Output);

    private sealed class AuditFixture : IDisposable
    {
        internal AuditFixture(string rootPath)
        {
            this.RootPath = rootPath;
            this.BaselinePath = Path.Combine(rootPath, "baseline.json");
            this.ReportPath = Path.Combine(rootPath, "report.md");
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(this.BaselinePath, "{\"schemaVersion\":1,\"allowedFindings\":[]}");
        }

        internal string RootPath { get; }

        internal string BaselinePath { get; }

        internal string ReportPath { get; }

        internal void WriteSource(string relativePath, string content)
        {
            var path = Path.Combine(
                this.RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        internal void DeleteSource(string relativePath)
        {
            var path = Path.Combine(
                this.RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Delete(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(this.RootPath))
            {
                Directory.Delete(this.RootPath, recursive: true);
            }
        }
    }
}
```

- [x] **Step 2: Run the focused tests and verify the red state**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~SyncDatabaseHotPathAuditTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: FAIL because `scripts/audit-sync-db-hotpaths.ps1` does not exist.

- [x] **Step 3: Implement the scanner CLI and stable finding model**

Create the script with the repository copyright/SPDX header, comment help, strict mode, and this contract:

```powershell
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$BaselinePath = (Join-Path $PSScriptRoot 'sync-db-hotpaths-baseline.json'),
    [string]$ReportPath = (Join-Path $PSScriptRoot '..\artifacts\issue-258\sync-db-hotpath-audit.md'),
    [switch]$UpdateBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
```

Scan `DBHelpers`, `DBManagerUI`, `EFCoreSqlite`, `GeneralHelpers`, `NativeUI`, `PluginUI`, `Translators`, `UIOverlays`, and root file `Echoglossian.cs`. Skip normalized paths containing `/bin/`, `/obj/`, `/vendor/`, `/Echoglossian.Tests/`, `/Echoglossian.Mock.Tests/`, or `/EFCoreSqlite/Migrations/`.

Use these exact categories and expressions:

```powershell
$directPatterns = @(
    [pscustomobject]@{ Category = 'sync-ef-save'; Pattern = '(?<!Async)\bSaveChanges\s*\(' },
    [pscustomobject]@{ Category = 'sync-ef-transaction'; Pattern = '(?<!Async)\bBeginTransaction\s*\(' },
    [pscustomobject]@{ Category = 'sync-ef-migrate'; Pattern = '\.Database\.(?:Migrate|EnsureCreated)\s*\(' },
    [pscustomobject]@{ Category = 'sync-sql-command'; Pattern = '(?<!Async)\bExecuteSqlRaw\s*\(' },
    [pscustomobject]@{ Category = 'direct-db-context'; Pattern = '\bnew\s+EchoglossianDbContext\s*\(' },
    [pscustomobject]@{ Category = 'blocking-wait'; Pattern = '\.(?:Result\b|Wait\s*\()' }
)

$persistenceHelperPattern = [regex]::new(
    '(?<helper>ReferenceTextPersistenceHelper|ActionTooltipPersistenceHelper|' +
    'ItemTooltipPersistenceHelper|TraitPersistenceHelper|' +
    'StringArrayDataPersistenceHelper|GameWindowPersistenceHelper|' +
    'TranslationFailurePersistenceHelper|LlmCapabilityPersistenceHelper)' +
    '\.(?<method>Find\w*|Insert\w*|Record\w*|Upsert\w*)\s*\(')

$databaseQueryPattern = [regex]::new(
    '(?<!Async)\b(?:First|FirstOrDefault|Single|SingleOrDefault|ToList|' +
    'ToArray|Any|Count|LongCount|Find)\s*\(')
```

Apply `$databaseQueryPattern` only within `DBHelpers`, `DBManagerUI`, and `EFCoreSqlite` so ordinary in-memory LINQ in addon handlers is not mislabeled as direct EF I/O. Record those matches as `sync-ef-query`.

Normalize separators to `/`, collapse evidence whitespace, and build IDs as `category|relative/path.cs|normalized evidence`. Line number belongs only in reports. Assign stages in this order:

```powershell
function Get-MigrationStage {
    param([Parameter(Mandatory)][string]$RelativePath)

    switch -Regex ($RelativePath) {
        'ReferenceText|MainCommand' { return 'DB-2' }
        'StringArray' { return 'DB-6' }
        'Quest|ToDo|Journal|ScenarioTree|RecommendList|AreaMap' { return 'DB-3' }
        'Action|Item|Trait|Tooltip' { return 'DB-4' }
        'GameWindow' { return 'DB-5' }
        'DBManagerUI|Echoglossian\.cs' { return 'DB-8' }
        default { return 'DB-7' }
    }
}
```

Normal execution fails for both new and stale findings:

```powershell
$unexpected = @($currentFindings | Where-Object { $_.id -notin $baselineIds })
$resolved = @($baselineFindings | Where-Object { $_.id -notin $currentIds })
```

`-UpdateBaseline` writes current sorted findings as UTF-8 JSON before comparison. Both modes write deterministic Markdown grouped by stage, path, and line. On mismatch, include each new or stale ID in an exception message, using the phrase `resolved baseline finding` for stale entries. On success, print totals by stage and return normally. An unhandled exception gives standalone callers a nonzero process exit without terminating a parent validation script on success.

- [x] **Step 4: Run the focused audit tests**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~SyncDatabaseHotPathAuditTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: four tests pass.

- [x] **Step 5: Generate and verify the repository baseline**

```powershell
.\scripts\audit-sync-db-hotpaths.ps1 -UpdateBaseline -ReportPath .\docs\issue-258-sync-db-hotpath-inventory.md
.\scripts\audit-sync-db-hotpaths.ps1 -ReportPath .\artifacts\issue-258\sync-db-hotpath-audit.md

$baseline = Get-Content -Raw .\scripts\sync-db-hotpaths-baseline.json | ConvertFrom-Json
$approvedStages = @('DB-2', 'DB-3', 'DB-4', 'DB-5', 'DB-6', 'DB-7', 'DB-8')
if ($baseline.allowedFindings.Count -eq 0) { throw 'Expected current sync DB debt.' }
if (@($baseline.allowedFindings | Where-Object stage -notin $approvedStages).Count -gt 0) {
    throw 'Baseline contains an invalid migration stage.'
}
```

Expected: both audit commands exit `0`; JSON and Markdown are non-empty; the artifact stays ignored.

- [x] **Step 6: Commit the scanner, tests, and inventory**

```powershell
git add -- scripts\audit-sync-db-hotpaths.ps1 scripts\sync-db-hotpaths-baseline.json Echoglossian.Tests\SyncDatabaseHotPathAuditTests.cs docs\issue-258-sync-db-hotpath-inventory.md
git commit -m "test(#258): add sync database hot-path audit"
git push
```

---

### Task 2: Record the Issue 258 baseline evidence protocol

**Files:**
- Create: `docs/issue-258-async-persistence-baseline.md`
- Modify: `Echoglossian.Tests/RepositoryGuidanceTests.cs`

**Interfaces:**
- Consumes: Issue 258 observations, `Echoglossian.log`, accepted-quest diagnostic logs, and repeatable in-game actions.
- Produces: one stable protocol that DB-2 and every later performance release must use for comparable evidence.

- [x] **Step 1: Add a failing repository-guidance contract**

Add this documented test to `RepositoryGuidanceTests`:

```csharp
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
```

- [x] **Step 2: Run the focused test and verify the red state**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~Issue258Baseline_documents_reproducible_persistence_evidence -p:VSTestMaxCpuCount=1 --nologo
```

Expected: FAIL because the baseline protocol file does not exist.

- [x] **Step 3: Write the controlled baseline protocol**

Create `docs/issue-258-async-persistence-baseline.md` with this exact content:

```markdown
# Issue 258 Async Persistence Baseline

## Evidence Status

Reference documents:

- Issue: https://github.com/lokinmodar/Echoglossian/issues/258
- [Master design](superpowers/specs/2026-09-01-issue-258-async-persistence-and-translation-toggle-design.md)
- [Current synchronous DB inventory](issue-258-sync-db-hotpath-inventory.md)

The Issue 258 report observed repeated drops from approximately 110 FPS to
87 FPS, accepted-quest prefetch bursts approximately every two seconds, and
SQLite WAL write activity approximately every second. These are reporter
observations, not a controlled benchmark.

## Controlled Scenario

1. Use the same Debug build, character, territory, target language, and
   translator for the before/after pair.
2. Enable global translation plus `TranslateActionMenuWindow`,
   `TranslateMainCommandWindow`, or `TranslateTooltips` so reference-text
   prefetch runs.
3. Enable at least one of `TranslateJournal`, `TranslateJournalDetail`,
   `TranslateToDoList`, `TranslateScenarioTree`, `TranslateRecommendList`, or
   `TranslateAreaMap` so accepted-quest prefetch runs.
4. Start with at least five accepted quests and a warm game session, then
   observe for two uninterrupted minutes without changing configuration.
5. Repeat once with a warm translation database and once with the targeted
   rows removed from a disposable database copy.

## Required Capture

- median, p95, and p99 frame time;
- observed FPS range;
- SQLite WAL write frequency and busy/retry count;
- persistence queue maximum depth and oldest-item age when those counters
  become available in DB-1;
- batch count, written-row count, and unchanged-row suppression count;
- timestamped excerpts from `Echoglossian.log` and
  `accepted-quest-prefetch-activity.log`.

## Comparison Rule

DB-2 and every later performance release append one dated before/after result
using this exact scenario. A result is not comparable if configuration,
translator, character quest set, observation duration, or database warmth
changes between the two captures.

## Logging Rule

Use summarized counters and lifecycle boundaries. Do not add per-frame or
per-row production logs to obtain the measurements.
```

Keep controlled capture as an explicit in-game gate. DB-0 tooling and unit tests must not claim live frame-time measurement.

- [x] **Step 4: Run the focused repository-guidance tests**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~RepositoryGuidanceTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: all selected tests pass.

- [x] **Step 5: Commit and push the evidence protocol**

```powershell
git add -- docs\issue-258-async-persistence-baseline.md Echoglossian.Tests\RepositoryGuidanceTests.cs
git commit -m "docs(#258): record async persistence baseline protocol"
git push
```

---

### Task 3: Make the audit part of the standard validation rail

**Files:**
- Modify: `scripts/validate-local-tests.ps1`
- Modify: `Echoglossian.Tests/RepositoryGuidanceTests.cs`

**Interfaces:**
- Consumes: `scripts/audit-sync-db-hotpaths.ps1` and its checked-in baseline.
- Produces: local validation that stops before restore/build when synchronous DB debt changes unexpectedly.

- [x] **Step 1: Add a failing validation-order contract**

Add this documented test to `RepositoryGuidanceTests`:

```csharp
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
```

- [x] **Step 2: Run the focused test and verify the red state**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~LocalValidation_runs_sync_database_audit_before_dotnet -p:VSTestMaxCpuCount=1 --nologo
```

Expected: FAIL because `validate-local-tests.ps1` does not invoke the audit.

- [x] **Step 3: Invoke the audit before dependency restore**

Inside the existing `Push-Location`/`try` block, insert this before the first `dotnet restore`:

```powershell
& .\scripts\audit-sync-db-hotpaths.ps1 `
    -ReportPath .\artifacts\issue-258\sync-db-hotpath-audit.md
```

Do not add `-UpdateBaseline` to the validation path.

- [x] **Step 4: Run the audit and focused contracts**

```powershell
.\scripts\audit-sync-db-hotpaths.ps1 -ReportPath .\artifacts\issue-258\sync-db-hotpath-audit.md
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~SyncDatabaseHotPathAuditTests|FullyQualifiedName~RepositoryGuidanceTests" -p:VSTestMaxCpuCount=1 --nologo
```

Expected: audit exits `0`; all selected tests pass; the artifact remains ignored.

- [x] **Step 5: Commit and push validation enforcement**

```powershell
git add -- scripts\validate-local-tests.ps1 Echoglossian.Tests\RepositoryGuidanceTests.cs
git commit -m "test(#258): enforce sync database audit"
git push
```

---

### Task 4: Validate and document DB-0 completion

**Files:**
- Modify: `docs/superpowers/plans/2026-09-01-issue-258-db0-audit-guardrails.md`

**Interfaces:**
- Consumes: all DB-0 commits and standard validation commands.
- Produces: a pushed, review-ready DB-0 branch with exact audit and test evidence and no production behavior change.

- [x] **Step 1: Run the audit in normal mode**

```powershell
.\scripts\audit-sync-db-hotpaths.ps1 -ReportPath .\artifacts\issue-258\sync-db-hotpath-audit.md
```

Expected: exit code `0`, zero unexpected findings, and zero resolved baseline findings.

- [x] **Step 2: Run the standard build and unit tests**

```powershell
dotnet build .\Echoglossian.sln -c Debug --no-restore
dotnet build .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 --nologo
```

Expected: both builds succeed and all unit tests pass. DB-0 changes tooling only, so this branch does not claim Mock/DalaMock or live FFXIV coverage.

- [x] **Step 3: Run repository hygiene checks**

```powershell
git diff --check
git status --short
git log --oneline --decorate origin/v4-series..HEAD
```

Expected: no whitespace errors, tracked artifacts, secrets, production C# changes, or staged/committed `Echoglossian.xml`; commits are limited to Issue 258 DB-0.

- [x] **Step 4: Record exact execution evidence in this plan**

Check completed boxes and append an `## Execution Notes` section containing audit totals grouped by DB-2 through DB-8, exact test totals, build warning/error totals, the DB-0 Mock/DalaMock limitation, and the controlled in-game baseline gate required before DB-2 performance claims.

- [x] **Step 5: Commit, push, and verify the remote branch**

```powershell
git add -- docs\superpowers\plans\2026-09-01-issue-258-db0-audit-guardrails.md
git commit -m "docs(#258): record DB-0 audit validation"
git push

$localHash = git rev-parse HEAD
$remoteHash = (git ls-remote origin refs/heads/issue-258-00-db-audit).Split("`t")[0]
if ($localHash -ne $remoteHash)
{
    throw "Remote Issue 258 branch does not match local HEAD."
}
```

Expected: hashes match. Report the branch, commit list, validation, and pull-request URL. Do not begin DB-1, open or merge a pull request, prepare release metadata, or publish a release. DB-0 is not release-eligible; future stages must satisfy the master design's mandatory user-test and explicit-release-request gate.

## Execution Notes

Task 4 was executed on 2026-09-01 in native Windows PowerShell. The normal
audit command exited `0` with zero unexpected findings and zero resolved
baseline findings. The approved debt inventory remains 222 findings:

- DB-2: 8
- DB-3: 11
- DB-4: 35
- DB-5: 15
- DB-6: 13
- DB-7: 119
- DB-8: 21

The solution build (`dotnet build .\Echoglossian.sln -c Debug --no-restore`) succeeded with 61 warnings and 0 errors. The test-project build (`dotnet build .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore`) succeeded with 1 warning and 0 errors; that warning is the existing Multilingual App Toolkit-unavailable warning from `Echoglossian.csproj`.
The required `dotnet test` command with `--no-build`,
`-p:VSTestMaxCpuCount=1`, and `--nologo` passed 1,299 of 1,299 tests, with 0
failed and 0 skipped.

Repository hygiene passed: `git diff --check` returned no whitespace errors;
the generated audit artifact remained ignored; and the Issue 258 DB-0 commit
range contains only DB-0 audit, evidence-protocol, validation-rail, and plan
work. `Echoglossian.xml` was an unrelated pre-existing modified file; it was
not staged or committed. DB-0 makes no runtime or schema changes.

DB-0 tooling and unit tests do not claim Mock/DalaMock coverage or live FFXIV
coverage. Controlled in-game baseline capture, following
`docs/issue-258-async-persistence-baseline.md`, is required before making
DB-2 performance claims.
