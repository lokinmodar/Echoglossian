// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

// quest-reader — reads FFXIV quest text sheets using Lumina, matching
// exactly what Dalamud/the plugin sees at runtime.
//
// Usage:
//   dotnet run -- --quest "Strange Bedfellows"
//   dotnet run -- --quest-id 69929
//   dotnet run -- --sheet quest/043/AktKmb114_04393
//   dotnet run -- --quest "The Paths We Walk" --lang en --all-rows
//   dotnet run -- --game-dir "D:\FFXIV\game" --quest "Strange Bedfellows"
//   dotnet run -- --quest "Strange Bedfellows" --json output.json
//   dotnet run -- --scenario-tree
//   dotnet run -- --scenario-tree --quest-id 69929
//
// Options:
//   --game-dir <path>    FFXIV game folder (parent of sqpack).
//                        Default: Steam common FFXIV path.
//   --quest <name>       Look up quest by display name (case-insensitive).
//   --quest-id <uint>    Look up quest by numeric RowId.
//   --sheet <name>       Read a raw text sheet directly, e.g.
//                        quest/043/AktKmb114_04393
//   --lang <en|ja|de|fr> Game language for the Quest sheet lookup.
//                        Default: en
//   --all-rows           Include empty rows in output.
//   --json <file>        Also write full output to a JSON file.
//   --scenario-tree      Probe the ScenarioTree typed sheet and show how its
//                        RowIds map to quest RowIds. Optionally filter to a
//                        single quest with --quest-id.

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

// ── Argument parsing ────────────────────────────────────────────────────────

const string DefaultGameDir =
    @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";

string gameDir = DefaultGameDir;
string? questName = null;
uint? questId = null;
string? sheetName = null;
Language language = Language.English;
bool allRows = false;
string? jsonOutputPath = null;
bool scenarioTreeProbe = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--game-dir":
            gameDir = args[++i];
            break;
        case "--quest":
            questName = args[++i];
            break;
        case "--quest-id":
            questId = uint.Parse(args[++i], CultureInfo.InvariantCulture);
            break;
        case "--sheet":
            sheetName = args[++i];
            break;
        case "--lang":
            language = args[++i].ToLower(CultureInfo.InvariantCulture) switch
            {
                "ja" => Language.Japanese,
                "de" => Language.German,
                "fr" => Language.French,
                _ => Language.English,
            };
            break;
        case "--all-rows":
            allRows = true;
            break;
        case "--json":
            jsonOutputPath = args[++i];
            break;
        case "--scenario-tree":
            scenarioTreeProbe = true;
            break;
        case "--help":
        case "-h":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (questName is null && questId is null && sheetName is null && !scenarioTreeProbe)
{
    Console.Error.WriteLine("Error: provide --quest, --quest-id, --sheet, or --scenario-tree.");
    PrintUsage();
    return 1;
}

// ── Open game data ───────────────────────────────────────────────────────────

if (!Directory.Exists(gameDir))
{
    Console.Error.WriteLine($"Game directory not found: {gameDir}");
    return 1;
}

// Lumina requires the sqpack directory; accept either parent or sqpack directly.
var sqpackDir = Path.GetFileName(gameDir)
    .Equals("sqpack", StringComparison.OrdinalIgnoreCase)
    ? gameDir
    : Path.Combine(gameDir, "sqpack");

if (!Directory.Exists(sqpackDir))
{
    Console.Error.WriteLine($"sqpack directory not found under: {gameDir}");
    return 1;
}

Console.WriteLine($"Opening game data: {sqpackDir}");
var opts = new LuminaOptions { PanicOnSheetChecksumMismatch = false };
var gameData = new GameData(sqpackDir, opts);
Console.WriteLine("Game data opened.");

// ── ScenarioTree probe mode ──────────────────────────────────────────────────

if (scenarioTreeProbe)
{
    return RunScenarioTreeProbe(gameData, language, questId);
}

// ── Resolve quest identity ───────────────────────────────────────────────────

