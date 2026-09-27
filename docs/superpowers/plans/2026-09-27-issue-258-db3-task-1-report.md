# DB-3 Task 1 report

## Status

Implemented the first isolated Task 1 foundation: `QuestPlatePersistenceWriter`
uses `IPersistenceCoordinator.TryScheduleRead` with `AsNoTracking` and async
materialization, while `QuestPlateRuntimeCache` is the single collision-safe
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

## Coverage

- Cache miss starts one async coordinator lookup and publishes only after the
  successful read completion.
- A same canonical key joins the existing runtime-owned completion.
- Runtime key includes QuestId, name, message, source/target scope, optional
  engine scope, source hash, and game version using length-prefixed segments.
- Read selection preserves QuestId, then name/message, then name-only fallback
  for no QuestId; it checks scope, source language, source hash, completeness,
  update date, and id.

## Concerns / follow-up ownership

- Task 2 must use this writer/cache from accepted-prefetch and introduce its
  explicit terminal cooldown state. Task 3 must use it in ToDoList. This task
  intentionally does not touch those callback paths.
- The legacy synchronous `DbOperations` APIs remain unchanged for out-of-scope
  callers. The new pure read policy is isolated for the async adapter; a later
  parity review should compare all legacy edge cases before redirecting any
  existing API.
- Async merge/write parity, including unchanged-write suppression and
  commit-before-cache publication, still needs the Task 1 persistence-write
  sub-scope before callback migration can safely persist translated fields.
