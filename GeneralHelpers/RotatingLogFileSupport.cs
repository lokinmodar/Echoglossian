// <copyright file="RotatingLogFileSupport.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Shared helpers for plain-text log files that rotate by line count while
///     keeping a stable active file name.
/// </summary>
internal static class RotatingLogFileSupport
{
    private const string ArchiveTimestampFormat = "yyyyMMdd-HHmmss-fff";

    /// <summary>
    ///     Gets the default maximum number of lines permitted in one active log
    ///     file before archival rotation occurs.
    /// </summary>
    internal const int DefaultMaxLineCount = 20000;

    /// <summary>
    ///     Splits the supplied text into logical lines using normalized newline
    ///     handling.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <returns>The logical lines that should be written individually.</returns>
    internal static string[] EnumerateLines(string text)
    {
        var normalizedText = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var segments = normalizedText.Split('\n');

        return segments.Length == 0 ? [string.Empty] : segments;
    }

    /// <summary>
    ///     Counts the number of existing lines in a log file while allowing
    ///     concurrent readers.
    /// </summary>
    /// <param name="filePath">The log file path.</param>
    /// <returns>The number of lines currently stored in the file.</returns>
    internal static long CountExistingLines(string filePath)
    {
        return CountExistingLinesUpTo(filePath, null);
    }

    /// <summary>
    ///     Counts the number of existing lines in a log file while allowing
    ///     concurrent readers, stopping once the supplied ceiling is reached.
    /// </summary>
    /// <param name="filePath">The log file path.</param>
    /// <param name="maxLineCount">The optional maximum line count to inspect.</param>
    /// <returns>The number of lines currently stored in the file, capped when requested.</returns>
    internal static long CountExistingLinesUpTo(
        string filePath,
        long? maxLineCount)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        long lineCount = 0;
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() != null)
        {
            lineCount++;
            if (maxLineCount.HasValue && lineCount >= maxLineCount.Value)
            {
                return maxLineCount.Value;
            }
        }

        return lineCount;
    }

    /// <summary>
    ///     Archives the active log file by moving it to a timestamped sibling
    ///     path that preserves the original file stem.
    /// </summary>
    /// <param name="activeFilePath">The active file path.</param>
    /// <returns>The archive path, or an empty string when no active file existed.</returns>
    internal static string RotateActiveFile(string activeFilePath)
    {
        if (!File.Exists(activeFilePath))
        {
            return string.Empty;
        }

        var timestamp = DateTimeOffset.Now;
        var archiveFilePath = BuildArchivePath(
            activeFilePath,
            timestamp,
            suffix: null);
        var suffix = 1;

        while (File.Exists(archiveFilePath))
        {
            archiveFilePath = BuildArchivePath(
                activeFilePath,
                timestamp,
                suffix);
            suffix++;
        }

        File.Move(activeFilePath, archiveFilePath);
        return archiveFilePath;
    }

    private static string BuildArchivePath(
        string activeFilePath,
        DateTimeOffset timestamp,
        int? suffix)
    {
        var directory = Path.GetDirectoryName(activeFilePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(activeFilePath);
        var extension = Path.GetExtension(activeFilePath);
        var suffixText = suffix.HasValue
            ? $"-{suffix.Value}"
            : string.Empty;

        return Path.Combine(
            directory,
            $"{fileNameWithoutExtension}-{timestamp.ToString(ArchiveTimestampFormat, CultureInfo.InvariantCulture)}{suffixText}{extension}");
    }
}
