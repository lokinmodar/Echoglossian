# Issue 258: Async Persistence Migration and Translation Toggle Design

**Status:** Design approved in chat; pending written-spec review

**Date:** 2026-09-01

**Target:** `v4-series`
**Issue:** https://github.com/lokinmodar/Echoglossian/issues/258

## Summary

Echoglossian must move all runtime Entity Framework and SQLite I/O away from Dalamud Framework, addon lifecycle, Draw, and PreDraw callbacks. The migration will introduce one bounded persistence coordinator, short-lived database contexts, bounded asynchronous reads, and a single asynchronous writer. Existing tables, row identities, lookup fallbacks, and save compatibility remain unchanged.

The work will ship through small issue-specific pull requests. The first two stages establish audit coverage and shared infrastructure without a release. The first user-facing release occurs after the high-impact reference-text migration. Later domain migrations receive normal releases. A global translation command and draggable HUD button are a small independent addendum and ship with the action/item/trait release.

The final lifecycle stage adopts `IAsyncDalamudPlugin` only after startup, persistence, translation brokers, configuration saves, and shutdown can be awaited safely.

## Context and Root Cause

Issue 258 correlates large frame-time spikes with accepted-quest and reference-text prefetch activity. Current persistence helpers can query and call `SaveChanges()` synchronously from work initiated by Framework or addon callbacks. Repeated canonical-row saves also write rows whose persisted values have not changed. SQLite WAL activity and write-lock competition amplify the visible stutter.

Changing individual calls from `SaveChanges()` to `SaveChangesAsync()` is insufficient. Without shared bounds and ownership, helpers would still create uncoordinated contexts, contend for SQLite writes, duplicate pending work, and allow background prefetch to compete with visible UI.

## Goals

- Execute zero EF or SQLite queries and saves on Framework, addon lifecycle, Draw, or PreDraw threads.
- Remove runtime `.Result`, `.Wait()`, and equivalent synchronous waits around database work.
- Keep queues bounded and deduplicate identical pending reads and writes.
- Give visible interactive work priority without starving background prefetch.
- Serialize SQLite writes through one consumer and keep transactions short.
- Batch compatible writes and generate zero `UPDATE` statements for unchanged rows.
- Publish committed rows to shared caches only after a successful commit.
- Preserve the database as the source of truth.
- Preserve all current schemas, canonical identities, lookup semantics, fallbacks, and existing-user compatibility.
- Support bounded, awaited startup and shutdown as preparation for `IAsyncDalamudPlugin`.
- Deliver reviewable pull requests and normal intermediate releases.
- Add an optional global translation command and draggable HUD toggle that restores and reapplies UI safely.

## Non-Goals

- Redesigning translation engines or creating another translation queue.
- Replacing SQLite or Entity Framework.
- Changing translation-table schemas, canonical keys, source hashes, or fallback behavior.
- Making memory caches authoritative.
- Broad cleanup of unrelated legacy code.
- Using the Dalamud DTR bar for the global toggle.
- Adding user-facing tuning controls for queue capacity, batching, or retry timing.

## Persistence Invariants

1. The database remains authoritative. Memory caches are projections used to suppress repeated work and avoid runtime-thread I/O.
2. Every migrated runtime call is cache-first. A cache miss schedules work and returns without blocking the game thread.
3. A migrated domain has one active runtime path. It must not run synchronous and asynchronous persistence in parallel.
4. Domains not yet migrated may retain their legacy implementation until their dedicated stage, but each stage must remove its target domain from runtime synchronous-I/O audit results.
5. No `DbContext` instance is shared between concurrent operations or across threads.
6. Reads use short-lived contexts with low bounded concurrency. The writer creates one short-lived context per batch.
7. SQLite writes have exactly one runtime consumer.
8. Reads publish only after a successful query; writes publish only after the corresponding commit.
9. Queue bounds and backpressure must never make a Framework, addon, Draw, or PreDraw callback wait.
10. Schema changes are not required. If implementation later discovers a genuine schema requirement, that change must be isolated in a separately approved additive migration.

## Architecture

### Runtime Boundary

```text
Framework / Addon / Draw / PreDraw
        |
        +-- cache hit ----------------------> use committed row
        |
        +-- cache miss or changed row ------> publish bounded work and return
                                                  |
                                      PersistenceCoordinator
                                         |              |
                                  bounded readers   single writer
                                         |              |
                                  short DbContext   batch DbContext
                                         |              |
                                         +------ commit-+
                                                    |
                                             publish cache
                                                    |
                                         request safe UI refresh
```

