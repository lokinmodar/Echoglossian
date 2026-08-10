// <copyright file="DialogueInterlocutorMetadataResolverTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models.Journal;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers resolution of persisted quest dialogue metadata with optional
///     managed live actor and player-state hints.
/// </summary>
public class DialogueInterlocutorMetadataResolverTests
{
    /// <summary>
    ///     Ensures exact accepted-quest metadata resolves without a live actor.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_PersistedAcceptedQuestMetadata_ReturnsQuestSheetDerivedExact()
    {
        var resolver = this.CreateResolver();

        var result = await resolver.ResolveAsync(this.CreateRequest());

        Assert.NotNull(result);
        Assert.Equal(DialogueInterlocutorResolutionTier.QuestSheetDerivedExact, result.ResolutionTier);
        Assert.Equal("Oriel", result.SpeakerHint);
        Assert.Equal("Radovan", result.AddresseeHint);
        Assert.Null(result.AddresseeGenderHint);
    }

    /// <summary>
    ///     Ensures a loaded actor matching the persisted addressee upgrades the
    ///     result while retaining the persisted quest metadata.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_PersistedAddresseeWithMatchingLoadedActor_ReturnsQuestSheetPlusLiveFusion()
    {
        var resolver = this.CreateResolver(
            liveActors: new Dictionary<string, LiveDialogueActorSnapshot>(StringComparer.Ordinal)
            {
                ["Radovan"] = new("Radovan", 1045123, "male", "hyur", "midlander"),
            });

        var result = await resolver.ResolveAsync(this.CreateRequest());

        Assert.NotNull(result);
        Assert.Equal(DialogueInterlocutorResolutionTier.QuestSheetPlusLiveFusion, result.ResolutionTier);
        Assert.Equal("male", result.AddresseeGenderHint);
        Assert.Equal("hyur", result.AddresseeRaceHint);
        Assert.Equal("midlander", result.AddresseeBodyTypeHint);
        Assert.Equal((uint)1045123, result.AddresseeActor!.DataId);
    }

    /// <summary>
    ///     Ensures player addressee detection derives gender from player state
    ///     without changing persisted quest metadata.
    /// </summary>
    [Theory]
    [InlineData("male")]
    [InlineData("female")]
    public async Task ResolveAsync_PlayerAddressee_UsesPlayerSexWithoutChangingQuestMetadata(string playerSex)
    {
        var metadata = this.CreateMetadata(addresseeHint: "Adventurer", addresseeRoleHint: "player");
        var resolver = this.CreateResolver(metadata: metadata, playerSex: playerSex);

        var result = await resolver.ResolveAsync(this.CreateRequest());

        Assert.NotNull(result);
        Assert.Equal(DialogueInterlocutorResolutionTier.QuestSheetDerivedExact, result.ResolutionTier);
        Assert.Equal(playerSex, result.AddresseeGenderHint);
        Assert.Equal("Adventurer", result.AddresseeHint);
        Assert.Equal("player", result.AddresseeRoleHint);
    }

    /// <summary>
    ///     Creates a resolver backed by deterministic managed quest, metadata,
    ///     actor, and player-state snapshots.
    /// </summary>
    /// <param name="metadata">The persisted metadata returned by the exact lookup.</param>
    /// <param name="liveActors">The managed live actor snapshots.</param>
    /// <param name="playerSex">The managed player sex hint.</param>
    /// <returns>A configured resolver.</returns>
    private DialogueInterlocutorMetadataResolver CreateResolver(
        QuestDialogueMetadata? metadata = null,
        IReadOnlyDictionary<string, LiveDialogueActorSnapshot>? liveActors = null,
        string? playerSex = null)
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
        QuestDialogueSheetEntry[] rows =
        [
            new("LucKbb111_03263_NPC_000_000", "Welcome, adventurer.", 0, 0),
        ];
        var expectedMetadata = metadata ?? this.CreateMetadata();

        return new DialogueInterlocutorMetadataResolver(
            tryCollectAcceptedQuestIds: () => [68799],
            tryResolveQuestProgress: questId => questId == 68799 ? snapshot : null,
            readDialogueEntries: _ => rows,
            findMetadataAsync: (lookup, _) =>
            {
                Assert.Equal("LucKbb111_03263_NPC_000_000", lookup.SourceRowKey);
                Assert.Equal(
                    QuestContentHash.ComputeLine(lookup.SourceRowKey, "Welcome, adventurer."),
                    lookup.SourceTextHash);
                return Task.FromResult<QuestDialogueMetadata?>(expectedMetadata);
            },
            captureLiveActors: () => liveActors ?? new Dictionary<string, LiveDialogueActorSnapshot>(),
            localPlayerName: () => "Player Name",
            playerSexHint: () => playerSex);
    }

    /// <summary>
    ///     Creates the fixed dialogue lookup request.
    /// </summary>
    /// <returns>A fixed dialogue lookup request.</returns>
    private DialogueInterlocutorMetadataRequest CreateRequest()
    {
        return new DialogueInterlocutorMetadataRequest(
            "Welcome, adventurer.",
            "Oriel",
            "en",
            "2026.08.10.0000",
            "v1");
    }

    /// <summary>
    ///     Creates fixed persisted quest dialogue metadata.
    /// </summary>
    /// <param name="addresseeHint">The derived addressee name.</param>
    /// <param name="addresseeRoleHint">The derived addressee role.</param>
    /// <returns>The persisted metadata row.</returns>
    private QuestDialogueMetadata CreateMetadata(
        string addresseeHint = "Radovan",
        string addresseeRoleHint = "npc")
    {
        return new QuestDialogueMetadata
        {
            QuestId = 68799,
            QuestSequence = 0,
            SourceLanguageCode = "en",
            GameVersion = "2026.08.10.0000",
            SourceRowKey = "LucKbb111_03263_NPC_000_000",
            SourceTextHash = QuestContentHash.ComputeLine(
                "LucKbb111_03263_NPC_000_000",
                "Welcome, adventurer."),
            DerivationVersion = "v1",
            SpeakerHint = "Oriel",
            AddresseeHint = addresseeHint,
            SpeakerRoleHint = "npc",
            AddresseeRoleHint = addresseeRoleHint,
            Provenance = "QuestSheetDerived",
            ConfidenceTier = 2,
        };
    }
}
