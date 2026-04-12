// <copyright file="QuestContentHash.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;

namespace Echoglossian;

/// <summary>
///     Computes a stable content fingerprint for a quest text sheet snapshot.
///     Used to detect whether a quest's translatable text actually changed between
///     game patches so that translations can be reused without retranslating.
///
///     Hash input: all SEQ, TODO, and SYSTEM rows concatenated as
///     "{key}={value}\n" pairs sorted by key. Result is the first 16 hex
///     characters of the SHA-256 digest (64-bit fingerprint — sufficient for
///     change detection, not a security primitive).
/// </summary>
internal static class QuestContentHash
{
    /// <summary>
    ///     Computes the content hash from classified quest text rows.
    /// </summary>
    /// <param name="seqRows">SEQ (journal summary) rows.</param>
    /// <param name="todoRows">TODO (objective) rows.</param>
    /// <param name="systemRows">SYSTEM (cinematic caption) rows.</param>
    /// <returns>A 16-character lowercase hex string fingerprint.</returns>
    public static string Compute(
        IReadOnlyList<QuestProgressEntry> seqRows,
        IReadOnlyList<QuestProgressEntry> todoRows,
        IReadOnlyList<QuestProgressEntry> systemRows)
    {
        // Build a deterministic sorted key=value list from all translated row types.
        var pairs = new List<string>(seqRows.Count + todoRows.Count + systemRows.Count);

        foreach (var row in seqRows)
        {
            pairs.Add($"{row.KeyText}={row.Text}");
        }

        foreach (var row in todoRows)
        {
            pairs.Add($"{row.KeyText}={row.Text}");
        }

        foreach (var row in systemRows)
        {
            pairs.Add($"{row.KeyText}={row.Text}");
        }

        pairs.Sort(StringComparer.Ordinal);

        var input = string.Join("\n", pairs);
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(inputBytes);

        // Return first 8 bytes (16 hex chars) — enough for change detection.
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }
}
