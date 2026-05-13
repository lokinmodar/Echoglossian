# LLM Translation Rework Iteration Log

This document tracks the implementation history of the LLM translation rework
branch in short, validated iterations.

Each iteration should record:

- scope and goal
- files or subsystems touched
- behavior-sensitive risks
- validation performed
- next intended cut

The purpose is to keep architecture work inspectable and prevent the branch from
turning into an opaque pile of partial changes.

## Iteration 0 - Scope Lock and Design Baseline

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - lock the first-pass product and architecture decisions before touching the
    runtime
  - make the rework traceable through an explicit iteration log
- Inputs folded into the design baseline:
  - `#201` visible user feedback for LLM quota or endpoint failures
  - `#176` local-LLM latency and prompt overhead
  - `#196` custom OpenAI-compatible provider support
  - `#174` explicit inclusion of retranslation and DB semantics in the rework
- Design decisions already locked:
  - surface-group routing starts as **LLM-only override**
  - local compact prompts are **per-engine**
  - custom OpenAI-compatible support is an **OpenAI-family variant**
  - session-aware translations are **runtime-only and non-persistent**
  - metrics should surface in a **Translator Debugger and Metrics** command and
    window
  - first operator-facing retranslation control should be **explicit
    `retranslate visible text and persist`**
  - `BattleTalk` should reuse the `Talk` session infrastructure, but with
    isolated session state
- Validation:
  - none required for this baseline entry
- Next cut:
  - implement iteration 1 as a narrow `#201` slice:
    normalized LLM failure classification plus visible operator feedback

## Iteration 1 - LLM Failure Classification and Visible Feedback Foundation

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - stop known LLM failure texts from being accepted as valid translations
  - normalize failure categories for the runtime
  - surface actionable LLM runtime failures through deduplicated Dalamud
    notifications
- Files touched:
  - `GeneralHelpers/TranslationFailureTextClassifier.cs`
  - `GeneralHelpers/TranslationResultGuard.cs`
  - `GeneralHelpers/TranslationFailureNotifications.cs`
  - `Translators/TranslationService.cs`
  - `Echoglossian.cs`
  - `Echoglossian.Tests/TranslationFailureTextClassifierTests.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
- What changed:
  - added a shared classifier for:
    - unavailable-provider messages caused by missing credentials/config
    - quota/rate-limit failures
    - authentication failures
    - endpoint/connection failures
    - timeout failures
    - generic provider failures
  - made the shared translated-result guard reject known unavailable-provider
    messages in addition to synthetic `[Translation Error: ...]` placeholders
  - taught `TranslationService` to classify failed translator output and report
    actionable LLM issues to the runtime feedback path
  - added a deduplicated LLM runtime notification path in the plugin with an
    `Open Configuration` button
- Behavior-sensitive risks:
  - this is intentionally the first-pass notification foundation, not the full
    retry/cooldown rework yet
  - quota/endpoint failures are now visible, but transient retry pacing is
    still governed by the existing translator/runtime behavior
  - non-LLM engines are intentionally excluded from this first-pass operator
    feedback path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - add short-lived runtime suppression for repeated transient LLM failures so
    endpoint/quota outages do not keep re-requesting the same text every frame

## Iteration 2 - Runtime-Only Suppression For Repeated Transient LLM Failures

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - stop repeated identical LLM failures from being retried on every repaint
  - keep transient provider failures out of the DB while still letting the
    runtime short-circuit repeated requests for a while
- Files touched:
  - `Cache/TranslationFailureCacheManager.cs`
  - `GeneralHelpers/TranslationPersistenceGuard.cs`
  - `Translators/TranslationService.cs`
  - `Echoglossian.Tests/TranslationFailureCacheManagerTests.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a transient exact-failure cache path in
    `TranslationFailureCacheManager`
  - wired `TranslationService` to send non-persistent LLM/runtime failure
    reasons into that transient cache with a short TTL
  - made `TranslationFailureCacheManager.Contains(...)` honor both:
    - persistent DB-backed failure rows
    - runtime-only transient failures
  - marked `llm-*` failure reasons as non-persistent so they never become
    cross-session DB truth
- Behavior-sensitive risks:
  - this is an in-memory suppression layer only; it is intentionally not a DB
    migration and does not redefine the persistent failure model
  - the first TTL is fixed and conservative; tuning may still be needed after
    in-game validation
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - start isolating per-engine prompt compaction work, beginning with the local
    LLM family and the smallest prompt-builder extraction that does not fork
    the translation pipeline

