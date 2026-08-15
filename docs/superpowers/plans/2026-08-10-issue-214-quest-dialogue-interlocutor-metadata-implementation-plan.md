# Issue 214 quest dialogue interlocutor metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the remaining `#214` scope on `issue-214-first-dialogue-speaker-context` / draft PR `#262` by deriving, persisting, and applying quest-scoped interlocutor metadata so first-line `Talk` and `BattleTalk` translations can carry addressee and gender hints without blocking Dalamud callbacks.

**Architecture:** Keep `TranslationService` as the only translation orchestrator and treat interlocutor metadata as an auxiliary enrichment path. Accepted-quest work captures immutable managed quest state on the framework tick, then a background derivation pipeline reads quest DIALOG rows, upserts `QuestDialogueMetadata`, and later `Talk` / `BattleTalk` resolve tiered hints from accepted-quest metadata plus live actor snapshots before building `DialogueTranslationContext`.

**Tech Stack:** C#/.NET 10, EF Core / SQLite migrations, Lumina raw quest sheets, existing `AcceptedQuestPrefetchRuntime`, `TranslationService`, `DialogueTranslationSessionStore`, `OwnedAsyncOperationSet`, xUnit, DalaMock where feasible, and PowerShell.

## Global Constraints

- Stay on the existing `issue-214-first-dialogue-speaker-context` branch and keep the work in draft PR `#262` to `v4-series`.
- Do not split this remaining work into a new issue branch unless the current branch becomes unrecoverably conflicted.
- No provider, database, file, model-list, prompt, glossary, configuration-persistence, or session operation may block a Dalamud framework/ImGui callback.
- Capture native data into immutable managed values before awaits. Never carry Atk pointers, spans, or borrowed buffers across `await`/`Task.Run`.
- Every task must be owned/observed/cancellable, stale results must be rejected, and native mutation must happen only after live pointer re-resolution on the framework thread.
- Reuse `TranslationService`, the existing broker/caches, `SourcePublicationLifecycle`, and DB-first semantics; do not create a second translation pipeline or queue.
- `IPlayerState.Sex` is the primary local-player sex source when the addressee is the player.
- Prefer stronger live actor or NPC evidence when it exists.
- The quest-metadata path stays auxiliary: no confident match means the current dialogue flow continues unchanged.
- Persist the logical identity exactly as `QuestId + QuestSequence + SourceLanguageCode + GameVersion + SourceRowKey + SourceTextHash`, versioned by `DerivationVersion`.
- Upsert matching logical keys instead of creating duplicates.

---

## File map

### New files

- `EFCoreSqlite/Models/Journal/QuestDialogueMetadata.cs` — dedicated persisted entity for derived quest dialogue hints.
- `NativeUI/Helpers/DialogueInterlocutorMetadata.cs` — immutable lookup/hint/snapshot records shared by DB, resolver, and handlers.
- `NativeUI/Helpers/QuestDialogueMetadataDerivation.cs` — quest-sheet DIALOG row acquisition and deterministic metadata derivation.
- `NativeUI/Helpers/DialogueInterlocutorMetadataResolver.cs` — accepted-quest lookup plus live-actor/player hint fusion.
- `Echoglossian.Tests/QuestDialogueMetadataPersistenceTests.cs` — DB upsert and exact-match lookup coverage.
- `Echoglossian.Tests/QuestDialogueMetadataDerivationTests.cs` — deterministic quest-sheet derivation coverage.
- `Echoglossian.Tests/DialogueInterlocutorMetadataResolverTests.cs` — tiered precedence and player/live fusion coverage.

### Modified files

