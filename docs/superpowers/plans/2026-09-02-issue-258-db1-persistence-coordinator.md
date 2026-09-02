# Issue 258 DB-1 Persistence Coordinator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the bounded asynchronous persistence coordinator required by Issue 258 and prove it with one low-frequency `LlmModelCapabilityObservation` write pilot, without changing database schema or adopting `IAsyncDalamudPlugin` early.

**Architecture:** One process-lifetime `PersistenceCoordinator` owns separate bounded interactive/background lanes for reads and writes, up to two concurrent short-lived read contexts, and exactly one batched write consumer. Runtime admission is always `Try*` and non-blocking; pending work coalesces by the existing canonical identity; writes publish to existing caches only after a successful transaction. DB-1 migrates only capability-observation writes; capability-rule writes and synchronous startup hydration remain legacy work for later stages.

**Tech Stack:** C# 14, .NET 10, `System.Threading.Channels`, EF Core 10.0.10, Microsoft.Data.Sqlite, xUnit 2.9.3, FluentAssertions 8.10.0, DalaMock hosted lifecycle tests.

**Spec:** `docs/superpowers/specs/2026-09-01-issue-258-async-persistence-and-translation-toggle-design.md`

## Global Constraints

- The database remains authoritative; memory caches are projections only.
- Preserve every existing table, column, migration, observation identity, rule identity, lookup fallback, and save format. DB-1 creates no migration.
- Runtime callbacks and translation continuations use non-blocking `Try*` admission and never call `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` for persistence.
- Interactive and background work have independent bounded capacity. When both are ready, dequeue at most three interactive items before one background item.
- Use no more than two concurrent readers. Every read and every write batch leases its own short-lived `EchoglossianDbContext`; no context or provider object is shared concurrently.
- The migrated capability-observation path has exactly one writer. It uses one transaction and one `SaveChangesAsync` call per non-empty changed batch.
- Duplicate pending reads join one completion. Duplicate pending writes replace the not-yet-started payload with the latest immutable value and join its completion. Once a slot is claimed, a later value may occupy a new bounded slot.
- Accepted writes are never dropped. A saturated or stopped lane rejects admission before ownership transfers to the coordinator.
- Retry only SQLite `BUSY` (`5`) and `LOCKED` (`6`) failures. Make at most three total attempts, with worker-side delays of 25 ms and 100 ms between attempts.
- Default internal bounds are: 64 interactive slots, 256 background slots, two readers, batch size 32, 5 ms batch collection window, four pooled contexts, one-second SQLite default timeout, and five-second coordinator drain deadline. These are internal constants, not user settings.
- A failed or cancelled transaction publishes no cache state. Successful writes invoke publication only after `CommitAsync` succeeds.
- Use `PluginRuntimeLog` delegates for summarized terminal failures and lifecycle summaries only. Do not add per-row, per-item, retry-by-retry, or frame logs.
- DB-1 migrates only `LlmModelCapabilityObservation`. Keep `LlmCapabilityPersistenceHelper.UpsertRules`, `LlmCapabilityRefreshPromoter`, capability-rule cache publication, synchronous startup migration, synchronous capability-cache hydration, and DB editor behavior unchanged.
- Under the current `IDalamudPlugin.Dispose`, DB-1 may reject new admission and start idempotent non-blocking completion, but it must not claim that plugin teardown awaited the five-second drain. DB-8 owns that lifecycle guarantee and `IAsyncDalamudPlugin` adoption.
- DB-1 is not release-eligible. Do not change version metadata, create a tag/release, touch DalamudPluginsD17, merge a pull request, or publish anything.
- Follow TDD for every production behavior: write the focused test, observe the expected failure, implement the minimum behavior, and rerun focused tests before committing.
- Do not stage the pre-existing generated `Echoglossian.xml` drift unless a narrowly reviewed generation diff contains only DB-1 members. Never stage unrelated worktree changes.

---

### Task 1: Add immutable coordinator contracts and bounded priority lanes

**Files:**
- Create: `Persistence/PersistencePriority.cs`
- Create: `Persistence/PersistenceWorkKey.cs`
- Create: `Persistence/PersistenceContracts.cs`
- Create: `Persistence/PersistenceCoordinatorOptions.cs`
- Create: `Persistence/BoundedPriorityQueue.cs`
- Create: `Echoglossian.Tests/Persistence/BoundedPriorityQueueTests.cs`

**Interfaces:**
- Consumes: `System.Threading.Channels` from the .NET 10 shared framework.
- Produces: `PersistencePriority`, `PersistenceWorkKey`, `PersistenceAdmissionStatus`, read/write result contracts, `PersistenceWriteRequest`, `PersistenceCoordinatorOptions.Default`, and `BoundedPriorityQueue<T>` for Tasks 2 and 3.