### Persistence Coordinator

The process-lifetime persistence coordinator owns admission, priorities, in-flight deduplication, execution bounds, write serialization, completion, and shutdown for migrated runtime domains. It handles database I/O only and must not duplicate responsibilities from `QueuedTranslationBroker`.

Work carries a domain, canonical identity, `Interactive` or `Background` priority, cancellation token, and completion result. Canonical identity must use the same fields already used by each domain's persisted lookup semantics.

Interactive and background work use separate bounded admission lanes. Scheduling uses weighted fairness: up to three interactive items are selected before one background item when both lanes contain work. Empty lanes never delay the other lane. Background producers may be delayed or asked to retry later; they may not allocate unbounded pending work.

### Read Flow

1. A runtime handler checks its existing shared cache.
2. On a hit, it uses the committed row immediately.
3. On a miss, it registers or joins an in-flight read by canonical identity and returns the original game text for the current frame.
4. A bounded reader creates a fresh context and executes the EF query asynchronously.
5. A successful result is published to the existing cache.
6. The owning runtime requests a safe refresh or retransformation on the Framework thread.
7. Empty or failed results use the domain's existing cooldown/failure behavior and are not retried per frame.

Concurrent EF operations must use different context instances. Async operations on a context are awaited immediately before that context performs another operation.

### Write Flow

1. Translation or canonicalization work finishes outside the game thread.
2. The producer publishes a write by canonical identity and awaits completion outside the game thread.
3. Duplicate pending writes coalesce. When multiple versions for the same identity are pending, the most recent complete canonical value replaces the older not-yet-started value.
4. The single writer builds a bounded batch of compatible operations and creates a fresh context.
5. Existing values are compared field by field using the domain's current persistence semantics.
6. Unchanged entities remain unmodified and generate no `UPDATE`.
7. Changed entities are inserted or updated and committed with `SaveChangesAsync`.
8. Only committed rows are published to caches and released to consumers.

Batch size and the short collection window are internal implementation constants. DB-1 will choose them from the DB-0 baseline and lock their overflow and latency behavior in tests; they are not configuration or schema contracts.

### Backpressure and Priority

- Runtime callbacks only use non-blocking admission APIs.
- Interactive producers receive reserved capacity and weighted scheduling preference.
- Background prefetch must stop producing additional translation work when its persistence or translation lane is saturated.
- Completed translation results accepted by the writer are never silently dropped.
- Queue-depth metrics are diagnostic counters, not per-item hot-path logs.

### Existing Helpers

Current static helpers are migrated domain by domain into asynchronous adapters over the coordinator and short-lived context factory. Pure canonical-row creation remains synchronous because it performs no I/O. Tests and design-time migration tooling may create contexts directly. By DB-9, runtime DB editor operations must also be asynchronous; the final sync allowlist is limited to design-time tooling and test setup.

## Error, Cancellation, and Shutdown Contract

### Database Errors

- Transient SQLite lock/busy failures receive at most three worker-side attempts with bounded backoff.
- Permanent failures complete the request as failed, publish no cache entry, retain original UI text, and emit one summarized `PluginRuntimeLog` entry for the operation.
- Failed operations are not retried from frame callbacks.
- A failed batch transaction rolls back and must not publish partially committed cache state.

### Translation Toggle Cancellation

Turning translations off stops new translation admission and cancels translation work that has not completed. Database writes already accepted by the persistence coordinator are allowed to finish so completed translation work is not lost. Their results remain persisted but are not applied to UI while global translation is disabled.

### Plugin Shutdown

Shutdown order is fixed:

1. Reject new translation and persistence admissions.
2. Restore mutated native UI state on the Framework thread while Dalamud services and addons are still available.
3. Clear translated overlays and visible presentation state.
4. Stop translation brokers and producers.
5. Complete the persistence queues and await their drain.
6. Allow accepted database work up to five seconds to finish.
7. Cancel remaining work after the deadline; incomplete transactions roll back.
8. Dispose contexts, caches, windows, textures, and services without fire-and-forget cleanup.

## Global Translation Toggle Addendum

`Config.Translate` remains the single persistent source of truth. The existing configuration checkbox, a command, and the HUD button all call one `TranslationActivationCoordinator` rather than changing runtime state independently.

