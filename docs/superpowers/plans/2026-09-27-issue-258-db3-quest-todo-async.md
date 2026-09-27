# Issue 258 DB-3 Quest and ToDo Async Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move QuestPlate, accepted-quest prefetch, and ToDoList runtime persistence off game-thread callbacks while retaining existing database-first lookup and translation behavior.

**Architecture:** Game-thread code captures only immutable quest payloads, resolves committed cache entries, and schedules non-blocking work. A QuestPlate persistence adapter uses the existing `PersistenceCoordinator` with short-lived async contexts; it publishes projections only after a completed read or committed write. Existing `QueuedTranslationBroker`, `TranslationService`, and quest caches remain the only translation and cache paths.

**Tech Stack:** .NET 10, EF Core SQLite async APIs, existing `PersistenceCoordinator`, `QueuedTranslationBroker`, xUnit, DalaMock where it can drive the lifecycle.

**Spec:** `docs/superpowers/specs/2026-09-01-issue-258-async-persistence-and-translation-toggle-design.md`

## Global Constraints

- Begin from `77949329c2671f353585763159bcec4d433ef983`; preserve schema, persistence compatibility, canonical QuestPlate identity, lookup order, source-hash and cross-version reuse.
- Framework tick, addon lifecycle, Draw, and PreDraw only capture native state on the game thread, read immutable snapshots/caches, or call non-blocking scheduling APIs.
- Do not create a translation queue, cache, or persistence pipeline; use `PersistenceCoordinator`, existing quest caches, `TranslationService`, and `QueuedTranslationBroker` only.
- No `FindQuestPlate`, `FindQuestPlateByName`, synchronous EF/SQLite query/save, or native pointer crosses from a callback to a background thread.
- Retain queued and processing/in-flight deduplication until a terminal result or cooldown. Rejection, empty result, and failure receive a bounded cooldown rather than frame/tick retry.
- Visible TODO objectives are interactive work and precede summary/system text; background accepted-quest prefetch must honor existing bounded admission.
- Cache entries are projections: publish only after an async read succeeds or a coordinator-backed transaction commits. The database remains authoritative.
- Introduce one `QuestPlateRuntimeCache` as the domain's shared projection and state registry, not as a second queue: its collision-safe canonical key owns a committed immutable projection, scheduled/processing/in-flight task, terminal cooldown, and generation. It releases the key only after an observed terminal completion or an explicit cooldown expiry.
- The registry must join a same-key request even after the coordinator writer has claimed it. It delegates all actual I/O/admission to `PersistenceCoordinator`; it may not hold work items, run EF, or become an alternative persistence path.
- Extract the existing QuestPlate candidate-selection, merge, and identity policy into pure helpers consumed by the legacy test/design-time entry points and the async adapter. Background code must not call Lumina, native UI, ImGui clipboard, addon, pointer, or game-thread APIs.
- Do not add a migration/schema change. If one becomes necessary, stop for a separate decision.
- Do not change global translation toggle, DTR, broker DB-4 work, release metadata, tags, manifests, or publishing.
- Leave `Echoglossian.xml` unstaged unless a deliberately generated and validated source change requires it.

## Review Focus

- A concurrent same-key visible request must join the existing queued or in-flight operation, including after dequeue, rather than creating duplicate EF work.
- A rejected coordinator admission, failed lookup/write, or empty translation must preserve original text and prevent retry on every frame until cooldown expires.
- Cancellation during unload must publish neither stale cache nor stale generation state; a replacement generation must start cleanly.
- Cache publication must be causally after the query or commit, and unchanged rows must not issue updates.
- Every background payload must be immutable managed data captured before scheduling; no `AtkValue`, addon, pointer, or Lumina/native object is dereferenced off the game thread.

---

### Task 1: Async QuestPlate persistence projection

**Files:**
- Create: `DBHelpers/QuestPlatePersistenceWriter.cs`
- Create: `NativeUI/Helpers/QuestPlateRuntimeCache.cs`
- Modify: `DBHelpers/DbOperations.cs`
- Modify: existing QuestPlate cache/projection helper(s) discovered in the current runtime
- Test: `Echoglossian.Tests/Persistence/QuestPlatePersistenceWriterTests.cs`
- Test: existing QuestPlate lookup regression tests

**Interfaces:**
- Consumes: `IPersistenceCoordinator`, existing `TranslationReuseScope`, current QuestPlate selection/merge semantics, and immutable formatted QuestPlate data.
- Produces: `QuestPlateRuntimeCache` collision-safe immutable projection/state API and a non-blocking cache-first scheduling API. The registry joins queued, processing, and claimed coordinator work for one canonical key; coordinator work completes with an immutable projection or terminal result.

- [ ] **Step 1: Write failing pure-policy and real-SQLite tests** for cache-hit/miss lookup scheduling, exact existing QuestId/message/name fallback order, same-key queued, processing, and claimed-operation coalescing, bounded admission rejection, cancellation, source-hash/version reuse, unchanged merge suppression, and read/commit-before-cache publication. Assert that no worker path invokes Lumina/native/UI/clipboard behavior.
- [ ] **Step 2: Run the focused test class serially and verify RED** because no asynchronous QuestPlate adapter/projection contract exists.
- [ ] **Step 3: Extract pure QuestPlate lookup/merge/identity policy and implement `QuestPlatePersistenceWriter` plus `QuestPlateRuntimeCache`** over the existing coordinator. Use short-lived contexts and EF async materialization/save only; preserve selection and merge behavior, disable clipboard/UI side effects on the worker path, publish immutable cache projections after success/commit, and retain terminal cooldown state without adding schema.
- [ ] **Step 4: Replace every callback-reachable QuestPlate find/insert/update in accepted-prefetch and ToDoList paths with cache-first dependencies.** Retain legacy/test/design-time APIs only where they are demonstrably outside DB-3 runtime callbacks and audit-allowed; preserve static broker/source-scope test contracts or migrate them explicitly to the new dependency contract.
- [ ] **Step 5: Run focused persistence and lookup regressions serially, then commit** as `perf(#258): add async QuestPlate persistence`.

