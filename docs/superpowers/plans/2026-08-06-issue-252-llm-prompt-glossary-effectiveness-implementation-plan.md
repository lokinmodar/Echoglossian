# Issue 252 LLM prompt and glossary effectiveness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ensure every LLM Prompt Save/Reset actually persists and rebuilds the active translator, make glossary refresh non-blocking, and protect configured exact terms deterministically without silently rewriting existing DB translations.

**Architecture:** Prompt editor actions return an explicit changed result to the existing configuration-window aggregation. Configuration snapshots are shallow immutable copies of scalar/string/value-type fields and are serialized by a single latest-wins background persistence coordinator. Runtime refresh remains framework-thread-owned and reacts to the live config signature. Glossaries load asynchronously into an immutable snapshot, and a shared longest-match protector replaces exact source terms with opaque markers before LLM translation, validates them, then restores configured targets.

**Tech Stack:** C#/.NET 10, Dalamud ImGui, `Channel<T>`/tasks, existing prompt managers and runtime signatures, async file I/O, immutable records, xUnit, resx, and PowerShell.

## Global Constraints

- Branch from the merged #214 result as `issue-252-llm-prompt-glossary-effectiveness`.
- Cover ChatGPT/OpenAI-compatible, Gemini, DeepSeek, OpenRouter, Ollama, LM Studio, and Claude prompt panels; include any other panel that currently uses `PromptEditorUI`.
- Do not delete or mass-invalidate existing translation rows when a prompt or glossary changes.
- The existing explicit visible-surface retranslation remains the opt-in refresh path and must persist only a validated new result.
- Configuration/file persistence and glossary loads may not execute on the ImGui/framework callback.
- Runtime rebuild is requested from live config state and applied on the next framework tick; the background persistence copy is never used as mutable runtime config.
- The config snapshot contract is valid only while every persisted reference-type field is `string`; enforce this with a reflection test.
- Glossary publication is atomic: readers see the old complete snapshot or the new complete snapshot, never a mutable list being filled.
- Exact protection uses longest source term first, ordinal language-appropriate matching defined by tests, opaque unique markers, and strict marker-count validation.
- A provider response that loses/duplicates a protected marker is not persisted as a successful translation.
- Never log API keys, complete prompts, glossary contents, or translated dialogue by default.

---

## File map

### New files

- `GeneralHelpers/ConfigurationSaveCoordinator.cs` — serialized latest-wins background persistence for immutable config snapshots.
- `Translators/Helpers/DialogueGlossaryTermProtector.cs` — deterministic protect/validate/restore contract.
- `Echoglossian.Tests/ConfigurationSaveCoordinatorTests.cs` — non-blocking, coalescing, snapshot, and exception tests.
- `Echoglossian.Tests/DialogueGlossaryTermProtectorTests.cs` — overlap, unchanged-target, marker, and invalid-response tests.

### Modified files

- `Config.cs` — safe persistence snapshot method and later issue config fields.
- `GeneralHelpers/Utils.cs` — queue persistence for the active runtime and mark runtime dirty without waiting for disk.
- `Echoglossian.cs` — own and shut down the configuration save coordinator.
- `PluginUI/Components/PromptEditorUI.cs` — return true only after successful Save/Reset mutation.
- LLM files under `PluginUI/EngineConfigUI/*EngineUI.cs` that call `PromptEditorUI.Draw` — aggregate the returned change and remove duplicate direct saves.
- `PluginUI/EngineConfigUI/LiveModelRefreshCoordinator.cs` — own refresh tasks, cancellation, and exception observation.
- `PluginUI/PluginConfigWindowRenderer.cs` — keep one aggregated save signal per draw.
- `GeneralHelpers/RuntimeConfigurationRefresh.cs` — asynchronous glossary request and existing prompt-signature rebuild contract.
- `Translators/Helpers/StructuredDialogueGlossaryLoader.cs` — cancellation-aware async file loading.
- `Translators/StructuredDialogueGlossaryStore.cs` — immutable snapshot and async latest-request publication.
- `PluginUI/Tabs/TranslationEnginesTab.cs` — async Reload/Clear controls and visible retranslation guidance.
- `Translators/TranslationService.cs` — protect and restore glossary terms around LLM dialogue translation.
- `Translators/TranslatorMetricsCollector.cs` — rejected-protected-marker outcome.
- `Echoglossian.Tests/RuntimeConfigurationRefreshContractTests.cs`.
- `Echoglossian.Tests/LiveModelRefreshCoordinatorTests.cs`.
- `Echoglossian.Tests/StructuredDialogueGlossaryLoaderTests.cs`.
- `Echoglossian.Tests/StructuredDialogueGlossaryStoreTests.cs`.
- `Echoglossian.Tests/TranslationServiceTests.cs`.
- `Properties/Resources.resx`, `Properties/Resources.en-US.resx`, `Properties/Resources.pt-BR.resx`, and generated `Properties/Resources.Designer.cs` — status/guidance strings; update other localized resx files according to repository localization convention.