- `Echoglossian.cs` — add `IPlayerState` plugin service access for the live player snapshot path.
- `EFCoreSqlite/EchoglossianDBContext.cs` — table, `DbSet`, and indexes for `QuestDialogueMetadata`.
- `EFCoreSqlite/Migrations/` — generated `AddQuestDialogueMetadata` migration files.
- `DBHelpers/DbOperations.cs` — async exact-match lookup and batch upsert for quest dialogue metadata.
- `NativeUI/Helpers/QuestContentHash.cs` — add a deterministic single-line hash helper reused by derivation and runtime lookup.
- `NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs` — schedule owned background metadata generation from the existing accepted-quest capture flow.
- `NativeUI/Helpers/AddonHandlerWiring.cs` — inject the new async dialogue-hint resolver into `TalkHandler` and `BattleTalkHandler`.
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs` — resolve and apply interlocutor hints before first-line translation.
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs` — same as `TalkHandler`.
- `Translators/DialogueTranslationContext.cs` — extend runtime dialogue context with optional speaker/addressee metadata.
- `Translators/DialogueTranslationSessionStore.cs` — accept resolved hint metadata when building one request context.
- `Translators/StructuredDialogueTranslationMetadata.cs` — add explicit speaker/addressee gender and provenance fields.
- `Translators/Helpers/StructuredDialogueTranslationRequestBuilder.cs` — project the new metadata into the structured contract.
- `Translators/Helpers/DialogueContextPromptHelper.cs` — carry the same hints through plain-text providers and the dialogue cache key.
- `Echoglossian.Tests/StructuredDialogueTranslationRequestBuilderTests.cs` — structured request projection coverage.
- `Echoglossian.Tests/DialogueContextPromptHelperTests.cs` — prompt and cache-key regressions for the new hints.
- `Echoglossian.Tests/AcceptedQuestPrefetchRuntimeContractTests.cs` — background scheduling contract checks.
- `Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs` — first-line handler integration and stale-result rejection tests.

## Interfaces produced and consumed

```csharp
public sealed class QuestDialogueMetadata
{
    public long Id { get; set; }
    public uint QuestId { get; set; }
    public ushort QuestSequence { get; set; }
    public string SourceLanguageCode { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string QuestSheetId { get; set; } = string.Empty;
    public string QuestTextSheetName { get; set; } = string.Empty;
    public string SourceRowKey { get; set; } = string.Empty;
    public string SourceTextHash { get; set; } = string.Empty;
    public string SourceTextPreview { get; set; } = string.Empty;
    public string SpeakerHint { get; set; } = string.Empty;
    public string AddresseeHint { get; set; } = string.Empty;
    public string SpeakerRoleHint { get; set; } = string.Empty;
    public string AddresseeRoleHint { get; set; } = string.Empty;
    public string Provenance { get; set; } = string.Empty;
    public int ConfidenceTier { get; set; }
    public string DerivationVersion { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal readonly record struct QuestDialogueMetadataLookup(
    uint QuestId,
    ushort QuestSequence,
    string SourceLanguageCode,
    string GameVersion,
    string SourceTextHash,
    string DerivationVersion);

internal readonly record struct QuestDialogueSheetEntry(
    string RowKey,
    string Text,
    int SourceOrder);

internal readonly record struct LiveDialogueActorSnapshot(
    string Name,
    uint? NpcId,
    string RoleHint,
    string GenderHint,
    string RaceHint,
    string BodyTypeHint);

internal readonly record struct DialogueInterlocutorHints(
    string SpeakerRoleHint,
    string SpeakerGenderHint,
    string AddresseeHint,
    string AddresseeRoleHint,
    string AddresseeGenderHint,
    string MetadataProvenance,
    int MetadataConfidenceTier);
```

```csharp
public Task<QuestDialogueMetadata?> FindQuestDialogueMetadataAsync(
    QuestDialogueMetadataLookup lookup,
    CancellationToken cancellationToken);

public Task UpsertQuestDialogueMetadataBatchAsync(
    IReadOnlyList<QuestDialogueMetadata> rows,
    CancellationToken cancellationToken);

public static IReadOnlyList<QuestDialogueSheetEntry> ReadDialogueEntries(
    QuestProgressSnapshot questProgressSnapshot);

public static IReadOnlyList<QuestDialogueMetadata> BuildEntries(
    QuestProgressSnapshot questProgressSnapshot,
    IReadOnlyList<QuestDialogueSheetEntry> dialogueEntries,
    string sourceLanguageCode,
    string gameVersion,
    string derivationVersion,
    DateTime observedAtUtc);

public static Task<DialogueInterlocutorHints> ResolveAsync(
    string speakerName,
    string sourceText,
    SourceClientLanguage sourceLanguage,
    Func<QuestDialogueMetadataLookup, CancellationToken, Task<QuestDialogueMetadata?>> findQuestDialogueMetadataAsync,
    CancellationToken cancellationToken);
```