- [ ] **Step 1: Write failing validation tests for keys and option bounds**

Create `Echoglossian.Tests/Persistence/BoundedPriorityQueueTests.cs` with the repository header and XML documentation. Add tests with these exact behaviors:

```csharp
[Fact]
public void PersistenceWorkKey_WithBlankDomain_ThrowsArgumentException()

[Fact]
public void PersistenceWorkKey_WithBlankCanonicalIdentity_ThrowsArgumentException()

[Fact]
public void DefaultOptions_ExposeApprovedInternalBounds()
```

The options assertion must cover `InteractiveCapacity == 64`, `BackgroundCapacity == 256`, `ReaderConcurrency == 2`, `MaxBatchSize == 32`, `BatchCollectionWindow == TimeSpan.FromMilliseconds(5)`, `MaxAttempts == 3`, retry delays `[25 ms, 100 ms]`, `ContextPoolSize == 4`, `SqliteDefaultTimeoutSeconds == 1`, and `ShutdownTimeout == TimeSpan.FromSeconds(5)`.

- [ ] **Step 2: Run the contract tests and verify RED**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~BoundedPriorityQueueTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: compilation fails because the `Echoglossian.Persistence` contracts do not exist.

- [ ] **Step 3: Implement the immutable contracts**

Use namespace `Echoglossian.Persistence`. Define these exact public-to-the-assembly shapes:

```csharp
internal enum PersistencePriority
{
    Interactive,
    Background,
}

internal readonly record struct PersistenceWorkKey
{
    internal PersistenceWorkKey(string domain, string canonicalIdentity);

    internal string Domain { get; }

    internal string CanonicalIdentity { get; }
}

internal enum PersistenceAdmissionStatus
{
    Accepted,
    Joined,
    Replaced,
    RejectedCapacity,
    RejectedShutdown,
}

internal enum PersistenceCompletionStatus
{
    Succeeded,
    Unchanged,
    Failed,
    Cancelled,
    Rejected,
}

internal readonly record struct PersistenceReadResult<T>(
    PersistenceCompletionStatus Status,
    T? Value,
    Exception? Error);

internal readonly record struct PersistenceWriteResult(
    PersistenceCompletionStatus Status,
    int AffectedRows,
    Exception? Error);

internal readonly record struct PersistenceWriteMutation(bool Changed)
{
    internal static PersistenceWriteMutation ChangedResult { get; } = new(true);

    internal static PersistenceWriteMutation UnchangedResult { get; } = new(false);
}

internal sealed record PersistenceWriteRequest(
    PersistenceWorkKey Key,
    PersistencePriority Priority,
    Func<EchoglossianDbContext, CancellationToken, Task<PersistenceWriteMutation>> ApplyAsync,
    Action PublishAfterCommit);
```

Validate nonblank key parts and non-null delegates at construction boundaries. `PersistenceCoordinatorOptions` is immutable and validates positive capacities, reader concurrency, batch size, context pool size, timeout, and exactly `MaxAttempts - 1` nonnegative retry delays. `Default` returns the values in Global Constraints and does not read configuration.

- [ ] **Step 4: Add failing bounded-capacity and fairness tests**

Add these deterministic cases to `BoundedPriorityQueueTests`:

```csharp
[Fact]
public void TryEnqueue_WhenLaneIsFull_ReturnsFalseWithoutDroppingAcceptedItem()

[Fact]
public async Task DequeueAsync_WhenBothLanesStayReady_SelectsThreeInteractiveThenOneBackground()

[Fact]
public async Task DequeueAsync_WhenInteractiveLaneIsEmpty_DoesNotDelayBackground()

[Fact]
public async Task DequeueAsync_AfterCompletionAndDrain_ThrowsChannelClosedException()
```

Pre-fill the lanes before dequeueing and assert the exact first eight labels `I1,I2,I3,B1,I4,I5,I6,B2`. Do not use `Task.Delay` to establish ordering; `WaitAsync` may be used only as a hang guard.

- [ ] **Step 5: Run the queue tests and verify the new RED state**

Run the Step 2 command again.

Expected: the contract tests pass and the queue behavior tests fail because `BoundedPriorityQueue<T>` does not exist.

- [ ] **Step 6: Implement the bounded priority queue**

Implement this contract:

```csharp
internal sealed class BoundedPriorityQueue<T>
{
    internal BoundedPriorityQueue(int interactiveCapacity, int backgroundCapacity);

    internal int InteractiveDepth { get; }

    internal int BackgroundDepth { get; }

    internal bool TryEnqueue(T item, PersistencePriority priority);

    internal ValueTask<T> DequeueAsync(CancellationToken cancellationToken);

    internal void Complete();
}
```

Create two `Channel<T>` instances with `BoundedChannelFullMode.Wait`, `SingleWriter = false`, `AllowSynchronousContinuations = false`, and non-blocking `TryWrite`. Use a single-reader asynchronous wake signal and resignal when queued items remain. Maintain depth with `Interlocked`; decrement only after a successful `TryRead`. The dequeue selector spends an interactive budget of three, then selects one background item when both lanes contain work; an empty lane never delays the other. `Complete` is idempotent and wakes blocked readers so drained queues terminate with `ChannelClosedException`.

- [ ] **Step 7: Verify Task 1 and commit**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~BoundedPriorityQueueTests -p:VSTestMaxCpuCount=1 --nologo
git diff --check
git add -- Persistence Echoglossian.Tests\Persistence\BoundedPriorityQueueTests.cs
git commit -m "feat(#258): add bounded persistence lanes"
git push -u origin issue-258-01-persistence-coordinator
```

Expected: all `BoundedPriorityQueueTests` pass; the commit contains contracts, options, lane implementation, and its tests only.

---

### Task 2: Add bounded read scheduling, coalescing, and metrics

**Files:**
- Create: `Persistence/IPersistenceCoordinator.cs`
- Create: `Persistence/PersistenceCoordinatorMetrics.cs`
- Create: `Persistence/PersistenceCoordinator.cs`
- Create: `Echoglossian.Tests/Persistence/PersistenceCoordinatorReadTests.cs`
- Create: `Echoglossian.Tests/Persistence/PersistenceCoordinatorTestContextFactory.cs`

**Interfaces:**
- Consumes: Task 1 contracts and `IDbContextFactory<EchoglossianDbContext>`.
- Produces: non-blocking `TryScheduleRead<T>`, two bounded reader workers, in-flight read coalescing, immutable metrics snapshots, and the coordinator skeleton extended by Task 3.

- [ ] **Step 1: Write failing non-blocking admission and read-coalescing tests**

Add these cases to `PersistenceCoordinatorReadTests`:

```csharp
[Fact]
public void TryScheduleRead_WhenWorkerIsBlocked_ReturnsAcceptedWithoutWaiting()

[Fact]
public void TryScheduleRead_WhenBackgroundLaneIsFull_ReturnsRejectedCapacityImmediately()

[Fact]
public void TryScheduleRead_WhenBackgroundIsFull_StillUsesReservedInteractiveCapacity()

[Fact]
public async Task TryScheduleRead_WithSameInFlightKey_JoinsOneQueryAndOnePublication()

[Fact]
public async Task ConcurrentReads_LeaseDistinctContextsAndRespectReaderConcurrency()

[Fact]
public void TryScheduleRead_AfterStopAccepting_ReturnsRejectedShutdown()
```

Use `TaskCompletionSource` instances with `RunContinuationsAsynchronously`, `ManualResetEventSlim`, `Interlocked`, and explicit release gates. The context factory records `ContextId` values and maximum simultaneous leases. Do not use sleeps to create ordering.

- [ ] **Step 2: Run the focused read tests and verify RED**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~PersistenceCoordinatorReadTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: compilation fails because `IPersistenceCoordinator`, metrics, and `PersistenceCoordinator` do not exist.

- [ ] **Step 3: Define the coordinator interface and immutable metrics snapshot**

Use this interface without adding a blocking or capacity-waiting admission API:

```csharp
internal interface IPersistenceCoordinator : IAsyncDisposable
{
    PersistenceAdmissionStatus TryScheduleRead<T>(
        PersistenceWorkKey key,
        PersistencePriority priority,
        Func<EchoglossianDbContext, CancellationToken, Task<T>> readAsync,
        Action<T>? publish,
        out Task<PersistenceReadResult<T>> completion);

    PersistenceAdmissionStatus TryScheduleWrite(
        PersistenceWriteRequest request,
        out Task<PersistenceWriteResult> completion);

    PersistenceMetricsSnapshot GetMetrics();

    void StopAccepting();

