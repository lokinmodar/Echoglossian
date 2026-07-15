# Issue 174 Story-Surface Retranslation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand visible retranslate-and-persist, provenance, and DB-manager handoff across `Talk`, `BattleTalk`, `TalkSubtitle`, `CutSceneSelectString`, and `TextGimmickHint`, while extracting reusable read-only inspection UI components from `DBManagerUI`.

**Architecture:** Introduce a shared read-only inspection display layer that accepts caller-supplied columns and rows, then reuse it from both `/eglodbmanager` and `/eglotranslatordebugger`. Add a runtime story-surface diagnostics store and a retranslation dispatcher, then extend each covered handler to record provenance and support explicit visible retranslate-and-persist while keeping EF metadata discovery and CRUD flows inside the DB manager.

**Tech Stack:** C# / .NET 10 Windows, xUnit, FluentAssertions, Dalamud ImGui UI, EF Core SQLite

## Global Constraints

- Preserve existing behavior unless explicitly changing operator-facing story-surface retranslation and provenance.
- Keep destructive DB actions in `/eglodbmanager`; `/eglotranslatordebugger` remains non-destructive.
- Cover `Talk`, `BattleTalk`, `TalkSubtitle`, `CutSceneSelectString`, and `TextGimmickHint`.
- Extend explicit visible retranslate-and-persist to all covered story-facing surfaces.
- Reuse one shared read-only display layer across debugger and DB manager while keeping data providers context-specific.
- Do not make the entire DB manager generic or context-free.
- Do not change quest, tooltip, toast, or canonical game-window persistence outside the covered story-facing surfaces.
- Do not change runtime-only dialogue-context persistence rules.
- Follow the repo `.editorconfig` and StyleCop settings, including file headers, XML docs, braces, and `this.` call style.
- Validate with `dotnet build Echoglossian.sln -c Debug --no-restore` and `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`.
- In-game verification is required for `/eglotranslatordebugger`, `/eglodbmanager`, and every covered surface.

---

## File Map

- `DBManagerUI/Models/InspectionColumnDefinition.cs`
  Shared read-only column descriptor used by both DB manager and debugger.
- `DBManagerUI/Models/InspectionCell.cs`
  Shared read-only cell payload with inline text plus optional tooltip text.
- `DBManagerUI/Models/InspectionRow.cs`
  Shared row payload for inspection tables.
- `DBManagerUI/Services/InspectionCellFormatter.cs`
  Shared value-to-cell formatting helper for null, blob, truncation, and tooltip behavior.
- `DBManagerUI/Components/InspectionTableView.cs`
  Shared read-only inspection table renderer.
- `DBManagerUI/Components/DbTableView.cs`
  DB-manager-specific adapter that converts EF metadata and row objects into shared inspection rows.
- `DBManagerUI/Services/DbTableNameMatcher.cs`
  DB-manager-specific helper that resolves a requested table name against the known entity list.
- `DBManagerUI/DBEditorWindow.cs`
  Existing DB manager composition root; stays EF/CRUD-specific but gains `OpenAndSelectTable`.
- `NativeUI/Helpers/VisibleStorySurfaceKind.cs`
  Runtime enum for the supported story-facing surfaces.
- `NativeUI/Helpers/VisibleStorySurfaceDiagnosticsSnapshot.cs`
  Runtime-only snapshot model for visible provenance and retranslation outcome.
- `NativeUI/Helpers/VisibleStorySurfaceDiagnosticsStore.cs`
  Runtime-only in-memory store that handlers write and the debugger reads.
- `NativeUI/Helpers/VisibleStorySurfaceTableMap.cs`
  Shared mapping from story-surface kind to DB table name, used by handlers and debugger.
- `PluginUI/Helpers/VisibleStorySurfaceRetranslationDispatcher.cs`
  Dispatcher that probes registered handlers for the first applicable visible retranslate target.
- `PluginUI/Helpers/VisibleStorySurfaceInspectionModelBuilder.cs`
  Debugger-specific adapter that converts runtime diagnostics snapshots into shared inspection rows.
- `NativeUI/AddonHandlers/Talk/IVisibleDialogueRetranslationHandler.cs`
  Existing visible retranslation contract broadened to the full story-surface set for this pass.
- `PluginUI/PluginRuntimeUi.cs`
  Runtime dispatcher entrypoint used by the debugger button.
- `PluginUI/TranslatorMetricsWindow.cs`
  Debugger UI that renders shared inspection rows, retranslation outcome, and `View In DB Manager`.
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
  Existing visible retranslation + provenance for `Talk`; now records into the shared diagnostics store.
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
  Existing visible retranslation + provenance for `BattleTalk`; now records into the shared diagnostics store.
- `NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs`
  New explicit visible retranslate-and-persist implementation plus provenance recording.
- `NativeUI/AddonHandlers/CutSceneSelectString/CutSceneSelectStringHandler.cs`
  New explicit visible retranslate-and-persist implementation for question-and-options payloads plus provenance recording.
- `NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs`
  New explicit visible retranslate-and-persist implementation plus provenance recording.
- `Echoglossian.Tests/InspectionCellFormatterTests.cs`
  Unit tests for the shared read-only cell formatting behavior.
- `Echoglossian.Tests/DbTableNameMatcherTests.cs`
  Unit tests for DB-manager table selection and handoff resolution.
- `Echoglossian.Tests/VisibleStorySurfaceDiagnosticsStoreTests.cs`
  Unit tests for snapshot retention and retranslation outcome updates.
