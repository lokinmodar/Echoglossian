using System.Text.Json;
using Microsoft.Data.Sqlite;

var options = ParseArgs(args);
if (string.IsNullOrWhiteSpace(options.Sql))
{
    Console.Error.WriteLine("--sql is required.");
    return 1;
}

var databasePath = ResolveDatabasePath(options.DatabasePath);
if (!File.Exists(databasePath))
{
    Console.Error.WriteLine($"Database not found: {databasePath}");
    return 1;
}

using var connection = new SqliteConnection($"Data Source={databasePath}");
connection.Open();

Console.WriteLine($"Database: {databasePath}");
Console.WriteLine();

using var command = connection.CreateCommand();
command.CommandText = options.Sql;

if (IsQuery(options.Sql))
{
    using var reader = command.ExecuteReader();
    var rows = ReadRows(reader);
    if (options.AsJson)
    {
        Console.WriteLine(
            JsonSerializer.Serialize(
                rows,
                new JsonSerializerOptions { WriteIndented = true }));
    }
    else
    {
        WriteRows(rows);
    }

    return 0;
}

var affectedRows = command.ExecuteNonQuery();
Console.WriteLine($"affected_rows = {affectedRows}");
return 0;

static Options ParseArgs(string[] args)
{
    var options = new Options();

    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--database-path":
                options.DatabasePath = GetRequiredValue(args, ref index);
                break;
            case "--sql":
                options.Sql = GetRequiredValue(args, ref index);
                break;
            case "--json":
                options.AsJson = true;
                break;
        }
    }

    return options;
}

static string GetRequiredValue(string[] args, ref int index)
{
    if (index + 1 >= args.Length)
    {
        throw new ArgumentException(
            $"Missing value for argument '{args[index]}'.");
    }

    index++;
    return args[index];
}

static string ResolveDatabasePath(string? explicitPath)
{
    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return explicitPath;
    }

    var appData = Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData);
    return Path.Combine(
        appData,
        "XIVLauncher",
        "pluginConfigs",
        "Echoglossian",
        "Echoglossian.db");
}

static bool IsQuery(string sql)
{
    var trimmedSql = sql.TrimStart();
    if (string.IsNullOrWhiteSpace(trimmedSql))
    {
        return false;
    }

    var firstToken = trimmedSql
        .Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0]
        .ToLowerInvariant();

    return firstToken is "select" or "pragma" or "with" or "explain";
}

static List<Dictionary<string, object?>> ReadRows(SqliteDataReader reader)
{
    var rows = new List<Dictionary<string, object?>>();

    while (reader.Read())
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            row[reader.GetName(index)] = reader.IsDBNull(index)
                ? null
                : reader.GetValue(index);
        }

        rows.Add(row);
    }

    return rows;
}

static void WriteRows(IEnumerable<Dictionary<string, object?>> rows)
{
    foreach (var row in rows)
    {
        Console.WriteLine(
            string.Join(
                " | ",
                row.Select(pair => $"{pair.Key}={pair.Value}")));
    }
}

internal sealed class Options
{
    public string? DatabasePath { get; set; }

    public string? Sql { get; set; }

    public bool AsJson { get; set; }
}
