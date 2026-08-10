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
        var provenanceProperty = typeof(DialogueInterlocutorMetadata).GetProperty("Provenance");
        var confidenceProperty = typeof(DialogueInterlocutorMetadata).GetProperty("ConfidenceTier");
        Assert.NotNull(provenanceProperty);
        Assert.NotNull(confidenceProperty);
        Assert.Equal("QuestSheetDerived", provenanceProperty.GetValue(result));
        Assert.Equal(2, confidenceProperty.GetValue(result));
    }

    /// <summary>
    ///     Ensures production resolver capture is marshaled through the Dalamud
    ///     framework before database work crosses an await boundary.
    /// </summary>
    [Fact]
    public void Resolver_ProductionCapture_UsesFrameworkThreadMarshal()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "Helpers",
            "DialogueInterlocutorMetadataResolver.cs"));

        Assert.Contains("FrameworkInterface.RunOnFrameworkThread(", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures native resolver values are captured on the framework thread,
    ///     while quest progress and sheet traversal execute after that boundary.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_CaptureScheduler_OnlyContainsNativeCaptureBeforeQuestTraversal()
    {
        var insideCapture = false;
        var captureSchedulerCalls = 0;
        var nativeCaptureReadCalls = 0;
        var questTraversalCalls = 0;
        var databaseLookupCalls = 0;
        var resolver = this.CreateResolver(
            nativeCaptureObserved: () =>
            {
                Assert.True(insideCapture);
                nativeCaptureReadCalls++;
            },
            questTraversalObserved: () =>
            {
                Assert.False(insideCapture);
                questTraversalCalls++;
            },
            lookupObserved: () =>
            {
                Assert.False(insideCapture);
                databaseLookupCalls++;
            },
            runOnFrameworkThreadAsync: (capture, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                captureSchedulerCalls++;
                insideCapture = true;
                try
                {
                    capture();
                }
                finally
                {
                    insideCapture = false;
                }

                return Task.CompletedTask;
            });

        var result = await resolver.ResolveAsync(this.CreateRequest());

        Assert.NotNull(result);
        Assert.Equal(1, captureSchedulerCalls);
        Assert.Equal(4, nativeCaptureReadCalls);
        Assert.Equal(2, questTraversalCalls);
        Assert.Equal(1, databaseLookupCalls);
    }

    /// <summary>
    ///     Ensures production wiring preserves persisted provenance and numeric
    ///     confidence instead of substituting the resolver evidence tier.
    /// </summary>
    [Fact]
    public void AddonHandlerWiring_PreservesPersistedMetadataIdentity()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "Helpers",
            "AddonHandlerWiring.cs"));

        Assert.Contains("metadata.Provenance", source, StringComparison.Ordinal);
        Assert.Contains("metadata.ConfidenceTier", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "metadata.ResolutionTier.ToString(),\n            metadata.ResolutionTier.ToString()",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures a loaded actor matching the persisted addressee upgrades the
    ///     result while retaining the persisted quest metadata.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_PersistedAddresseeWithMatchingLoadedActor_ReturnsQuestSheetPlusLiveFusion()
    {
        var resolver = this.CreateResolver(
            liveActors:
            [
                new("Radovan", 1045123, "male", "hyur", "midlander"),
            ]);

        var result = await resolver.ResolveAsync(this.CreateRequest());

        Assert.NotNull(result);
        Assert.Equal(DialogueInterlocutorResolutionTier.QuestSheetPlusLiveFusion, result.ResolutionTier);
        Assert.Equal("male", result.AddresseeGenderHint);
        Assert.Equal("hyur", result.AddresseeRaceHint);
        Assert.Equal("midlander", result.AddresseeBodyTypeHint);
        Assert.Equal((uint)1045123, result.AddresseeActor!.DataId);
    }

    /// <summary>
    ///     Ensures an ambiguous loaded actor name cannot upgrade persisted quest
    ///     metadata to live fusion.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_DuplicateNamedLiveActors_LeavesResultQuestSheetDerivedExact()
    {
        LiveDialogueActorSnapshot[] liveActors =
        [
            new("Radovan", 1045123, "male", "hyur", "midlander"),
            new("Radovan", 1045124, "female", "elezen", "wildwood"),
        ];
        var resolver = this.CreateResolver(liveActors: liveActors);

        var result = await resolver.ResolveAsync(this.CreateRequest());

        Assert.NotNull(result);
        Assert.Equal(DialogueInterlocutorResolutionTier.QuestSheetDerivedExact, result.ResolutionTier);
        Assert.Null(result.AddresseeActor);
        Assert.Null(result.AddresseeGenderHint);
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
    /// <param name="nativeCaptureObserved">The callback invoked for each native capture read.</param>
    /// <param name="questTraversalObserved">The callback invoked for each quest traversal read.</param>
    /// <param name="lookupObserved">The callback invoked at the database lookup boundary.</param>
    /// <param name="runOnFrameworkThreadAsync">The capture scheduler override.</param>
    /// <returns>A configured resolver.</returns>
    private DialogueInterlocutorMetadataResolver CreateResolver(
        QuestDialogueMetadata? metadata = null,
        IReadOnlyList<LiveDialogueActorSnapshot>? liveActors = null,
        string? playerSex = null,
        Action? nativeCaptureObserved = null,
        Action? questTraversalObserved = null,
        Action? lookupObserved = null,
        Func<Action, CancellationToken, Task>? runOnFrameworkThreadAsync = null)
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
            tryCollectAcceptedQuestIds: () =>
            {
                nativeCaptureObserved?.Invoke();
                return [68799];
            },
            tryResolveQuestProgress: questId =>
            {
                questTraversalObserved?.Invoke();
                return questId == 68799 ? snapshot : null;
            },
            readDialogueEntries: _ =>
            {
                questTraversalObserved?.Invoke();
                return rows;
            },
            findMetadataAsync: (lookup, _) =>
            {
                lookupObserved?.Invoke();
                Assert.Equal("LucKbb111_03263_NPC_000_000", lookup.SourceRowKey);
                Assert.Equal(
                    QuestContentHash.ComputeLine(lookup.SourceRowKey, "Welcome, adventurer."),
                    lookup.SourceTextHash);
                return Task.FromResult<QuestDialogueMetadata?>(expectedMetadata);
            },
            captureLiveActors: () =>
            {
                nativeCaptureObserved?.Invoke();
                return liveActors ?? [];
            },
            localPlayerName: () =>
            {
                nativeCaptureObserved?.Invoke();
                return "Player Name";
            },
            playerSexHint: () =>
            {
                nativeCaptureObserved?.Invoke();
                return playerSex;
            },
            runOnFrameworkThreadAsync: runOnFrameworkThreadAsync);
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

    /// <summary>
    ///     Locates the repository root for production wiring contract checks.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
