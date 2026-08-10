// <copyright file="QuestDialogueMetadataDerivationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers deterministic speaker and addressee derivation from quest dialogue rows.
/// </summary>
public class QuestDialogueMetadataDerivationTests
{
    /// <summary>
    ///     Ensures paired NAME rows identify speakers, SEQ rows delimit turns,
    ///     and neighboring named turns produce deterministic addressee hints.
    /// </summary>
    [Fact]
    public void BuildEntries_PairedDialogueRows_DerivesDeterministicMetadata()
    {
        var snapshot = new QuestProgressSnapshot(
            QuestId: 68799,
            QuestSequence: 0,
            QuestName: "For Better or Worse",
            QuestSheetName: "quest/032/LucKbb111_03263",
            QuestSteps: [],
            QuestSeqTexts: [],
            QuestSystemTexts: [],
            ContentHash: "quest-content-hash");
        var observedAtUtc = DateTime.UnixEpoch;
        QuestDialogueSheetEntry[] rows =
        [
            new("LucKbb111_03263_SEQ_00", "Phase zero", 0),
            new("LucKbb111_03263_NPC_NAME_000_000", "Oriel", 1),
            new("LucKbb111_03263_NPC_000_000", "Welcome, adventurer.", 2),
            new("LucKbb111_03263_NPC_NAME_000_001", "Radovan", 3),
            new("LucKbb111_03263_NPC_000_001", "We should leave.", 4),
            new("LucKbb111_03263_SEQ_01", "Phase one", 5),
            new("LucKbb111_03263_NPC_NAME_001_000", "Oriel", 6),
            new("LucKbb111_03263_NPC_001_000", "Stay alert.", 7),
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
}
