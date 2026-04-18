using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var options = InspectorOptions.Parse(args);
string databasePath = string.IsNullOrWhiteSpace(options.DatabasePath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "pluginConfigs", "Echoglossian", "Echoglossian.db")
    : options.DatabasePath;

if (!File.Exists(databasePath))
{
    throw new FileNotFoundException("Database not found.", databasePath);
}

using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
connection.Open();

Console.WriteLine($"Database: {databasePath}");

if (!string.IsNullOrWhiteSpace(options.Sql))
{
    WriteSection("custom query");
    WriteRows(connection, options.Sql!, options.AsJson);
    return;
}

foreach (string table in options.Tables)
{
    ShowTableSummary(connection, table, options.Limit, options.SkipSchema, options.AsJson);
}

static void ShowTableSummary(SqliteConnection connection, string table, int limit, bool skipSchema, bool asJson)
{
    bool tableExists = ExecuteScalar<long>(
        connection,
        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;",
        ("$name", table)) > 0;

    WriteSection(table + (tableExists ? string.Empty : " (missing)"));
    if (!tableExists)
    {
        return;
    }

    List<Dictionary<string, object?>> schemaRows = Query(
        connection,
        $"PRAGMA table_info({table});");

    if (!skipSchema)
    {
        Console.WriteLine("-- schema --");
        WriteObjects(schemaRows, asJson);
    }

    long rowCount = ExecuteScalar<long>(connection, $"SELECT COUNT(*) FROM {table};");
    Console.WriteLine($"row_count = {rowCount}");

    string orderColumn = GetPreferredOrderColumn(schemaRows);
    List<Dictionary<string, object?>> latestRows = Query(
        connection,
        $"SELECT * FROM {table} ORDER BY {orderColumn} DESC LIMIT {limit};");

    Console.WriteLine("-- latest rows --");
    WriteObjects(latestRows, asJson);

    List<string> translatedColumns = schemaRows
        .Select(row => row["name"]?.ToString())
        .Where(name => !string.IsNullOrWhiteSpace(name) && name.Contains("Translated", StringComparison.Ordinal))
        .Cast<string>()
        .ToList();

    if (translatedColumns.Count > 0)
    {
        Console.WriteLine("-- translated coverage --");
        foreach (string translatedColumn in translatedColumns)
        {
            List<Dictionary<string, object?>> coverage = Query(
                connection,
                $"""
                 SELECT
                     '{translatedColumn}' AS ColumnName,
                     COUNT(*) AS FilledCount
                 FROM {table}
                 WHERE {translatedColumn} IS NOT NULL
                   AND CAST({translatedColumn} AS TEXT) <> '';
                 """);

            WriteObjects(coverage, asJson);
        }
    }

    List<string> columnNames = schemaRows
        .Select(row => row["name"]?.ToString())
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Cast<string>()
        .ToList();

    if (table.Equals("gamewindows", StringComparison.OrdinalIgnoreCase))
    {
        string? addonColumn = columnNames.FirstOrDefault(name =>
            string.Equals(name, "WindowAddonName", StringComparison.Ordinal)
            || string.Equals(name, "AddonName", StringComparison.Ordinal));

        if (addonColumn is null)
        {
            return;
        }

        Console.WriteLine("-- grouped by AddonName --");
        WriteObjects(
            Query(
                connection,
                $"""
                SELECT
                    {addonColumn} AS AddonName,
                    COUNT(*) AS Count
                FROM gamewindows
                GROUP BY {addonColumn}
                ORDER BY Count DESC, {addonColumn} ASC;
                """),
            asJson);
    }

    if (table.Equals("stringarraydatas", StringComparison.OrdinalIgnoreCase)
        && columnNames.Contains("ArrayType", StringComparer.Ordinal))
    {
        Console.WriteLine("-- grouped by ArrayType --");
        WriteObjects(
            Query(
                connection,
                """
                SELECT
                    ArrayType,
                    COUNT(*) AS Count
                FROM stringarraydatas
                GROUP BY ArrayType
                ORDER BY Count DESC, ArrayType ASC;
                """),
            asJson);
    }
}

static List<Dictionary<string, object?>> Query(
    SqliteConnection connection,
    string sql,
    params (string Name, object? Value)[] parameters)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    foreach ((string name, object? value) in parameters)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    using SqliteDataReader reader = command.ExecuteReader();
    List<Dictionary<string, object?>> rows = new();
    while (reader.Read())
    {
        Dictionary<string, object?> row = new(StringComparer.Ordinal);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            row[reader.GetName(i)] = value;
        }

        rows.Add(row);
    }

    return rows;
}

static T ExecuteScalar<T>(
    SqliteConnection connection,
    string sql,
    params (string Name, object? Value)[] parameters)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    foreach ((string name, object? value) in parameters)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    object? valueObject = command.ExecuteScalar();
    if (valueObject is null or DBNull)
    {
        return default!;
    }

    return (T)Convert.ChangeType(valueObject, typeof(T), CultureInfo.InvariantCulture);
}

static string GetPreferredOrderColumn(List<Dictionary<string, object?>> schemaRows)
{
    List<string> columnNames = schemaRows
        .Select(row => row["name"]?.ToString())
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Cast<string>()
        .ToList();

    foreach (string candidate in new[] { "UpdatedDate", "CreatedDate", "Id" })
    {
        if (columnNames.Contains(candidate, StringComparer.Ordinal))
        {
            return candidate;
        }
    }

    return columnNames[0];
}

static void WriteSection(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}

static void WriteRows(SqliteConnection connection, string sql, bool asJson)
{
    WriteObjects(Query(connection, sql), asJson);
}

static void WriteObjects(List<Dictionary<string, object?>> rows, bool asJson)
{
    if (rows.Count == 0)
    {
        Console.WriteLine("(no rows)");
        return;
    }

    if (asJson)
    {
        Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        return;
    }

    foreach (Dictionary<string, object?> row in rows)
    {
        Console.WriteLine(JsonSerializer.Serialize(row, new JsonSerializerOptions
        {
            WriteIndented = false,
        }));
    }
}

internal sealed class InspectorOptions
{
    public string? DatabasePath { get; init; }

    public List<string> Tables { get; init; } = new() { "gamewindows", "stringarraydatas" };

    public string? Sql { get; init; }

    public int Limit { get; init; } = 10;

    public bool SkipSchema { get; init; }

    public bool AsJson { get; init; }

    public static InspectorOptions Parse(string[] args)
    {
        string? databasePath = null;
        List<string> tables = new();
        string? sql = null;
        int limit = 10;
        bool skipSchema = false;
        bool asJson = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--database-path":
                    databasePath = args[++i];
                    break;
                case "--table":
                    tables.Add(args[++i]);
                    break;
                case "--sql":
                    sql = args[++i];
                    break;
                case "--limit":
                    limit = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--skip-schema":
                    skipSchema = true;
                    break;
                case "--json":
                    asJson = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return new InspectorOptions
        {
            DatabasePath = databasePath,
            Sql = sql,
            Limit = limit,
            SkipSchema = skipSchema,
            AsJson = asJson,
            Tables = tables.Count > 0 ? tables : new List<string> { "gamewindows", "stringarraydatas" },
        };
    }
}
