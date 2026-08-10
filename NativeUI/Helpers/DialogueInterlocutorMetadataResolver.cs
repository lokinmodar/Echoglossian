// <copyright file="DialogueInterlocutorMetadataResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.ClientState.Objects.Types;

using Echoglossian.EFCoreSqlite.Models.Journal;

namespace Echoglossian;

/// <summary>
///     Resolves exact persisted quest dialogue metadata and enriches it with
///     managed live actor and player-state hints.
/// </summary>
public sealed class DialogueInterlocutorMetadataResolver
{
    private readonly Func<IReadOnlyList<LiveDialogueActorSnapshot>> captureLiveActors;
    private readonly Func<QuestDialogueMetadataLookup, CancellationToken, Task<QuestDialogueMetadata?>> findMetadataAsync;
    private readonly Func<string> localPlayerName;
    private readonly Func<string?> playerSexHint;
    private readonly Func<QuestProgressSnapshot, IReadOnlyList<QuestDialogueSheetEntry>> readDialogueEntries;
    private readonly Func<IReadOnlyList<uint>> tryCollectAcceptedQuestIds;
    private readonly Func<uint, QuestProgressSnapshot?> tryResolveQuestProgress;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DialogueInterlocutorMetadataResolver" /> class.
    /// </summary>
    /// <param name="plugin">The plugin that owns exact database lookups.</param>
    public DialogueInterlocutorMetadataResolver(Echoglossian plugin)
        : this(
            CollectAcceptedQuestIds,
            ResolveQuestProgress,
            QuestDialogueMetadataDerivation.ReadDialogueEntries,
            plugin.FindQuestDialogueMetadataAsync,
            CaptureLiveActors,
            CaptureLocalPlayerName,
            CapturePlayerSexHint)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DialogueInterlocutorMetadataResolver" /> class.
    /// </summary>
    /// <param name="tryCollectAcceptedQuestIds">The accepted quest identifier collector.</param>
    /// <param name="tryResolveQuestProgress">The live quest progress resolver.</param>
    /// <param name="readDialogueEntries">The quest dialogue row reader.</param>
    /// <param name="findMetadataAsync">The exact persisted metadata lookup.</param>
    /// <param name="captureLiveActors">The managed live actor snapshot collector.</param>
    /// <param name="localPlayerName">The managed local player name reader.</param>
    /// <param name="playerSexHint">The managed local player sex hint reader.</param>
    internal DialogueInterlocutorMetadataResolver(
        Func<IReadOnlyList<uint>> tryCollectAcceptedQuestIds,
        Func<uint, QuestProgressSnapshot?> tryResolveQuestProgress,
        Func<QuestProgressSnapshot, IReadOnlyList<QuestDialogueSheetEntry>> readDialogueEntries,
        Func<QuestDialogueMetadataLookup, CancellationToken, Task<QuestDialogueMetadata?>> findMetadataAsync,
        Func<IReadOnlyList<LiveDialogueActorSnapshot>> captureLiveActors,
        Func<string> localPlayerName,
        Func<string?> playerSexHint)
    {
        this.tryCollectAcceptedQuestIds = tryCollectAcceptedQuestIds;
        this.tryResolveQuestProgress = tryResolveQuestProgress;
        this.readDialogueEntries = readDialogueEntries;
        this.findMetadataAsync = findMetadataAsync;
        this.captureLiveActors = captureLiveActors;
        this.localPlayerName = localPlayerName;
        this.playerSexHint = playerSexHint;
    }

