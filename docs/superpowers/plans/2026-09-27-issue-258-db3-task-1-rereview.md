# DB-3 Task 1 fix round 1 re-review

## Scope

Reviewed only the original Task 1 requirements/report/review and
`.superpowers/sdd/2026-09-27-issue-258-db3-quest-todo-async/review-d29efee..659ce19.diff`.

## Verdict

**Changes required.** The patch makes useful partial progress on shared policy,
read-side terminal cooldown, and cache invalidation. It does not satisfy the
original Task 1 registry and test contract, and it introduces an expired-cooldown
retry exception.

## Original findings

### Critical  -  copied policy is not legacy QuestPlate policy: NOT ADDRESSED

`Echoglossian.SelectPreferredQuestPlate`,
`SelectPreferredQuestPlateForSave`, and `MergeQuestPlateValues` are now reused,
which addresses the hand-copied selector/merge portion. But the async writer
still does not perform the legacy write's complete behavior: it does not set
`UpdatedDate` before persisting, and the unchanged `Equivalent` comparison still
omits values the shared merge can alter (including language, engine, game
version, sheet name, and dates). This can still incorrectly suppress a commit
and publish a projection that was not committed. No differential fallback/merge
tests were added.

### Critical  -  terminal failures, empty reads, and admission rejection have no cooldown: NOT ADDRESSED

`RemoveOperation` now assigns a one-second cooldown for an existing read-side
no-projection entry, which is partial progress. However, write work remains
outside the registry, so write rejection/failure/empty terminal states have no
registry cooldown at all. The package adds no deterministic clock-based tests
for rejected, failed, or empty outcomes; it also does not establish the required
bounded observable terminal-state behavior for all operation types.

### Critical  -  generation cancellation leaves a stale, unjoinable cache entry: NOT ADDRESSED

`DisablePublication` now calls `AdvanceGeneration`, clearing entries and
preventing the common pre-existing stale-entry case. The entries themselves do
not carry generation ownership, though, and no captured generation guards a
late old-generation completion/cleanup. A pre-cancellation operation can still
run its completion path against entries created by the replacement generation.
The required cancellation/read-race/reload same-key test is also still absent.

### Important  -  write work is outside the single runtime in-flight registry: NOT ADDRESSED

No change registers `TryPersist` under `QuestPlateRuntimeKey`. It still uses
only the separate `quest-plate-write` coordinator key. Consequently reads do
not join queued/processing/claimed writes, writes do not join the cache-owned
completion, and coordinator claim state is not represented by the one required
runtime registry.

### Important  -  test suite does not establish the stated contract: NOT ADDRESSED

The package changes no tests. The report still records only three focused tests;
there is no RED/GREEN evidence for legacy parity, terminal-state cooldown,
generation races, coordinator rejection, claimed-write joining, or native/UI
isolation.

## New Critical/Important findings in this fix diff

### Important  -  expired cooldown attempts throw instead of admitting a retry

`QuestPlateRuntimeCache.TryRegister` no longer returns `false` when an existing
entry's cooldown has expired, but then calls `this.entries.Add(key, new
Entry(completion))` without removing or replacing that existing entry. Therefore
the first retry after any terminal cooldown throws `ArgumentException` for a
duplicate dictionary key instead of becoming the new owner. Replace the expired
entry atomically (or remove it before adding) under the same lock, and add a
deterministic expiry/retry regression test.

## Positive observations

- The async read uses the existing read selector; the async write uses the
  existing save selector and merge routine.
- Cache invalidation is now requested when the writer disables publication.
- Read-side no-projection completion now retains a bounded cooldown rather than
  immediately deleting the entry.
