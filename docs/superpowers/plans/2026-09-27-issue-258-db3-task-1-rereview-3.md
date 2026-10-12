# DB-3 Task 1 fix round 3 re-review

## Scope

Reviewed the original Task 1 review, both prior re-reviews, and only
`.superpowers/sdd/2026-09-27-issue-258-db3-quest-todo-async/review-8d80e20..407415d.diff`.

## Verdict

**Changes required.** The diff makes generation-qualified publication and
terminal cleanup lock-atomic, resolving the prior publication TOCTOU. The
original DB-3 contract is still not met: read and write ownership is split,
legacy persistence parity is not demonstrated, and the terminal-cache path
cannot actually admit a retry after its cooldown.

## Prior findings

### Critical  -  copied policy is not legacy QuestPlate policy: NOT ADDRESSED

This diff does not change selection, merge, timestamp, equality, or parity
tests. The writer-side policy/equality concerns and absence of differential
fallback/merge coverage remain as described in the prior reviews.

### Critical  -  terminal failures, empty reads, and admission rejection have no cooldown: NOT ADDRESSED

The one-second cooldown remains read-only; `TryPersist` is still outside the
runtime registry, so write rejection, failure, cancellation, and other
terminal write outcomes have no same-key terminal state. In addition, the
read-side completion lookup makes the current cooldown ineffective (see the
new finding below). No deterministic terminal-outcome tests were added.

### Critical  -  generation cancellation leaves a stale, unjoinable cache entry: NOT ADDRESSED

The new `Entry.Generation` plus generation-qualified `Publish` and
`RemoveOperation` resolves the earlier non-atomic publish/cleanup race: an old
operation can no longer publish or clean up a new generation after
`AdvanceGeneration`. However, the entry is assigned `this.Generation` inside
`TryRegister`, whereas the writer supplies the separately captured
`operationGeneration` to publish and cleanup. If generation advances between
the writer's capture and registration, registration creates a current-generation
entry while the operation carries the old generation. Its publication and
terminal cleanup are then rejected, leaving an entry with no projection that
future callers join. Registration must be qualified by the same captured
generation (and fail/retry on a mismatch), or generation ownership must be
captured atomically with registration. The required unload/reload race test is
still absent.

### Important  -  write work is outside the single runtime in-flight registry: NOT ADDRESSED

`TryPersist` still uses the coordinator's distinct `quest-plate-write` key and
does not register or join `QuestPlateRuntimeCache`. Reads and writes for one
runtime key can therefore execute independently, and queued/processing/claimed
writes are not represented by the required shared registry.

### Important  -  test suite does not establish the stated contract: NOT ADDRESSED

No test files changed in this package. The required RED-to-GREEN evidence for
legacy parity, terminal cooldowns, backpressure, claimed-write joining,
cancellation/reload, and native/UI isolation is still absent.

### Important  -  expired cooldown attempts throw instead of admitting a retry: ADDRESSED

`TryRegister` now removes an expired entry under the cache lock before adding
the replacement, so the duplicate-key exception identified in re-review one is
removed. That narrow fix is not sufficient to make an ordinary `TryFind` retry
work, as noted below.

### Critical  -  generation guards are non-atomic and can publish stale data: ADDRESSED

`Publish` now checks both cache and entry generation under `gate`, and
`RemoveOperation` does the equivalent check under that lock. The old
`IsCurrentGeneration` pre-check has been removed from the mutation path.

## New Critical/Important findings in this diff

### Important  -  cooled-down read entries are permanently joined, never retried

`RemoveOperation` leaves a terminal no-projection entry in `entries` with a
cooldown. The normal `TryFind` path first calls `TryGetCompletion`; that method
returns the stored completed task without considering `CooldownUntil`. Thus it
joins the old terminal result indefinitely and never reaches `TryRegister`,
where expiry removal was added. A cache miss/empty result therefore does not
retry after the intended cooldown. Make `TryGetCompletion` treat an expired
terminal no-projection entry as absent (removing/replacing it under `gate`), or
centralize expiry ownership in one atomic acquisition method; add a deterministic
clock-based expiry/retry test.

## Positive observations

- Generation validation and the cache mutation now occur under the same lock.
- Old-generation publication and terminal cleanup are rejected after a normal
  generation advance.
