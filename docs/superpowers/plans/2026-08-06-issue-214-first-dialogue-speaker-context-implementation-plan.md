# Issue 214 first-dialogue speaker context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the first Talk/BattleTalk line use its visible speaker and active glossary, while moving dialogue DB/provider work completely off the game UI path with owned cancellation-safe publication.

**Architecture:** A captured dialogue context marks the request as dialogue even when it has no prior turns. `TranslationService` routes that request to the existing dialogue-aware translator and includes current-speaker identity in its cache key. Talk handlers synchronously capture only managed strings and an operation generation, then an owned async operation performs EF lookup, translation, and persistence. Completion publishes managed state through `SourcePublicationLifecycle`; the next live framework callback performs any native presentation.

**Tech Stack:** C#/.NET 10, EF Core async queries, existing `TranslationService`, `DialogueTranslationSessionStore`, `SourcePublicationLifecycle`, xUnit, DalaMock/Echoglossian.Mock, and PowerShell.

## Global Constraints

- Branch from current `v4-series` as `issue-214-first-dialogue-speaker-context`.
- Do not introduce a second translation broker, queue, or cache.
- Do not persist runtime dialogue history or context-dependent results to the canonical translation tables.
- The first line must use the original visible speaker metadata even when speaker-name translation is disabled.
- A `DialogueTranslationContext` is usable because it identifies a dialogue request; prior turns are optional.
- Include current speaker in context cache identity so the same text spoken by different speakers cannot collide.
- Remove synchronous Talk/BattleTalk DB lookups from addon callbacks.
- Never move an `Atk*` pointer, span, or borrowed `SeString` buffer into an async closure.
- Cancellation stops publication immediately. Provider work that cannot accept a token is bounded with `WaitAsync` and may finish only as an observed discarded task.
- Keep logs quiet for ordinary cancellation and stale-generation rejection.
- Preserve overlay-only, native, and swap presentation rules.

---

## File map

### New files

- `NativeUI/Helpers/OwnedAsyncOperationSet.cs` — lifecycle owner for started tasks; observes exceptions and cancels on disposal without scheduling or pacing requests.
- `Echoglossian.Tests/OwnedAsyncOperationSetTests.cs` — suspended-operation, cancellation, and exception-observation tests.

### Modified files

- `Translators/Helpers/DialogueContextPromptHelper.cs` — dialogue request usability, current-speaker prompt metadata, and cache identity.
- `Translators/TranslationService.cs` — first-turn dialogue-aware routing and cancellation overloads.
- `Translators/DialogueTranslationSessionStore.cs` — retain the current managed speaker/source contract unchanged while accepting configured bounds later.
- `DBHelpers/DbOperations.cs` — async Talk/BattleTalk lookup methods preserving current ordering/language semantics.
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs` — remove refresh-time DB access, use async lookup and owned operation.
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs` — use async lookup and owned operation.
- `NativeUI/Helpers/AddonHandlerWiring.cs` — inject async lookup delegates.
- `Echoglossian.Tests/DialogueContextPromptHelperTests.cs` — first-turn speaker/cache regressions.
- `Echoglossian.Tests/TranslationServiceTests.cs` — first-turn routing and cancellation.
- `Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs` — callback responsiveness and stale-publication tests.
- `Echoglossian.Tests/DbOperationsTests.cs` — async Talk/BattleTalk lookup parity tests.
- `Echoglossian.Mock.Tests` dialogue lifecycle fixture, if the current harness can drive Talk/BattleTalk refresh payloads.

## Interfaces produced and consumed

`DialogueContextPromptHelper` continues to expose its existing API, with these semantics:

```csharp
public static bool HasUsableDialogueContext(
    DialogueTranslationContext dialogueContext);

public static string BuildDialogueContextCacheKey(
    string text,
    string sourceLanguage,
    string targetLanguage,
    DialogueTranslationContext dialogueContext);
```

`HasUsableDialogueContext` returns true for a captured dialogue request even when `PriorTurns` is empty. The cache-key payload includes `CurrentSpeaker = dialogueContext.SpeakerName` in addition to prior turns.

Add cancellation-capable `TranslationService` overloads without removing existing callers:

```csharp
public Task<string> TranslateAsync(
    string text,
    SourceClientLanguage sourceLanguage,
    string targetLanguage,
    DialogueTranslationContext dialogueContext,
    TranslationSurfaceGroup surfaceGroup,
    TranslatorResolution translatorResolution,
    string? originContext,
    CancellationToken cancellationToken);
```

Add DB delegates with semantic parity:

```csharp
public Task<TalkMessage?> FindAndReturnTalkMessageAsync(
    TalkMessage talkMessage,
    CancellationToken cancellationToken);

public Task<BattleTalkMessage?> FindAndReturnBattleTalkMessageAsync(
    BattleTalkMessage battleTalkMessage,
    CancellationToken cancellationToken);
```

The task owner is not a queue:

```csharp
public sealed class OwnedAsyncOperationSet : IDisposable
{
    public void Run(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
```