    Task CompleteAsync(CancellationToken cancellationToken = default);
}
```

`PersistenceMetricsSnapshot` contains read/write interactive and background depths, their high-water marks, total accepted/rejected/coalesced operations, active/max readers, active/max writers, batch count, committed writes, unchanged writes, retry count, terminal failures, cancelled operations, and `OldestQueuedAge`. `PersistenceCoordinatorMetrics` updates counters only with `Interlocked` and returns immutable snapshots; no metric update writes logs.

- [ ] **Step 4: Implement the read half of `PersistenceCoordinator`**

The constructor contract is:

```csharp
internal PersistenceCoordinator(
    IDbContextFactory<EchoglossianDbContext> contextFactory,
    PersistenceCoordinatorOptions? options = null,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
    Func<DateTimeOffset>? utcNow = null,
    Func<Exception, bool>? transientFailureClassifier = null,
    Action<string>? warningLog = null,
    Action<string>? errorLog = null);
```

Start exactly `ReaderConcurrency` worker tasks with `Task.Run`. Protect only the short admission/dictionary transition with a private lock; never hold it while awaiting or executing a caller delegate. Keep a key in the in-flight read map from accepted admission through query completion so duplicates join the same typed `TaskCompletionSource`. A new key enters the priority queue only when `TryEnqueue` succeeds; on saturation, remove the provisional slot and return a completed `Rejected` result. Each worker leases a context with `CreateDbContextAsync`, immediately awaits the caller query, publishes only on success, disposes the context asynchronously, completes all joiners, and removes the key. Terminal failure produces one summarized error message containing the domain but no payload.

`TryScheduleWrite` may throw `NotSupportedException` only until Task 3 implements it. `StopAccepting` atomically rejects later reads. Do not implement final completion behavior yet beyond stopping and draining read workers.

- [ ] **Step 5: Run focused tests, refactor while green, and commit**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~BoundedPriorityQueueTests|FullyQualifiedName~PersistenceCoordinatorReadTests" -p:VSTestMaxCpuCount=1 --nologo
git diff --check
git add -- Persistence Echoglossian.Tests\Persistence
git commit -m "feat(#258): coordinate bounded persistence reads"
git push
```

Expected: the selected tests pass; no test depends on wall-clock ordering or reflection.

---

### Task 3: Add the serialized batched writer, retry, and bounded drain

**Files:**
- Modify: `Persistence/PersistenceCoordinator.cs`
- Modify: `Persistence/PersistenceCoordinatorMetrics.cs`
- Create: `Echoglossian.Tests/Persistence/PersistenceCoordinatorWriteTests.cs`
- Create: `Echoglossian.Tests/Persistence/PersistenceCoordinatorShutdownTests.cs`
- Create: `Echoglossian.Tests/Persistence/PersistenceCoordinatorSqliteTests.cs`

**Interfaces:**
- Consumes: Task 2 `IPersistenceCoordinator`, options, priority queue, metrics, and context factory seam.
- Produces: bounded write admission, latest-pending replacement, one writer, batches of at most 32, atomic EF transactions, post-commit publication, BUSY/LOCKED retry, unchanged suppression, and idempotent five-second `CompleteAsync`.

- [ ] **Step 1: Write failing write admission, coalescing, and serialization tests**

Create `PersistenceCoordinatorWriteTests.cs` with these deterministic tests:

```csharp
[Fact]
public void TryScheduleWrite_WhenWorkerIsBlocked_ReturnsAcceptedWithoutWaiting()

[Fact]
public void TryScheduleWrite_WhenLaneIsFull_ReturnsRejectedCapacityWithoutDroppingAcceptedWork()

[Fact]
public async Task DuplicatePendingWrite_ReplacesPayloadWithLatestAndJoinsCompletion()

[Fact]
public async Task DuplicateAfterClaim_UsesANewBoundedSlotWithoutMutatingActiveRequest()

[Fact]
public async Task Writer_NeverUsesMoreThanOneContextConcurrently()

[Fact]
public async Task Writer_CollectsCompatibleRequestsIntoOneBoundedBatch()

[Fact]
public async Task Publication_DoesNotRunUntilCommitCompletes()
```

Gate the first write or commit explicitly. Assert the latest immutable payload, joined result tasks, maximum active writer count of one, one context/transaction for a collected batch, and no publication before the commit gate opens.

- [ ] **Step 2: Run the focused writer tests and verify RED**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~PersistenceCoordinatorWriteTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: tests fail because `TryScheduleWrite` is not implemented.

- [ ] **Step 3: Implement bounded write slots and the single writer pump**

Use a pending-write dictionary keyed by `PersistenceWorkKey` plus one queued slot per pending identity. Serialize only dictionary/slot transitions with a short lock:

- before the writer claims a slot, a duplicate replaces its immutable `PersistenceWriteRequest` and joins the same completion;
- the writer removes the pending-map entry when it claims the slot;
- a duplicate arriving after claim creates a fresh slot and must pass bounded `TryEnqueue` admission;
- a rejected new slot completes as `Rejected` and never becomes coordinator-owned.