## Iteration 3 - Local LLM Prompt Compaction Defaults

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - reduce avoidable prompt overhead for the first local LLM engines without
    refactoring the entire translator stack
  - keep custom per-engine prompts intact while making empty/default local
    prompts cheaper than the shared cloud-LLM template
- Files touched:
  - `PluginUI/Helpers/PromptTemplateManager.cs`
  - `GeneralHelpers/Utils.cs`
  - `Translators/LmStudioTranslator.cs`
  - `Translators/OllamaTranslator.cs`
  - `PluginUI/EngineConfigUI/LmStudioEngineUI.cs`
  - `PluginUI/EngineConfigUI/OllamaEngineUI.cs`
  - `Echoglossian.Tests/PromptTemplateManagerTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added compact built-in prompt defaults dedicated to:
    - `LM Studio`
    - `Ollama`
  - added `PromptTemplateManager.GetDefaultPrompt(...)` so engine-specific
    defaults stay centralized instead of hardcoded in each callsite
  - made `LM Studio` and `Ollama` runtime translators fall back to those
    compact defaults when their saved prompt is empty
  - aligned reset-to-default and config-reset behavior so local LLM engines
    keep their own compact defaults instead of being repopulated with the
    larger cloud-LLM template
- Behavior-sensitive risks:
  - this changes only the built-in default prompt path for local LLM engines;
    user-customized prompts are intentionally preserved
  - this does not yet add session reuse or request-shape telemetry; it is only
    the first prompt-overhead cut for `#176`
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - add the first pass of the `Translator Debugger and Metrics` runtime
    foundation, keeping metrics aggregated and out of the hot-path log

## Iteration 4 - Aggregated Translator Metrics Foundation

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - create the first runtime metrics foundation for the future
    `Translator Debugger and Metrics` window
  - keep metrics aggregated in memory and out of hot-path logs
- Files touched:
  - `Translators/TranslatorMetricsCollector.cs`
  - `Translators/TranslationService.cs`
  - `Echoglossian.Tests/TranslatorMetricsCollectorTests.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added an in-memory aggregated metrics collector keyed by translation engine
  - introduced aggregated outcome kinds for:
    - live success
    - live failure
    - request short-circuit before live translation
  - instrumented `TranslationService` to record:
    - live request latency
    - success/failure outcome
    - known-failure-cache short-circuits
  - added tests for both:
    - the collector aggregation behavior
    - `TranslationService` metrics signaling
- Behavior-sensitive risks:
  - this is aggregate-only telemetry, not per-request tracing
  - no UI is attached yet in this iteration; this commit only builds the data
    source for the debugger window
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - add the command and window for `Translator Debugger and Metrics`, backed by
    these snapshots and a local clear/reset action

## Iteration 5 - Translator Debugger and Metrics Command and Window

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - expose the aggregated translator runtime metrics through a dedicated
    command and inspectable UI window
- Files touched:
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `Echoglossian.cs`
  - `PluginUI/PluginRuntimeUi.cs`
  - `docs/commands/eglotranslatordebugger.md`
  - `docs/commands/README.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added the dedicated command:
    - `/eglotranslatordebugger`
  - added a dedicated window that shows per-engine aggregated metrics:
    - live requests
    - successes
    - failures
    - short-circuits
    - average, max, and last latency
    - last failure reason
  - wired the window lifecycle alongside the existing plugin draw hooks and
    command registration/disposal path
  - documented the command under `docs/commands`
- Behavior-sensitive risks:
  - this window is intentionally diagnostic and currently uses concise English
    labels rather than full localized resources
  - metrics remain session-scoped and in-memory only; there is still no
    persistence or historical export in this first pass
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - revisit `#176` with engine-specific runtime cost reductions beyond prompt
    size alone, likely around local-LLM request behavior and later dialogue
    session context

## Iteration 6 - Align Cloud LLM Prompt Wiring To Existing Prompt Templates

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - stop duplicating the long cloud-LLM prompt inline across multiple
    translators
  - make the existing per-engine prompt editors actually control the runtime
    path for the OpenAI-style LLM family
  - fix `OpenRouter` sending an unrendered prompt template instead of a prompt
    with substituted placeholders