### User Interface

- No DTR integration.
- Visibility is opt-in through `ShowTranslationToggleButton`.
- The button is an always-draggable transparent ImGui HUD element.
- Position persists in configuration and is clamped to the active viewport after display or resolution changes.
- Drag motion beyond the interaction threshold suppresses click activation on mouse release.
- Enabled state uses a cyan/violet tint and soft glow.
- Disabled state is desaturated, low-opacity, and has no glow.
- Transition state uses amber and blocks repeated toggles until the Framework-thread transition completes.
- Tooltip, setting labels, command feedback, and notifications use `.resx` resources.
- Command surface: `/eglo translations on|off|toggle`.

The supplied `translation-icon.svg` is treated only as a proposed visual asset. It may enter the repository only after provenance and redistribution rights are documented. Otherwise, a project-owned icon with the same general translation concept will be created without copying protected artwork.

### Disable Flow

1. Persist `Config.Translate = false`.
2. Stop new translation admission and cancel incomplete translation work.
3. Restore only native nodes or values actually mutated by Echoglossian.
4. Clear translated overlays and hover presentation.
5. Preserve database rows and committed caches.

### Enable Flow

1. Validate the configured translation engine and required settings.
2. Persist `Config.Translate = true` and reactivate shared brokers and handlers.
3. Reapply committed cached translations to currently visible, configuration-enabled surfaces.
4. Enqueue only missing translations through the existing broker.

## Staged Delivery

Every stage begins from the latest `origin/v4-series`, uses a fresh Issue 258 branch, and targets `v4-series` through a small pull request. A migrated domain is complete only when its runtime synchronous path is removed and its focused tests pass.

| Stage | Branch | Scope | Release policy |
|---|---|---|---|
| DB-0 | `issue-258-00-db-audit` | Master spec, reusable sync-I/O audit, baseline evidence protocol, and contract-test skeleton | No release |
| DB-1 | `issue-258-01-persistence-coordinator` | Bounded priority lanes, context factory, single writer, batching, coalescing, retry, metrics, shutdown, and one low-frequency pilot | No release |
| DB-2 | `issue-258-02-reference-text` | Reference text, canonical-row persistence, prefetch reads/writes, unchanged-row suppression | Normal release; first performance release |
| DB-3 | `issue-258-03-quest-todo` | QuestPlate, accepted-quest prefetch, ToDoList cache-first reads, in-flight deduplication | Normal release |
| UI-A | `issue-258-04-translation-toggle` | Activation coordinator, command, draggable HUD button, restoration and reapplication | No standalone release; ships with DB-4 |
| DB-4 | `issue-258-05-action-item-trait` | Action, item, and trait detail persistence and bounded prefetch | Normal release, including UI-A |
| DB-5 | `issue-258-06-game-window` | Shared DB-first GameWindow persistence | Normal release |
| DB-6 | `issue-258-07-string-array-data` | Structured StringArrayData persistence and consumers | Normal release |
| DB-7 | `issue-258-08-remaining-domains` | Talk, BattleTalk, MiniTalk, text hints, toasts, nameplates, selections, context menus, NPC/location, capabilities, failures, and residual runtime helpers | Normal release |
| DB-8 | `issue-258-09-async-lifecycle` | Async startup, migrations, preload, configuration flush, runtime DB editor, bounded disposal, and `IAsyncDalamudPlugin` | No separate label or preview release |
| DB-9 | `issue-258-10-enforcement` | Remove residual runtime sync APIs, final audit, compatibility proof, and before/after report | Normal final release; never labeled canary or candidate |

DB-1's pilot must be low-frequency and semantically simple, using LLM capability observation persistence. It proves coordinator wiring without delaying the high-value reference-text migration. It must not create a second active persistence path.

## Branch, Commit, and Push Policy

- Fetch `origin/v4-series` before every stage and branch from that fetched ref.
- Use one numbered Issue 258 branch per pull request; do not accumulate all stages on a long-lived branch.
- Keep each pull request focused on one row in the delivery table.
- Prefer one to four behavioral commits per pull request.
- Include `#258` in implementation, test, and documentation commit subjects where practical.
- Push after every validated stable commit.
- Do not stage unrelated working-tree changes.
- Commit `Echoglossian.xml` only when a validated code change intentionally regenerates it; documentation-only stages must not include it.
- Merge each stage before creating the next branch so every review is against the current `v4-series` rather than a large stack.