The single writer dequeues by the same 3:1 priority rule, waits the injected/default 5 ms collection window after the first item, and collects at most `MaxBatchSize` ready slots. For every attempt it leases a fresh context, begins one async transaction, awaits each retry-safe `ApplyAsync`, calls `SaveChangesAsync` once only when at least one mutation is changed, commits, disposes the context, then invokes every publication delegate. Complete changed requests as `Succeeded`, unchanged requests as `Unchanged`, and give the batch's affected-row count to successful results. If any apply/save/commit step fails, rollback/dispose and publish nothing for the entire batch.

- [ ] **Step 4: Add failing SQLite transaction, unchanged, and retry tests**

Create `PersistenceCoordinatorSqliteTests.cs` with a GUID-named temporary database owner and optional `DbCommandInterceptor`. Add:

```csharp
[Fact]
public async Task FailedBatch_RollsBackEveryRowAndPublishesNothing()

[Fact]
public async Task UnchangedMutation_EmitsNoUpdateAndReportsUnchanged()

[Fact]
public async Task ChangedMutation_EmitsOneUpdateAndPublishesAfterCommit()

[Fact]
public async Task BusyThenBusyThenSuccess_UsesExactlyThreeAttemptsAndTwoDelays()

[Fact]
public async Task ThreeBusyFailures_StopWithoutPublicationOrFourthAttempt()
```

Use a real migrated SQLite file for transaction and SQL-command assertions. Use an injected classifier/scripted executor for retry counts; do not rely on racing SQLite locks. The interceptor counts commands beginning with `UPDATE` after trimming leading whitespace. Cleanup calls `SqliteConnection.ClearAllPools()` and retries directory deletion only for `IOException` or `UnauthorizedAccessException`.

- [ ] **Step 5: Implement retry and unchanged-row behavior**

The default classifier unwraps an `AggregateException` or one inner exception and returns true only when a `SqliteException.SqliteErrorCode` is `5` or `6`. Attempt the whole batch at most three times. Delay 25 ms after attempt one and 100 ms after attempt two through the injected delay function. Increment retry metrics once per delay and emit only one terminal error after the last failed attempt. Caller operation delegates must be documented as retry-safe and must create/attach entities inside the supplied context rather than capture tracked entities.

When every mutation reports unchanged, commit the empty transaction without calling `SaveChangesAsync`; complete each request as `Unchanged`, increment unchanged metrics, and emit no `UPDATE`.

- [ ] **Step 6: Add failing bounded-shutdown tests**

Create `PersistenceCoordinatorShutdownTests.cs` with:

```csharp
[Fact]
public async Task CompleteAsync_RejectsNewAdmissionAndDrainsAcceptedWrites()

[Fact]
public async Task CompleteAsync_WhenDeadlineExpires_CancelsAndRollsBackRemainingBatch()

[Fact]
public async Task CompleteAsync_IsIdempotent()

[Fact]
public async Task DisposeAsync_LeavesZeroQueuedAndActiveMetrics()
```

Use `ShutdownTimeout = TimeSpan.Zero` for the deadline case so no test sleeps five seconds. For the drain case, start completion while a write is gated, assert later admission rejects, release the gate, and assert commit precedes completion.

- [ ] **Step 7: Implement idempotent completion and verify Task 3**

`StopAccepting` atomically closes admission and is idempotent. `CompleteAsync` calls it, completes both read and write queues, and awaits reader/writer pumps. The default wait is bounded by five seconds. On timeout it cancels the coordinator-owned token, awaits worker termination, rolls back any active transaction through cancellation/disposal, and completes remaining queued/in-flight requests as `Cancelled`. Multiple callers share one completion task. `DisposeAsync` delegates to `CompleteAsync`.