- `Echoglossian.Tests/VisibleStorySurfaceRetranslationDispatcherTests.cs`
  Unit tests for handler probing and first-applicable dispatch behavior.
- `Echoglossian.Tests/VisibleStorySurfaceInspectionModelBuilderTests.cs`
  Unit tests for debugger row building and DB-table handoff mapping.

### Task 1: Extract Shared Read-Only Inspection Display Primitives

**Files:**
- Create: `DBManagerUI/Models/InspectionColumnDefinition.cs`
- Create: `DBManagerUI/Models/InspectionCell.cs`
- Create: `DBManagerUI/Models/InspectionRow.cs`
- Create: `DBManagerUI/Services/InspectionCellFormatter.cs`
- Create: `DBManagerUI/Components/InspectionTableView.cs`
- Modify: `DBManagerUI/Components/DbTableView.cs`
- Test: `Echoglossian.Tests/InspectionCellFormatterTests.cs`

**Interfaces:**
- Consumes: existing `DbTableView` row objects and EF metadata.
- Produces: `InspectionColumnDefinition`, `InspectionCell`, `InspectionRow`, `InspectionCellFormatter.Format(object? value, int maxInlineLength = 256)`, `InspectionTableView.Draw(string tableId, IReadOnlyList<InspectionColumnDefinition> columns, IReadOnlyList<InspectionRow> rows, HashSet<int>? selection = null, Action<int>? onRowDoubleClick = null)`.

- [ ] **Step 1: Write the failing test**

```csharp
// Echoglossian.Tests/InspectionCellFormatterTests.cs
using Echoglossian.DBManagerUI.Models;
using Echoglossian.DBManagerUI.Services;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

public class InspectionCellFormatterTests
{
  [Fact]
  public void Format_TruncatesLongStringsAndKeepsTooltip()
  {
    var longText = new string('x', 300);

    InspectionCell cell = InspectionCellFormatter.Format(longText);

    cell.Text.Should().EndWith("…");
    cell.Text.Length.Should().Be(257);
    cell.Tooltip.Should().Be(longText);
  }

  [Fact]
  public void Format_FormatsNullAndBlobValues()
  {
    InspectionCellFormatter.Format(null).Text.Should().Be("(null)");
    InspectionCellFormatter.Format(new byte[4]).Text.Should().Be("[BLOB 4 bytes]");
  }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~InspectionCellFormatterTests"`
Expected: FAIL with missing `InspectionCell` / `InspectionCellFormatter` types.

- [ ] **Step 3: Write minimal implementation**

```csharp
// DBManagerUI/Models/InspectionColumnDefinition.cs
namespace Echoglossian.DBManagerUI.Models;

public readonly record struct InspectionColumnDefinition(
    string Id,
    string Header,
    float Width = 150f,
    bool Wrap = true);

// DBManagerUI/Models/InspectionCell.cs
namespace Echoglossian.DBManagerUI.Models;

public readonly record struct InspectionCell(
    string Text,
    string? Tooltip = null);

// DBManagerUI/Models/InspectionRow.cs
namespace Echoglossian.DBManagerUI.Models;

public sealed record InspectionRow(
    string Key,
    IReadOnlyList<InspectionCell> Cells);

// DBManagerUI/Services/InspectionCellFormatter.cs
namespace Echoglossian.DBManagerUI.Services;

using Echoglossian.DBManagerUI.Models;

public static class InspectionCellFormatter
{
  public static InspectionCell Format(object? value, int maxInlineLength = 256)
  {
    if (value == null)
    {
      return new InspectionCell("(null)");
    }

    if (value is byte[] bytes)
    {
      return new InspectionCell($"[BLOB {bytes.Length} bytes]");
    }

    string text = value.ToString() ?? string.Empty;
    if (text.Length <= maxInlineLength)
    {
      return new InspectionCell(text);
    }

    return new InspectionCell(
        text[..maxInlineLength] + "…",
        text);
  }
}

// DBManagerUI/Components/InspectionTableView.cs
namespace Echoglossian.DBManagerUI.Components;

using Echoglossian.DBManagerUI.Models;

public sealed class InspectionTableView
{
  public void Draw(
      string tableId,
      IReadOnlyList<InspectionColumnDefinition> columns,
      IReadOnlyList<InspectionRow> rows,
      HashSet<int>? selection = null,
      Action<int>? onRowDoubleClick = null)
  {
    // Move the current read-only rendering loop from DbTableView here.
  }
}
```

- [ ] **Step 4: Adapt `DbTableView` to the shared primitives**

```csharp
// DBManagerUI/Components/DbTableView.cs
private readonly InspectionTableView inspectionTableView = new();

public void Draw()
{
  var props = this.getScalarProps();
  var rows = this.getRows();

  if (rows == null)
  {
    ImGui.Text("No records loaded.");
    return;
  }

  if (rows.Count == 0 || props == null || props.Count == 0)
  {
    ImGui.Text("No records found in this table.");
    return;
  }

  var columns = props
      .Select(static prop => new InspectionColumnDefinition(prop.Name, prop.Name))
      .ToList();
  var inspectionRows = rows
      .Select((row, index) => new InspectionRow(
          index.ToString(CultureInfo.InvariantCulture),
          props.Select(prop =>
                  InspectionCellFormatter.Format(
                      this.SafeGetValue(row, prop.PropertyInfo!)))
              .ToList()))
      .ToList();

  this.inspectionTableView.Draw(
      "##dbTable",
      columns,
      inspectionRows,
      this.getSelection(),
      rowIndex => this.onRowDoubleClick(rows[rowIndex]));
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~InspectionCellFormatterTests"`
Expected: PASS with 2 passing tests.

