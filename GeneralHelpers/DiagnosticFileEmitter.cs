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
        var blockBuilder = new StringBuilder();
        blockBuilder.AppendLine(
            $"===== {DateTimeOffset.Now:O} | {title} =====");
        blockBuilder.AppendLine(content.TrimEnd());
        blockBuilder.AppendLine();

        lock (SyncRoot)
        {
            Directory.CreateDirectory(Echoglossian.ConfigDirectory);
            File.AppendAllText(
                filePath,
                blockBuilder.ToString(),
                Encoding.UTF8);
        }

        return filePath;
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
}
