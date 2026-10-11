# DB-3 Task 1 independent review

## Verdict

- **Spec compliance: fail.** The new adapter uses asynchronous EF operations
  through `IPersistenceCoordinator` and clones its input, but it does not meet
  the DB-3 required QuestPlate semantic parity or the required terminal state
  registry/cooldown/generation contract.
- **Implementation quality: changes required.** The foundation is narrow and
  the successful-read/write publication hooks are correctly positioned after
  coordinator completion/commit, but the gaps below make it unsafe to wire into
  the DB-3 runtime.

## Findings

### Critical  -  copied policy is not legacy QuestPlate policy

`QuestPlatePersistencePolicy` is a second, hand-copied policy rather than an
extraction shared with `DbOperations`, contrary to Task 1 Step 3. Its write
selection and merge differ materially from the existing behavior:

- `QuestPlatePersistenceWriter.cs:128-134` fetches a single broad `QuestId OR
  QuestName` candidate set. The legacy save lookup is ordered QuestId, then
  name/message, then name, and applies the canonical/null-QuestId fallback at
  each stage (`DbOperations.cs:2502-2565`). A broad candidate list can choose a
  different duplicate row through scoring.
- `QuestPlatePersistencePolicy.SelectForSave` uses
  `RuntimeLanguageHelper.LanguagesMatch` for source language
  (`QuestPlatePersistenceWriter.cs:202-209`), while legacy save identity uses
  `LegacyWriteSourceLanguagesMatch` (`DbOperations.cs:2673-2683`). The latter
  deliberately recognizes only the four supported persisted display-name
  aliases. This can merge rows that the legacy path keeps separate.
- `Merge` drops the legacy `CreatedDate`, `UpdatedDate`, legacy-projection
  synchronization, and canonical-row pruning behavior
  (`QuestPlatePersistenceWriter.cs:212-229` versus
  `DbOperations.cs:3429-3450` and following). `Equivalent` also ignores fields
  that merge can change, including languages, engine, game version, sheet name,
  and dates (`QuestPlatePersistenceWriter.cs:231-236`). Consequently it can
  return `Unchanged`, skip `SaveChangesAsync`, and publish a projection that was
  never committed.

Extract the actual legacy helpers into a common pure policy (including source
language, candidate order, merge, pruning, and equality semantics), make both
paths call it, and add differential tests covering each ordered fallback and
merge edge.

### Critical  -  terminal failures, empty reads, and admission rejection have no cooldown

The DB-3 global constraints require a same-key registry to retain terminal
cooldown state for rejection, failure, and empty results. Instead,
`TryFind` removes every no-projection result immediately
(`QuestPlatePersistenceWriter.cs:94-103`), and `RemoveOperation` deletes the
entry (`QuestPlateRuntimeCache.cs:120-131`). A coordinator capacity rejection
returns an already-completed rejected task, so the continuation deletes the
entry immediately; the next frame/tick can enqueue again. A successful empty
lookup behaves the same way. No cache type stores a terminal timestamp or
status, and `TryPersist` has no registry ownership at all.

Keep the canonical key through a bounded, observable cooldown for rejected,
failed, cancelled-as-required, and empty outcomes; only permit a new operation
after expiry. Add deterministic clock-based tests for each terminal outcome and
backpressure rejection.

### Critical  -  generation cancellation leaves a stale, unjoinable cache entry

`DisablePublication` cancels the writer and suppresses publication
(`QuestPlatePersistenceWriter.cs:35-38, 85-90`), but neither it nor the cache
clears or versions entries. If a read wins the race with cancellation, its
completion is `Succeeded` with a non-null projection; the continuation therefore
does not call `RemoveOperation` (`QuestPlatePersistenceWriter.cs:94-103`), while
publication has been suppressed. The cache retains an entry with no projection
and a completed old-generation task. A replacement generation sharing the cache
cannot register the key (`QuestPlateRuntimeCache.cs:66-78`) and joins that stale
task (`QuestPlatePersistenceWriter.cs:81-97`) instead of starting cleanly.

Introduce cache generation ownership/clear-on-dispose and ensure a cancelled or
superseded generation cannot publish or block the replacement generation. Add a
race test where read completion occurs after `DisablePublication`, followed by
unload/reload with the same key.

### Important  -  write work is outside the single runtime in-flight registry

`TryPersist` only calls the coordinator using the separate
`quest-plate-write` work key (`QuestPlatePersistenceWriter.cs:117-158`); it
never registers in `QuestPlateRuntimeCache`. A simultaneous `TryFind` for the
same `QuestPlateRuntimeKey` can therefore schedule a read while a write is
queued, processing, or coordinator-claimed. Conversely, same-key writes rely on
the coordinator's replacement semantics rather than joining a registry task,
and a claimed write cannot be replaced. This violates the Task 1 interface that
the single registry own queued, processing, and claimed same-key work.

Unify read/write operation ownership under the canonical runtime key, with a
completion that represents the final committed projection, and test queued,
processing, and claimed-write joins.

### Important  -  test suite does not establish the stated contract

The three tests only prove one read hit, direct cache registration, and one
write merge. They do not test legacy fallback/merge parity, source hash/version
reuse, unchanged-update suppression, coordinator rejection, failure/empty
cooldown, cancellation/generation replacement, same-key work after coordinator
claim, or worker-side native/Lumina/UI isolation. This falls short of Task 1
Step 1's required RED coverage. The report's statement that the policy preserves
the full legacy order is not supported by a differential regression suite.

### Minor  -  API documentation claims immutable payloads without enforcing a projection type

The cache exposes and returns mutable `QuestPlate` entities (albeit cloned)
(`QuestPlateRuntimeCache.cs:46-60, 100-117`). This is workable with disciplined
callers, but it does not constitute an immutable projection API as specified.
Consider a dedicated immutable managed snapshot/record, or make mutation
boundaries explicit and ensure UI consumers never mutate cache-derived data.

## Verified positive properties

- The read path uses `AsNoTracking` and `ToListAsync`; the write path uses
  asynchronous EF materialization and the coordinator transaction.
- Read publication is invoked by the coordinator after the read operation, and
  write publication is invoked through `PublishAfterCommit`; successful normal
  paths therefore do not publish before the corresponding operation completes.
- The worker closures are given cloned managed `QuestPlate` data and contain no
  direct Lumina, addon, pointer, clipboard, or UI access.

These positives do not resolve the Critical findings above.
