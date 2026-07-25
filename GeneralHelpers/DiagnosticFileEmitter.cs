// <copyright file="DiagnosticFileEmitter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Emits purpose-specific diagnostic files into the plugin config
///     directory so structured runtime state can be inspected outside the
///     Dalamud log stream.
/// </summary>
internal static class DiagnosticFileEmitter
{
    private static readonly Lock SyncRoot = new();
    private static readonly Dictionary<string, long> LineCountsByFilePath = new(StringComparer.OrdinalIgnoreCase);
    private static int? maxLineCountOverride;

    /// <summary>
    ///     Appends a structured diagnostic block to a purpose-named log file in
    ///     the same directory as the SQLite DB.
    /// </summary>
    /// <param name="purpose">The purpose-specific file stem.</param>
    /// <param name="title">The title of the emitted block.</param>
    /// <param name="content">The structured content to append.</param>
    /// <returns>The absolute path of the emitted file, or an empty string when unavailable.</returns>
    public static string Emit(
        string purpose,
        string title,
        string content)
    {
        if (string.IsNullOrWhiteSpace(purpose) ||
            string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(content) ||
            string.IsNullOrWhiteSpace(Echoglossian.ConfigDirectory))
        {
            return string.Empty;
        }

        var safeFileName = BuildSafeFileName(purpose);
        if (safeFileName.Length == 0)
        {
            return string.Empty;
        }

        var filePath = Path.Combine(
            Echoglossian.ConfigDirectory,
            $"{safeFileName}.log");
        var blockLines = BuildBlockLines(title, content);

        lock (SyncRoot)
        {
            Directory.CreateDirectory(Echoglossian.ConfigDirectory);
            var currentLineCount = GetTrackedLineCount(filePath);
            var maxLineCount = GetMaxLineCount();
            if (currentLineCount >= maxLineCount ||
                (currentLineCount > 0 &&
                 currentLineCount + blockLines.Count > maxLineCount))
            {
                RotatingLogFileSupport.RotateActiveFile(filePath);
                currentLineCount = 0;
                LineCountsByFilePath[filePath] = 0;
            }

            using var writer = new StreamWriter(
                new FileStream(
                    filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite),
                new UTF8Encoding(false));
            foreach (var line in blockLines)
            {
                writer.WriteLine(line);
            }

            writer.Flush();
            LineCountsByFilePath[filePath] = currentLineCount + blockLines.Count;
        }

        return filePath;
    }

    /// <summary>
    ///     Clears cached line counts and restores the default line cap for
    ///     isolated tests.
    /// </summary>
    internal static void ResetForTests()
    {
        lock (SyncRoot)
        {
            LineCountsByFilePath.Clear();
            maxLineCountOverride = null;
        }
    }

    /// <summary>
    ///     Overrides the line-count rotation threshold for isolated tests.
    /// </summary>
    /// <param name="maxLineCount">The maximum active-file line count.</param>
    internal static void SetMaxLineCountForTests(int maxLineCount)
    {
        lock (SyncRoot)
        {
            maxLineCountOverride = maxLineCount > 0
                ? maxLineCount
                : RotatingLogFileSupport.DefaultMaxLineCount;
        }
    }

    private static string BuildSafeFileName(string purpose)
    {
        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = purpose
            .Trim()
            .Select(character =>
                invalidFileNameChars.Contains(character) || char.IsWhiteSpace(character)
                    ? '-'
                    : char.ToLowerInvariant(character))
            .ToArray();

        var sanitizedPurpose = new string(sanitizedChars).Trim('-');
        while (sanitizedPurpose.Contains("--", StringComparison.Ordinal))
        {
            sanitizedPurpose = sanitizedPurpose.Replace(
                "--",
                "-",
                StringComparison.Ordinal);
        }

        return sanitizedPurpose;
    }

    private static List<string> BuildBlockLines(string title, string content)
    {
        var blockLines = new List<string>
        {
            $"===== {DateTimeOffset.Now:O} | {title} =====",
        };

        blockLines.AddRange(
            RotatingLogFileSupport.EnumerateLines(content.TrimEnd()));
        blockLines.Add(string.Empty);
        return blockLines;
    }

    private static int GetMaxLineCount()
    {
        return maxLineCountOverride ?? RotatingLogFileSupport.DefaultMaxLineCount;
    }

    private static long GetTrackedLineCount(string filePath)
    {
        if (LineCountsByFilePath.TryGetValue(filePath, out var lineCount))
        {
            return lineCount;
        }

        lineCount = RotatingLogFileSupport.CountExistingLinesUpTo(
            filePath,
            GetMaxLineCount());
        LineCountsByFilePath[filePath] = lineCount;
        return lineCount;
    }
}