## Interfaces produced and consumed

```csharp
public sealed class ConfigurationSaveCoordinator : IAsyncDisposable
{
    public ConfigurationSaveCoordinator(
        Func<Config, CancellationToken, Task> persistAsync,
        Action<Exception>? errorObserver = null);

    public void QueueSave(Config snapshot);
    public Task CompleteAsync(CancellationToken cancellationToken = default);
}
```

`Config.CreatePersistenceSnapshot()` returns a shallow copy after normalization. A reflection test fails if a future persisted field introduces a mutable reference type without updating snapshot logic.

```csharp
public static bool PromptEditorUI.Draw(
    PromptTemplateManager templateManager,
    Echoglossian.PromptType type,
    string defaultPrompt,
    string label);
```

```csharp
public readonly record struct ProtectedDialogueText(
    string Text,
    IReadOnlyList<ProtectedGlossaryMarker> Markers);

public static ProtectedDialogueText Protect(
    string sourceText,
    IReadOnlyList<StructuredDialogueGlossaryEntry> entries);

public static bool TryRestore(
    string providerText,
    ProtectedDialogueText protectedText,
    out string restoredText,
    out string? failureReason);
```

## Task 1: Make Prompt Save/Reset observable by every LLM panel

- [ ] Add pure contract tests or source-contract tests proving a valid Save and Reset return `true`, while invalid Save and idle draw return `false`.
- [ ] Change `PromptEditorUI.Draw` to accumulate and return `changed`; return true only after `SetPrompt` actually updates the config-backed prompt.
- [ ] Change every caller to `changed |= PromptEditorUI.Draw(...)`.
- [ ] Remove per-engine `Echoglossian.SaveConfig(config)` blocks from LLM panels so `PluginConfigWindowRenderer` emits one aggregate save. Preserve field validation side effects before returning.
- [ ] Add/extend `RuntimeConfigurationRefreshContractTests` to assert all prompt properties appear in `ComputeTranslationRuntimeSignature` and that changing each one requests exactly one translator rebuild on the next framework tick.
- [ ] Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PromptEditor|FullyQualifiedName~RuntimeConfigurationRefreshContractTests"
```

Expected result before implementation: prompt-only Save is invisible to the enclosing changed aggregation.

- [ ] Commit:

```powershell
git add -- PluginUI/Components/PromptEditorUI.cs PluginUI/EngineConfigUI GeneralHelpers/RuntimeConfigurationRefresh.cs Echoglossian.Tests
git commit -m "fix(#252): persist prompt editor actions"
```

## Task 2: Move active configuration persistence off ImGui

- [ ] Add `ConfigurationSaveCoordinatorTests` with a suspended persistence delegate. Assert `QueueSave` returns immediately, multiple queued snapshots coalesce to the latest pending snapshot, unexpected exceptions are observed, and `CompleteAsync` flushes the last accepted snapshot.
- [ ] Extend `LiveModelRefreshCoordinatorTests` with a suspended refresh, an exception, a newer signature arriving in flight, and shutdown cancellation. Assert Draw-facing request methods return immediately and every exception is observed once.
- [ ] Add a reflection test over public instance fields in `Config`: after excluding `[NonSerialized]`, every reference-typed field must be `string`. This protects shallow snapshot safety.
- [ ] Implement `Config.CreatePersistenceSnapshot()` with `MemberwiseClone`; normalize the live object before cloning, not after enqueue.
- [ ] Implement a single-reader bounded channel with capacity one and latest-wins behavior. The pump awaits the injected persistence delegate with `ConfigureAwait(false)` and owns all exceptions.
- [ ] Make `LiveModelRefreshCoordinator` own its refresh tasks/tokens, catch and report unexpected exceptions, and expose a plugin-shutdown reset that cancels in-flight model discovery without waiting on the framework thread.
- [ ] Construct the coordinator during plugin startup with a delegate that invokes `PluginInterface.SavePluginConfig(snapshot)` on a worker. `SaveConfig` must still honor `PluginConfigSaveScope` for tests/preview hosts; only the active real plugin path queues persistence.
- [ ] Call `OnConfigurationSaved` immediately with the live config so runtime refresh occurs on the next framework tick. Never pass the clone to runtime rebuild logic.
- [ ] On plugin disposal, stop accepting saves and start a bounded flush before service teardown. If the synchronous Dalamud disposal contract cannot await, retain the coordinator task until its final save completes and document the narrow unload-only lifetime; do not call `.Wait()` on a live UI frame.
- [ ] Run coordinator, save-scope, startup, and shutdown tests.
- [ ] Commit:

```powershell
git add -- Config.cs GeneralHelpers/ConfigurationSaveCoordinator.cs GeneralHelpers/Utils.cs Echoglossian.cs PluginUI/EngineConfigUI/LiveModelRefreshCoordinator.cs Echoglossian.Tests/ConfigurationSaveCoordinatorTests.cs Echoglossian.Tests/PluginConfigSaveScopeTests.cs Echoglossian.Tests/LiveModelRefreshCoordinatorTests.cs Echoglossian.Mock.Tests
git commit -m "refactor(#252): persist config outside the UI callback"
```

## Task 3: Load and publish glossary snapshots asynchronously

- [ ] Add loader tests using a temporary file and cancellation token. Add a store test that suspends load A, completes newer load B first, then completes A and asserts B remains published.
- [ ] Add `LoadFromFileAsync(string, CancellationToken)` using async file I/O; keep parsing pure and shared with existing tests.
- [ ] Replace mutable `List` state with one immutable internal state record containing entries and status. Publish with `Volatile.Write` or one short lock after the complete result is built.
- [ ] Add `RefreshAsync(string?, CancellationToken)` and a monotonically increasing request generation. A stale load may complete but cannot replace a newer snapshot.
- [ ] Change the UI Reload button and runtime-signature refresh to start an owned async refresh and return immediately. UI status reads only `GetSnapshot()`.
- [ ] Ensure Clear increments the generation so an older in-flight load cannot repopulate after Clear.
- [ ] Run glossary loader/store tests and commit:

```powershell
git add -- Translators/Helpers/StructuredDialogueGlossaryLoader.cs Translators/StructuredDialogueGlossaryStore.cs PluginUI/Tabs/TranslationEnginesTab.cs GeneralHelpers/RuntimeConfigurationRefresh.cs Echoglossian.Tests/StructuredDialogueGlossaryLoaderTests.cs Echoglossian.Tests/StructuredDialogueGlossaryStoreTests.cs
git commit -m "fix(#252): refresh dialogue glossary asynchronously"
```

## Task 4: Add deterministic exact-term protection

- [ ] Write tests for `Scions`, `The Order of the Twin Adder`, overlapping terms (`Twin Adder` versus the full phrase), repeated terms, punctuation, same source/target, case behavior, and terms absent from the source.
- [ ] Assert longest-match-first replacement, one unique marker per protected occurrence, and restoration to the configured target text.
- [ ] Add negative tests where the provider removes, duplicates, edits, or invents markers; `TryRestore` must fail with a stable non-secret reason.
- [ ] Implement markers that cannot collide with source input. If source contains the marker prefix, derive a different nonce for that request.
- [ ] In `TranslationService` dialogue LLM path, read one glossary snapshot, protect the current source text, send the protected text plus existing glossary contract, validate/restore provider output, and only then run `TranslationResultGuard`/persistence acceptance.
- [ ] Do not protect non-LLM engines or non-dialogue surfaces in this issue. Do not alter prior history text.
- [ ] Record aggregate success/failure counters without terms or prompts.
- [ ] Run protector, service, structured request, and metrics tests.
- [ ] Commit:

```powershell
git add -- Translators/Helpers/DialogueGlossaryTermProtector.cs Translators/TranslationService.cs Translators/TranslatorMetricsCollector.cs Echoglossian.Tests/DialogueGlossaryTermProtectorTests.cs Echoglossian.Tests/TranslationServiceTests.cs Echoglossian.Tests/TranslatorMetricsCollectorTests.cs
git commit -m "feat(#252): protect exact dialogue glossary terms"
```

## Task 5: Make refresh semantics explicit to the operator

- [ ] Add resx text explaining that prompt/glossary changes affect new requests and do not automatically erase stored translations; point to the existing explicit “Retranslate Visible Dialogue And Persist” action.
- [ ] Add a unit/source contract proving prompt/glossary Save does not delete DB rows or toggle `TranslateAlreadyTranslatedTexts`.
- [ ] Verify explicit retranslation bypasses the stored row, validates protected markers, persists the replacement, and stays asynchronous under a suspended provider.
- [ ] Regenerate `Resources.Designer.cs` with the repository's normal resx build path; do not hand-edit generated properties unless that is the established workflow.
- [ ] Commit:

```powershell
git add -- PluginUI/Tabs/TranslationEnginesTab.cs Properties Echoglossian.Tests
git commit -m "docs(#252): clarify prompt and glossary refresh behavior"
```

## Task 6: Validate and close #252

- [ ] Run focused tests, full build/tests, Mock tests, `git diff --check`, and commit `Echoglossian.xml` if changed.
- [ ] In game with Gemini Talk Swap, save a visibly different valid prompt and confirm the next uncached request uses it after the framework-tick rebuild. Reset and confirm the default returns.
- [ ] Load a glossary containing both reported terms; verify exact preservation in native, overlay-only, and swap modes. Deliberately slow file I/O/provider responses and confirm config/game UI remains responsive.
- [ ] Verify a pre-existing DB translation remains until explicit visible retranslation is requested, then verify the refreshed row is persisted.
- [ ] Attach sanitized evidence to #252 and open the PR to `v4-series`.