```csharp
public static DialogueTranslationContext BuildContext(
    string sessionNamespace,
    string sessionKey,
    string speakerName,
    string sourceText,
    int historyLimit,
    TimeSpan ttl,
    DialogueInterlocutorHints hints,
    DateTime? observedAtUtc = null);
```

## Task 1: Persist exact-match quest dialogue metadata

- [ ] Add `QuestDialogueMetadataPersistenceTests` that seeds rows differing by `QuestSequence`, `SourceLanguageCode`, `GameVersion`, `SourceRowKey`, `SourceTextHash`, and `DerivationVersion`, then asserts lookup only reuses the exact logical key and upsert replaces older duplicates.
- [ ] Add `QuestDialogueMetadata` under `EFCoreSqlite/Models/Journal` and register `DbSet<QuestDialogueMetadata>` plus a unique lookup index in `EchoglossianDbContext`.
- [ ] Implement `FindQuestDialogueMetadataAsync` and `UpsertQuestDialogueMetadataBatchAsync` in `DbOperations.cs` using `AsNoTracking`, `ToListAsync(cancellationToken)`, and merge-on-logical-key behavior.
- [ ] Generate the `AddQuestDialogueMetadata` migration and stage the generated files from `EFCoreSqlite/Migrations/`.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~QuestDialogueMetadataPersistenceTests"
```

- [ ] Commit:

```powershell
git add -- EFCoreSqlite DBHelpers\DbOperations.cs Echoglossian.Tests\QuestDialogueMetadataPersistenceTests.cs
git commit -m "feat(#214): persist quest dialogue metadata"
```

## Task 2: Derive deterministic metadata from quest DIALOG rows

- [ ] Write `QuestDialogueMetadataDerivationTests` around synthetic row-key sequences such as `_SEQ_00`, `*_NAME_000_000`, `*_000_000`, `*_NAME_000_001`, `*_000_001`; assert speaker pairing uses the matching `*_NAME_*` row, sequence assignment follows the latest `_SEQ_` boundary, adjacent different-speaker turns become weak addressee fallbacks, and `SourceTextHash` is stable.
- [ ] Extend `QuestContentHash` with a deterministic `ComputeLine(string sourceRowKey, string sourceText)` helper that returns the same 16-character lowercase hex fingerprint format already used for quest content hashes.
- [ ] Implement `QuestDialogueMetadataDerivation.ReadDialogueEntries(...)` so quest-sheet acquisition skips `_SEQ_`, `_TODO_`, `_SYSTEM_`, and empty values, while preserving all populated DIALOG / NAME rows in source order.
- [ ] Implement `BuildEntries(...)` with this exact heuristic:
  - use the most recent `_SEQ_NN` row to establish `QuestSequence`;
  - pair `*_NAME_xxx_yyy` rows with text rows sharing the same numeric suffix;
  - use the paired speaker row as `SpeakerHint` and `SpeakerRoleHint = "npc"`;
  - set `AddresseeHint` to the next different named speaker in the same sequence, else the previous different named speaker in the same sequence;
  - set `ConfidenceTier = 2` when both speaker and addressee come from named same-sequence evidence, `1` when only the adjacent-speaker fallback is available, and `0` when no safe addressee hint exists.
- [ ] Keep `SourceRowKey`, `SourceTextHash`, `QuestSheetId`, and `QuestTextSheetName` on every derived row so later runtime reuse never falls back to a quest-global text search.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~QuestDialogueMetadataDerivationTests"
```

- [ ] Commit:

```powershell
git add -- NativeUI\Helpers\QuestDialogueMetadataDerivation.cs NativeUI\Helpers\QuestContentHash.cs Echoglossian.Tests\QuestDialogueMetadataDerivationTests.cs
git commit -m "feat(#214): derive quest dialogue interlocutor metadata"
```

## Task 3: Generate quest dialogue metadata asynchronously from accepted quests

