# DB-3 Task 1 fix round 4 re-review

## Scope

Reviewed the Task 1 plan and all prior Task 1 reviews, restricted to
`.superpowers/sdd/2026-09-27-issue-258-db3-quest-todo-async/review-407415d..c028865.diff`.

## Verdict

**Changes required.** The replacement correctly makes cache admission capture
the generation under the registry lock, makes completion generation-qualified
under that lock, expires a terminal entry during both join and completion
lookup, and brings normal writes under the cache registry. It does not yet
preserve all legacy write semantics or establish the required test contract.
It also makes a concurrent required write disappear when it joins an existing
read.

## Prior Critical/Important findings

### Critical  -  copied policy is not legacy QuestPlate policy: NOT ADDRESSED

The new read fallback delegates selection to the legacy selector and the write
path delegates merge/selection to legacy helpers, which is useful progress.
However, `MergeQuestPlateValues` always assigns `UpdatedDate`, while
`Equivalent` still omits both dates (`DBHelpers/QuestPlatePersistenceWriter.cs:149-153,266-273`). An otherwise identical merge is consequently marked
`Unchanged`, skips `SaveChangesAsync`, and the cache receives the modified,
uncommitted timestamp. The required differential fallback/merge regressions
are still absent, so semantic parity is not demonstrated.

### Critical  -  terminal failures, empty reads, and admission rejection have no cooldown: ADDRESSED

Both reads and writes now enter `TryRegisterOrJoin`, and their terminal
continuations call `Complete`, which retains a cooldown for every non-projection
result (`QuestPlatePersistenceWriter.cs:66-103,121-175`; `QuestPlateRuntimeCache.cs:108-137,232-257`).

### Critical  -  generation cancellation leaves a stale, unjoinable cache entry: ADDRESSED

Admission captures the entry generation in the same lock that inserts the
entry. `Complete` validates cache and entry generation in that same lock, and
`AdvanceGeneration` clears old entries. An old completion therefore cannot
publish into or clean up a replacement entry (`QuestPlateRuntimeCache.cs:35-41,108-137,232-257`).

### Important  -  write work is outside the single runtime in-flight registry: ADDRESSED

`TryPersist` now uses the same `QuestPlateRuntimeKey` registry before it asks
the coordinator to schedule work (`QuestPlatePersistenceWriter.cs:116-175`).

### Important  -  test suite does not establish the stated contract: NOT ADDRESSED

The six focused tests add an empty-result expiry and a simple replacement
generation check, but do not cover coordinator rejection/failure/cancellation
cooldowns, legacy ordered fallback and merge differentials, source-hash/version
reuse, unchanged-write suppression, read/write claimed-operation behavior,
or worker native/UI isolation. They also do not make a test RED before the
implementation. Task 1's required evidence remains incomplete.

### Important  -  expired cooldown attempts throw instead of admitting a retry: ADDRESSED

Both `TryRegisterOrJoin` and `TryGetCompletion` remove an expired no-projection
entry under `gate`; the new deterministic test exercises the completion-lookup
route (`QuestPlateRuntimeCache.cs:108-137,181-203`; `QuestPlatePersistenceWriterTests.cs:82-103`).

### Critical  -  generation guards are non-atomic and can publish stale data: ADDRESSED

The old pre-mutation generation check is no longer relied upon by the writer.
`Complete` owns the lock-atomic generation and entry validation.

## New Critical/Important findings

### Critical  -  a write that joins an in-flight read is silently lost

When `TryPersist` finds an existing cache owner, it maps that owner's terminal
result to `PersistenceWriteResult` and returns without scheduling or arranging
the requested mutation (`DBHelpers/QuestPlatePersistenceWriter.cs:121-129`).
Thus the interleaving `TryFind(key)` -> read finds no row -> `TryPersist(key)`
reports `Joined`/`Succeeded` but never inserts or updates the translated
QuestPlate. A shared registry must coalesce the operation without dropping the
stronger write intent: promote/chain the upsert after a missing read terminal
state (and join it), or otherwise make the owned operation represent both
requested intents. Add tests for queued, processing, and coordinator-claimed
read followed by a same-key write, proving the write commits and publishes only
  after that commit.