- Files touched:
  - `PluginUI/Helpers/PromptTemplateManager.cs`
  - `Translators/ChatGPTTranslator.cs`
  - `Translators/DeepSeekTranslator.cs`
  - `Translators/GeminiTranslator.cs`
  - `Translators/OpenRouterTranslator.cs`
  - `Translators/TranslatorFactory.cs`
  - `Echoglossian.Tests/PromptTemplateManagerTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a shared static prompt renderer in `PromptTemplateManager`
  - made `ChatGPT`, `DeepSeek`, `Gemini`, and `OpenRouter` resolve their
    prompt template from config with built-in defaults when blank
  - removed duplicated inline long-form prompt assembly from those translators
  - made `OpenRouter` render `{text}`, `{sourceLanguage}`, and
    `{targetLanguage}` before sending the request
  - switched `ChatGPTTranslator` factory construction to use the full `Config`
    object so it can follow the same prompt path as the other LLM engines
- Behavior-sensitive risks:
  - this intentionally changes the runtime meaning of saved prompt templates
    for `ChatGPT`, `DeepSeek`, and `Gemini` from "UI-only field" to "live
    runtime input"
  - `OpenRouter` users with a blank custom prompt now fall back to the shared
    default prompt instead of sending an empty template
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - start shaping engine-specific runtime metrics beyond aggregate latency,
    likely provider/model metadata in the debugger window before touching
    dialogue session context

## Iteration 7 - Expose Provider and Model Context In Translator Metrics

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - make the translator debugger more useful for real LLM diagnosis by showing
    which provider and model are associated with each engine bucket
  - keep this purely aggregate and session-scoped, without adding hot-path
    trace noise
- Files touched:
  - `Translators/TranslatorMetricsCollector.cs`
  - `Translators/TranslationService.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `Echoglossian.Tests/TranslatorMetricsCollectorTests.cs`
  - `docs/commands/eglotranslatordebugger.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added optional provider/model metadata to metrics snapshots
  - taught `TranslationService` to describe the active engine bucket from
    config at runtime-refresh time
  - expanded the debugger window with `Provider` and `Model` columns
  - updated metrics tests to cover metadata retention in the collector
- Behavior-sensitive risks:
  - this records only the latest provider/model description per engine id in
    the current session; it is not a historical audit trail
  - the metadata is configuration-derived, not scraped from provider responses
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - consider the first runtime-only dialogue session scaffolding for `Talk`,
    while keeping `BattleTalk` isolated on the same infrastructure

## Iteration 8 - Runtime-Only Dialogue Session Scaffolding

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - add shared runtime-only short-lived dialogue session plumbing for future
    context-aware LLM translation
  - keep `Talk` and `BattleTalk` isolated while preserving the current DB and
    cache semantics
- Files touched:
  - `Translators/DialogueTranslationTurn.cs`
  - `Translators/DialogueTranslationContext.cs`
  - `Translators/DialogueTranslationSessionStore.cs`
  - `Translators/IDialogueContextAwareTranslator.cs`
  - `Translators/TranslationService.cs`
  - `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
  - `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
  - `Echoglossian.Tests/DialogueTranslationSessionStoreTests.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a bounded runtime-only session store with:
    - namespace isolation
    - TTL expiry pruning
    - prior-turn history trimming
  - introduced `DialogueTranslationContext` plus an optional
    `IDialogueContextAwareTranslator` extension point
  - extended `TranslationService.TranslateAsync(...)` with an overload that
    dispatches context only when the active translator explicitly opts in
  - wired `Talk` and `BattleTalk` to build short-lived session context for
    live line translation while keeping those namespaces isolated from each
    other
  - added tests for session reuse, namespace isolation, TTL expiry, and
    service-level context dispatch
- Behavior-sensitive risks:
  - no translator consumes the context yet; this iteration is scaffolding only
  - session history remains source-side, runtime-only, and non-persistent by
    design
  - existing translation persistence behavior remains unchanged
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - teach one narrow LLM path to consume the runtime-only dialogue context
    without persisting session-influenced output

## Iteration 9 - First Runtime-Only Dialogue Context Consumer

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - teach one real LLM path to consume the runtime-only dialogue context
  - prevent session-aware dialogue output from being persisted into the Talk
    and BattleTalk DB rows
- Files touched:
  - `Translators/ChatGPTTranslator.cs`
  - `Translators/TranslationService.cs`
  - `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
  - `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - made `ChatGPTTranslator` implement the optional
    `IDialogueContextAwareTranslator` path
  - added context-aware prompt composition that appends prior dialogue turns
    only when prior history actually exists
  - added `TranslationService.WillUseDialogueContext(...)` so callers can know
    whether the runtime will really switch to the context-aware path
  - updated `Talk` and `BattleTalk` to skip DB insertion when the live
    translation used runtime-only dialogue context
