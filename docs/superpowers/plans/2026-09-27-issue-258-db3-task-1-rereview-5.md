# DB-3 Task 1 fix round 5 re-review

## Scope

Reviewed the Task 1 requirements, re-review 4, and only the review package
`.superpowers/sdd/2026-09-27-issue-258-db3-quest-todo-async/review-c028865..5599c11.diff`.

## Verdict

**Changes required.** The package addresses the prior critical loss of a
same-key write that arrived during a read: it reserves a deferred writer,
promotes it after the read terminal result, and exercises an SQLite
read-then-write case. It also restores the legacy always-commit timestamp
behavior for merge writes. However, the deferred writer is not protected from
the read entry's cooldown expiry, so its admitted mutation can still be
discarded before it reaches the coordinator.

## Prior Critical/Important findings

### Critical  -  copied policy is not legacy QuestPlate policy: ADDRESSED in behavior; evidence remains incomplete

The writer now applies `UpdatedDate` and returns `ChangedResult` for an
existing merge, matching the legacy `InsertQuestPlate`/`UpdateQuestPlate`
write behavior. The new SQLite regression establishes commit-before-cache for
the timestamp. The requested differential fallback/merge test matrix remains
absent; this is retained under the prior incomplete-test evidence finding,
not a new policy defect in this package.

### Critical  -  terminal failures, empty reads, and admission rejection have no cooldown: ADDRESSED

No regression is introduced by this package.

### Critical  -  generation cancellation leaves a stale, unjoinable cache entry: ADDRESSED

No regression is introduced by this package.

### Important  -  write work is outside the single runtime in-flight registry: ADDRESSED

The deferred writer remains owned by the runtime registry until promotion, and
normal writes continue to enter the same registry first.

### Important  -  test suite does not establish the stated contract: NOT ADDRESSED

The two added tests are useful but do not supply the required rejected,
failure, cancellation, ordered fallback/merge differential, source-hash and
cross-version reuse, unchanged-write suppression, claimed-operation, or
worker native/UI-isolation evidence. They also do not force a queued,
processing, and coordinator-claimed read before the write.

### Important  -  expired cooldown attempts throw instead of admitting a retry: ADDRESSED

No regression is introduced for ordinary terminal cooldown retry.

### Critical  -  generation guards are non-atomic and can publish stale data: ADDRESSED

No regression is introduced by this package.

### Critical  -  a write that joins an in-flight read is silently lost: PARTIALLY ADDRESSED

The immediate read-then-write path is now serialized and covered. The new
cooldown-expiry race below still allows that admitted deferred write to be
lost, so this prior finding cannot be considered fully closed.

## New Critical/Important findings

### Critical  -  cooldown expiry can evict an admitted deferred write before promotion

`TryRegisterWriteOrJoin` reserves `DeferredWriteCompletion` on the current
read entry but leaves that entry's terminal read cooldown active
(`QuestPlateRuntimeCache.cs:166-176`). After the read completes with no
projection, `Complete` sets `CooldownUntil` (`QuestPlateRuntimeCache.cs:306-314`)
before `ScheduleDeferredPersistAsync` wakes and calls `TryPromoteDeferredWrite`
(`QuestPlatePersistenceWriter.cs:153-160`). If the continuation is delayed
beyond the one-second cooldown, any later `TryFind` or `TryPersist` removes the
entry as expired (`QuestPlateRuntimeCache.cs:120-123,149-153`). Promotion then
fails and the original admitted write completes `Cancelled`, without ever
reaching the coordinator.

This violates the required queued/processing/in-flight ownership and loses a
translation persistence request. A deferred-write reservation must be an
in-flight state that blocks cooldown eviction until its terminal coordinator
completion (or explicit generation cancellation). Add a deterministic clock
test which completes the read, advances past cooldown before promotion, admits
a competing lookup/write, and proves the original write still commits exactly
once.