- [ ] Extend `AcceptedQuestPrefetchRuntimeContractTests` with source-level checks proving the framework tick still calls `ScheduleAcceptedQuestPrefetch(...)`, and dialogue metadata generation is started through an owned background operation rather than inline DB or sheet work.
- [ ] Reuse the existing accepted-quest capture path in `AcceptedQuestPrefetchRuntime.cs`: after `TryCaptureAcceptedQuestPrefetchWorkItem(...)` succeeds, start a second owned background operation that calls `ReadDialogueEntries(...)`, `BuildEntries(...)`, and `UpsertQuestDialogueMetadataBatchAsync(...)`.
- [ ] Capture only immutable managed values before the background handoff: `QuestProgressSnapshot`, `SourceClientLanguage`, `GetGameVersion()`, `DerivationVersion`, and the accepted-quest generation number.
- [ ] Reject stale work items by comparing the captured generation to the current accepted-quest generation before upsert, and swallow ordinary cancellation without noisy logs.
- [ ] Do not block or serialize the framework tick on dialogue metadata completion. The accepted-quest scan remains the trigger; the heavy work lives entirely after the capture boundary.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~AcceptedQuestPrefetchRuntimeContractTests"
```

- [ ] Commit:

```powershell
git add -- NativeUI\Helpers\AcceptedQuestPrefetchRuntime.cs Echoglossian.Tests\AcceptedQuestPrefetchRuntimeContractTests.cs
git commit -m "feat(#214): precompute quest dialogue metadata on quest accept"
```

## Task 4: Resolve tiered live/player/quest hints

- [ ] Add `DialogueInterlocutorMetadataResolverTests` covering three exact outcomes:
  - persisted accepted-quest metadata alone yields `QuestSheetDerivedExact`;
  - persisted addressee + matching loaded live actor upgrades to `QuestSheetPlusLiveFusion`;
  - persisted player addressee + `IPlayerState.Sex` yields `AddresseeGenderHint = "male"` or `"female"` without touching quest metadata.
- [ ] Add `[PluginService] public static IPlayerState PlayerStateInterface { get; set; } = null!;` in `Echoglossian.cs`.
- [ ] Create `DialogueInterlocutorMetadata.cs` and `DialogueInterlocutorMetadataResolver.cs`.
- [ ] Implement `ResolveAsync(...)` with this order:
  - resolve accepted quest ids with `QuestProgressResolver.TryCollectAcceptedQuestIds`;
  - for each accepted quest, resolve current `QuestProgressSnapshot` and query `FindQuestDialogueMetadataAsync` by exact `QuestId + QuestSequence + source language + game version + source text hash`;
  - require a unique hit, or a unique hit whose `SpeakerHint` matches the current visible speaker name;
  - if the resolved addressee matches the local player name or `AddresseeRoleHint == "player"`, use `PlayerStateInterface.Sex`;
  - if the resolved speaker/addressee name matches a loaded actor in `ObjectTableInterface`, capture a managed `LiveDialogueActorSnapshot` and prefer its gender/race/body-type hints over persisted unknowns.
- [ ] Keep the native capture boundary strict: copy name, `DataId`, sex, race, and body-type into managed strings/values before the first `await`; do not store `Character*`, `Human*`, `Span<byte>`, or borrowed customize data on the resolver result.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DialogueInterlocutorMetadataResolverTests"
```

- [ ] Commit:

```powershell
git add -- Echoglossian.cs NativeUI\Helpers\DialogueInterlocutorMetadata.cs NativeUI\Helpers\DialogueInterlocutorMetadataResolver.cs Echoglossian.Tests\DialogueInterlocutorMetadataResolverTests.cs
git commit -m "feat(#214): resolve live and quest dialogue hints"
```

## Task 5: Extend dialogue context for both structured and plain-text providers