- Behavior-sensitive risks:
  - this is intentionally a single-engine first pass and does not yet cover
    the other LLM families
  - session-aware output remains cacheable in-memory for the current session,
    but not persistable to the DB
  - first-seen lines without prior history still follow the normal non-context
    path and therefore keep their existing persistence behavior
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether the next consumer should be another OpenAI-style engine or
    whether to focus on translator-debugger metrics for the new context path

## Iteration 10 - Context-Aware Request Visibility In Translator Metrics

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - expose whether runtime translator traffic is actually using short-lived
    dialogue context
  - keep this aggregated and session-scoped, with no hot-path trace logging
- Files touched:
  - `Translators/TranslatorMetricsCollector.cs`
  - `Translators/TranslationService.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `Echoglossian.Tests/TranslatorMetricsCollectorTests.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
  - `docs/commands/eglotranslatordebugger.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added aggregated `ContextAwareRequestCount` tracking per engine
  - taught `TranslationService` to mark whether a live request actually used
    dialogue context
  - surfaced the new context-aware count in the debugger summary and table
  - updated tests and command docs accordingly
- Behavior-sensitive risks:
  - this shows only aggregate counts, not which exact lines used context
  - short-circuited requests do not count as context-aware live requests
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - either expand runtime-only dialogue context to another LLM family or begin
    the first operator-facing retranslation control from the rework plan

## Iteration 11 - Deterministic Dialogue Retranslation Persistence Foundation

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - prepare the first explicit `retranslate visible text and persist` control
    without yet wiring it into the debugger window
  - make refreshed dialogue rows deterministic enough that a manual
    retranslation really becomes the preferred stored result on the next lookup
- Files touched:
  - `DBHelpers/DbOperations.cs`
  - `NativeUI/AddonHandlers/Talk/IVisibleDialogueRetranslationHandler.cs`
  - `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
  - `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
  - `Echoglossian.Tests/DbOperationsTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added an explicit runtime contract for retranslating the currently visible
    dialogue line and reporting the outcome back to UI
  - taught `TalkHandler` and `BattleTalkHandler` how to:
    - capture the current visible source line
    - force a fresh live translation without dialogue-session persistence
    - persist the refreshed result through a dedicated upsert path
    - update the current in-memory resolved state and overlay when the line is
      still current
  - added `UpsertTalkDataAsync(...)` and `UpsertBattleTalkDataAsync(...)`
  - made dialogue DB lookups prefer the most recently refreshed row when
    multiple historical rows exist for the same source line
  - added DB tests covering:
    - Talk upsert refresh
    - BattleTalk upsert refresh
    - "most recent row wins" lookup ordering for Talk
- Behavior-sensitive risks:
  - this is foundation only; there is still no user-facing button invoking the
    path yet
  - lookups now intentionally prefer the newest dialogue row, which is meant
    to make explicit retranslation win without deleting older engine history
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - wire the explicit visible-dialogue retranslation action into the
    `Translator Debugger and Metrics` window with session-scoped status
    reporting

## Iteration 12 - Translator Debugger Retranslation Control

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - expose the first operator-facing `retranslate visible text and persist`
    control in the `Translator Debugger and Metrics` window
  - keep the action discoverable for engine troubleshooting without moving it
    into the main config UI
- Files touched:
  - `Echoglossian.cs`
  - `PluginUI/PluginRuntimeUi.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `docs/commands/eglotranslatordebugger.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - injected the visible-dialogue retranslation callback into
    `TranslatorMetricsWindow`
  - added a `Retranslate Visible Dialogue And Persist` button with:
    - in-flight disable state
    - session-scoped outcome message
    - success/failure coloring
  - added plugin-side routing that scans the registered addon handlers and
    invokes the first visible `Talk` or `BattleTalk` retranslation handler
  - documented the first-pass scope and persistence semantics in the command
    doc
- Behavior-sensitive risks:
  - the first pass deliberately only targets `Talk` and `BattleTalk`
  - if both were somehow visible at once, the current handler registration
    order would prefer `Talk`
  - this is still an operator/debugger workflow, not a broad end-user config
    action
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - verify the in-game UX of the retranslation control and then decide whether
    the next step should deepen `#174` semantics or extend runtime-only
    context to another LLM family

## Iteration 13 - LM Studio Runtime-Only Dialogue Context

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - extend the runtime-only dialogue-session path to the first local LLM
    engine
  - keep the existing local-LLM prompt flow intact while allowing prior turns
    to improve consistency for live dialogue
