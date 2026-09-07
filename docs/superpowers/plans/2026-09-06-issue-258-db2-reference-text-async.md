# Issue 258 DB-2 ReferenceText Async Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move ReferenceText prefetch persistence out of Framework callbacks while preserving canonical lookup, promotion, and cache semantics.

**Architecture:** The Framework callback captures immutable sheet payloads and submits background work to the existing `PersistenceCoordinator`. A ReferenceText adapter performs the short-lived async lookup and coordinator-backed upsert, publishing the existing cache projection only after successful read or commit. Missing name and description fields are translated as one engine-neutral field batch through `TranslationService`; strict response validation falls back to individual translations when an engine does not preserve the transport envelope. The established translation broker remains the sole translation pipeline.

**Tech Stack:** .NET 10, EF Core SQLite async APIs, existing `PersistenceCoordinator`, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-01-issue-258-async-persistence-and-translation-toggle-design.md`

## Global Constraints

- Preserve database schema, canonical identity, language/version fallback, and DB-first semantics.
- Framework callbacks must not block, construct a context, query EF, save EF changes, or await persistence work.
- Use only the existing coordinator with background priority for bulk ReferenceText prefetch.
- Publish cache values only after an async read completes or an async write commits.
- Field batching must benefit any `ITranslator` that preserves the transport envelope; LLM-specific structured output may optimize the same contract but must not define it.
- A malformed or incomplete batch response must never be partially persisted; retry the missing fields individually through the same translation service.
- Leave `Echoglossian.xml` unstaged unless an intentional generated-doc change is validated.

---

### Task 1: Engine-neutral field translation batch

**Files:**
- Create: `Translators/TranslationField.cs`
- Create: `Translators/TranslationFieldBatchResult.cs`
- Create: `Translators/Helpers/TranslationFieldEnvelopeCodec.cs`
- Modify: `Translators/TranslationService.cs`
- Test: `Echoglossian.Tests/TranslationFieldBatchTests.cs`

- [x] Write failing tests for one-call name/description translation, delimiter-bearing input, reordered or malformed output, strict all-fields validation, and safe individual fallback.
- [x] Run focused tests and confirm failures are caused by the missing field-batch contract.
- [x] Implement the minimal engine-neutral API over the existing translator resolution, using an escaped invariant envelope and individual fallback through the same captured translator.
- [x] Run focused tests, then commit as `perf(#258): batch structured translation fields`.

### Task 2: Coordinator-backed ReferenceText adapter

**Files:**
- Create: `DBHelpers/ReferenceTextPersistenceWriter.cs`
- Modify: `DBHelpers/ReferenceTextPersistenceHelper.cs`
- Test: `Echoglossian.Tests/Persistence/ReferenceTextPersistenceWriterTests.cs`

- [x] Write failing SQLite regression tests for inserting, preserving complete rows, atomically upgrading incomplete rows with a complete translated payload, suppressing unchanged writes, deduplication, capacity rejection, cancellation, restart, and post-commit cache publication.
- [x] Run the focused tests and confirm each new assertion fails for the missing adapter behavior.
- [x] Implement the minimal generic adapter over `IPersistenceCoordinator`, retaining the current query and merge selection semantics with async query materialization and `SaveChangesAsync`.
- [x] Run focused tests, then commit the adapter and tests as `perf(#258): add async reference-text persistence adapter`.

### Task 3: Cache-first ReferenceText prefetch scheduling

**Files:**
- Modify: `NativeUI/Helpers/ReferenceTextPrefetchRuntime.cs`
- Modify: `DBHelpers/ReferenceTextDbOperations.cs`
- Modify: `Echoglossian.cs`
- Test: `Echoglossian.Tests/MainCommandTextPersistenceTests.cs`
- Test: `Echoglossian.Tests/ReferenceTextPrefetchRuntimeTests.cs`

- [x] Write failing contract tests proving the Framework entry only schedules ReferenceText work and that completed/unchanged rows do not start new persistence writes.
- [x] Run focused tests and confirm the callback contract fails against the synchronous path.
- [x] Wire the adapter to the existing process-lifetime coordinator; submit prefetch reads/writes at background priority and keep broker calls off the Framework callback.
- [x] Translate all currently missing fields as one field batch and schedule one complete canonical-row write; never enqueue competing name-only and description-only writes.
- [x] Confirm migrated runtime registrations add no synchronous ReferenceText findings; retain startup preload and setup-helper debt for DB-8/DB-9.
- [x] Run focused tests, commit as `perf(#258): schedule reference-text prefetch asynchronously`, and push the branch.

### Task 4: Validate DB-2

**Files:**
- Modify: `docs/issue-258-sync-db-hotpath-inventory.md`

- [x] Run `dotnet build Echoglossian.sln -c Debug --no-restore`.
- [x] Run `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`.
- [x] Run the Mock build/test commands and `scripts/audit-sync-db-hotpaths.ps1`.
- [x] Produce the Debug DLL and record its absolute path and exact commit for manual in-game testing.

## Execution Notes

- The complete unit/integration suite passed 1,466/1,466 tests after adding
  regressions for active ReferenceText read and commit cancellation during
  plugin unload.
- The Mock/DalaMock suite passed 25/25 tests after restoring its missing assets. Its build completed with zero errors; existing vendor and Multilingual App Toolkit warnings remain.
- The synchronous database audit passed with 225 findings and DB-2 at 10. The baseline was not expanded or regenerated; retained DB-2 findings are startup preload and synchronous setup/helper debt assigned to later lifecycle/enforcement stages.
- Automated coverage drives real SQLite, the production persistence coordinator, shared translation broker, field batching/fallback, post-commit cache publication, and the production-used cursor logic. Live Lumina enumeration, the actual Dalamud Framework subscription, native tooltip lifecycle, and provider-specific envelope preservation remain explicit in-game verification boundaries.
- The final handoff records the absolute Debug DLL path and exact source commit; no schema, release metadata, translation toggle, Talk/BattleTalk, or DB-4+ migration is included.

### Post-validation unload/reload correction

- The first manual Test 5 run froze indefinitely when the plugin was enabled
  immediately after unload. The prior instance stopped cache publication but
  did not cancel accepted ReferenceText SQLite work, so a read or transaction
  could outlive its owner and compete with synchronous startup migration checks.
- The correction preserves the current `IDalamudPlugin` lifecycle and does not
  adopt the DB-8 `IAsyncDalamudPlugin` work early. It gives ReferenceText work an
  owner-lifetime cancellation token and propagates it through active reads and
  the coordinator's complete write transaction, including save and commit.
- The two new real-SQLite regression tests were each observed failing without
  that propagation and passing with it. The full unit suite passed 1,466/1,466,
  Mock/DalaMock passed 25/25, and the synchronous database audit remained at
  225 findings with DB-2 at 10.
- The user repeated Test 5 with the correction while ReferenceText/prefetch
  activity was present and reported no unload/reload errors. The tested
  pre-commit DLL SHA-256 was
  `AE86FFC9FA436611C2492287D108027C03D02001EE1373DF767E187808A84349`.
- This functional validation does not substitute for the controlled DB-2
  before/after performance capture defined in
  `docs/issue-258-async-persistence-baseline.md`. The logs do not contain frame
  percentiles or every required coordinator metric, so those values remain
  explicitly unreported rather than inferred.
