# Issue 203 no-translation engine matrix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the old mixed “no translation” report with current-version, engine-by-engine evidence and fix any shared activation/routing defect that prevents configured Google, Gemini, DeepL, or Yandex translation from reaching the visible surface.

**Architecture:** Build a deterministic matrix across engine configuration, target-language support, effective surface routing, display mode, request outcome, and publication. A content-free async health probe verifies provider reachability separately from addon presentation. Pure tests cover catalog/routing/activation, while current in-game checks prove actual surface capture/application. DeepL-specific defects remain isolated from the multi-engine matrix and are not conflated with #212.

**Tech Stack:** C#/.NET 10, existing language-support/engine-selection/activation/routing helpers, `TranslationService`, translator metrics/diagnostics, ImGui/resx, xUnit fakes, Mock/DalaMock, GitHub evidence, and PowerShell.

## Global Constraints

- Branch from the merged #171 result as `issue-203-no-translation-matrix`.
- Reproduce against the current plugin build; the issue's historical version is evidence context, not a regression oracle.
- Test one engine at a time with explicit source, target, surface, and display mode. Do not infer all engines are broken from one provider failure.
- A provider health result and a UI publication result are separate dimensions.
- Health probes are explicit, asynchronous, cancellable, rate-limited, and never launched per frame.
- Never send real dialogue in a health probe; use a short localized fixed test phrase disclosed in the UI.
- Do not log credentials or full provider responses.
- Do not broaden into DeepL feature work from #212; file/link a separate current defect if DeepL alone fails after shared fixes.
- Close #203 only with a completed current-version matrix and either tested fixes or evidence that no current defect remains.

---

## File map

### New files

- `Translators/TranslationEngineRuntimeDescriptor.cs` — immutable engine/config/support diagnostic descriptor.
- `Translators/TranslationEngineRuntimeMatrixBuilder.cs` — pure current-config matrix rows.
- `Translators/TranslationEngineHealthProbe.cs` — explicit async provider probe through `TranslationService` with sanitized result.
- `Echoglossian.Tests/TranslationEngineRuntimeMatrixTests.cs`.
- `Echoglossian.Tests/TranslationEngineHealthProbeTests.cs`.
- `docs/testing/llm-engine-surface-matrix.md` — reproducible current-version operator checklist and result table.

### Modified files

- `LanguagesHandling/LanguageEngineSupport.cs` and `LanguagesDictionary.cs` only if matrix tests expose a current mapping error.
- `GeneralHelpers/TranslationEngineSelectionMigrationHelper.cs`.
- `GeneralHelpers/TranslationActivationGuard.cs`.
- `Translators/LlmSurfaceGroupRoutingPolicy.cs`.
- `PluginUI/Helpers/TranslationEngineConfigurationHelper.cs`.
- `Translators/TranslationService.cs` only for a matrix-proven resolver defect.
- `PluginUI/TranslatorMetricsWindow.cs` or `PluginUI/Tabs/TroubleshootingTab.cs` — explicit health probe and matrix snapshot.
- Existing selection, activation, language support, routing, service, and Mock tests.
- `Properties/Resources.resx`, `Properties/Resources.en-US.resx`, `Properties/Resources.pt-BR.resx`, and `Properties/Resources.Designer.cs`.

## Matrix row contract

```csharp
public readonly record struct TranslationEngineRuntimeDescriptor(
    Echoglossian.TransEngines SelectedEngine,
    Echoglossian.TransEngines EffectiveEngine,
    TranslationSurfaceGroup SurfaceGroup,
    string SourceLanguage,
    string TargetLanguage,
    JournalTranslationDisplayMode DisplayMode,
    bool LanguageSupportsEngine,
    bool ConfigurationComplete,
    string? BlockReason);
```

The documentation table includes at minimum:

| Engine | Surface | Mode | Config complete | Request sent | Provider accepted | Result accepted | Published | Outcome |
|---|---|---|---:|---:|---:|---:|---:|---|
| Google | Talk | Native/Overlay/Swap | | | | | | |
| Gemini | Talk | Native/Overlay/Swap | | | | | | |
| DeepL | Talk | Native/Overlay/Swap | | | | | | |
| Yandex Public/Cloud as configured | Talk | Native/Overlay/Swap | | | | | | |

Repeat a representative non-dialogue surface to distinguish dialogue override from global routing.

## Task 1: Centralize a read-only runtime descriptor

- [ ] Add tests covering every concrete engine enum, unsupported `All`, persisted legacy id/key normalization, Turkish target support for the reported engines, missing credentials/endpoints, global engine versus dialogue override, and all three presentation modes.
- [ ] Implement a pure matrix builder by composing existing `LanguageEngineSupport`, `TranslationEngineSelectionMigrationHelper`, `TranslationActivationGuard`, `TranslationEngineConfigurationHelper`, and `LlmSurfaceGroupRoutingPolicy`. Do not duplicate their business rules.
- [ ] Make missing/unsupported dimensions explicit rather than returning a generic unavailable row.
- [ ] Fix only deterministic mapping/normalization defects exposed by these failing tests. If no mapping defect exists, commit the descriptor/tests without speculative behavior change.
- [ ] Commit:

```powershell
git add -- Translators/TranslationEngineRuntimeDescriptor.cs Translators/TranslationEngineRuntimeMatrixBuilder.cs LanguagesHandling GeneralHelpers PluginUI/Helpers/TranslationEngineConfigurationHelper.cs Translators/LlmSurfaceGroupRoutingPolicy.cs Echoglossian.Tests/TranslationEngineRuntimeMatrixTests.cs
git commit -m "test(#203): define current engine routing matrix"
```

## Task 2: Add an explicit asynchronous provider health probe

- [ ] Write tests with suspended, successful, auth-failed, rate-limited, unavailable, empty, and cancelled fake translators. Assert starting the probe returns control, concurrent clicks deduplicate per engine, timeout is bounded, and results contain no request/provider body.
- [ ] Implement the probe through `TranslationService.TranslateAsync` with a fixed short source phrase, captured source/target/effective engine, timeout token, and `TranslationSurfaceGroup.Default`. Bypass DB persistence and content-failure persistence for probe origin.
- [ ] Return a sanitized result separating configuration, transport/provider, acceptance, elapsed time, and cancellation.
- [ ] Add a localized explicit button/status to troubleshooting/metrics UI. Draw only reads the last immutable result and never waits or polls a task with `.Result`.
- [ ] Commit:

```powershell
git add -- Translators/TranslationEngineHealthProbe.cs PluginUI/TranslatorMetricsWindow.cs PluginUI/Tabs/TroubleshootingTab.cs Properties Echoglossian.Tests/TranslationEngineHealthProbeTests.cs
git commit -m "feat(#203): add non-blocking engine health probe"
```

## Task 3: Prove request-to-publication routing

- [ ] Extend `TranslationServiceTests` with one fake translator per reported engine id and assert the resolved engine receives the request for Default and Dialogue surfaces under global and override configurations.
- [ ] Extend lifecycle/Mock tests to drive a managed Talk capture where feasible and assert: request scheduled, accepted result stored as managed state, overlay-only never mutates native state, native applies on a later live callback, and swap shows translated native/original overlay.
- [ ] Add stale source/config tests so a successful health/provider result cannot publish to a superseded surface.
- [ ] If DalaMock cannot drive the necessary Talk payload, retain pure publication tests and document the exact in-game gap; do not claim startup-only coverage.
- [ ] Fix any failing shared route/activation/publication contract in its existing helper; do not add engine-specific branches in the handler.
- [ ] Commit:

```powershell
git add -- Translators/TranslationService.cs NativeUI/AddonHandlers/Talk Echoglossian.Tests Echoglossian.Mock.Tests
git commit -m "fix(#203): preserve engine results through publication"
```

## Task 4: Execute and record the current-version matrix

- [ ] Write `docs/testing/llm-engine-surface-matrix.md` with prerequisites, credential redaction, fixed source/target phrase, cache/DB controls, mode steps, expected log/metric fields, responsiveness check, and the result table.
- [ ] Run Google, Gemini, DeepL, and the configured Yandex variants from English to Turkish on Talk in Native, Overlay-only, and Swap. Run one representative non-dialogue surface for each engine.
- [ ] For each failure, locate the first false dimension: unsupported/config/activation, request not sent, provider rejected, accepted-result guard, stale publication, or presentation. Add a failing automated test before changing code.
- [ ] When a defect is provider-specific and independent, keep #203 evidence but open/link a focused issue rather than mixing a broad provider rewrite. DeepL-only findings explicitly remain outside #212 unless that issue's current scope exactly matches.
- [ ] Commit the completed redacted matrix:

```powershell
git add -- docs/testing/llm-engine-surface-matrix.md
git commit -m "docs(#203): record current engine verification matrix"
```

## Task 5: Validate, update tracker, and close #203

- [ ] Run matrix/health/selection/activation/routing/service tests, full build/tests, Mock tests, `git diff --check`, and include `Echoglossian.xml` if changed.
- [ ] Delay each provider/probe and verify the game UI remains responsive; rapidly change engine/target/surface and verify stale completions are discarded.
- [ ] Attach the completed current-version table, plugin commit/version, sanitized failure classifications, and linked provider-specific follow-ups to #203.
- [ ] Update tracker #12 with links to all seven issue outcomes and leave #239 as dependency status only.
- [ ] Open the #203 PR to `v4-series`; close the issue only after the matrix has no unexplained “no translation” row.