## Task 1: Regress and fix first-turn dialogue routing

**Files:**

- Modify: `Echoglossian.Tests/DialogueContextPromptHelperTests.cs`.
- Modify: `Echoglossian.Tests/TranslationServiceTests.cs`.
- Modify: `Translators/Helpers/DialogueContextPromptHelper.cs`.
- Modify: `Translators/TranslationService.cs`.

- [ ] Write a helper test whose context has `SpeakerName = "Alphinaud"` and `PriorTurns = []`; assert `HasUsableDialogueContext` is true, `AppendDialogueContext` contains `Current speaker: Alphinaud`, and its cache key differs from an otherwise identical `Alisaie` request.
- [ ] Write a `TranslationService` test using a fake `IDialogueContextAwareTranslator`; pass a first-turn context and assert the context-aware overload is called exactly once while the plain overload is not called.
- [ ] Add an anonymous first-turn test (`SpeakerName = string.Empty`, no history) and assert it still uses the context-aware route, because the captured Talk namespace/session is what allows glossary/provider dialogue contracts.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DialogueContextPromptHelperTests|FullyQualifiedName~TranslationServiceTests"
```

Expected result: the new tests fail because current code requires `PriorTurns.Count > 0` and omits current speaker from the cache key.

- [ ] Change `WillUseDialogueContext` to require only a present captured context plus `IDialogueContextAwareTranslator`. Change the helper to treat that captured context as usable, append current-speaker metadata even with no history, and serialize `CurrentSpeaker` into the context cache key.
- [ ] Keep prior history labeled as consistency context and continue instructing the model to translate only the current text.
- [ ] Re-run the focused tests and confirm all pass.
- [ ] Commit:

```powershell
git add -- Translators/Helpers/DialogueContextPromptHelper.cs Translators/TranslationService.cs Echoglossian.Tests/DialogueContextPromptHelperTests.cs Echoglossian.Tests/TranslationServiceTests.cs
git commit -m "fix(#214): use speaker context on first dialogue line"
```

## Task 2: Add cancellation at the shared async translation boundary

**Files:**

- Modify: `Translators/TranslationService.cs`.
- Modify: `Echoglossian.Tests/TranslationServiceTests.cs`.

- [ ] Add a fake translator whose `TranslateAsync` returns a suspended `TaskCompletionSource<string?>`. Call the new cancellation overload, cancel its token, and assert the returned task cancels without accepting the later provider result.
- [ ] Add overloads that thread `CancellationToken` through `TranslationService` and apply `WaitAsync(cancellationToken)` to existing translator tasks. Keep all existing overloads delegating with `CancellationToken.None` for compatibility.
- [ ] Add `ConfigureAwait(false)` to every new non-UI await. Do not call the synchronous `ITranslator.Translate` path.
- [ ] Run the `TranslationServiceTests` filter and confirm cancellation and existing metrics/failure tests pass.
- [ ] Commit:

```powershell
git add -- Translators/TranslationService.cs Echoglossian.Tests/TranslationServiceTests.cs
git commit -m "refactor(#214): add cancellable async translation boundary"
```

## Task 3: Add async DB lookup parity

**Files:**

- Modify: `DBHelpers/DbOperations.cs`.
- Modify: `Echoglossian.Tests/DbOperationsTests.cs`.

- [ ] Seed multiple Talk and BattleTalk rows that differ by source-language equivalence, engine, target, and update time. Assert the current synchronous method's selected row, then assert the new async method selects the same row.
- [ ] Add cancellation tests with an already-cancelled token.
- [ ] Implement async candidate materialization with `ToListAsync(cancellationToken)`, then reuse the existing in-memory language-equivalence filter and `OrderTalkMessageLookupQuery`/`OrderBattleTalkMessageLookupQuery` ordering. Use a fresh EF context per call.
- [ ] Do not update the static `FoundTalkMessage`/`FoundBattleTalkMessage` diagnostic fields from a UI callback; if retained, update them only after the async query completes.
- [ ] Run the focused DB tests and confirm sync/async parity.
- [ ] Commit:

```powershell
git add -- DBHelpers/DbOperations.cs Echoglossian.Tests/DbOperationsTests.cs
git commit -m "refactor(#214): make dialogue lookups asynchronous"
```

## Task 4: Own every dialogue background operation

**Files:**

- Create: `NativeUI/Helpers/OwnedAsyncOperationSet.cs`.
- Create: `Echoglossian.Tests/OwnedAsyncOperationSetTests.cs`.

- [ ] Write tests proving `Run` returns before a suspended operation completes, disposal cancels the operation token, and an injected error observer receives exactly one unexpected exception.
- [ ] Implement a lock-protected or concurrent set of active tasks, a shutdown `CancellationTokenSource`, linked tokens per operation, and a private async observer that removes completed tasks. Swallow ordinary `OperationCanceledException`; route unexpected exceptions through the injected logger.
- [ ] Do not add pacing, cache keys, retries, or result storage. Those features belong to existing translation infrastructure.
- [ ] Dispose by cancelling shutdown and preventing new work. Do not synchronously wait on active tasks from a game callback.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~OwnedAsyncOperationSetTests"
```