Run:

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~PersistenceCoordinator -p:VSTestMaxCpuCount=1 --nologo
git diff --check
git add -- Persistence Echoglossian.Tests\Persistence
git commit -m "feat(#258): serialize batched persistence writes"
git push
```

Expected: all coordinator tests pass, maximum active writer is one, retry tests make no wall-clock waits, and failed batches publish no cache entries.

---

### Task 4: Migrate capability observations and wire the DB-1 lifecycle boundary

**Files:**
- Create: `Persistence/EchoglossianDbContextRuntimeFactory.cs`
- Create: `DBHelpers/LlmCapabilityObservationWriter.cs`
- Create: `Translators/Capabilities/LlmCapabilityObservationRuntime.cs`
- Modify: `DBHelpers/LlmCapabilityPersistenceHelper.cs`
- Modify: `Translators/Capabilities/LlmCapabilityPolicyService.cs`
- Modify: `Translators/ChatGPTTranslator.cs`
- Modify: `Translators/ClaudeTranslator.cs`
- Modify: `Translators/DeepSeekTranslator.cs`
- Modify: `Translators/GeminiTranslator.cs`
- Modify: `Translators/LmStudioTranslator.cs`
- Modify: `Translators/OllamaTranslator.cs`
- Modify: `Translators/OpenRouterTranslator.cs`
- Modify: `Echoglossian.cs`
- Modify: `PluginRuntime/Startup/PluginStartupStage.cs`
- Modify: `Echoglossian.Tests/LlmCapabilityPersistenceTests.cs`
- Modify: `Echoglossian.Tests/LlmCapabilityPolicyServiceTests.cs`
- Create: `Echoglossian.Tests/Persistence/LlmCapabilityObservationWriterTests.cs`
- Modify: `Echoglossian.Mock.Tests/PluginStartupSmokeTests.cs`
- Modify: `scripts/sync-db-hotpaths-baseline.json`
- Modify: `docs/issue-258-sync-db-hotpath-inventory.md`
- Modify: `docs/superpowers/plans/2026-09-02-issue-258-db1-persistence-coordinator.md`

**Interfaces:**
- Consumes: Task 3 coordinator and the existing seven-field observation identity `(Engine, ProviderScope, EndpointScope, ModelId, ParameterName, StatusCode, MessageExcerpt)`.
- Produces: one exclusive async observation path, truthful awaited learning results on translator continuations, post-commit `LlmCapabilityCacheManager` publication, process-lifetime registration, non-blocking synchronous teardown boundary, and updated DB audit evidence.

- [ ] **Step 1: Write failing observation-writer parity tests**

Create `LlmCapabilityObservationWriterTests.cs` and update `LlmCapabilityPersistenceTests` with async tests:

```csharp
[Fact]
public async Task RecordAsync_NewIdentity_InsertsOneObservation()

[Fact]
public async Task RecordAsync_ExistingIdentity_UpdatesOnlyErrorCodeAndObservedTime()

[Fact]
public async Task RecordAsync_RepeatedPendingIdentity_CoalescesToLatestObservation()

[Fact]
public async Task RecordAsync_DoesNotPublishBeforeCommit()

[Fact]
public async Task RecordAsync_FailedCommit_DoesNotPublish()
```

Verify all seven identity fields and the unchanged schema. Seed/migrate test databases directly; production observation writes must use `LlmCapabilityObservationWriter`.
Replace the old `LlmCapabilityPersistenceHelper.RecordObservation` call in `LlmCapabilityPersistenceTests` with test-only direct EF setup (`MigrateAsync`, `Add`, and `SaveChangesAsync`) before exercising runtime hydration. This direct context use remains test setup, not a production fallback.

- [ ] **Step 2: Run focused persistence tests and verify RED**

```powershell
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~LlmCapabilityObservationWriterTests|FullyQualifiedName~LlmCapabilityPersistenceTests" -p:VSTestMaxCpuCount=1 --nologo
```

Expected: compilation fails because the runtime factory and observation writer do not exist.

- [ ] **Step 3: Implement the runtime context factory and observation adapter**

`EchoglossianDbContextRuntimeFactory` implements `IDbContextFactory<EchoglossianDbContext>` by wrapping `PooledDbContextFactory<EchoglossianDbContext>`. Build options from `<configDir>/Echoglossian.db` with `SqliteConnectionStringBuilder.DataSource`, `DefaultTimeout = 1`, and pool size four. It exposes both `CreateDbContext` and `CreateDbContextAsync` only as required by the EF interface. Do not call `Migrate` or share an active context.

`LlmCapabilityObservationWriter` snapshots the supplied model before admission, assigns `ObservedAtUtc = DateTime.UtcNow` when it is default, builds the canonical key from all seven identity fields using a collision-safe length-prefixed encoding, and calls `TryScheduleWrite`. Its retry-safe apply delegate uses `FirstOrDefaultAsync` with the exact existing predicate. On a match, compare and update only `ProviderErrorCode` and `ObservedAtUtc`; on no match, add a fresh entity clone. The publication delegate calls `LlmCapabilityCacheManager.PublishObservation` only while this writer's publication gate remains enabled. Unregistration disables publication for accepted writes that finish after plugin-visible caches are cleared.

`LlmCapabilityObservationRuntime` owns exactly one registered writer:

```csharp
internal static void Register(LlmCapabilityObservationWriter writer);

internal static void Unregister(LlmCapabilityObservationWriter writer);

