# Issue 258 DB-4 checks

Profile: light. Source: `.tasks/issue-258-db4-action-item-trait-async.md` (all 23 criteria binding), master design, inventory, baseline, and execution ledger referenced there. No schema or public contract changes. Existing sweep and out-of-scope classifications from the task apply unchanged.

## Canonical persistence (reading floor ~35k tokens)

- C1 cache hit returns a detached managed copy without admission. Proof: `ActionItemTooltipPersistenceWriterTests.CacheHitDoesNotAdmit`.
- C2 miss returns before SQLite completion. Proof: `ActionItemTooltipPersistenceWriterTests.ReadCoalescesAndDoesNotPublishBeforeCompletion`.
- C3 identity, reuse scope and historical selection remain compatible. Proof: existing `ActionItemTooltipPersistenceTests`, cache tests, and `ActionItemTooltipPersistenceWriterTests.HistoricalPromotionPreservesIdentity`. Proper nouns: `WorkerPreservesSourceEquivalentNameWithTranslatedDescription`, `WorkerCachedSourceEquivalentNameWithTranslatedDescriptionSkipsWork`, and `WorkerSourceEquivalentNameWithoutDescriptionRemainsRejected` cover all three domains.
- C4 publish only successful non-null reads. Proof: `ActionItemTooltipPersistenceWriterTests.ReadMissDoesNotPublish`.
- C5 unchanged writes emit no UPDATE; publish only committed changes. Proof: `ActionItemTooltipPersistenceWriterTests.UnchangedWritePreservesTimestamp`, `PublishesOnlyAfterCommit`.
- C6 duplicate work joins existing ownership. Proof: `ReadCoalescesAndDoesNotPublishBeforeCompletion`, coordinator tests, runtime ownership review.
- C7 rejection, cancellation and unload never fall back synchronously or publish stale data. Proof: `RejectsAfterShutdown`, `UnloadSuppressesPublication`, coordinator capacity/cancellation tests and runtime contract tests.

## Action/Trait prefetch (~25k tokens)

- C8/C9 6 captures per existing 2-second interval; Background persistence. Proof: runtime contract tests and focused prefetch tests.
- C10 Framework capture includes source, target, engine, hash, version; workers never read native/Lumina state. Proof: runtime contract tests and independent callback review.
- C11 ownership survives translation and write completion, terminal failure suppresses retry. Proof: runtime operation tests and independent review.

## Item prefetch (~20k tokens)

- C12 10 captures per 2 seconds. Proof: runtime contract tests.
- C13 preserve item families, HQ, inventory normalization and broker. Proof: existing structured tooltip payload tests and independent diff review.
- C14 errors preserve original and terminate ownership. Proof: runtime operation tests and translation validation tests.

## Visible tooltip (~30k tokens)

- C15 hits apply in same Framework pass with existing display modes. Proof: existing structured tooltip display tests, independent diff review; native application additionally needs in-game testing.
- C16 misses retain original, Interactive lane, 30-second cooldown. Proof: runtime contract tests and cache-first lookup review.
- C17 workers publish managed data; next Framework pass applies. Proof: runtime contract tests and callback review; native integration gap documented in ledger.
- C18 provider error/empty/unusable output never displayed or persisted. Proof: existing `StructuredTooltipTranslationValidationTests` and `WorkerUnusableResponseKeepsOriginalAndTerminatesOwnership` (six domain/error cases, zero generic broker error logs).
- C19 disabled config gates preserve UI ownership. Proof: runtime gate tests and existing display ownership tests.

## Compatibility and delivery

- C20 schema, migration, keys unchanged; no migrated sync callers. Proof: git diff path review and runtime contract tests.
- C21 audit findings classified as DB-4, DB-8 startup, or non-DB. Proof: `scripts/audit-sync-db-hotpaths.ps1` and ledger classification.
- C22 full build/unit/Mock commands from AGENTS; real SQLite proofs above. Proof: commands recorded in ledger. Mock startup is not native payload proof.
- C23 pushed execution branch and PR to v4-series, exact DLL/commit/SHA256. Proof: git remote, PR metadata, `Get-FileHash`; no merge/release.

## Landing

None: explicit writer follows existing DB-2/DB-3 patterns and preserves schema; no one-way doors.

## Handoff

Four slices share canonical helpers (~110k reading floor total); build in this agent, then fresh independent verifier after final slice. No mid-slice handoff planned.
