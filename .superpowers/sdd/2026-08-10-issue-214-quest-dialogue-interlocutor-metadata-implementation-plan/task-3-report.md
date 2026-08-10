# Task 3 Report: Generate quest dialogue metadata asynchronously from accepted quests

## Status

DONE_WITH_CONCERNS

## Commit

`69a8f84 feat(#214): precompute quest dialogue metadata on quest accept`

## Implementation

- Reused `ScheduleAcceptedQuestPrefetch` after its existing immutable work-item capture succeeds.
- Added an `OwnedAsyncOperationSet` dedicated to dialogue metadata generation; it runs separately from `AsyncSerialActionPump` and is disposed during plugin shutdown.
- Captured `QuestProgressSnapshot`, `SourceClientLanguage`, game version, derivation version (`v1`), and accepted-quest generation before the handoff.
- The background operation invokes `ReadDialogueEntries`, `BuildEntries`, and `UpsertQuestDialogueMetadataBatchAsync`; it rejects stale generations immediately before persistence. `OwnedAsyncOperationSet` observes ordinary cancellation without logging it.
- Extended the source-level contract test to enforce the owned background handoff and helper calls while preserving the tick scheduling contract.

## TDD Evidence

The new `ScheduleAcceptedQuestPrefetch_StartsOwnedDialogueMetadataGeneration` test was written before runtime changes. Its first execution failed as expected because `acceptedQuestDialogueMetadataOperations` did not exist. After the implementation, the focused contract suite passed (2/2).

## Validation