- [ ] **Step 6: Commit**

```powershell
git add DBManagerUI/Models/InspectionColumnDefinition.cs DBManagerUI/Models/InspectionCell.cs DBManagerUI/Models/InspectionRow.cs DBManagerUI/Services/InspectionCellFormatter.cs DBManagerUI/Components/InspectionTableView.cs DBManagerUI/Components/DbTableView.cs Echoglossian.Tests/InspectionCellFormatterTests.cs
git commit -m "refactor: extract read-only inspection table primitives"
```

### Task 2: Add DB Manager Table Handoff Without Pulling CRUD Into The Debugger

**Files:**
- Create: `DBManagerUI/Services/DbTableNameMatcher.cs`
- Modify: `DBManagerUI/DBEditorWindow.cs`
- Test: `Echoglossian.Tests/DbTableNameMatcherTests.cs`

**Interfaces:**
- Consumes: `List<string>` table names built by `DBEditorWindow`.
- Produces: `DbTableNameMatcher.Match(IReadOnlyList<string> tables, string requestedTable)` and `DbEditorWindow.OpenAndSelectTable(string tableName)`.

- [ ] **Step 1: Write the failing test**

```csharp
// Echoglossian.Tests/DbTableNameMatcherTests.cs
using Echoglossian.DBManagerUI.Services;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

public class DbTableNameMatcherTests
{
  [Fact]
  public void Match_ReturnsExactTableName()
  {
    var matched = DbTableNameMatcher.Match(
        ["TalkMessage", "BattleTalkMessage", "SelectString"],
        "SelectString");

    matched.Should().Be("SelectString");
  }

  [Fact]
  public void Match_ReturnsNullForUnknownTable()
  {
    var matched = DbTableNameMatcher.Match(
        ["TalkMessage", "BattleTalkMessage"],
        "UnknownTable");

    matched.Should().BeNull();
  }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~DbTableNameMatcherTests"`
Expected: FAIL with missing `DbTableNameMatcher`.

- [ ] **Step 3: Write minimal implementation**

```csharp
// DBManagerUI/Services/DbTableNameMatcher.cs
namespace Echoglossian.DBManagerUI.Services;

public static class DbTableNameMatcher
{
  public static string? Match(
      IReadOnlyList<string> tables,
      string requestedTable)
  {
    return tables.FirstOrDefault(
        table => string.Equals(
            table,
            requestedTable,
            StringComparison.Ordinal));
  }
}

// DBManagerUI/DBEditorWindow.cs
public void OpenAndSelectTable(string tableName)
{
  this.IsOpen = true;
  if (this.tableNames == null)
  {
    this.InitializeTableNames();
  }

  var matchedTable = this.tableNames == null
      ? null
      : DbTableNameMatcher.Match(this.tableNames, tableName);
  if (matchedTable == null)
  {
    return;
  }

  this.selectedTable = matchedTable;
  this.page = 0;
  this.LoadRows();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~DbTableNameMatcherTests"`
Expected: PASS with 2 passing tests.

- [ ] **Step 5: Commit**

```powershell
git add DBManagerUI/Services/DbTableNameMatcher.cs DBManagerUI/DBEditorWindow.cs Echoglossian.Tests/DbTableNameMatcherTests.cs
git commit -m "feat: add db manager table handoff"
```

### Task 3: Add Story-Surface Diagnostics Storage And Debugger Display Models

**Files:**
- Create: `NativeUI/Helpers/VisibleStorySurfaceKind.cs`
- Create: `NativeUI/Helpers/VisibleStorySurfaceDiagnosticsSnapshot.cs`
- Create: `NativeUI/Helpers/VisibleStorySurfaceDiagnosticsStore.cs`
- Create: `NativeUI/Helpers/VisibleStorySurfaceTableMap.cs`
- Create: `PluginUI/Helpers/VisibleStorySurfaceInspectionModelBuilder.cs`
- Test: `Echoglossian.Tests/VisibleStorySurfaceDiagnosticsStoreTests.cs`
- Test: `Echoglossian.Tests/VisibleStorySurfaceInspectionModelBuilderTests.cs`

**Interfaces:**
- Consumes: handler-resolved provenance and retranslation outcome.
- Produces:
  - `VisibleStorySurfaceKind`
  - `VisibleStorySurfaceDiagnosticsSnapshot`
  - `VisibleStorySurfaceDiagnosticsStore.Record(VisibleStorySurfaceDiagnosticsSnapshot snapshot)`
  - `VisibleStorySurfaceDiagnosticsStore.SetRetranslationOutcome(VisibleStorySurfaceKind surface, bool success, string message, DateTime observedAtUtc)`
  - `VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot()`
  - `VisibleStorySurfaceTableMap.Resolve(VisibleStorySurfaceKind surface)`
  - `VisibleStorySurfaceInspectionModelBuilder.BuildRows(VisibleStorySurfaceDiagnosticsSnapshot snapshot)`

- [ ] **Step 1: Write the failing tests**

