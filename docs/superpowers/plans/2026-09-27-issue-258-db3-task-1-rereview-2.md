# DB-3 Task 1 fix round 2 re-review

## Scope

Reviewed the original Task 1 contract, the two prior reviews, and only
`.superpowers/sdd/2026-09-27-issue-258-db3-quest-todo-async/review-659ce19..8d80e20.diff`.

## Verdict

**Changes required.** The diff fixes the duplicate-key exception on retry after
cooldown expiry and adds a best-effort generation check. It does not implement
the required unified read/write registry or the required test contract. The
generation check also has a time-of-check/time-of-use race, so it cannot prove
that an old generation cannot publish into a replacement generation.

## Prior findings

### Critical  -  copied policy is not legacy QuestPlate policy: NOT ADDRESSED

The expanded `Equivalent` comparison covers several previously omitted
serialized fields, but the diff still leaves the writer-side policy separate
from the legacy entry point and does not cover legacy behavior with differential
tests. In particular, the shared `MergeQuestPlateValues` assigns
`UpdatedDate = DateTime.Now`; `Equivalent` does not compare that value, so an
otherwise unchanged merge can be reported `Unchanged` and its uncommitted
projection can be published. The required extraction/consumption of one pure
candidate-selection, merge, and identity policy, plus fallback/merge parity
tests, remains absent.

### Critical  -  terminal failures, empty reads, and admission rejection have no cooldown: NOT ADDRESSED

Read-side no-projection completion still receives the one-second cache cooldown,
but `TryPersist` remains outside `QuestPlateRuntimeCache`. Write rejection,
failure, cancellation, and any empty/terminal result therefore have no shared
same-key cooldown or observable terminal state. No deterministic tests cover
rejection, failure, or empty-result cooldown behavior.

### Critical  -  generation cancellation leaves stale, unjoinable cache entry: NOT ADDRESSED

`AdvanceGeneration` clears entries and the continuation now returns when its
captured generation is no longer current, which prevents the specific old
continuation cleanup from touching an already-replaced entry. However, generation
validation is separate from cache mutation. An old read/write can pass
`IsCurrentGeneration`, then `AdvanceGeneration` can clear the registry, and the
old `Publish` can create a projection in the new generation. Further, a joined
reader owns `gate.Task`; after generation advance its continuation returns before
completing that gate, leaving that joined completion permanently non-terminal.
Generation must be carried by entries and checked under the same cache lock as
publish/terminal transition (or be represented by a generation-bound cache
instance). Add the cancellation/read-race/reload same-key test.

### Important  -  write work is outside the single runtime in-flight registry: NOT ADDRESSED

`TryPersist` still schedules exclusively under the coordinator key
`quest-plate-write` and neither registers nor joins a
`QuestPlateRuntimeCache` operation. Same-key reads and writes can consequently
run independently, and a caller cannot join queued, processing, or claimed
write work through the required one registry.

### Important  -  test suite does not establish the stated contract: NOT ADDRESSED

This diff changes no tests. `QuestPlatePersistenceWriterTests` still contains
only three tests: read publication, direct cache registration, and one write
merge. It does not establish RED-to-GREEN coverage for fallback/merge parity,
same-key claimed-write joins, rejection/failure/empty cooldowns, generation
races, cancellation/reload, unchanged suppression, or worker native/UI
isolation.

### Important  -  expired cooldown attempts throw instead of admitting a retry: ADDRESSED

`TryRegister` now removes an expired entry while holding `gate` before adding
the new owner. This fixes the duplicate-key exception reported in round one.
The change still needs a deterministic regression test, but the code defect in
that finding is corrected.

## Fresh Critical/Important findings

### Critical  -  generation guards are non-atomic and can publish stale data

`IsCurrentGeneration` is a volatile read outside the `QuestPlateRuntimeCache`
lock, while `Publish` can add an entry when none exists. The interleaving
`old IsCurrentGeneration == true -> AdvanceGeneration clears -> old Publish`
publishes old data into the new generation. The equivalent cleanup interleaving
can also alter a replacement entry. Make generation ownership part of `Entry`
and expose lock-atomic generation-qualified publish/complete APIs; do not use a
pre-mutation boolean check.

## Positive observations

- Retry after an expired cooldown is now atomically admitted rather than
  throwing a duplicate dictionary-key exception.
- The added generation checks are directionally correct and reduce ordinary
  stale-completion exposure, but they are insufficient as synchronization.