- [ ] Add `StructuredDialogueTranslationRequestBuilderTests` and `DialogueContextPromptHelperTests` assertions that one context with `SpeakerGenderHint = "female"` and `AddresseeHint = "Alphinaud"` produces both a distinct cache key and explicit metadata lines, while an empty-hint context still serializes cleanly.
- [ ] Extend `DialogueTranslationContext` with optional `SpeakerRoleHint`, `SpeakerGenderHint`, `AddresseeHint`, `AddresseeRoleHint`, `AddresseeGenderHint`, `MetadataProvenance`, and `MetadataConfidenceTier` trailing parameters so current call sites remain source-compatible.
- [ ] Update `DialogueTranslationSessionStore.BuildContext(...)` to accept `DialogueInterlocutorHints` and place those fields only on the returned current-request context; retained prior turns remain unchanged runtime history.
- [ ] Extend `StructuredDialogueTranslationMetadata` and `StructuredDialogueTranslationRequestBuilder.Build(...)` so the structured contract exposes `speaker_gender_hint`, `addressee_original`, `addressee_role_hint`, `addressee_gender_hint`, `metadata_provenance`, and `metadata_confidence_tier`.
- [ ] Update `DialogueContextPromptHelper` so non-structured providers append only non-empty lines in this order: current speaker, speaker role, speaker gender, addressee, addressee role, addressee gender, previous turns. Also include these fields in `BuildDialogueContextCacheKey(...)`.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StructuredDialogueTranslationRequestBuilderTests|FullyQualifiedName~DialogueContextPromptHelperTests"
```

- [ ] Commit:

```powershell
git add -- Translators\DialogueTranslationContext.cs Translators\DialogueTranslationSessionStore.cs Translators\StructuredDialogueTranslationMetadata.cs Translators\Helpers\StructuredDialogueTranslationRequestBuilder.cs Translators\Helpers\DialogueContextPromptHelper.cs Echoglossian.Tests\StructuredDialogueTranslationRequestBuilderTests.cs Echoglossian.Tests\DialogueContextPromptHelperTests.cs
git commit -m "feat(#214): carry interlocutor hints through dialogue contracts"
```

## Task 6: Apply the resolver in Talk and BattleTalk

- [ ] Extend `NativeDialogueHandlerLifecycleTests` with handler-level first-line coverage: inject a fake resolver that returns `AddresseeHint = "Alphinaud"` / `AddresseeGenderHint = "male"` and assert the first `IDialogueContextAwareTranslator` request receives those fields before any prior turns exist.
- [ ] Add stale-result coverage proving a retired source generation cancels the metadata resolver path the same way it already cancels DB lookup and provider work.
- [ ] Add one regression proving DB reuse does not call the hint resolver when an existing translated `TalkMessage` / `BattleTalkMessage` row already satisfies the request.
- [ ] Update `AddonHandlerWiring.cs` so `TalkHandler` and `BattleTalkHandler` receive one additional delegate:

```csharp
Func<string, string, SourceClientLanguage, CancellationToken, Task<DialogueInterlocutorHints>>
```

- [ ] In both handlers, call that delegate only on the fresh-translation path after DB miss and before `DialogueTranslationSessionStore.BuildContext(...)`, then pass the resolved hints into the returned `DialogueTranslationContext`.
- [ ] Keep all resolver, DB, translation, and persistence awaits under the existing `OwnedAsyncOperationSet` / `SourcePublicationLifecycle` ownership checks. If hint resolution finishes after the source retires, reject publication without mutating overlay or native state.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~NativeDialogueHandlerLifecycleTests"
```

- [ ] Commit:

```powershell
git add -- NativeUI\Helpers\AddonHandlerWiring.cs NativeUI\AddonHandlers\Talk\TalkHandler.cs NativeUI\AddonHandlers\Talk\BattleTalkHandler.cs Echoglossian.Tests\NativeDialogueHandlerLifecycleTests.cs
git commit -m "fix(#214): apply quest interlocutor hints to first dialogue lines"
```

## Task 7: Validate #214 end-to-end and update PR 262

- [ ] Run the full validation set required by repo policy:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
git diff --check
```

- [ ] If `Echoglossian.xml` changed as part of the validated code edits, stage and commit it with the owning task. Do not overwrite unrelated local XML edits.
- [ ] In game, verify on a real accepted quest that:
  - quest acceptance schedules metadata generation asynchronously;
  - a first `Talk` or `BattleTalk` line can use addressee metadata with no prior-turn history;
  - a loaded actor match upgrades persisted unknown gender when possible;
  - unrelated non-quest dialogue receives no accidental quest hint;
  - callback responsiveness remains unchanged while DB/provider work is delayed.
- [ ] Capture sanitized evidence for `#214` and update draft PR `#262` so its summary mentions quest-derived interlocutor metadata, `IPlayerState.Sex`, live actor fusion, and the remaining in-game validation results.
- [ ] After the task-by-task review loop passes, request the most-capable whole-branch review before marking the branch ready for merge.