```csharp
// Echoglossian.Tests/VisibleStorySurfaceDiagnosticsStoreTests.cs
using Echoglossian.NativeUI.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

public class VisibleStorySurfaceDiagnosticsStoreTests
{
  [Fact]
  public void Record_StoresLatestSnapshot()
  {
    VisibleStorySurfaceDiagnosticsStore.Clear();

    VisibleStorySurfaceDiagnosticsStore.Record(
        new VisibleStorySurfaceDiagnosticsSnapshot(
            VisibleStorySurfaceKind.TalkSubtitle,
            "DB reuse",
            "TalkSubtitleMessage",
            "Original subtitle",
            "Translated subtitle",
            false,
            2,
            DateTime.UtcNow,
            null,
            null));

    VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot()!.Surface
        .Should().Be(VisibleStorySurfaceKind.TalkSubtitle);
  }

  [Fact]
  public void SetRetranslationOutcome_UpdatesLatestSnapshotForSameSurface()
  {
    VisibleStorySurfaceDiagnosticsStore.Clear();
    var observedAtUtc = new DateTime(2026, 07, 11, 15, 0, 0, DateTimeKind.Utc);
    VisibleStorySurfaceDiagnosticsStore.Record(
        new VisibleStorySurfaceDiagnosticsSnapshot(
            VisibleStorySurfaceKind.TextGimmickHint,
            "Live translation",
            "TextGimmickHintMessage",
            "Original hint",
            "Translated hint",
            false,
            8,
            observedAtUtc,
            null,
            null));

    VisibleStorySurfaceDiagnosticsStore.SetRetranslationOutcome(
        VisibleStorySurfaceKind.TextGimmickHint,
        true,
        "TextGimmickHint visible text was retranslated and persisted.",
        observedAtUtc.AddMinutes(1));

    VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot()!
        .LastRetranslationMessage.Should().Contain("retranslated and persisted");
  }
}

// Echoglossian.Tests/VisibleStorySurfaceInspectionModelBuilderTests.cs
using Echoglossian.NativeUI.Helpers;
using Echoglossian.PluginUI.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

public class VisibleStorySurfaceInspectionModelBuilderTests
{
  [Fact]
  public void Resolve_MapsCutSceneSelectStringToSelectString()
  {
    VisibleStorySurfaceTableMap.Resolve(
            VisibleStorySurfaceKind.CutSceneSelectString)
        .Should().Be("SelectString");
  }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~VisibleStorySurfaceDiagnosticsStoreTests|FullyQualifiedName~VisibleStorySurfaceInspectionModelBuilderTests"`
Expected: FAIL with missing diagnostics store and inspection model builder types.

- [ ] **Step 3: Write minimal implementation**

```csharp
// NativeUI/Helpers/VisibleStorySurfaceKind.cs
namespace Echoglossian.NativeUI.Helpers;

public enum VisibleStorySurfaceKind
{
  Talk,
  BattleTalk,
  TalkSubtitle,
  CutSceneSelectString,
  TextGimmickHint,
}

// NativeUI/Helpers/VisibleStorySurfaceDiagnosticsSnapshot.cs
namespace Echoglossian.NativeUI.Helpers;

public readonly record struct VisibleStorySurfaceDiagnosticsSnapshot(
    VisibleStorySurfaceKind Surface,
    string ProvenanceLabel,
    string TableName,
    string OriginalText,
    string TranslatedText,
    bool UsedRuntimeOnlyDialogueContext,
    int EffectiveTranslationEngineId,
    DateTime ObservedAtUtc,
    bool? LastRetranslationSuccess,
    string? LastRetranslationMessage);

// NativeUI/Helpers/VisibleStorySurfaceDiagnosticsStore.cs
namespace Echoglossian.NativeUI.Helpers;

public static class VisibleStorySurfaceDiagnosticsStore
{
  private static readonly Lock SyncRoot = new();
  private static VisibleStorySurfaceDiagnosticsSnapshot? latestSnapshot;

  public static void Clear()
  {
    lock (SyncRoot)
    {
      latestSnapshot = null;
    }
  }

  public static void Record(VisibleStorySurfaceDiagnosticsSnapshot snapshot)
  {
    lock (SyncRoot)
    {
      latestSnapshot = snapshot;
    }
  }

  public static void SetRetranslationOutcome(
      VisibleStorySurfaceKind surface,
      bool success,
      string message,
      DateTime observedAtUtc)
  {
    lock (SyncRoot)
    {
      if (latestSnapshot == null || latestSnapshot.Value.Surface != surface)
      {
        return;
      }

      latestSnapshot = latestSnapshot.Value with
      {
        LastRetranslationSuccess = success,
        LastRetranslationMessage = message,
        ObservedAtUtc = observedAtUtc,
      };
    }
  }

  public static VisibleStorySurfaceDiagnosticsSnapshot? GetLatestSnapshot()
  {
    lock (SyncRoot)
    {
      return latestSnapshot;
    }
  }
}

// NativeUI/Helpers/VisibleStorySurfaceTableMap.cs
namespace Echoglossian.NativeUI.Helpers;

public static class VisibleStorySurfaceTableMap
{
  public static string Resolve(VisibleStorySurfaceKind surface)
  {
    return surface switch
    {
      VisibleStorySurfaceKind.Talk => "TalkMessage",
      VisibleStorySurfaceKind.BattleTalk => "BattleTalkMessage",
      VisibleStorySurfaceKind.TalkSubtitle => "TalkSubtitleMessage",
      VisibleStorySurfaceKind.CutSceneSelectString => "SelectString",
      VisibleStorySurfaceKind.TextGimmickHint => "TextGimmickHintMessage",
      _ => "TalkMessage",
    };
  }
}

// PluginUI/Helpers/VisibleStorySurfaceInspectionModelBuilder.cs
namespace Echoglossian.PluginUI.Helpers;

using Echoglossian.DBManagerUI.Models;
using Echoglossian.NativeUI.Helpers;

public static class VisibleStorySurfaceInspectionModelBuilder
{
  public static IReadOnlyList<InspectionRow> BuildRows(
      VisibleStorySurfaceDiagnosticsSnapshot snapshot)
  {
    return
    [
      new InspectionRow("surface", [new InspectionCell("Surface"), new InspectionCell(snapshot.Surface.ToString())]),
      new InspectionRow("source", [new InspectionCell("Source"), new InspectionCell(snapshot.ProvenanceLabel)]),
      new InspectionRow("table", [new InspectionCell("DB table"), new InspectionCell(snapshot.TableName)]),
      new InspectionRow("text", [new InspectionCell("Original"), new InspectionCell(snapshot.OriginalText, snapshot.OriginalText)]),
      new InspectionRow("translated", [new InspectionCell("Translated"), new InspectionCell(snapshot.TranslatedText, snapshot.TranslatedText)]),
    ];
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~VisibleStorySurfaceDiagnosticsStoreTests|FullyQualifiedName~VisibleStorySurfaceInspectionModelBuilderTests"`
Expected: PASS with 3 passing tests.

