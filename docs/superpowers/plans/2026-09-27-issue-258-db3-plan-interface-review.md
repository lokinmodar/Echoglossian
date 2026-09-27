# DB-3 Quest/ToDo Async Interface Review

## Verdict

**Do not implement the plan unchanged.** It correctly identifies the target
callbacks and the `PersistenceCoordinator`, but it leaves the cache contract,
write/in-flight ownership, and the split between game-thread capture and
background work unspecified. Those omissions can either retain synchronous
SQLite in `AcceptedQuestPrefetchRuntime`/`ToDoListHandler` or change canonical
QuestPlate lookup and merge behavior.

## Required interface decisions before Task 1

1. There is no existing `QuestPlate` persistence projection cache. The only
   quest caches found are `QuestUiTranslationCache` (applied UI text) and
   `QuestHoverTranslationCache` (pointer-keyed presentation), neither of which
   can answer a canonical DB lookup. The plan must name the owner and contract
   of the required projection (prefer `QuestPlatePersistenceWriter`, not either
   UI cache): an immutable `QuestPlate` projection or DTO; `TryGet...` for
   callback reads; a non-blocking `TrySchedule...` result; `DisablePublication`
   / generation invalidation; and the exact lifetime in `Echoglossian` plus
   `QuestAddonWiring`/`QuestAddonHandlerDependencies`. The projection identity
   must include every lookup discriminator: lookup mode, QuestId/name/message,
   normalized source and target language, conditional engine scope, and source
   content hash/game-version compatibility. A name-only fallback must not be
   aliased to a full lookup without preserving its different fallback order.

2. `FindQuestPlate`, `FindQuestPlateByName`, `TryFindQuestPlateForSave`,
   `SelectPreferredQuestPlate`, `SelectPreferredQuestPlateForSave`, and
   `MergeQuestPlateValues` encode distinct read and save semantics. Task 1
   must explicitly extract/reuse these selection and merge rules for an
   `AsNoTracking().ToListAsync(...)` read and a tracked async write. In
   particular retain QuestId -> name+message -> name ordering for full lookup,
   QuestId -> name for fallback lookup, language/engine `TranslationReuseScope`,
   legacy source-language matching, source-hash cross-version reuse, ordering
   by identity/completeness/date/id, and game-version bump behavior. Do not call
   `QuestLuminaResolver.TryPopulateQuestId` or `GetGameVersion` in a worker;
   capture/populate those managed values on the game thread first.

3. The current insert/update functions call `ImGui.SetClipboardText`. A
   coordinator worker cannot retain that UI-thread side effect. The plan must
   decide and test an explicit game-thread continuation or omit it only after
   proving QuestPlate prefetch did not previously honor that option. It must
   also clone/serialize the mutable `QuestPlate` before scheduling, because a
   tracked entity or `CanonicalRows` collection cannot cross the handoff.

4. `PersistenceCoordinator.TryScheduleRead` already keeps a read key in
   `inFlightReads` through publication and terminal completion. In contrast,
   `TryScheduleWrite` removes a key when the writer claims it, so a second write
   can be admitted while the first is executing. The DB-3 plan must either
   extend coordinator write ownership through terminal completion or specify a
   QuestPlate operation registry that joins/replaces safely without creating a
   second queue. It must define which full canonical row wins and prove no
   name/objective/summary/system translation is lost when completions race.

5. Model the projection explicitly as non-authoritative. Publish the read
   result only after successful async materialization and the write result only
   in `PublishAfterCommit`; publish an immutable post-merge row even for an
   unchanged write. Failed, cancelled, rejected, and empty reads must never be
   cached as a successful row. A writer-owned cancellation/publication gate
   analogous to `ReferenceTextPersistenceWriter` is required because coordinator
   reads have only coordinator-wide cancellation while DB-3 needs unload and
   replacement-generation suppression.

## Required Task 2 changes

- `AcceptedQuestPrefetchRequestQueue.TryDequeue` currently removes
  `queuedQuestSources` immediately. Therefore the same quest is accepted again
  while `AsyncSerialActionPump` is resolving or translating it. Replace this
  with a canonical-key state machine (`Queued`, `Processing`, `InFlight`,
  `Cooldown`) whose terminal callback alone releases/changes the key; preserve
  merged sources and expose a completion method that is generation-safe.
- A quest id alone is insufficient once operation scope affects persisted
  lookup/broker identity. Capture the immutable source language,
  target/reuse scope, game version, sequence, priority, and generation on the
  Framework thread; use the exact existing scoped broker-key construction for
  translation. Do not hand off `AtkValue`, addon/node pointers, a live
  `QuestManager`, or Lumina/native objects.