- Files touched:
  - `Translators/LmStudioTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - made `LmStudioTranslator` implement
    `IDialogueContextAwareTranslator`
  - added a context-aware translation overload that:
    - falls back to the normal path when no prior turns exist
    - builds a distinct cache key when prior dialogue context exists
  - introduced prompt composition that appends bounded prior dialogue turns
    only for the live context-aware path
  - preserved the existing runtime-only persistence semantics by relying on the
    already-wired `TranslationService.WillUseDialogueContext(...)` and the
    `Talk` / `BattleTalk` DB skip path
- Behavior-sensitive risks:
  - context-aware cache keys are intentionally distinct from the base local
    cache key, which can increase memory use for repeated variant dialogue
    histories within a session
  - the first pass adds context only when prior turns actually exist; first
    lines still follow the old deterministic path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - apply the same runtime-only dialogue-session path to `Ollama` so the local
    LLM family behaves consistently

## Iteration 14 - Ollama Runtime-Only Dialogue Context

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - bring the same runtime-only dialogue-session behavior to `Ollama`
  - keep local LLM dialogue consistency behavior aligned across the first
    local-engine family without changing DB persistence
- Files touched:
  - `Translators/OllamaTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - made `OllamaTranslator` implement `IDialogueContextAwareTranslator`
  - added a context-aware translation overload that:
    - falls back to the existing path when no prior turns exist
    - uses a distinct cache key when prior dialogue history exists
  - factored prompt composition so bounded prior dialogue turns are appended
    only in the context-aware runtime path
  - preserved the current runtime-only semantics by reusing the existing
    `TranslationService` dialogue-context detection and the handler-side DB
    persistence skip
- Behavior-sensitive risks:
  - as with `LM Studio`, dialogue-history-aware cache keys can increase
    per-session cache cardinality for repeated variant histories
  - first lines without prior turns still go through the old deterministic
    no-context path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether to extend the same runtime-only context path to more
    OpenAI-style engines next or pivot to the next operator-facing piece of
    `#174`

## Iteration 15 - OpenRouter Runtime-Only Dialogue Context

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - extend runtime-only dialogue-session support to the next OpenAI-style LLM
    engine after `ChatGPT`
  - keep dialogue-context behavior aligned across the remote LLM family without
    changing persistence semantics
- Files touched:
  - `Translators/OpenRouterTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - made `OpenRouterTranslator` implement
    `IDialogueContextAwareTranslator`
  - added a context-aware translation overload that:
    - falls back to the existing path when no prior turns exist
    - uses a distinct cache key when prior dialogue history exists
  - factored prompt composition so prior turns are appended only for the
    live runtime-only context path
  - preserved the current `Talk` / `BattleTalk` no-DB-persist behavior for
    context-influenced output by reusing the existing `TranslationService`
    detection path
- Behavior-sensitive risks:
  - dialogue-history-aware cache keys increase per-session cache cardinality
    for this engine in the same way as the other context-aware paths
  - first lines without prior turns still follow the old deterministic
    no-context path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - continue the same runtime-only path for the remaining OpenAI-style LLM
    engines or pivot back to the next visible operator-facing gap in `#174`

## Iteration 16 - DeepSeek Runtime-Only Dialogue Context

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - extend runtime-only dialogue-session support to `DeepSeek`
  - keep the OpenAI-style remote LLM family aligned on dialogue-context
    behavior without altering DB persistence
- Files touched:
  - `Translators/DeepSeekTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - made `DeepSeekTranslator` implement
    `IDialogueContextAwareTranslator`
  - added a context-aware translation overload that:
    - falls back to the existing path when no prior turns exist
    - uses a distinct cache key when prior dialogue history exists
  - factored prompt composition so prior turns are appended only for the
    runtime-only context path
  - preserved the current `Talk` / `BattleTalk` no-DB-persist behavior for
    context-influenced output by reusing the existing `TranslationService`
    detection path
- Behavior-sensitive risks:
  - dialogue-history-aware cache keys increase per-session cache cardinality
    for `DeepSeek` in the same way as the other context-aware paths
  - first lines without prior turns still follow the old deterministic
    no-context path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - continue the same runtime-only path for `Gemini`, then `Claude`, to finish
    the first pass over the remaining remote LLM engines

## Iteration 17 - Gemini Runtime-Only Dialogue Context

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - extend runtime-only dialogue-session support to `Gemini`
  - keep the remote LLM family aligned on dialogue-context behavior while
    preserving the existing retry policy and DB semantics
- Files touched:
  - `Translators/GeminiTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - made `GeminiTranslator` implement
    `IDialogueContextAwareTranslator`
  - added a context-aware translation overload that:
    - falls back to the existing path when no prior turns exist
    - uses a distinct cache key when prior dialogue history exists
  - factored prompt composition so prior turns are appended only for the
    runtime-only context path
  - preserved the current `Gemini` retry/backoff request behavior while
    reusing the existing `TranslationService` path that prevents DB persistence
    for context-influenced `Talk` / `BattleTalk` output
