// <copyright file="DialogueInterlocutorMetadataResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Game.ClientState.Objects.Types;

using Echoglossian.EFCoreSqlite.Models.Journal;

using QuestManager = FFXIVClientStructs.FFXIV.Client.Game.QuestManager;

namespace Echoglossian;

/// <summary>
///     Resolves exact persisted quest dialogue metadata and enriches it with
///     managed live actor and player-state hints.
/// </summary>
public sealed class DialogueInterlocutorMetadataResolver
{
    private readonly Func<IReadOnlyList<LiveDialogueActorSnapshot>> captureLiveActors;
    private readonly Func<uint, byte> captureQuestSequence;
    private readonly Func<QuestDialogueMetadataLookup, CancellationToken, Task<QuestDialogueMetadata?>> findMetadataAsync;
    private readonly Func<string> localPlayerName;
    private readonly Func<string?> playerSexHint;
    private readonly Func<QuestProgressSnapshot, CancellationToken, IReadOnlyList<QuestDialogueSheetEntry>> readDialogueEntries;
    private readonly Func<Action, CancellationToken, Task> runOnFrameworkThreadAsync;
    private readonly Func<IReadOnlyList<uint>> tryCollectAcceptedQuestIds;
    private readonly Func<uint, byte, CancellationToken, QuestProgressSnapshot?> tryResolveQuestProgress;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DialogueInterlocutorMetadataResolver" /> class.
    /// </summary>
    /// <param name="plugin">The plugin that owns exact database lookups.</param>
    public DialogueInterlocutorMetadataResolver(Echoglossian plugin)
        : this(
            CollectAcceptedQuestIds,
            CaptureQuestSequence,
            ResolveQuestProgress,
            QuestDialogueMetadataDerivation.ReadDialogueEntries,
            plugin.FindQuestDialogueMetadataAsync,
            CaptureLiveActors,
            CaptureLocalPlayerName,
            CapturePlayerSexHint,
            RunOnFrameworkThreadAsync)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DialogueInterlocutorMetadataResolver" /> class.
    /// </summary>
    /// <param name="tryCollectAcceptedQuestIds">The accepted quest identifier collector.</param>
    /// <param name="captureQuestSequence">The live quest sequence capture.</param>
    /// <param name="tryResolveQuestProgress">The live quest progress resolver.</param>
    /// <param name="readDialogueEntries">The quest dialogue row reader.</param>
    /// <param name="findMetadataAsync">The exact persisted metadata lookup.</param>
    /// <param name="captureLiveActors">The managed live actor snapshot collector.</param>
    /// <param name="localPlayerName">The managed local player name reader.</param>
    /// <param name="playerSexHint">The managed local player sex hint reader.</param>
    /// <param name="runOnFrameworkThreadAsync">The native capture scheduler.</param>
    internal DialogueInterlocutorMetadataResolver(
        Func<IReadOnlyList<uint>> tryCollectAcceptedQuestIds,
        Func<uint, byte> captureQuestSequence,
        Func<uint, byte, CancellationToken, QuestProgressSnapshot?> tryResolveQuestProgress,
        Func<QuestProgressSnapshot, CancellationToken, IReadOnlyList<QuestDialogueSheetEntry>> readDialogueEntries,
        Func<QuestDialogueMetadataLookup, CancellationToken, Task<QuestDialogueMetadata?>> findMetadataAsync,
        Func<IReadOnlyList<LiveDialogueActorSnapshot>> captureLiveActors,
        Func<string> localPlayerName,
        Func<string?> playerSexHint,
        Func<Action, CancellationToken, Task>? runOnFrameworkThreadAsync = null)
    {
        this.tryCollectAcceptedQuestIds = tryCollectAcceptedQuestIds;
        this.captureQuestSequence = captureQuestSequence;
        this.tryResolveQuestProgress = tryResolveQuestProgress;
        this.readDialogueEntries = readDialogueEntries;
        this.findMetadataAsync = findMetadataAsync;
        this.captureLiveActors = captureLiveActors;
        this.localPlayerName = localPlayerName;
        this.playerSexHint = playerSexHint;
        this.runOnFrameworkThreadAsync = runOnFrameworkThreadAsync ?? RunInlineAsync;
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

        ResolverCaptureSnapshot? captureSnapshot = null;
        await this.runOnFrameworkThreadAsync(
            () => captureSnapshot = this.CaptureSnapshot(),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (captureSnapshot == null)
        {
            return null;
        }

        var liveActors = captureSnapshot.LiveActors;
        var localPlayerName = captureSnapshot.LocalPlayerName;
        var playerSexHint = captureSnapshot.PlayerSexHint;
        var matchingRows = this.FindMatchingRows(
            captureSnapshot.AcceptedQuests,
            request.SourceText,
            cancellationToken);

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
            return BuildVisibleSpeakerLiveActorFallback(
                request.VisibleSpeakerName,
                liveActors,
                localPlayerName,
                playerSexHint);
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
            Provenance = resolvedMetadata.Provenance,
            ConfidenceTier = resolvedMetadata.ConfidenceTier,
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

    /// <summary>
    ///     Captures native resolver inputs into immutable managed values before
    ///     asynchronous work begins.
    /// </summary>
    /// <returns>The immutable resolver capture.</returns>
    private ResolverCaptureSnapshot CaptureSnapshot()
    {
        var liveActors = this.captureLiveActors().ToArray();
        var localPlayerName = this.localPlayerName();
        var playerSexHint = this.playerSexHint();
        var acceptedQuests = this.tryCollectAcceptedQuestIds()
            .Select(questId => new AcceptedQuestCapture(
                questId,
                this.captureQuestSequence(questId)))
            .ToArray();
        return new ResolverCaptureSnapshot(
            liveActors,
            localPlayerName,
            playerSexHint,
            acceptedQuests);
    }

    /// <summary>
    ///     Resolves exact accepted-quest dialogue rows outside the native capture
    ///     scheduler using the copied accepted quest identifiers.
    /// </summary>
    /// <param name="acceptedQuests">The accepted quest identifiers and sequences captured on the framework thread.</param>
    /// <param name="sourceText">The visible source text requiring an exact match.</param>
    /// <param name="cancellationToken">The token that cancels traversal.</param>
    /// <returns>The exact matching quest snapshots and dialogue entries.</returns>
    private IReadOnlyList<(QuestProgressSnapshot Snapshot, QuestDialogueSheetEntry Entry)> FindMatchingRows(
        IReadOnlyList<AcceptedQuestCapture> acceptedQuests,
        string sourceText,
        CancellationToken cancellationToken)
    {
        List<(QuestProgressSnapshot Snapshot, QuestDialogueSheetEntry Entry)> matchingRows = [];
        foreach (var acceptedQuest in acceptedQuests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var questProgress = this.tryResolveQuestProgress(
                acceptedQuest.QuestId,
                acceptedQuest.QuestSequence,
                cancellationToken);
            if (!questProgress.HasValue)
            {
                continue;
            }

            foreach (var entry in this.readDialogueEntries(questProgress.Value, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.QuestSequence == questProgress.Value.QuestSequence &&
                    string.Equals(entry.Text, sourceText, StringComparison.Ordinal))
                {
                    matchingRows.Add((questProgress.Value, entry));
                }
            }
        }

        return matchingRows;
    }

    /// <summary>
    ///     Marshals one native capture action onto the Dalamud framework thread.
    /// </summary>
    /// <param name="capture">The native capture action.</param>
    /// <param name="cancellationToken">The token that cancels the resolver operation.</param>
    /// <returns>A task representing the framework-thread capture.</returns>
    private static async Task RunOnFrameworkThreadAsync(
        Action capture,
        CancellationToken cancellationToken)
    {
        await Echoglossian.FrameworkInterface.RunOnFrameworkThread(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                capture();
            }).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    ///     Runs managed capture delegates inline for native-free unit tests.
    /// </summary>
    /// <param name="capture">The managed capture action.</param>
    /// <param name="cancellationToken">The token that cancels the resolver operation.</param>
    /// <returns>A completed task after capture.</returns>
    private static Task RunInlineAsync(Action capture, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        capture();
        return Task.CompletedTask;
    }

    private static IReadOnlyList<uint> CollectAcceptedQuestIds()
    {
        return QuestProgressResolver.TryCollectAcceptedQuestIds(out var questIds) ? questIds : [];
    }

    private static byte CaptureQuestSequence(uint questId)
    {
        return QuestManager.GetQuestSequence((ushort)(questId & 0xFFFF));
    }

    private static QuestProgressSnapshot? ResolveQuestProgress(
        uint questId,
        byte questSequence,
        CancellationToken cancellationToken)
    {
        return QuestProgressResolver.TryResolveQuestProgress(
            new QuestProgressCapture(
                questId,
                questSequence,
                Echoglossian.ClientStateInterface.ClientLanguage),
            cancellationToken,
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

    /// <summary>
    ///     Builds a live-actor-only fallback when the visible speaker uniquely
    ///     matches a loaded actor but exact persisted quest metadata is not yet
    ///     available.
    /// </summary>
    /// <param name="visibleSpeakerName">The current visible speaker name.</param>
    /// <param name="liveActors">The copied live actor snapshot.</param>
    /// <param name="localPlayerName">The copied local player name.</param>
    /// <param name="playerSexHint">The copied local player sex hint.</param>
    /// <returns>The live-only fallback metadata; otherwise, <see langword="null" />.</returns>
    private static DialogueInterlocutorMetadata? BuildVisibleSpeakerLiveActorFallback(
        string? visibleSpeakerName,
        IReadOnlyList<LiveDialogueActorSnapshot> liveActors,
        string localPlayerName,
        string? playerSexHint)
    {
        if (string.IsNullOrWhiteSpace(visibleSpeakerName))
        {
            return null;
        }

        var speakerActor = FindUniqueLiveActor(liveActors, visibleSpeakerName);
        if (speakerActor == null)
        {
            return null;
        }

        var speakerIsPlayer = string.Equals(
            speakerActor.Name,
            localPlayerName,
            StringComparison.Ordinal);
        return new DialogueInterlocutorMetadata
        {
            ResolutionTier = DialogueInterlocutorResolutionTier.LiveActorVisibleSpeakerFallback,
            Provenance = "LiveActorFallback",
            ConfidenceTier = 0,
            SpeakerHint = speakerActor.Name,
            AddresseeHint = string.Empty,
            SpeakerRoleHint = speakerIsPlayer ? "player" : "npc",
            AddresseeRoleHint = string.Empty,
            SpeakerGenderHint = speakerActor.GenderHint ?? (speakerIsPlayer ? playerSexHint : null),
            SpeakerRaceHint = speakerActor.RaceHint,
            SpeakerBodyTypeHint = speakerActor.BodyTypeHint,
            AddresseeGenderHint = null,
            AddresseeRaceHint = null,
            AddresseeBodyTypeHint = null,
            SpeakerActor = speakerActor,
            AddresseeActor = null,
        };
    }

    private static string? NormalizeSexHint(string? sex)
    {
        return sex?.ToLowerInvariant() switch
        {
            "male" => "male",
            "0" => "male",
            "female" => "female",
            "1" => "female",
            _ => null,
        };
    }

    /// <summary>
    ///     Carries immutable managed resolver state across the database await boundary.
    /// </summary>
    /// <param name="LiveActors">The copied live actor snapshots.</param>
    /// <param name="LocalPlayerName">The copied local player name.</param>
    /// <param name="PlayerSexHint">The copied local player sex hint.</param>
    /// <param name="AcceptedQuests">The copied accepted quest identifiers and sequences.</param>
    private sealed record ResolverCaptureSnapshot(
        IReadOnlyList<LiveDialogueActorSnapshot> LiveActors,
        string LocalPlayerName,
        string? PlayerSexHint,
        IReadOnlyList<AcceptedQuestCapture> AcceptedQuests);

    /// <summary>
    ///     Carries one accepted quest's native identity and sequence beyond the
    ///     framework-thread capture boundary.
    /// </summary>
    /// <param name="QuestId">The accepted quest identifier.</param>
    /// <param name="QuestSequence">The captured live quest sequence.</param>
    private readonly record struct AcceptedQuestCapture(
        uint QuestId,
        byte QuestSequence);
}