- `ProcessAcceptedQuestPrefetchWorkItem` currently runs `FindQuestPlate`,
  `FindQuestPlateByName`, `InsertQuestPlate`, and `UpdateQuestPlate` through
  `AsyncSerialActionPump`. Task 2 must replace all of these callbacks, including
  the later field persistence calls at runtime lines approximately 985/1031/
  1068/1105, with the Task-1 cache-first API. The static
  `RunAcceptedQuestPrefetchOperationEntry` is used by source-scope/broker tests;
  either preserve it as a pure test seam or replace its callers and tests in the
  same task. Do not leave it on the production runtime path.
- The plan needs a bounded, injectable monotonic clock/cooldown policy for
  rejected coordinator/broker admission, failed lookup/write, empty result, and
  translation failure. `ToDoListRetryInterval` alone only throttles a handler
  refresh; it does not keep the queue key owned after dequeue. Explicitly test
  expiry and terminal-state release, including cancellation/reload.
- Visible ToDo requests need `Interactive` persistence priority and must be
  dequeued before the background accepted-quest scan. The present two-loop tick
  gives queue requests first only at that tick; it has no retained state when
  the background scan already owns the same quest. Define promotion/join
  behavior rather than silently treating it as a duplicate.

## Required Task 3 changes

`ToDoListHandler.RefreshToDoList`, `OnToDoListEvent`, and `OnToDoListPreDrawEvent`
all reach `FindQuestPlate` or `FindQuestPlateByName` via
`TryResolveVisibleQuestEntries` and `TryResolveToDoListFallbackQuestTitle`.
Task 3 must add cache-first dependency delegates (or one writer dependency) to
`QuestAddonHandlerDependencies` and `QuestAddonWiring`, update the test factory
in `QuestAddonHandlerLifecycleTests`, and remove both direct calls from these
callback-reachable methods. On a miss it must render the captured original
managed strings and issue one interactive accepted-quest request; it may not
capture a native pointer for the completion callback. Safe refresh happens on a
later addon event/PreDraw after the committed projection is observable.

Keep objective priority explicit: schedule the visible TODO objective payload
before summary/current-message/system candidates. Existing prefetch ordering
must be inspected and a regression must assert the broker submission order,
not merely the persistence lane.

## Concrete files and tests missing from the plan

- Modify: `Echoglossian.cs` to construct/disable the writer before coordinator
  drain; `NativeUI/Helpers/QuestAddonWiring.cs` and
  `NativeUI/AddonHandlers/Quest/QuestAddonHandlerDependencies.cs` for the
  cache-first handler contract; likely `NativeUI/AddonHandlers/Quest/QuestAddonHandlerBase.cs`.
- Modify: `Persistence/PersistenceCoordinator.cs` and its contract tests if
  claimed-write in-flight coalescing is fixed centrally; otherwise add focused
  operation-registry tests to the writer test file.
- Modify: `NativeUI/Helpers/AcceptedQuestPrefetchRuntime.cs` at every
  QuestPlate persistence call, and `AcceptedQuestPrefetchRequestQueue.cs`.
- Add/extend: `Echoglossian.Tests/Persistence/QuestPlatePersistenceWriterTests.cs`;
  `AcceptedQuestPrefetchRequestQueueTests.cs`;
  `AcceptedQuestPrefetchRuntimeContractTests.cs`; `PrefetchBrokerSourceScopeTests.cs`;
  `QuestOperationSourceScopeTests.cs`; `QuestAddonHandlerLifecycleTests.cs`;
  `QuestHandlerTargetLanguageContractTests.cs`; `DbOperationsTests.cs` and
  `QuestPlatePersistenceTests.cs` for canonical parity/merge compatibility.

Required test cases beyond the current plan wording: a real SQLite test for
each full/name fallback order and hash mismatch; same key joining after read
dequeue and after write claim; conflicting canonical field completions; no
`UPDATE` on unchanged merged rows; no publication before commit, after failure,
or after generation cancellation; source/target/engine isolation; a controlled
clock proving rejection/empty/failure cooldown; request promotion; and source
contract tests proving callback-reachable methods contain neither legacy
QuestPlate delegates nor synchronous EF/SQLite calls.

## Semantic risks to preserve

`FindQuestPlate` and `FindQuestPlateByName` are still used by out-of-scope quest
handlers, so DB-3 must not delete or globally redirect their legacy APIs. The
new cache-first path is limited to ToDoList and accepted-quest prefetch. Do not
update the hot-path baseline merely because legacy helpers remain; the current
DB-3 inventory only records development-command findings, so any baseline edit
requires fresh audit evidence rather than a claimed count reduction.