- [ ] **Step 5: Commit**

```powershell
git add NativeUI/Helpers/VisibleStorySurfaceKind.cs NativeUI/Helpers/VisibleStorySurfaceDiagnosticsSnapshot.cs NativeUI/Helpers/VisibleStorySurfaceDiagnosticsStore.cs NativeUI/Helpers/VisibleStorySurfaceTableMap.cs PluginUI/Helpers/VisibleStorySurfaceInspectionModelBuilder.cs Echoglossian.Tests/VisibleStorySurfaceDiagnosticsStoreTests.cs Echoglossian.Tests/VisibleStorySurfaceInspectionModelBuilderTests.cs
git commit -m "feat: add story surface diagnostics models"
```

### Task 4: Broaden The Visible Retranslation Runtime Contract And Dispatcher

**Files:**
- Create: `PluginUI/Helpers/VisibleStorySurfaceRetranslationDispatcher.cs`
- Modify: `NativeUI/AddonHandlers/Talk/IVisibleDialogueRetranslationHandler.cs`
- Modify: `PluginUI/PluginRuntimeUi.cs`
- Test: `Echoglossian.Tests/VisibleStorySurfaceRetranslationDispatcherTests.cs`

**Interfaces:**
- Consumes: `List<(string AddonName, IAddonTranslationHandler Handler)> registeredAddonHandlers`.
- Produces:
  - `VisibleStorySurfaceRetranslationDispatcher.DispatchAsync(IEnumerable<(string AddonName, IAddonTranslationHandler Handler)> handlers)`
  - broadened `IVisibleDialogueRetranslationHandler` semantics covering all supported story-facing surfaces without renaming the interface in this pass.
  - `Action<string> openDbManagerForTable` injected into `TranslatorMetricsWindow`

- [ ] **Step 1: Write the failing test**

```csharp
// Echoglossian.Tests/VisibleStorySurfaceRetranslationDispatcherTests.cs
using Echoglossian.NativeUI.AddonHandlers.Talk;
using Echoglossian.NativeUI.Handlers;
using Echoglossian.PluginUI.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

public class VisibleStorySurfaceRetranslationDispatcherTests
{
  [Fact]
  public async Task DispatchAsync_ReturnsFirstApplicableResult()
  {
    var dispatcher = new VisibleStorySurfaceRetranslationDispatcher();
    var handlers = new List<(string AddonName, IAddonTranslationHandler Handler)>
    {
      ("Talk", new FakeRetranslationHandler(false, false, "Talk")),
      ("TalkSubtitle", new FakeRetranslationHandler(true, true, "TalkSubtitle")),
    };

    var result = await dispatcher.DispatchAsync(handlers);

    result.IsApplicable.Should().BeTrue();
    result.SurfaceName.Should().Be("TalkSubtitle");
  }

  private sealed class FakeRetranslationHandler :
      IAddonTranslationHandler,
      IVisibleDialogueRetranslationHandler
  {
    private readonly VisibleDialogueRetranslationResult result;

    public FakeRetranslationHandler(bool applicable, bool success, string surfaceName)
    {
      this.result = new VisibleDialogueRetranslationResult(
          applicable,
          success,
          surfaceName,
          surfaceName + " result");
    }

    public Dictionary<IAddonLifecycle.AddonEvent, IAddonLifecycle.AddonEventDelegate> GetEventHandlers() => new();

    public Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync()
        => Task.FromResult(this.result);
  }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~VisibleStorySurfaceRetranslationDispatcherTests"`
Expected: FAIL with missing dispatcher type.

- [ ] **Step 3: Write minimal implementation**