// If only a raw sheet name was given, skip quest lookup.
if (sheetName is null)
{
    var questSheet = gameData.GetExcelSheet<Quest>(language);
    if (questSheet is null)
    {
        Console.Error.WriteLine("Could not load Quest sheet.");
        return 1;
    }

    Quest? foundQuest = null;
    if (questId.HasValue)
    {
        if (questSheet.TryGetRow(questId.Value, out var row))
        {
            foundQuest = row;
        }
        else
        {
            Console.Error.WriteLine($"Quest RowId {questId} not found.");
            return 1;
        }
    }
    else
    {
        // Linear search by display name.
        foreach (var q in questSheet)
        {
            if (q.Name.ExtractText()
                 .Equals(questName, StringComparison.OrdinalIgnoreCase))
            {
                foundQuest = q;
                break;
            }
        }

        if (foundQuest is null)
        {
            Console.Error.WriteLine($"Quest '{questName}' not found.");
            return 1;
        }
    }

    var q2 = foundQuest.Value;
    var internalId = ReadQuestInternalId(q2);
    var displayName = q2.Name.ExtractText();
    var rowId = q2.RowId;

    Console.WriteLine();
    Console.WriteLine($"  Quest RowId     : {rowId}");
    Console.WriteLine($"  Display Name    : {displayName}");
    Console.WriteLine($"  Internal Id     : {internalId}");

    sheetName = BuildQuestTextSheetName(internalId);
    if (sheetName.Length == 0)
    {
        Console.Error.WriteLine(
            $"Could not derive text sheet name from internal ID '{internalId}'.");
        return 1;
    }

    Console.WriteLine($"  Text Sheet      : {sheetName}");
    Console.WriteLine();
}
else
{
    Console.WriteLine($"  Text Sheet      : {sheetName}");
    Console.WriteLine();
}

// ── Read the text sheet ──────────────────────────────────────────────────────

var rawSheet = gameData.GetExcelSheet<RawRow>(name: sheetName);
if (rawSheet is null || rawSheet.Count == 0)
{
    Console.Error.WriteLine($"Text sheet not found or empty: {sheetName}");
    return 1;
}

var rowCount = (int)rawSheet.Count;
Console.WriteLine($"Rows in sheet: {rowCount}");
Console.WriteLine();

var rows = new List<QuestSheetRow>();

for (int i = 0; i < rowCount; i++)
{
    var row = rawSheet.GetRow((uint)i);
    var key = row.ReadStringColumn(0).ExtractText();
    var value = row.ReadStringColumn(1).ExtractText();

    if (!allRows && (key.Length == 0 || value.Length == 0))
    {
        continue;
    }

    var type = ClassifyRow(key);
    rows.Add(new QuestSheetRow(i, key, value, type));
}

// ── Print table ──────────────────────────────────────────────────────────────

if (rows.Count == 0)
{
    Console.WriteLine("No rows with content found. Use --all-rows to include empty rows.");
}
else
{
    var grouped = rows.GroupBy(r => r.Type).ToList();

    foreach (var group in grouped.OrderBy(g => RowTypeOrder(g.Key)))
    {
        var typeName = group.Key.ToString().ToUpper(CultureInfo.InvariantCulture);
        var separator = new string('─', 80);
        Console.WriteLine(separator);
        Console.WriteLine($"  [{typeName}]  ({group.Count()} rows)");
        Console.WriteLine(separator);

        foreach (var r in group)
        {
            Console.WriteLine($"  [{r.Index:D3}] {r.Key}");
            if (r.Value.Length > 0)
            {
                foreach (var line in WrapText(r.Value, 76))
                {
                    Console.WriteLine($"       {line}");
                }
            }
            else
            {
                Console.WriteLine("       (empty)");
            }

            Console.WriteLine();
        }
    }
}

// Summary statistics
Console.WriteLine("── Summary ──────────────────────────────────────────────────────────────────");
var allPopulated = rows.Where(r => r.Value.Length > 0).ToList();
Console.WriteLine($"  Total rows read : {rowCount}");
Console.WriteLine($"  Non-empty rows  : {allPopulated.Count}");
foreach (var g in allPopulated.GroupBy(r => r.Type).OrderBy(g => RowTypeOrder(g.Key)))
{
    Console.WriteLine(
        $"    {g.Key,-12} : {g.Count()} rows");
}

// ── JSON output ──────────────────────────────────────────────────────────────