internal static PersistenceAdmissionStatus TryRecord(
    LlmModelCapabilityObservation observation,
    out Task<PersistenceWriteResult> completion);
```

Registration rejects a second different writer; unregistration is reference-checked and idempotent. When unavailable, return `RejectedShutdown` with a completed rejected result. Remove `RecordObservation` from `LlmCapabilityPersistenceHelper`; keep `UpsertRules` byte-for-byte behaviorally unchanged.

- [ ] **Step 4: Write failing async policy tests**

Convert the four `LearnFromProviderFailure` tests to `async Task`, add an async temporary-directory helper that registers a real coordinator/writer, and add:

```csharp
[Fact]
public async Task LearnFromProviderFailureAsync_ReportsObservationOnlyAfterCommit()

[Fact]
public async Task LearnFromProviderFailureAsync_WhenAdmissionRejects_ReportsPersistenceFailed()
```

Keep the exact-model rule-promotion assertions: DB-1 changes the observation path only and must not change `UpsertRules` semantics.

- [ ] **Step 5: Make learning await the observation writer and update all callers**

Rename the policy method to:

```csharp
public static async Task<LlmCapabilityLearningResult> LearnFromProviderFailureAsync(
    LlmCapabilityScope scope,
    LlmCapabilityParameterName parameterName,
    int? statusCode,
    string? responseText,
    CancellationToken cancellationToken = default)
```

After classification, submit to `LlmCapabilityObservationRuntime`, await the returned completion with `ConfigureAwait(false)`, and set `ObservationRecorded` true only for `Succeeded` or `Unchanged`. Preserve the existing conditional synchronous rule promotion and its post-save rule publication. Return `persistence-failed` for rejected, failed, or cancelled observation work and keep one summarized warning without provider payload.

Update all seven translator files so their already-async error-learning paths await this method. Convert the two ChatGPT local learning helpers to `async Task` and await them from the existing async response path. Do not add a compatibility wrapper with the old synchronous signature and do not use fire-and-forget calls.

- [ ] **Step 6: Wire one coordinator instance and test the lifecycle boundary**

In `Echoglossian`, create the runtime factory, coordinator, and observation writer after `CreateOrUseDb()` completes and before capability cache initialization or translator use. Register the writer and mark new audit stage `PersistenceCoordinatorStarted`.

At the start of `Dispose(bool)`, unregister the writer, disable its late publication, call `StopAccepting`, and mark `PersistenceAdmissionsStopped`. After translation producers/brokers are stopped, start `CompleteAsync` exactly once and retain/observe its task without blocking `Dispose`. Do not add a `PersistenceDrainCompleted` startup-audit claim and do not adopt `IAsyncDalamudPlugin` in DB-1.

Update `PluginStartupSmokeTests` to assert the coordinator-started stage after startup and the admissions-stopped stage after disposal. State in test documentation that this proves registration/order only, not awaited drain or SQLite scheduling.
Where the Mock startup test currently seeds an observation through `LlmCapabilityPersistenceHelper.RecordObservation`, seed it directly through its isolated `EchoglossianDbContext` and `SaveChanges`; tests may create contexts directly under the master spec.

- [ ] **Step 7: Run focused unit and Mock tests**

```powershell
dotnet build .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --nologo
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~PersistenceCoordinator|FullyQualifiedName~LlmCapability" -p:VSTestMaxCpuCount=1 --nologo
dotnet restore .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj --nologo
dotnet build .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore --nologo
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~PluginStartupSmokeTests -p:VSTestMaxCpuCount=1 --nologo
```

Expected: all selected tests pass. Mock coverage claims only hosted lifecycle registration/unregistration.

- [ ] **Step 8: Regenerate the DB audit baseline and verify only pilot debt was removed**

Before updating, copy the checked-in baseline to ignored artifacts. Then run:

```powershell
Copy-Item .\scripts\sync-db-hotpaths-baseline.json .\artifacts\issue-258\db1-baseline-before.json
.\scripts\audit-sync-db-hotpaths.ps1 -UpdateBaseline -ReportPath .\docs\issue-258-sync-db-hotpath-inventory.md
.\scripts\audit-sync-db-hotpaths.ps1 -ReportPath .\artifacts\issue-258\db1-sync-db-hotpath-audit.md
```

Compare the before/after JSON by `id`. Expected removals are limited to the deleted synchronous observation method (`direct-db-context`, `sync-ef-migrate`, `sync-ef-query`, its two `sync-ef-save` calls) and its `LlmCapabilityPolicyService` persistence-helper invocation. Any added finding or unrelated removal is a failure that must be fixed before commit. The synchronous `UpsertRules` and startup cache-hydration findings remain approved DB-7 debt.

- [ ] **Step 9: Run full validation and record exact evidence**

```powershell
.\scripts\audit-sync-db-hotpaths.ps1 -ReportPath .\artifacts\issue-258\db1-sync-db-hotpath-audit.md
dotnet build .\Echoglossian.sln -c Debug --no-restore --nologo
dotnet build .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --nologo
dotnet test .\Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 --nologo
dotnet build .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore --nologo
dotnet test .\Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1 --nologo
git diff --check
git status --short
```

Append an `## Execution Notes` section to this plan with exact audit totals by stage, removed finding IDs, build warning/error totals, unit and Mock test totals, schema/migration status, and the honest lifecycle limitation: DB-1 does not claim an awaited plugin drain or in-game performance improvement.