```csharp
// PluginUI/Helpers/VisibleStorySurfaceRetranslationDispatcher.cs
namespace Echoglossian.PluginUI.Helpers;

using Echoglossian.NativeUI.AddonHandlers.Talk;
using Echoglossian.NativeUI.Handlers;

public sealed class VisibleStorySurfaceRetranslationDispatcher
{
  public async Task<VisibleDialogueRetranslationResult> DispatchAsync(
      IEnumerable<(string AddonName, IAddonTranslationHandler Handler)> handlers)
  {
    foreach (var (_, handler) in handlers)
    {
      if (handler is not IVisibleDialogueRetranslationHandler visibleHandler)
      {
        continue;
      }

      var result = await visibleHandler
          .RetranslateVisibleTextAndPersistAsync()
          .ConfigureAwait(false);
      if (result.IsApplicable)
      {
        return result;
      }
    }

    return new VisibleDialogueRetranslationResult(
        false,
        false,
        "StorySurface",
        "No visible supported story-facing surface is currently available to retranslate.");
  }
}

// PluginUI/PluginRuntimeUi.cs
private readonly VisibleStorySurfaceRetranslationDispatcher visibleStorySurfaceRetranslationDispatcher = new();

private async Task<VisibleDialogueRetranslationResult>
    RetranslateVisibleDialogueAndPersistAsync()
{
  if (this.registeredAddonHandlers == null || this.registeredAddonHandlers.Count == 0)
  {
    return new VisibleDialogueRetranslationResult(
        false,
        false,
        "StorySurface",
        "No registered story-surface handler is currently available to retranslate visible text.");
  }

  return await this.visibleStorySurfaceRetranslationDispatcher
      .DispatchAsync(this.registeredAddonHandlers)
      .ConfigureAwait(false);
}

private void OpenDbManagerForTable(string tableName)
{
  this.dbEditorWindow?.OpenAndSelectTable(tableName);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~VisibleStorySurfaceRetranslationDispatcherTests"`
Expected: PASS with 1 passing test.

- [ ] **Step 5: Commit**

```powershell
git add PluginUI/Helpers/VisibleStorySurfaceRetranslationDispatcher.cs NativeUI/AddonHandlers/Talk/IVisibleDialogueRetranslationHandler.cs PluginUI/PluginRuntimeUi.cs Echoglossian.Tests/VisibleStorySurfaceRetranslationDispatcherTests.cs
git commit -m "feat: broaden visible story surface retranslation dispatch"
```

### Task 5: Extend Provenance And Visible Retranslation For `Talk`, `BattleTalk`, And `TalkSubtitle`

**Files:**
- Modify: `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
- Modify: `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
- Modify: `NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs`
- Modify: `PluginUI/TranslatorMetricsWindow.cs`
- Test: `Echoglossian.Tests/VisibleStorySurfaceInspectionModelBuilderTests.cs`

**Interfaces:**
- Consumes:
  - `VisibleStorySurfaceDiagnosticsStore.Record(...)`
  - `VisibleStorySurfaceDiagnosticsStore.SetRetranslationOutcome(...)`
  - `VisibleStorySurfaceInspectionModelBuilder.BuildRows(...)`
- `VisibleStorySurfaceTableMap.Resolve(...)`
- Produces:
  - provenance snapshots for `Talk`, `BattleTalk`, and `TalkSubtitle`
  - explicit `RetranslateVisibleTextAndPersistAsync()` support in `TalkSubtitleHandler`

- [ ] **Step 1: Write the failing test**

```csharp
// Add to Echoglossian.Tests/VisibleStorySurfaceInspectionModelBuilderTests.cs
[Fact]
public void BuildRows_IncludesRetranslationOutcomeWhenPresent()
{
  var snapshot = new VisibleStorySurfaceDiagnosticsSnapshot(
      VisibleStorySurfaceKind.TalkSubtitle,
      "DB reuse",
      "TalkSubtitleMessage",
      "Original subtitle",
      "Translated subtitle",
      false,
      2,
      DateTime.UtcNow,
      true,
      "TalkSubtitle visible text was retranslated and persisted.");

  var rows = VisibleStorySurfaceInspectionModelBuilder.BuildRows(snapshot);

  rows.Should().Contain(row => row.Key == "retranslation");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~VisibleStorySurfaceInspectionModelBuilderTests"`
Expected: FAIL because the builder does not emit a retranslation row yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// PluginUI/Helpers/VisibleStorySurfaceInspectionModelBuilder.cs
public static IReadOnlyList<InspectionRow> BuildRows(
    VisibleStorySurfaceDiagnosticsSnapshot snapshot)
{
  var rows = new List<InspectionRow>
  {
    new("surface", [new InspectionCell("Surface"), new InspectionCell(snapshot.Surface.ToString())]),
    new("source", [new InspectionCell("Source"), new InspectionCell(snapshot.ProvenanceLabel)]),
    new("table", [new InspectionCell("DB table"), new InspectionCell(snapshot.TableName)]),
    new("original", [new InspectionCell("Original"), new InspectionCell(snapshot.OriginalText, snapshot.OriginalText)]),
    new("translated", [new InspectionCell("Translated"), new InspectionCell(snapshot.TranslatedText, snapshot.TranslatedText)]),
  };

  if (!string.IsNullOrWhiteSpace(snapshot.LastRetranslationMessage))
  {
    rows.Add(new InspectionRow(
        "retranslation",
        [
          new InspectionCell("Retranslation"),
          new InspectionCell(snapshot.LastRetranslationMessage, snapshot.LastRetranslationMessage),
        ]));
  }

  return rows;
}

// NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs
public sealed class TalkSubtitleHandler :
    IAddonTranslationHandler,
    IVisibleDialogueRetranslationHandler

