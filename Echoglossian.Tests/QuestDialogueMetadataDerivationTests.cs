// <copyright file="QuestDialogueMetadataDerivationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers deterministic speaker and addressee derivation from quest dialogue rows.
/// </summary>
public class QuestDialogueMetadataDerivationTests
{
    /// <summary>
    ///     Ensures the reader carries SEQ boundary state into retained rows so
    ///     the builder preserves multi-sequence metadata without SEQ rows.
    /// </summary>
    [Fact]
    public void ReadDialogueEntries_MultiSequenceRows_PreservesSequenceForBuildEntries()
    {
        var snapshot = this.CreateSnapshot();
        QuestDialogueSheetEntry[] rawRows =
        [
            new("LucKbb111_03263_SEQ_00", "Phase zero", 0, 0),
            new("LucKbb111_03263_NPC_NAME_000_000", "Oriel", 1, 0),
            new("LucKbb111_03263_NPC_000_000", "Welcome, adventurer.", 2, 0),
            new("LucKbb111_03263_SEQ_01", "Phase one", 3, 0),
            new("LucKbb111_03263_NPC_NAME_001_000", "Radovan", 4, 0),
            new("LucKbb111_03263_NPC_001_000", "Stay alert.", 5, 0),
        ];

        var dialogueEntries = QuestDialogueMetadataDerivation.ReadDialogueEntries(
            snapshot,
            rawRows);
        var metadata = QuestDialogueMetadataDerivation.BuildEntries(
            snapshot,
            dialogueEntries,
            sourceLanguageCode: "en",
            gameVersion: "2026.08.10.0000",
            derivationVersion: "v1",
            DateTime.UnixEpoch);

        Assert.DoesNotContain(
            dialogueEntries,
            entry => entry.RowKey.Contains("_SEQ_", StringComparison.Ordinal));
        Assert.Equal((ushort)0, dialogueEntries[0].QuestSequence);
        Assert.Equal((ushort)1, dialogueEntries[^1].QuestSequence);
        Assert.Equal([0, 1], metadata.Select(entry => (int)entry.QuestSequence));
    }

    /// <summary>
    ///     Ensures paired NAME rows identify speakers, SEQ rows delimit turns,
    ///     and neighboring named turns produce deterministic addressee hints.
    /// </summary>
    [Fact]
    public void BuildEntries_PairedDialogueRows_DerivesDeterministicMetadata()
    {
        var snapshot = this.CreateSnapshot();
        var observedAtUtc = DateTime.UnixEpoch;
        QuestDialogueSheetEntry[] rows =
        [
            new("LucKbb111_03263_SEQ_00", "Phase zero", 0, 0),
            new("LucKbb111_03263_NPC_NAME_000_000", "Oriel", 1, 0),
            new("LucKbb111_03263_NPC_000_000", "Welcome, adventurer.", 2, 0),
            new("LucKbb111_03263_NPC_NAME_000_001", "Radovan", 3, 0),
            new("LucKbb111_03263_NPC_000_001", "We should leave.", 4, 0),
            new("LucKbb111_03263_SEQ_01", "Phase one", 5, 1),
            new("LucKbb111_03263_NPC_NAME_001_000", "Oriel", 6, 1),
            new("LucKbb111_03263_NPC_001_000", "Stay alert.", 7, 1),
        ];

        var metadata = QuestDialogueMetadataDerivation.BuildEntries(
            snapshot,
            rows,
            sourceLanguageCode: "en",
            gameVersion: "2026.08.10.0000",
            derivationVersion: "v1",
            observedAtUtc);

        Assert.Equal(3, metadata.Count);

        var first = metadata[0];
        Assert.Equal((ushort)0, first.QuestSequence);
        Assert.Equal("LucKbb111_03263_NPC_000_000", first.SourceRowKey);
        Assert.Equal("Oriel", first.SpeakerHint);
        Assert.Equal("npc", first.SpeakerRoleHint);
        Assert.Equal("Radovan", first.AddresseeHint);
        Assert.Equal("npc", first.AddresseeRoleHint);
        Assert.Equal(2, first.ConfidenceTier);
        Assert.Equal("QuestSheetDerived", first.Provenance);
        Assert.Equal("LucKbb111_03263", first.QuestSheetId);
        Assert.Equal(snapshot.QuestSheetName, first.QuestTextSheetName);
        Assert.Equal(
            QuestContentHash.ComputeLine(first.SourceRowKey, "Welcome, adventurer."),
            first.SourceTextHash);
        Assert.Equal(observedAtUtc, first.CreatedAtUtc);
        Assert.Equal(observedAtUtc, first.UpdatedAtUtc);

        var second = metadata[1];
        Assert.Equal((ushort)0, second.QuestSequence);
        Assert.Equal("Radovan", second.SpeakerHint);
        Assert.Equal("Oriel", second.AddresseeHint);
        Assert.Equal(1, second.ConfidenceTier);

        var third = metadata[2];
        Assert.Equal((ushort)1, third.QuestSequence);
        Assert.Equal("Oriel", third.SpeakerHint);
        Assert.Empty(third.AddresseeHint);
        Assert.Equal(0, third.ConfidenceTier);

        Assert.Equal(
            QuestContentHash.ComputeLine("key", "text"),
            QuestContentHash.ComputeLine("key", "text"));
        Assert.Matches("^[0-9a-f]{16}$", first.SourceTextHash);
    }

    /// <summary>
    /// Ensures cancellation is observed while filtering source-ordered quest
    /// dialogue rows.
    /// </summary>
    [Fact]
    public void ReadDialogueEntries_CancelledTraversal_ThrowsOperationCanceledException()
    {
        var method = typeof(QuestDialogueMetadataDerivation).GetMethod(
            "ReadDialogueEntries",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(QuestProgressSnapshot),
                typeof(IReadOnlyList<QuestDialogueSheetEntry>),
                typeof(CancellationToken),
            ],
            modifiers: null);
        Assert.NotNull(method);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(
            null,
            [this.CreateSnapshot(), Array.Empty<QuestDialogueSheetEntry>(), cancellationTokenSource.Token]));

        Assert.IsType<OperationCanceledException>(exception.InnerException);
    }

    /// <summary>
    /// Ensures cancellation is observed before metadata pairing traverses the
    /// source-ordered dialogue rows.
    /// </summary>
    [Fact]
    public void BuildEntries_CancelledTraversal_ThrowsOperationCanceledException()
    {
        var method = typeof(QuestDialogueMetadataDerivation).GetMethod(
            "BuildEntries",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(QuestProgressSnapshot),
                typeof(IReadOnlyList<QuestDialogueSheetEntry>),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(DateTime),
                typeof(CancellationToken),
            ],
            modifiers: null);
        Assert.NotNull(method);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(
            null,
            [
                this.CreateSnapshot(),
                Array.Empty<QuestDialogueSheetEntry>(),
                "en",
                "2026.08.10.0000",
                "v1",
                DateTime.UnixEpoch,
                cancellationTokenSource.Token,
            ]));

        Assert.IsType<OperationCanceledException>(exception.InnerException);
    }

    /// <summary>
    ///     Creates the shared quest snapshot fixture.
    /// </summary>
    /// <returns>The fixed quest progress snapshot.</returns>
    private QuestProgressSnapshot CreateSnapshot()
    {
        return new QuestProgressSnapshot(
            QuestId: 68799,
            QuestSequence: 0,
            QuestName: "For Better or Worse",
            QuestSheetName: "quest/032/LucKbb111_03263",
            QuestSteps: [],
            QuestSeqTexts: [],
            QuestSystemTexts: [],
            ContentHash: "quest-content-hash");
    }
}