- Behavior-sensitive risks:
  - dialogue-history-aware cache keys increase per-session cache cardinality
    for `Gemini` in the same way as the other context-aware paths
  - the context-aware path still inherits the current retry model, so failures
    under prior-turn context can hold the request longer than the other engines
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - finish the first remote-LLM pass by carrying the same runtime-only
    dialogue-context path into `Claude`

## Iteration 18 - Claude Runtime-Only Dialogue Context

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - complete the first remote-LLM pass by extending runtime-only
    dialogue-session support to `Claude`
  - keep `Claude` aligned with the other context-aware engines without
    redefining its existing request or persistence model
- Files touched:
  - `Translators/ClaudeTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - made `ClaudeTranslator` implement
    `IDialogueContextAwareTranslator`
  - added a context-aware translation overload that:
    - falls back to the existing path when no prior turns exist
    - uses a distinct cache key when prior dialogue history exists
  - factored prompt composition so prior turns are appended only for the
    runtime-only context path
  - preserved the current `Talk` / `BattleTalk` no-DB-persist behavior for
    context-influenced output by reusing the existing `TranslationService`
    detection path
- Behavior-sensitive risks:
  - dialogue-history-aware cache keys increase per-session cache cardinality
    for `Claude` in the same way as the other context-aware paths
  - this intentionally leaves `Claude` on its current prompt-base path rather
    than mixing additional prompt cleanup into the same iteration
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether the next step should consolidate shared context-aware prompt
    helpers or pivot back to the next operator-facing and routing pieces of
    the LLM rework plan

## Iteration 19 - Shared Dialogue Context Prompt Helper

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - remove the repeated runtime-only dialogue-context boilerplate now that the
    first engine pass is complete
  - keep behavior identical while centralizing the shared prompt and cache-key
    rules
- Files touched:
  - `Translators/Helpers/DialogueContextPromptHelper.cs`
  - `Translators/ChatGPTTranslator.cs`
  - `Translators/LmStudioTranslator.cs`
  - `Translators/OllamaTranslator.cs`
  - `Translators/OpenRouterTranslator.cs`
  - `Translators/DeepSeekTranslator.cs`
  - `Translators/GeminiTranslator.cs`
  - `Translators/ClaudeTranslator.cs`
  - `Echoglossian.Tests/DialogueContextPromptHelperTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added `DialogueContextPromptHelper` for:
    - usable-context detection
    - context-aware cache-key generation
    - prompt enrichment with prior-turn history
  - updated all current context-aware translators to use the helper instead of
    carrying duplicated private methods and string literals
  - added narrow tests for the helper behavior
- Behavior-sensitive risks:
  - this is an internal consolidation pass only; no translator-specific prompt
    semantics were intentionally changed
  - any future tweak to the shared context wording now affects all
    context-aware engines, which is the intended tradeoff of this refactor
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide between the next operator-facing LLM routing/config piece and
    deeper metrics/debugger work on top of the now-shared context-aware path

## Iteration 20 - Dialogue Session Visibility In Translator Debugger

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - make the runtime-only dialogue-context path inspectable by operators
  - expose retained `Talk` / `BattleTalk` session state without adding hot-path
    logging
- Files touched:
  - `Translators/DialogueTranslationSessionStore.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `Echoglossian.Tests/DialogueTranslationSessionStoreTests.cs`
  - `docs/commands/eglotranslatordebugger.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added immutable dialogue session snapshots to the runtime-only session
    store
  - exposed current retained session count and a dedicated dialogue session
    table in `Translator Debugger and Metrics`
  - added a `Clear Dialogue Sessions` button that clears only the in-memory
    runtime-only context store
  - added a narrow session-store test for the snapshot shape
- Behavior-sensitive risks:
  - this remains session-scoped and in-memory only; it does not touch DB data
    or persisted translation history
  - the new table uses current retained turn counts, not original full
    conversation history beyond the existing bounded session limit
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - move to the next operator-facing configuration/routing piece of the LLM
    rework, now that the context path is both implemented and inspectable