    /// <summary>
    ///     Resolves exact accepted-quest dialogue metadata for one visible text payload.
    /// </summary>
    /// <param name="request">The visible dialogue source values.</param>
    /// <param name="cancellationToken">The token that cancels asynchronous database lookups.</param>
    /// <returns>The resolved dialogue metadata; otherwise, <see langword="null" />.</returns>
    public async Task<DialogueInterlocutorMetadata?> ResolveAsync(
        DialogueInterlocutorMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceText) ||
            string.IsNullOrWhiteSpace(request.SourceLanguageCode) ||
            string.IsNullOrWhiteSpace(request.GameVersion) ||
            string.IsNullOrWhiteSpace(request.DerivationVersion))
        {
            return null;
        }

        // Capture all plugin-service values before database work crosses an await boundary.
        var liveActors = this.captureLiveActors();
        var localPlayerName = this.localPlayerName();
        var playerSexHint = this.playerSexHint();
        var acceptedQuestIds = this.tryCollectAcceptedQuestIds();
        List<(QuestProgressSnapshot Snapshot, QuestDialogueSheetEntry Entry)> matchingRows = [];
        foreach (var acceptedQuestId in acceptedQuestIds)
        {
            var questProgress = this.tryResolveQuestProgress(acceptedQuestId);
            if (!questProgress.HasValue)
            {
                continue;
            }

            foreach (var entry in this.readDialogueEntries(questProgress.Value))
            {
                if (entry.QuestSequence == questProgress.Value.QuestSequence &&
                    string.Equals(entry.Text, request.SourceText, StringComparison.Ordinal))
                {
                    matchingRows.Add((questProgress.Value, entry));
                }
            }
        }

        List<QuestDialogueMetadata> candidates = [];
        foreach (var (snapshot, entry) in matchingRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lookup = new QuestDialogueMetadataLookup(
                snapshot.QuestId,
                entry.QuestSequence,
                request.SourceLanguageCode,
                request.GameVersion,
                entry.RowKey,
                QuestContentHash.ComputeLine(entry.RowKey, entry.Text),
                request.DerivationVersion);
            var metadata = await this.findMetadataAsync(lookup, cancellationToken).ConfigureAwait(false);
            if (metadata != null)
            {
                candidates.Add(metadata);
            }
        }

        var resolvedMetadata = SelectUniqueMetadata(candidates, request.VisibleSpeakerName);
        if (resolvedMetadata == null)
        {
            return null;
        }

        var speakerActor = FindUniqueLiveActor(liveActors, resolvedMetadata.SpeakerHint);
        var addresseeActor = FindUniqueLiveActor(liveActors, resolvedMetadata.AddresseeHint);
        var addresseeIsPlayer = string.Equals(
            resolvedMetadata.AddresseeHint,
            localPlayerName,
            StringComparison.Ordinal) || string.Equals(
            resolvedMetadata.AddresseeRoleHint,
            "player",
            StringComparison.OrdinalIgnoreCase);
        var usesLiveActor = speakerActor != null || addresseeActor != null;

        return new DialogueInterlocutorMetadata
        {
            ResolutionTier = usesLiveActor
                ? DialogueInterlocutorResolutionTier.QuestSheetPlusLiveFusion
                : DialogueInterlocutorResolutionTier.QuestSheetDerivedExact,
            SpeakerHint = resolvedMetadata.SpeakerHint,
            AddresseeHint = resolvedMetadata.AddresseeHint,
            SpeakerRoleHint = resolvedMetadata.SpeakerRoleHint,
            AddresseeRoleHint = resolvedMetadata.AddresseeRoleHint,
            SpeakerGenderHint = speakerActor?.GenderHint,
            SpeakerRaceHint = speakerActor?.RaceHint,
            SpeakerBodyTypeHint = speakerActor?.BodyTypeHint,
            AddresseeGenderHint = addresseeActor?.GenderHint ?? (addresseeIsPlayer ? playerSexHint : null),
            AddresseeRaceHint = addresseeActor?.RaceHint,
            AddresseeBodyTypeHint = addresseeActor?.BodyTypeHint,
            SpeakerActor = speakerActor,
            AddresseeActor = addresseeActor,
        };
    }

    private static IReadOnlyList<uint> CollectAcceptedQuestIds()
    {
        return QuestProgressResolver.TryCollectAcceptedQuestIds(out var questIds) ? questIds : [];
    }

    private static QuestProgressSnapshot? ResolveQuestProgress(uint questId)
    {
        return QuestProgressResolver.TryResolveQuestProgress(
            questId.ToString(CultureInfo.InvariantCulture),
            out var snapshot) ? snapshot : null;
    }

    private static string CaptureLocalPlayerName()
    {
        return Echoglossian.ObjectTableInterface?.LocalPlayer?.Name.TextValue ?? string.Empty;
    }

    private static string? CapturePlayerSexHint()
    {
        return NormalizeSexHint(Echoglossian.PlayerStateInterface?.Sex.ToString());
    }

    private static IReadOnlyList<LiveDialogueActorSnapshot> CaptureLiveActors()
    {
        List<LiveDialogueActorSnapshot> actors = [];
        if (Echoglossian.ObjectTableInterface == null)
        {
            return actors;
        }

        foreach (var gameObject in Echoglossian.ObjectTableInterface)
        {
            if (gameObject is not ICharacter character ||
                string.IsNullOrWhiteSpace(gameObject.Name.TextValue))
            {
                continue;
            }

            var customize = character.CustomizeData;
            actors.Add(new LiveDialogueActorSnapshot(
                gameObject.Name.TextValue,
                gameObject.BaseId,
                NormalizeSexHint(customize.Sex.ToString()),
                customize.Race.ToString().ToLowerInvariant(),
                customize.BodyType.ToString().ToLowerInvariant()));
        }

        return actors;
    }

    private static QuestDialogueMetadata? SelectUniqueMetadata(
        IReadOnlyList<QuestDialogueMetadata> candidates,
        string? visibleSpeakerName)
    {
        var distinctCandidates = candidates
            .DistinctBy(candidate => candidate.Id)
            .ToList();
        if (distinctCandidates.Count == 1)
        {
            return distinctCandidates[0];
        }

        if (string.IsNullOrWhiteSpace(visibleSpeakerName))
        {
            return null;
        }

        var speakerMatches = distinctCandidates
            .Where(candidate => string.Equals(
                candidate.SpeakerHint,
                visibleSpeakerName,
                StringComparison.Ordinal))
            .ToList();
        return speakerMatches.Count == 1 ? speakerMatches[0] : null;
    }

    private static LiveDialogueActorSnapshot? FindUniqueLiveActor(
        IReadOnlyList<LiveDialogueActorSnapshot> liveActors,
        string name)
    {
        var matches = liveActors
            .Where(actor => string.Equals(actor.Name, name, StringComparison.Ordinal))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static string? NormalizeSexHint(string? sex)
    {
        return sex?.ToLowerInvariant() switch
        {
            "male" => "male",
            "female" => "female",
            _ => null,
        };
    }
}
