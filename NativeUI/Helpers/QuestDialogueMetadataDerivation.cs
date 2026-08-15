// <copyright file="QuestDialogueMetadataDerivation.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.RegularExpressions;

using Echoglossian.EFCoreSqlite.Models.Journal;

using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace Echoglossian;

/// <summary>
///     Derives deterministic speaker and addressee metadata from one quest's
///     raw dialogue sheet rows.
/// </summary>
internal static partial class QuestDialogueMetadataDerivation
{
    /// <summary>
    ///     Reads populated dialogue and speaker-name rows from the quest text
    ///     sheet in their original source order.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <returns>The ordered dialogue and speaker-name sheet entries.</returns>
    public static IReadOnlyList<QuestDialogueSheetEntry> ReadDialogueEntries(
        QuestProgressSnapshot questProgressSnapshot)
    {
        return ReadDialogueEntries(questProgressSnapshot, CancellationToken.None);
    }

    /// <summary>
    ///     Reads populated dialogue and speaker-name rows while observing
    ///     cancellation during sheet traversal.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <param name="cancellationToken">The token that cancels sheet traversal.</param>
    /// <returns>The ordered dialogue and speaker-name sheet entries.</returns>
    internal static IReadOnlyList<QuestDialogueSheetEntry> ReadDialogueEntries(
        QuestProgressSnapshot questProgressSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(questProgressSnapshot.QuestSheetName))
        {
            return [];
        }

        var dataManager = Echoglossian.DManager;
        var questTextSheet = dataManager?.GameData.GetExcelSheet<RawRow>(
            name: questProgressSnapshot.QuestSheetName);
        if (questTextSheet == null || questTextSheet.Count == 0)
        {
            return [];
        }

        var rawEntries = new List<QuestDialogueSheetEntry>();
        var evaluator = Echoglossian.SeStringEvaluator;
        var rowCount = Convert.ToInt32(questTextSheet.Count, CultureInfo.InvariantCulture);
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = questTextSheet.GetRow((uint)rowIndex);
            var rowKey = EvaluateQuestText(row.ReadStringColumn(0), evaluator);
            var text = EvaluateQuestText(row.ReadStringColumn(1), evaluator);
            if (rowKey.Length == 0 || text.Length == 0)
            {
                continue;
            }