- `dotnet build Echoglossian.sln -c Debug --no-restore`: passed, 0 errors. Existing repository warnings remain.
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --no-restore -p:VSTestMaxCpuCount=1`: passed, 1140/1140.
- `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore`: blocked by existing vendored DalaMock/API drift: `MockFramework` lacks `IFramework.CreateDebouncer(TimeSpan, Action)` and `MockCharacter` lacks `IGameObject.CurrentDistance` and `IGameObject.NextDistance`.

## Remaining Verification

The DalaMock build failure prevents an automated hosted-runtime check. In-game verification should confirm that accepting a quest schedules metadata generation without affecting prefetch responsiveness and that configuration refreshes suppress stale metadata writes.

## Fix Round 1: Stale Generation Cancellation

Review identified that the generation comparison before the asynchronous database call was a TOCTOU guard. The accepted-quest state clear now rotates and cancels a generation-scoped token source. The captured token is linked through the existing `OwnedAsyncOperationSet`, and the metadata operation checks the linked token immediately before calling the database upsert. The initial regression test failed because no generation cancellation source existed; the covering suite passes after the fix.

Commit: `ee51a47 fix(#214): cancel stale quest dialogue metadata writes`

Exact command and output:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~AcceptedQuestPrefetchRuntimeContractTests"

Execução de teste para C:\Users\lokin\.codex\worktrees\7417\Echoglossian\Echoglossian.Tests\bin\x64\Debug\net10.0-windows\Echoglossian.Tests.dll (.NETCoreApp,Version=v10.0)
Versão do VSTest 18.0.2 (x64)

Iniciando execução de teste, espere...
1 arquivos de teste no total corresponderam ao padrão especificado.

Aprovado!  – Com falha:     0, Aprovado:     3, Ignorado:     0, Total:     3, Duração: 14 ms - Echoglossian.Tests.dll (net10.0)
```

## Fix Round 2: Commit-Boundary Generation Guard

Review confirmed that a cancellation check before calling persistence cannot protect a later commit. The public two-parameter metadata upsert API remains unchanged. Its implementation now delegates to a private guarded overload that executes the full deduplicated batch in one transaction, invokes the accepted-quest generation guard immediately before `CommitAsync`, and returns without committing when the generation is stale.

Commit: `ec5d9e9 fix(#214): guard quest metadata transaction commits`

Exact command and output:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~AcceptedQuestPrefetchRuntimeContractTests|FullyQualifiedName~QuestDialogueMetadataPersistenceTests"

Execução de teste para C:\Users\lokin\.codex\worktrees\7417\Echoglossian\Echoglossian.Tests\bin\x64\Debug\net10.0-windows\Echoglossian.Tests.dll (.NETCoreApp,Version=v10.0)
Versão do VSTest 18.0.2 (x64)

Iniciando execução de teste, espere...
1 arquivos de teste no total corresponderam ao padrão especificado.

Aprovado!  – Com falha:     0, Aprovado:     6, Ignorado:     0, Total:     6, Duração: 3 s - Echoglossian.Tests.dll (net10.0)
```

## Fix Round 3: Behavioral Commit-Cancellation Evidence

The public two-parameter upsert API remains unchanged. The guarded overload is now internal to the existing friend test assembly. `UpsertQuestDialogueMetadataBatchAsync_CancelledAtCommitBoundary_RollsBackRows` runs against a migrated SQLite database, cancels the token from the final commit guard, asserts that `CommitAsync` throws an `OperationCanceledException` subtype, and verifies that transaction disposal leaves no metadata rows. This provides behavioral evidence that the generation-linked cancellation token reaches the persistence commit boundary and stale work does not become observable.

Commit: `8f30d63 test(#214): verify quest metadata commit cancellation`

Exact command and output:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --no-restore --filter "FullyQualifiedName~AcceptedQuestPrefetchRuntimeContractTests|FullyQualifiedName~QuestDialogueMetadataPersistenceTests"

Execução de teste para C:\Users\lokin\.codex\worktrees\7417\Echoglossian\Echoglossian.Tests\bin\x64\Debug\net10.0-windows\Echoglossian.Tests.dll (.NETCoreApp,Version=v10.0)
Versão do VSTest 18.0.2 (x64)

Iniciando execução de teste, espere...
1 arquivos de teste no total corresponderam ao padrão especificado.

Aprovado!  – Com falha:     0, Aprovado:     7, Ignorado:     0, Total:     7, Duração: 3 s - Echoglossian.Tests.dll (net10.0)
```

## Fix Round 4: Stale Completion Non-Observability

The round-2 commit guard and round-3 cancellation test have been removed. A
managed generation check and a SQLite commit cannot be made one atomic action:
the generation is process memory while the commit is provider-owned database
state. Holding a synchronous lock across the commit would allow a framework
configuration-refresh callback to block on database work, violating the binding
non-blocking constraint. Making the generation part of the database transaction
would require a persisted runtime/session epoch and read-side filtering, which is
a schema and lookup-semantics expansion beyond Task 3.

The residual race is instead made non-load-bearing at the data boundary:

- `BuildEntries` captures `UpdatedAtUtc` before the existing generation and
  cancellation checks. Therefore, an old-generation operation that reaches the
  guard-to-commit race carries an earlier observation timestamp than a replacement
  generated after the state clear.
- The existing exact logical key includes quest id, quest sequence, source
  language, game version, source row key, source text hash, and derivation version.
  A stale row with a different source identity cannot satisfy a current lookup.
- For the same exact key, `ON CONFLICT DO UPDATE` now executes only when the
  incoming `UpdatedAtUtc` is at least as new as the persisted value. An older
  write that completes after a newer write cannot replace or become observable
  instead of the newer current result.
- If no newer exact-key row exists, a raced row is still exact-key metadata
  derived deterministically from the same versioned quest sheet and source hash;
  its generation is not part of persisted lookup semantics, so retaining it does
  not redefine the DB source of truth.

The public two-parameter `UpsertQuestDialogueMetadataBatchAsync` API remains
unchanged. The ineffective internal guard overload and its source-text contract
test were removed. No database, provider, sheet, file, model-list, prompt,
glossary, configuration, or session operation was moved onto a framework/ImGui
callback.

### TDD Evidence

The replacement persistence test was written before the SQL predicate. Its first
run failed on the intended stale-overwrite behavior:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1 --filter "FullyQualifiedName~UpsertQuestDialogueMetadataBatchAsync_OlderBatchCompletesLast_PreservesNewerRow"

Com falha Echoglossian.Tests.QuestDialogueMetadataPersistenceTests.UpsertQuestDialogueMetadataBatchAsync_OlderBatchCompletesLast_PreservesNewerRow [979 ms]
Mensagem de erro:
 Assert.Equal() Failure: Strings differ
         ↓ (pos 0)
Expected: "Current speaker"
Actual:   "Stale speaker"
         ↑ (pos 0)

Com falha! – Com falha:     1, Aprovado:     0, Ignorado:     0, Total:     1, Duração: 989 ms - Echoglossian.Tests.dll (net10.0)
```

After the production change, the focused covering suites passed:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1 --filter "FullyQualifiedName~AcceptedQuestPrefetchRuntimeContractTests|FullyQualifiedName~QuestDialogueMetadataPersistenceTests"

  Dalamud.NET.Sdk: root at C:\Users\lokin\AppData\Roaming\XIVLauncher\addon\Hooks\dev\
C:\Users\lokin\.codex\worktrees\7417\Echoglossian\Echoglossian.csproj(50,5): warning : Echoglossian.csproj is Multilingual build enabled, but the Multilingual App Toolkit is unavailable during the build. If building with Visual Studio, please check to ensure that toolkit is properly installed.
  Echoglossian -> C:\Users\lokin\.codex\worktrees\7417\Echoglossian\bin\x64\Debug\win-x64\Echoglossian.dll
  Echoglossian.Tests -> C:\Users\lokin\.codex\worktrees\7417\Echoglossian\Echoglossian.Tests\bin\x64\Debug\net10.0-windows\Echoglossian.Tests.dll
Execução de teste para C:\Users\lokin\.codex\worktrees\7417\Echoglossian\Echoglossian.Tests\bin\x64\Debug\net10.0-windows\Echoglossian.Tests.dll (.NETCoreApp,Version=v10.0)
Versão do VSTest 18.0.2 (x64)

Iniciando execução de teste, espere...
1 arquivos de teste no total corresponderam ao padrão especificado.

Aprovado!  – Com falha:     0, Aprovado:     6, Ignorado:     0, Total:     6, Duração: 3 s - Echoglossian.Tests.dll (net10.0)
```

### Additional Validation

```text
dotnet build Echoglossian.sln -c Debug --no-restore

Compilação com êxito.
    1 Aviso(s)
    0 Erro(s)
```

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --no-restore -p:VSTestMaxCpuCount=1

Aprovado!  – Com falha:     0, Aprovado:  1142, Ignorado:     0, Total:  1142, Duração: 25 s - Echoglossian.Tests.dll (net10.0)
```