## Validation Strategy

### Automated Contract Audit

DB-0 adds a safe, rerunnable repository script that inventories synchronous EF and SQLite operations and maps them to runtime entry points. The script must fail CI when prohibited calls are introduced under Framework, addon lifecycle, Draw, PreDraw, handler, overlay, or runtime helper paths. The allowlist must be explicit and restricted to design-time migration tooling and test setup by DB-9.

### Unit and Integration Tests

- Queue capacity and non-blocking runtime admission.
- Three-to-one interactive/background fairness without starvation.
- Read and write in-flight coalescing by canonical identity.
- Latest-value replacement for not-yet-started duplicate writes.
- One writer active at a time.
- Separate context ownership for concurrent reads.
- Batch commit and rollback behavior against a real temporary SQLite database.
- Zero affected rows and zero emitted `UPDATE` for unchanged canonical data.
- Three-attempt transient retry limit and no frame-triggered retry loop.
- Five-second bounded drain, cancellation, and rollback.
- Existing database and migration compatibility.
- Existing lookup, source-scope, language, game-version, and fallback semantics.
- Toggle state persistence, command parsing, drag-versus-click behavior, and transition gating.

Standard validation for every meaningful stage:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Lifecycle and UI stages additionally run the Mock/DalaMock startup and shutdown suite. Mock tests may claim only the integration behavior they actually drive; native addon restoration and application still require in-game verification when the harness cannot model the payload.

### In-Game Evidence

DB-0 records a reproducible Issue 258 scenario before behavior changes. DB-2 and each later performance release repeat the same scenario and record:

- median, p95, and p99 frame time;
- observed FPS range;
- persistence queue depth and maximum age;
- database batch and row counts;
- SQLite busy/retry count;
- number of unchanged rows suppressed;
- WAL write frequency during accepted-quest and reference-text prefetch.

Diagnostics use counters and summarized `PluginRuntimeLog` messages. Per-item hot-path logs are forbidden and temporary probes are removed or disabled before release.

## Acceptance Criteria

The Issue 258 migration is complete when all of the following are true:

- No production runtime EF/SQLite query or save executes synchronously. Framework, addon lifecycle, Draw, and PreDraw paths have no exceptions.
- No runtime UI path uses `.Result`, `.Wait()`, or a synchronous wrapper around database I/O.
- All runtime queues are bounded and duplicate in-flight work is coalesced.
- SQLite writes have one consumer and short-lived batch contexts.
- Interactive visible work receives priority without starving background work.
- Unchanged canonical rows emit zero `UPDATE` statements.
- Cache publication follows successful commit and the database remains authoritative.
- Existing databases open without destructive or semantics-changing migration.
- Every migrated domain preserves current lookup and fallback tests.
- Translation disable restores all and only Echoglossian-mutated native state, clears overlays, and persists its off state.
- Translation enable reapplies committed cached translations only to configured surfaces and schedules only missing work.
- Shutdown is awaited and bounded; no configuration or database flush is fire-and-forget.
- `IAsyncDalamudPlugin` is adopted only after the lifecycle contract above passes unit, Mock/DalaMock, and in-game checks.
- Before/after evidence demonstrates removal of the Issue 258 persistence-correlated frame spikes without new error or log spam.

## Compatibility and Rollback

This design changes execution and coordination, not stored data semantics. No database migration is expected. Each domain stage can be rolled back by reverting its pull request; the preceding plugin version can read rows written by the migrated version because schemas and identities remain unchanged.

The main compatibility risks are stale cache publication, incorrect canonical deduplication, lost writes during shutdown, and restoration of UI that was never mutated. The design mitigates them through post-commit publication, reuse of existing canonical identities, bounded awaited drain, and mutation-aware restoration contracts.

## Primary References

- Dalamud `IAsyncDalamudPlugin`: https://dalamud.dev/api/Dalamud.Plugin/Interfaces/IAsyncDalamudPlugin/
- EF Core asynchronous programming: https://learn.microsoft.com/en-us/ef/core/miscellaneous/async
- EF Core `DbContext` lifetime and threading: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
- EF Core transactions: https://learn.microsoft.com/en-us/ef/core/saving/transactions
- Issue 258: https://github.com/lokinmodar/Echoglossian/issues/258