            rawEntries.Add(new QuestDialogueSheetEntry(rowKey, text, rowIndex, 0));
        }

        return ReadDialogueEntries(
            questProgressSnapshot,
            rawEntries,
            cancellationToken);
    }

    /// <summary>
    ///     Filters evaluated raw quest rows while carrying each latest SEQ
    ///     boundary onto retained dialogue and speaker-name rows.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <param name="rawEntries">The source-ordered evaluated raw sheet rows.</param>
    /// <returns>The ordered dialogue and speaker-name sheet entries.</returns>
    internal static IReadOnlyList<QuestDialogueSheetEntry> ReadDialogueEntries(
        QuestProgressSnapshot questProgressSnapshot,
        IReadOnlyList<QuestDialogueSheetEntry> rawEntries)
    {
        return ReadDialogueEntries(
            questProgressSnapshot,
            rawEntries,
            CancellationToken.None);
    }

    /// <summary>
    ///     Filters evaluated raw quest rows while observing cancellation during
    ///     source-ordered traversal.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <param name="rawEntries">The source-ordered evaluated raw sheet rows.</param>
    /// <param name="cancellationToken">The token that cancels row traversal.</param>
    /// <returns>The ordered dialogue and speaker-name sheet entries.</returns>
    internal static IReadOnlyList<QuestDialogueSheetEntry> ReadDialogueEntries(
        QuestProgressSnapshot questProgressSnapshot,
        IReadOnlyList<QuestDialogueSheetEntry> rawEntries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rawEntries);
        cancellationToken.ThrowIfCancellationRequested();

        List<QuestDialogueSheetEntry> dialogueEntries = [];
        ushort questSequence = 0;
        foreach (var entry in rawEntries.OrderBy(entry => entry.SourceOrder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetSequence(entry.RowKey, out var sequence))
            {
                questSequence = sequence;
                continue;
            }

            if (entry.RowKey.Length == 0 || entry.Text.Length == 0 || IsNonDialogueRow(entry.RowKey))
            {
                continue;
            }

            dialogueEntries.Add(entry with { QuestSequence = questSequence });
        }

        return dialogueEntries;
    }

    /// <summary>
    ///     Builds persistent metadata rows from source-ordered quest sheet entries.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <param name="dialogueEntries">The source-ordered quest sheet entries.</param>
    /// <param name="sourceLanguageCode">The source language code.</param>
    /// <param name="gameVersion">The game version that supplied the rows.</param>
    /// <param name="derivationVersion">The derivation algorithm version.</param>
    /// <param name="observedAtUtc">The UTC timestamp for the derived rows.</param>
    /// <returns>The derived metadata rows for paired dialogue text entries.</returns>
    public static IReadOnlyList<QuestDialogueMetadata> BuildEntries(
        QuestProgressSnapshot questProgressSnapshot,
        IReadOnlyList<QuestDialogueSheetEntry> dialogueEntries,
        string sourceLanguageCode,
        string gameVersion,
        string derivationVersion,
        DateTime observedAtUtc)
    {
        return BuildEntries(
            questProgressSnapshot,
            dialogueEntries,
            sourceLanguageCode,
            gameVersion,
            derivationVersion,
            observedAtUtc,
            CancellationToken.None);
    }

    /// <summary>
    ///     Builds persistent metadata rows while observing cancellation during
    ///     dialogue pairing and candidate scans.
    /// </summary>
    /// <param name="questProgressSnapshot">The resolved quest progress snapshot.</param>
    /// <param name="dialogueEntries">The source-ordered quest sheet entries.</param>
    /// <param name="sourceLanguageCode">The source language code.</param>
    /// <param name="gameVersion">The game version that supplied the rows.</param>
    /// <param name="derivationVersion">The derivation algorithm version.</param>
    /// <param name="observedAtUtc">The UTC timestamp for the derived rows.</param>
    /// <param name="cancellationToken">The token that cancels metadata derivation.</param>
    /// <returns>The derived metadata rows for paired dialogue text entries.</returns>
    internal static IReadOnlyList<QuestDialogueMetadata> BuildEntries(
        QuestProgressSnapshot questProgressSnapshot,
        IReadOnlyList<QuestDialogueSheetEntry> dialogueEntries,
        string sourceLanguageCode,
        string gameVersion,
        string derivationVersion,
        DateTime observedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var turns = ReadNamedTurns(dialogueEntries, cancellationToken);
        var questSheetId = GetQuestSheetId(questProgressSnapshot.QuestSheetName);
        List<QuestDialogueMetadata> results = [];

        foreach (var turn in turns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextSpeaker = turns.FirstOrDefault(candidate =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return candidate.QuestSequence == turn.QuestSequence &&
                       candidate.SourceOrder > turn.SourceOrder &&
                       !string.Equals(candidate.SpeakerHint, turn.SpeakerHint, StringComparison.Ordinal);
            });
            var previousSpeaker = nextSpeaker == default
                ? turns.LastOrDefault(candidate =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return candidate.QuestSequence == turn.QuestSequence &&
                           candidate.SourceOrder < turn.SourceOrder &&
                           !string.Equals(candidate.SpeakerHint, turn.SpeakerHint, StringComparison.Ordinal);
                })
                : default;
            var addresseeHint = nextSpeaker != default
                ? nextSpeaker.SpeakerHint
                : previousSpeaker.SpeakerHint ?? string.Empty;

            results.Add(new QuestDialogueMetadata
            {
                QuestId = questProgressSnapshot.QuestId,
                QuestSequence = turn.QuestSequence,
                SourceLanguageCode = sourceLanguageCode,
                GameVersion = gameVersion,
                QuestSheetId = questSheetId,
                QuestTextSheetName = questProgressSnapshot.QuestSheetName,
                SourceRowKey = turn.RowKey,
                SourceTextHash = QuestContentHash.ComputeLine(turn.RowKey, turn.Text),
                SourceTextPreview = turn.Text,
                SpeakerHint = turn.SpeakerHint,
                AddresseeHint = addresseeHint,
                SpeakerRoleHint = "npc",
                AddresseeRoleHint = addresseeHint.Length == 0 ? string.Empty : "npc",
                Provenance = "QuestSheetDerived",
                ConfidenceTier = nextSpeaker != default ? 2 : previousSpeaker != default ? 1 : 0,
                DerivationVersion = derivationVersion,
                CreatedAtUtc = observedAtUtc,
                UpdatedAtUtc = observedAtUtc,
            });
        }

        return results;
    }

    private static List<QuestDialogueNamedTurn> ReadNamedTurns(
        IReadOnlyList<QuestDialogueSheetEntry> dialogueEntries,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var namesBySequenceAndSuffix = new Dictionary<(ushort Sequence, string Suffix), string>();
        var textEntries = new List<(QuestDialogueSheetEntry Entry, ushort Sequence)>();
        var orderedEntries = dialogueEntries.OrderBy(entry =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return entry.SourceOrder;
        });
        foreach (var entry in orderedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetSequence(entry.RowKey, out var sequence))
            {
                continue;
            }

            if (!TryGetDialogueSuffix(entry.RowKey, out var suffix))
            {
                continue;
            }

            if (IsNameRow(entry.RowKey))
            {
                namesBySequenceAndSuffix[(entry.QuestSequence, suffix)] = entry.Text;
                continue;
            }

            if (!IsNonDialogueRow(entry.RowKey))
            {
                textEntries.Add((entry, entry.QuestSequence));
            }
        }

        List<QuestDialogueNamedTurn> turns = [];
        foreach (var (entry, sequence) in textEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetDialogueSuffix(entry.RowKey, out var suffix) ||
                !namesBySequenceAndSuffix.TryGetValue((sequence, suffix), out var speakerHint))
            {
                continue;
            }

            turns.Add(new QuestDialogueNamedTurn(
                entry.RowKey,
                entry.Text,
                entry.SourceOrder,
                sequence,
                speakerHint));
        }

        return turns;
    }

    private static bool IsNonDialogueRow(string rowKey)
    {
        return rowKey.Contains("_SEQ_", StringComparison.Ordinal) ||
               rowKey.Contains("_TODO_", StringComparison.Ordinal) ||
               rowKey.Contains("_SYSTEM_", StringComparison.Ordinal);
    }

    private static bool IsNameRow(string rowKey)
    {
        return rowKey.Contains("_NAME_", StringComparison.Ordinal);
    }

    private static bool TryGetSequence(string rowKey, out ushort sequence)
    {
        sequence = 0;
        var match = SequenceRowPattern().Match(rowKey);
        return match.Success && ushort.TryParse(
            match.Groups[1].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out sequence);
    }

    private static bool TryGetDialogueSuffix(string rowKey, out string suffix)
    {
        suffix = string.Empty;
        var match = DialogueSuffixPattern().Match(rowKey);
        if (!match.Success)
        {
            return false;
        }

        suffix = match.Value;
        return true;
    }

    private static string GetQuestSheetId(string questSheetName)
    {
        var separatorIndex = questSheetName.LastIndexOf('/');
        return separatorIndex < 0 ? questSheetName : questSheetName[(separatorIndex + 1)..];
    }

    private static string EvaluateQuestText(
        ReadOnlySeString text,
        ISeStringEvaluator? evaluator)
    {
        if (evaluator == null ||
            Echoglossian.FrameworkInterface?.IsInFrameworkUpdateThread != true)
        {
            return text.ExtractText();
        }

        try
        {
            return evaluator.Evaluate(
                    text,
                    language: Echoglossian.ClientStateInterface.ClientLanguage)
                .ExtractText();
        }
        catch (Exception)
        {
            return text.ExtractText();
        }
    }

    [GeneratedRegex(@"_SEQ_(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SequenceRowPattern();

    [GeneratedRegex(@"\d{3}_\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex DialogueSuffixPattern();

    private readonly record struct QuestDialogueNamedTurn(
        string RowKey,
        string Text,
        int SourceOrder,
        ushort QuestSequence,
        string SpeakerHint);
}

/// <summary>
///     Represents one evaluated quest dialogue sheet row in source order.
/// </summary>
/// <param name="RowKey">The evaluated source row key.</param>
/// <param name="Text">The evaluated source text.</param>
/// <param name="SourceOrder">The zero-based source row order.</param>
/// <param name="QuestSequence">The latest preceding SEQ boundary value.</param>
internal readonly record struct QuestDialogueSheetEntry(
    string RowKey,
    string Text,
    int SourceOrder,
    ushort QuestSequence);