- [ ] **Step 10: Commit and push the pilot/lifecycle slice**

Stage only the files listed in Task 4 plus the reviewed DB-1 files from Tasks 1-3 if a fix round changed them. Exclude unrelated generated XML drift.

```powershell
git add -- Persistence DBHelpers\LlmCapabilityObservationWriter.cs DBHelpers\LlmCapabilityPersistenceHelper.cs Translators\Capabilities\LlmCapabilityObservationRuntime.cs Translators\Capabilities\LlmCapabilityPolicyService.cs Translators\ChatGPTTranslator.cs Translators\ClaudeTranslator.cs Translators\DeepSeekTranslator.cs Translators\GeminiTranslator.cs Translators\LmStudioTranslator.cs Translators\OllamaTranslator.cs Translators\OpenRouterTranslator.cs Echoglossian.cs PluginRuntime\Startup\PluginStartupStage.cs Echoglossian.Tests Echoglossian.Mock.Tests\PluginStartupSmokeTests.cs scripts\sync-db-hotpaths-baseline.json docs\issue-258-sync-db-hotpath-inventory.md docs\superpowers\plans\2026-09-02-issue-258-db1-persistence-coordinator.md
git commit -m "feat(#258): pilot async capability persistence"
git push
```

Expected: one to four behavioral commits plus the plan commit are on `issue-258-01-persistence-coordinator`; the remote hash matches local `HEAD`; no release, tag, merge, D17 manifest, candidate, or canary operation has occurred.

---

## Final Branch Gate

## Execution Notes

- Task 4 removed exactly six DB-1 audit occurrences and added none: one direct context, migrate, query, two saves, and the policy helper invocation. The final audit is 225 findings: DB-2=10, DB-3=2, DB-4=42, DB-5=18, DB-6=15, DB-7=125, DB-8=13. The removed IDs are `direct-db-context` occurrence 2, `persistence-helper-call` occurrence 1, `sync-ef-migrate` occurrence 2, `sync-ef-query` occurrence 2, and `sync-ef-save` occurrences 2 and 3.
- Capability observation writes use the coordinator writer, a four-context `PooledDbContextFactory`, a one-second SQLite timeout, seven-field length-prefixed identity, immutable snapshots, and post-commit publication. The static capability-runtime fixtures are serialized because they own shared configuration, file-log, runtime, and cache state.
- Full validation (2026-09-02): solution build succeeded with 65 warnings and 0 errors; test-project build succeeded with 1 warning and 0 errors; unit tests passed 1382/1382, failed 0, skipped 0 in 47.624 seconds. The prior RED run was 1378 passed and 4 failed (46 seconds), traced to parallel mutation of `Echoglossian.ConfigDirectory` and `PluginRuntimeFileLog`; the collection isolation is the GREEN fix.
- Mock build succeeded with 733 warnings and 0 errors; Mock tests passed 25/25, failed 0, skipped 0 in 17.611 seconds. Mock smoke assertions prove registration/order only; they do not prove SQLite scheduling, native UI behavior, or an awaited drain.
- Schema and migrations were unchanged. DB-1 does not prove an awaited plugin drain or in-game performance improvement; synchronous `Dispose` stops admission and starts one retained `CompleteAsync` after producers stop.

After all four tasks pass their per-task spec and quality reviews:

1. Generate a whole-branch review package from merge base `d799e53` through `HEAD`.
2. Run one independent final code review focused on concurrency, capacity accounting, coalescing races, transaction boundaries, retry idempotency, post-commit publication, static runtime cleanup, and preservation of capability-rule behavior.
3. Fix Critical/Important findings through the bounded SDD fix loop and rerun only the covering tests before the final full validation.
4. Push the validated branch and open a focused pull request to `v4-series` referencing Issue 258.
5. Stop at a review-ready build/PR. DB-1 has no release path and no permission to merge.
