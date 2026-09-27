# DB-3 Task 1 report

## Status

Implemented the isolated Task 1 foundation: `QuestPlatePersistenceWriter`
uses `IPersistenceCoordinator` for `AsNoTracking` async reads and transactional
async upserts. `QuestPlateRuntimeCache` is the single collision-safe
projection/in-flight registry. The worker receives a deep-cloned managed
`QuestPlate`; it does not access Lumina, UI, clipboard, addon, or native state.
No handler callback was changed in this task.

## TDD evidence

RED (after restore because this clean worktree had no assets):

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~QuestPlatePersistenceWriterTests --no-restore -p:VSTestMaxCpuCount=1
```

The focused compile failed as intended with `CS0246` for missing
`QuestPlateRuntimeCache`, `QuestPlatePersistenceWriter`, and
`QuestPlateRuntimeResult` contracts.

GREEN:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~QuestPlatePersistenceWriterTests --no-restore -p:VSTestMaxCpuCount=1
```

Result: passed 2/2. The build retains pre-existing project warnings; no new
warnings remained from these files after the focused cleanup.

Second RED:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~QuestPlatePersistenceWriterTests --no-restore -p:VSTestMaxCpuCount=1
```

The new SQLite upsert test failed as intended because `TryPersist` did not yet
exist (`CS1061`). Its first implementation then failed the cache-publication
assertion, proving writes did not own a projection entry; the cache publication
was corrected to create the committed projection only from the coordinator's
post-commit callback.

Second GREEN: the same command passed 3/3.

## Review fix round 1

The focused test command was rerun after exposing the existing `Echoglossian`
QuestPlate selectors and merge routine as internal shared policy. The first
round was RED at compilation while the writer referred to the wrong enclosing
type; after correcting the shared call sites it was GREEN, 3/3. The async read
now invokes the exact legacy read selector and the async write invokes the
exact legacy merge routine and save selector; cancellation advances the shared
cache generation, and empty/failed/rejected operation entries hold a one-second
terminal cooldown rather than immediately admitting another operation.

## Coverage

- Cache miss starts one async coordinator lookup and publishes only after the
  successful read completion.
- A same canonical key joins the existing runtime-owned completion.
- Runtime key includes QuestId, name, message, source/target scope, optional
  engine scope, source hash, and game version using length-prefixed segments.
- Read selection preserves QuestId, then name/message, then name-only fallback
  for no QuestId; it checks scope, source language, source hash, completeness,
  update date, and id.
- Upsert uses a short-lived coordinator transaction, queries tracked candidates
  asynchronously, preserves non-empty merge values, suppresses unchanged
  writes, and publishes only from `PublishAfterCommit`.

## Concerns / follow-up ownership

- Task 2 must use this writer/cache from accepted-prefetch and introduce its
  explicit terminal cooldown state. Task 3 must use it in ToDoList. This task
  intentionally does not touch those callback paths.
- The legacy synchronous `DbOperations` APIs remain unchanged for out-of-scope
  callers. The new pure read policy is isolated for the async adapter; a later
  parity review should compare all legacy edge cases before redirecting any
  existing API.
- The legacy synchronous `DbOperations` APIs were deliberately not redirected:
  they remain required by out-of-scope handlers. The async adapter's policy is
  a pure equivalent for its managed inputs, but a complete differential suite
  against every historic `DbOperations` merge edge remains desirable before
  expanding its runtime callers.