public async Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync()
{
  string originalText;
  lock (this.stateGate)
  {
    originalText = this.currentOriginalText;
  }

  if (string.IsNullOrWhiteSpace(originalText))
  {
    return new VisibleDialogueRetranslationResult(
        false,
        false,
        TalkSubtitleAddonName,
        "No visible TalkSubtitle line is available to retranslate.");
  }

  var translatedText = await this.translationService.TranslateAsync(
      originalText,
      ClientStateInterface.ClientLanguage.Humanize(),
      LangDict[LanguageInt].Code,
      TranslationSurfaceGroup.Dialogue).ConfigureAwait(false) ?? string.Empty;

  if (string.IsNullOrWhiteSpace(translatedText))
  {
    return new VisibleDialogueRetranslationResult(
        true,
        false,
        TalkSubtitleAddonName,
        "TalkSubtitle retranslation did not produce a usable translated result.");
  }

  var row = new TalkSubtitleMessage(
      originalText,
      ClientStateInterface.ClientLanguage.Humanize(),
      translatedText,
      LangDict[LanguageInt].Code,
      this.GetDialogueTranslationEngineId(),
      DateTime.Now,
      DateTime.Now);
  var persistenceResult = await this.insertTalkSubtitleMessageAsync(row).ConfigureAwait(false);
  this.SetResolvedState(originalText, translatedText, this.NormalizeForReplacement(translatedText));
  VisibleStorySurfaceDiagnosticsStore.SetRetranslationOutcome(
      VisibleStorySurfaceKind.TalkSubtitle,
      !persistenceResult.StartsWith("ErrorSavingData:", StringComparison.Ordinal),
      "TalkSubtitle visible text was retranslated and persisted.",
      DateTime.UtcNow);

  return new VisibleDialogueRetranslationResult(
      true,
      true,
      TalkSubtitleAddonName,
      "TalkSubtitle visible text was retranslated and persisted.");
}
```

- [ ] **Step 4: Integrate provenance recording for the three handlers**

```csharp
// TalkHandler.cs / BattleTalkHandler.cs / TalkSubtitleHandler.cs
VisibleStorySurfaceDiagnosticsStore.Record(
    new VisibleStorySurfaceDiagnosticsSnapshot(
        VisibleStorySurfaceKind.TalkSubtitle,
        foundStoredRow ? "DB reuse" : "Fresh live translation",
        VisibleStorySurfaceTableMap.Resolve(
            VisibleStorySurfaceKind.TalkSubtitle),
        originalText,
        translatedText,
        usedRuntimeOnlyDialogueContext,
        this.GetDialogueTranslationEngineId(),
        DateTime.UtcNow,
        null,
        null));

// PluginUI/TranslatorMetricsWindow.cs
private readonly Action<string> openDbManagerForTable;
private readonly InspectionTableView inspectionTableView = new();

public TranslatorMetricsWindow(
    Config config,
    Func<Task<VisibleDialogueRetranslationResult>> retranslateVisibleDialogueAsync,
    Action<string> openDbManagerForTable)
{
  this.config = config;
  this.retranslateVisibleDialogueAsync = retranslateVisibleDialogueAsync;
  this.openDbManagerForTable = openDbManagerForTable;
}