- [ ] Commit:

```powershell
git add -- NativeUI/Helpers/OwnedAsyncOperationSet.cs Echoglossian.Tests/OwnedAsyncOperationSetTests.cs
git commit -m "feat(#214): own cancellable dialogue operations"
```

## Task 5: Remove synchronous DB and bare Task.Run from Talk

**Files:**

- Modify: `NativeUI/AddonHandlers/Talk/TalkHandler.cs`.
- Modify: `NativeUI/Helpers/AddonHandlerWiring.cs`.
- Modify: `Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs`.

- [ ] Replace the injected sync lookup with `Func<TalkMessage, CancellationToken, Task<TalkMessage?>>` and inject `FindAndReturnTalkMessageAsync` from wiring.
- [ ] Write a handler test with a suspended DB delegate. Drive the managed capture/queue seam and assert the callback returns, `translationInFlight` is set, and no translated state publishes until the DB task completes.
- [ ] Add a stale-generation test: capture line A, suspend lookup/provider, capture line B, complete A, and assert A never updates overlay or native-ready managed state.
- [ ] Delete `TryLoadStoredTranslation` from the refresh path. The refresh callback may reuse only already-published in-memory state; all persistent lookup happens inside `ResolveTranslationAsync`.
- [ ] Replace both bare `Task.Run(() => ResolveTranslationAsync(...))` call sites with `OwnedAsyncOperationSet.Run(token => ResolveTranslationAsync(..., token), sourceOperation.CancellationToken)`.
- [ ] Pass the operation token to async lookup, translation, second-chance DB lookup, and insert. Check `SourcePublicationLifecycle.IsCurrent` before side effects and use `TryPublish` for final managed state.
- [ ] Do not publish overlays from a worker if the overlay implementation touches ImGui/native state. If `PublishOverlay` is not managed-snapshot-only, store the result and let the next framework draw consume it.
- [ ] Dispose the task owner from `OnPluginUnload`/handler disposal without waiting.
- [ ] Run the Talk lifecycle and owned-operation tests.
- [ ] Commit:

```powershell
git add -- NativeUI/AddonHandlers/Talk/TalkHandler.cs NativeUI/Helpers/AddonHandlerWiring.cs Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs
git commit -m "fix(#214): keep Talk database work off callbacks"
```

## Task 6: Apply the same async contract to BattleTalk

**Files:**

- Modify: `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`.
- Modify: `NativeUI/Helpers/AddonHandlerWiring.cs`.
- Modify: `Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs`.

- [ ] Inject `Func<BattleTalkMessage, CancellationToken, Task<BattleTalkMessage?>>` and use the same handler-owned operation set.
- [ ] Add suspended-DB and stale-source tests equivalent to Talk, including speaker-name translation failure continuing with translated body text.
- [ ] Replace all bare background starts in the BattleTalk translation path. Preserve unrelated diagnostic/export work only if it is already owned and observed; otherwise route it through the same non-queue task owner.
- [ ] Pass the captured cancellation token through DB, body translation, optional speaker translation, insert, and publication.
- [ ] Confirm no unsafe method or pointer appears in the operation lambda or any method reachable after its first await.
- [ ] Run the focused lifecycle suite and commit:

```powershell
git add -- NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs NativeUI/Helpers/AddonHandlerWiring.cs Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs
git commit -m "fix(#214): keep BattleTalk work off callbacks"
```

## Task 7: Validate responsiveness and close #214

- [ ] Run repository searches:

```powershell
rg -n "TryLoadStoredTranslation|Task\.Run|\.Result|\.Wait\(|GetAwaiter\(\)\.GetResult\(\)" NativeUI\AddonHandlers\Talk Translators\TranslationService.cs
rg -n "Atk(UnitBase|Value|ResNode|TextNode)|unsafe|Span<" NativeUI\AddonHandlers\Talk
```

Expected result: no synchronous wait or refresh-time DB helper remains in Talk/BattleTalk async paths; any unsafe code is confined to capture/apply callbacks before async scheduling.

- [ ] Run focused tests, full build, full unit tests, and Mock tests from the program protocol.
- [ ] In game, delay DB/provider responses and verify Talk/BattleTalk animations, input, and window repaint remain responsive. Verify line 1 sends the visible speaker and glossary, line 2 includes bounded prior history, rapid line changes discard stale completions, and plugin unload produces no unobserved exception.
- [ ] Verify native, overlay-only, and swap modes independently. Native pointers must be re-read on the framework callback; no worker mutates a node.
- [ ] Attach sanitized prompt/request evidence and responsiveness results to #214, without logging dialogue contents by default.
- [ ] Run `git diff --check`, commit generated `Echoglossian.xml` if changed, and open the issue PR to `v4-series`.