if (jsonOutputPath is not null)
{
    var output = new JsonOutput(sheetName, rowCount, rows);
    var jsonOpts = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
    var json = JsonSerializer.Serialize(output, jsonOpts);
    File.WriteAllText(jsonOutputPath, json);
    Console.WriteLine($"\nJSON written to: {jsonOutputPath}");
}

return 0;

// ── Helpers ──────────────────────────────────────────────────────────────────

static string ReadQuestInternalId(Quest quest)
{
    // Quest.Id is a ReadOnlySeString containing the internal name,
    // e.g. "AktKmb114_04393". We use reflection to remain compatible
    // across Lumina minor versions where the exact property name may differ.
    var type = typeof(Quest);
    foreach (var name in new[] { "Id", "QuestId", "InternalId" })
    {
        var prop = type.GetProperty(
            name, BindingFlags.Instance | BindingFlags.Public);
        if (prop?.GetValue(quest) is ReadOnlySeString rosStr)
        {
            var text = rosStr.ExtractText().Trim('[', ']').Trim();
            if (text.Length > 0)
            {
                return text;
            }
        }
    }

    return string.Empty;
}

static string BuildQuestTextSheetName(string internalId)
{
    // Derive: quest/{last5chars[0..2]}/{internalId}
    // e.g.  "AktKmb114_04393" → "quest/043/AktKmb114_04393"
    if (internalId.Length < 5)
    {
        return string.Empty;
    }

    var suffix = internalId.Substring(internalId.Length - 5);
    var dir = suffix.Substring(0, 3);
    return $"quest/{dir}/{internalId}";
}

/// <summary>Classifies a sheet row key into a display category.</summary>
static RowType ClassifyRow(string key)
{
    if (key.Contains("_TODO_", StringComparison.Ordinal))
    {
        return RowType.Todo;
    }

    if (key.Contains("_SEQ_", StringComparison.Ordinal))
    {
        return RowType.Seq;
    }

    if (key.Contains("_SYSTEM_", StringComparison.Ordinal))
    {
        return RowType.System;
    }

    if (key.Length > 0)
    {
        return RowType.Dialog;
    }

    return RowType.Empty;
}

static int RowTypeOrder(RowType t) =>
    t switch
    {
        RowType.Seq => 0,
        RowType.Todo => 1,
        RowType.System => 2,
        RowType.Dialog => 3,
        RowType.Empty => 4,
        _ => 5,
    };