var snapshot = VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot();
if (snapshot != null)
{
  var rows = VisibleStorySurfaceInspectionModelBuilder.BuildRows(snapshot.Value);
  this.inspectionTableView.Draw(
      "##VisibleStorySurfaceSnapshot",
      [
        new InspectionColumnDefinition("field", "Field", 180f),
        new InspectionColumnDefinition("value", "Value", 520f),
      ],
      rows);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~VisibleStorySurfaceInspectionModelBuilderTests"`
Expected: PASS with the new retranslation row assertion included.

- [ ] **Step 6: Commit**

```powershell
git add NativeUI/AddonHandlers/Talk/TalkHandler.cs NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs PluginUI/TranslatorMetricsWindow.cs Echoglossian.Tests/VisibleStorySurfaceInspectionModelBuilderTests.cs
git commit -m "feat: add subtitle and dialogue provenance workflow"
```

### Task 6: Extend Visible Retranslation For `CutSceneSelectString` And `TextGimmickHint`, Then Finish Integration

**Files:**
- Modify: `NativeUI/AddonHandlers/CutSceneSelectString/CutSceneSelectStringHandler.cs`
- Modify: `NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs`
- Modify: `PluginUI/TranslatorMetricsWindow.cs`
- Modify: `DBManagerUI/DBEditorWindow.cs`
- Test: `Echoglossian.Tests/VisibleStorySurfaceDiagnosticsStoreTests.cs`

**Interfaces:**
- Consumes:
  - `VisibleStorySurfaceDiagnosticsStore`
  - `VisibleStorySurfaceTableMap.Resolve(...)`
  - `DbEditorWindow.OpenAndSelectTable(string tableName)`
- Produces:
  - explicit visible retranslate-and-persist for `CutSceneSelectString`
  - explicit visible retranslate-and-persist for `TextGimmickHint`
  - `View In DB Manager` button wired to the current snapshot table

- [ ] **Step 1: Write the failing test**

```csharp
// Add to Echoglossian.Tests/VisibleStorySurfaceDiagnosticsStoreTests.cs
[Fact]
public void Record_AllowsCutSceneSelectStringSnapshots()
{
  VisibleStorySurfaceDiagnosticsStore.Clear();

  VisibleStorySurfaceDiagnosticsStore.Record(
      new VisibleStorySurfaceDiagnosticsSnapshot(
          VisibleStorySurfaceKind.CutSceneSelectString,
          "Fresh live translation",
          "SelectString",
          "Question",
          "Translated question",
          false,
          4,
          DateTime.UtcNow,
          null,
          null));

  VisibleStorySurfaceDiagnosticsStore.GetLatestSnapshot()!.TableName
      .Should().Be("SelectString");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~VisibleStorySurfaceDiagnosticsStoreTests"`
Expected: FAIL if the store or snapshot shape still cannot represent the `SelectString` table-backed story surface cleanly.

- [ ] **Step 3: Write minimal implementation**

```csharp
// NativeUI/AddonHandlers/CutSceneSelectString/CutSceneSelectStringHandler.cs
public sealed class CutSceneSelectStringHandler :
    IAddonTranslationHandler,
    IVisibleDialogueRetranslationHandler

public async Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync()
{
  string originalQuestion;
  List<string> originalOptions;
  lock (this.stateGate)
  {
    originalQuestion = this.state.CurrentOriginalQuestion;
    originalOptions = [.. this.state.CurrentOriginalOptions];
  }

  if (string.IsNullOrWhiteSpace(originalQuestion))
  {
    return new VisibleDialogueRetranslationResult(
        false,
        false,
        AddonName,
        "No visible CutSceneSelectString prompt is available to retranslate.");
  }

  var (translatedQuestion, translatedOptions) =
      await this.TranslateDialogAsync(originalQuestion, originalOptions).ConfigureAwait(false);

  var payload = new SelectString(
      originalQuestion,
      ClientStateInterface.ClientLanguage.Humanize(),
      JsonConvert.SerializeObject(originalOptions),
      translatedQuestion,
      JsonConvert.SerializeObject(translatedOptions),
      LangDict[LanguageInt].Code,
      this.config.ChosenTransEngine,
      DateTime.Now,
      DateTime.Now);
  var persistenceResult = await this.insertCutSceneSelectStringMessageAsync(payload).ConfigureAwait(false);
  this.SetResolvedState(
      originalQuestion,
      originalOptions,
      translatedQuestion,
      translatedOptions,
      this.NormalizeForReplacement(translatedQuestion),
      translatedOptions.Select(this.NormalizeForReplacement).ToList());
  VisibleStorySurfaceDiagnosticsStore.SetRetranslationOutcome(
      VisibleStorySurfaceKind.CutSceneSelectString,
      !persistenceResult.StartsWith("ErrorSavingData:", StringComparison.Ordinal),
      "CutSceneSelectString visible text was retranslated and persisted.",
      DateTime.UtcNow);

  return new VisibleDialogueRetranslationResult(
      true,
      true,
      AddonName,
      "CutSceneSelectString visible text was retranslated and persisted.");
}

// NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs
internal sealed class TextGimmickHintHandler :
    IAddonTranslationHandler,
    IVisibleDialogueRetranslationHandler

public async Task<VisibleDialogueRetranslationResult> RetranslateVisibleTextAndPersistAsync()
{
  string originalText;
  lock (this.stateGate)
  {
    originalText = this.currentOriginalText;
  }

  if (string.IsNullOrWhiteSpace(originalText))
  {
    return new VisibleDialogueRetranslationResult(
        false,
        false,
        TextGimmickHintAddonName,
        "No visible TextGimmickHint line is available to retranslate.");
  }

  var translatedText = await this.translationService.TranslateAsync(
      originalText,
      ClientStateInterface.ClientLanguage.Humanize(),
      LangDict[LanguageInt].Code).ConfigureAwait(false) ?? string.Empty;
  var row = new TextGimmickHintMessage(
      originalText,
      ClientStateInterface.ClientLanguage.Humanize(),
      translatedText,
      LangDict[LanguageInt].Code,
      this.config.ChosenTransEngine,
      DateTime.Now,
      DateTime.Now);
  var persistenceResult = await this.insertTextGimmickHintMessageAsync(row).ConfigureAwait(false);
  this.SetResolvedState(originalText, translatedText, this.NormalizeForReplacement(translatedText));
  VisibleStorySurfaceDiagnosticsStore.SetRetranslationOutcome(
      VisibleStorySurfaceKind.TextGimmickHint,
      !persistenceResult.StartsWith("ErrorSavingData:", StringComparison.Ordinal),
      "TextGimmickHint visible text was retranslated and persisted.",
      DateTime.UtcNow);

  return new VisibleDialogueRetranslationResult(
      true,
      true,
      TextGimmickHintAddonName,
      "TextGimmickHint visible text was retranslated and persisted.");
}
```

- [ ] **Step 4: Wire the debugger handoff and run the full validation commands**

```csharp
// PluginUI/TranslatorMetricsWindow.cs
if (snapshot != null && ImGui.Button("View In DB Manager"))
{
  this.openDbManagerForTable(
      VisibleStorySurfaceTableMap.Resolve(snapshot.Value.Surface));
}

// DBManagerUI/DBEditorWindow.cs
// Reuse OpenAndSelectTable from Task 2; do not add delete logic to the debugger path.
```

Run: `dotnet build Echoglossian.sln -c Debug --no-restore`
Expected: BUILD SUCCEEDED

Run: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
Expected: PASS

- [ ] **Step 5: Perform in-game verification and commit**

Run these in game:
- `/eglotranslatordebugger`
- verify `Talk`, `BattleTalk`, `TalkSubtitle`, `CutSceneSelectString`, and `TextGimmickHint` show provenance
- verify the retranslate button refreshes and persists all five supported surfaces
- verify `View In DB Manager` opens `/eglodbmanager` on `TalkMessage`, `BattleTalkMessage`, `TalkSubtitleMessage`, `SelectString`, and `TextGimmickHintMessage` respectively

```powershell
git add NativeUI/AddonHandlers/CutSceneSelectString/CutSceneSelectStringHandler.cs NativeUI/AddonHandlers/Toasts/TextGimmickHintHandler.cs PluginUI/TranslatorMetricsWindow.cs DBManagerUI/DBEditorWindow.cs Echoglossian.Tests/VisibleStorySurfaceDiagnosticsStoreTests.cs
git commit -m "feat: finish story surface retranslation workflow"
```