### Task 2: Accepted-quest prefetch scheduling and lifecycle

**Files:**
- Modify: `NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs`
- Modify: `NativeUI/Helpers/AcceptedQuestPrefetchRequestQueue.cs`
- Modify: `Echoglossian.cs` only for existing runtime registration/lifetime ownership if needed
- Test: `Echoglossian.Tests/AcceptedQuestPrefetchRuntimeTests.cs`
- Test: `Echoglossian.Tests/AcceptedQuestPrefetchRequestQueueTests.cs`

**Interfaces:**
- Consumes: Task 1 cache-first QuestPlate API and process-lifetime `PersistenceCoordinator`.
- Produces: a framework-safe request queue that retains a canonical key through queued/processing/in-flight work and records terminal cooldowns.

- [ ] **Step 1: Write failing tests** that a Framework/addon callback schedules immutable requests without EF/SQLite, deduplicates the same canonical key while queued and after dequeue, honors backpressure/rejection, prioritizes visible TODO objectives over summary/system candidates, and never sends native pointers across threads.
- [ ] **Step 2: Add RED tests for failure, empty translation, cancellation, unload/reload/restart generation replacement, and no stale publication after cancellation.**
- [ ] **Step 3: Run the focused tests serially and verify RED** against the current synchronous/dequeue-removal behavior.
- [ ] **Step 4: Implement the smallest queue/runtime adaptation** that captures managed snapshots on the game thread, schedules coordinator-backed lookup/persistence and existing broker work without awaiting, retains dedupe ownership through terminal completion/cooldown, and uses the existing lifetime cancellation ownership. Replace all `FindQuestPlate`, `FindQuestPlateByName`, `InsertQuestPlate`, and `UpdateQuestPlate` callbacks passed through the prefetch operation entry.
- [ ] **Step 5: Run focused tests and commit** as `perf(#258): schedule accepted quest prefetch asynchronously`.

### Task 3: ToDoList cache-first handler migration

**Files:**
- Modify: `NativeUI/AddonHandlers/Quest/ToDoListHandler.cs` and only directly required QuestPlate call sites
- Test: `Echoglossian.Tests/ToDoListHandlerTests.cs`
- Test: existing relevant handler/QuestPlate tests

**Interfaces:**
- Consumes: Task 1 committed QuestPlate projections and Task 2 non-blocking scheduling/deduplication.
- Produces: immediate cache-hit rendering and cache-miss original-text rendering with interactive TODO scheduling only.

- [ ] **Step 1: Write failing handler tests** proving Draw/PreDraw/lifecycle paths make no synchronous persistence call, cache hits preserve existing translated output, misses schedule once and return original text, visible TODO work is interactive, and failure/empty/rejection observes cooldown.
- [ ] **Step 2: Run focused tests serially and verify RED** because current handler directly queries QuestPlate or removes dedupe too early.
- [ ] **Step 3: Replace direct QuestPlate lookup/save calls in the ToDoList path with existing cache projection and non-blocking request scheduling.** Do not alter overlay/native mutation semantics or unrelated quest handlers.
- [ ] **Step 4: Run focused handler tests and the established lookup suite; commit** as `perf(#258): make ToDoList QuestPlate cache-first`.

### Task 4: DB-3 audit, integration validation, and handoff build

**Files:**
- Modify: `docs/issue-258-sync-db-hotpath-inventory.md` only if the audit proves specific DB-3 runtime findings were removed
- Test: `Echoglossian.Mock.Tests` lifecycle tests or a narrowly added DalaMock fixture when feasible

- [ ] **Step 1: Run `scripts/audit-sync-db-hotpaths.ps1` and update the baseline only for DB-3 findings demonstrably removed by Tasks 1-3.** Do not change unrelated stage counts.
- [ ] **Step 2: Run Debug solution build, serial full `Echoglossian.Tests`, and Mock build/test commands.** If the vendored Mock incompatibility blocks execution, retain exact compiler evidence and state that native payload behavior still requires in-game verification.
- [ ] **Step 3: Perform an independent whole-branch review** for callback I/O, canonical lookup/merge parity, cache publication, cancellation/reload, dedupe race, priority, and pointer ownership; remediate Critical/Important findings through the SDD fix loop.
- [ ] **Step 4: Produce the final Debug DLL from HEAD, compute SHA-256, push the branch, and open a focused PR into `v4-series`.** Attach the PR to this task. Do not merge, tag, publish, or modify release metadata.

## Execution Notes

- The initial audit inventory has no direct `ToDoList`/QuestPlate runtime entry at the DB-3 baseline; audit changes are evidence-driven and must not remove the two DB-3 `QuestProbeCommandHelpers` development-command findings unless this scope actually migrates them.
- The requested implementation method is subagent-driven: maintain a task ledger under `docs/superpowers/plans/`, dispatch one implementer and one independent reviewer per task, and finish with a separate whole-branch review.
- Critical interface review (2026-09-27): Task 1 owns the single QuestPlate projection/state registry and pure policy extraction; Task 2 and Task 3 must consume it rather than duplicate state. This revision resolves the pre-implementation blockers recorded in `docs/superpowers/plans/2026-09-27-issue-258-db3-plan-interface-review.md`.