static IEnumerable<string> WrapText(string text, int width)
{
    // Wrap on spaces only, no hyphenation.
    var words = text.Split(' ');
    var line = new System.Text.StringBuilder();
    foreach (var word in words)
    {
        // Handle embedded newlines from multi-line values.
        foreach (var segment in word.Split('\n'))
        {
            if (line.Length > 0 && line.Length + 1 + segment.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(segment);
        }
    }

    if (line.Length > 0)
    {
        yield return line.ToString();
    }
}

static void PrintUsage()
{
    Console.WriteLine("""
        quest-reader — offline FFXIV quest sheet reader (uses Lumina, matches plugin output exactly)

        USAGE:
          dotnet run -- [options]

        OPTIONS:
          --game-dir <path>    FFXIV game folder (parent of sqpack\ or sqpack\ itself).
                               Default: Steam common FFXIV sqpack path
          --quest <name>       Look up quest by display name (case-insensitive)
          --quest-id <uint>    Look up quest by numeric RowId
          --sheet <name>       Read a raw text sheet directly
                               e.g.  quest/043/AktKmb114_04393
          --lang <en|ja|de|fr> Quest sheet language (default: en)
          --all-rows           Include empty rows in output
          --json <file>        Write full structured output to a JSON file
          --scenario-tree      Probe the ScenarioTree typed sheet and cross-reference
                               its RowIds with quest names. Use with --quest-id to
                               filter to a single quest.
          --help               Print this message

        EXAMPLES:
          dotnet run -- --quest "Strange Bedfellows"
          dotnet run -- --quest-id 69929
          dotnet run -- --sheet quest/014/HeaVnz025_01475 --all-rows
          dotnet run -- --quest "The Paths We Walk" --json paths-we-walk.json
          dotnet run -- --scenario-tree
          dotnet run -- --scenario-tree --quest-id 69929
        """);
}
// ── ScenarioTree probe ───────────────────────────────────────────────────────

static int RunScenarioTreeProbe(GameData gameData, Language language, uint? filterQuestId)
{
    // ScenarioTree is a typed Excel sheet where each RowId corresponds to a
    // Quest RowId. The sheet exposes the quest's scenario order and the
    // Quest field links back to the Quest sheet.
    //
    // Structure confirmed from Lumina.Excel.Sheets.ScenarioTree:
    //   RowId     — same integer as Quest.RowId for that quest
    //   Quest     — RowRef<Quest> back to the Quest sheet row
    //
    // This means ScenarioTree.RowId IS the quest RowId, and the addon
    // ScenarioTree renders the live objective (TODO row) for that quest.

    var stSheet = gameData.GetExcelSheet<ScenarioTree>(language);
    if (stSheet is null || stSheet.Count == 0)
    {
        Console.Error.WriteLine("ScenarioTree sheet not found or empty.");
        return 1;
    }

    var questSheet = gameData.GetExcelSheet<Quest>(language);

    Console.WriteLine();
    Console.WriteLine($"ScenarioTree rows: {stSheet.Count}");
    Console.WriteLine();

    int printed = 0;
    const int MaxPrint = 40;

    foreach (var stRow in stSheet)
    {
        if (filterQuestId.HasValue && stRow.RowId != filterQuestId.Value)
        {
            continue;
        }

        // Cross-reference: look up the quest name for this RowId.
        string questName = "(unknown)";
        string internalId = "(unknown)";
        string sheetPath = "(unknown)";

        if (questSheet != null && questSheet.TryGetRow(stRow.RowId, out var qRow))
        {
            questName = qRow.Name.ExtractText();
            internalId = ReadQuestInternalId(qRow);
            sheetPath = BuildQuestTextSheetName(internalId);
        }

        // Inspect all properties on the ScenarioTree row for additional fields.
        var stRowType = stRow.GetType();
        var props = stRowType.GetProperties(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public);

        Console.WriteLine($"  RowId={stRow.RowId,-8}  Quest='{questName}'");
        Console.WriteLine($"           InternalId={internalId}  Sheet={sheetPath}");

        foreach (var p in props)
        {
            if (p.Name == "RowId" || p.Name == "SubrowId")
            {
                continue;
            }

            try
            {
                var v = p.GetValue(stRow);
                if (v != null)
                {
                    var vs = v.ToString();
                    if (vs != null &&
                        vs != p.PropertyType.FullName &&
                        vs != p.PropertyType.Name)
                    {
                        Console.WriteLine($"           {p.Name}={vs}");
                    }
                }
            }
            catch
            {
                // ignore unreadable properties
            }
        }

        Console.WriteLine();

        printed++;
        if (!filterQuestId.HasValue && printed >= MaxPrint)
        {
            Console.WriteLine($"  ... (showing first {MaxPrint} of {stSheet.Count}. Use --quest-id to filter.)");
            break;
        }
    }

    if (printed == 0)
    {
        Console.WriteLine($"  No ScenarioTree row found for quest RowId={filterQuestId}.");
    }

    return 0;
}
// ── Record types ─────────────────────────────────────────────────────────────

/// <summary>Row type classification based on the key suffix pattern.</summary>
internal enum RowType
{
    /// <summary>Journal summary entry (_SEQ_NN).</summary>
    Seq,

    /// <summary>Objective / todo step (_TODO_NN).</summary>
    Todo,

    /// <summary>System / cinematic caption (_SYSTEM_NNN_NNN).</summary>
    System,

    /// <summary>NPC or character dialog (NPCNAME_NNN_NNN).</summary>
    Dialog,

    /// <summary>Empty / padding row.</summary>
    Empty,
}

/// <summary>A single parsed row from a quest text sheet.</summary>
internal sealed record QuestSheetRow(int Index, string Key, string Value, RowType Type);

/// <summary>Top-level JSON output structure.</summary>
internal sealed record JsonOutput(
    string SheetName,
    int TotalRows,
    List<QuestSheetRow> Rows);
