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

## Iteration 21 - LLM-Only Dialogue Routing Foundation

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - introduce the first routing foundation for LLM-only surface-group
    overrides without forking the shared `TranslationService`
  - make `Talk` and `BattleTalk` the first real dialogue-family consumers so
    routing affects the places where runtime-only context already exists
- Files touched:
  - `Config.cs`
  - `Translators/TranslationSurfaceGroup.cs`
  - `Translators/LlmSurfaceGroupRoutingPolicy.cs`
  - `Translators/TranslationService.cs`
  - `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
  - `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
  - `Echoglossian.Tests/LlmSurfaceGroupRoutingPolicyTests.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added first-pass config fields for a dialogue-family LLM override:
    - `UseDialogueLlmOverride`
    - `DialogueLlmEngine`
    - `DialogueLlmEngineKey`
  - added `TranslationSurfaceGroup` so requests can carry a coarse routing
    hint without creating a parallel translation service
  - added `LlmSurfaceGroupRoutingPolicy`:
    - global engine remains the default path
    - only LLM-backed override selections are accepted
    - incomplete override configs fall back safely to the global engine
  - refactored `TranslationService` so request resolution now happens per
    surface group:
    - effective engine id is now used consistently for:
      - known-failure cache lookups
      - runtime failure feedback
      - aggregated translator metrics
    - translator instances are cached per engine inside the same shared
      service
  - updated `Talk` and `BattleTalk` to use the dialogue surface group for:
    - live translation requests
    - runtime-only dialogue-context checks
    - dialogue-session keys
    - DB lookup / persistence engine ids
  - added narrow tests for:
    - routing-policy resolution
    - per-surface translator selection inside `TranslationService`
- Behavior-sensitive risks:
  - this first routing pass only updates `Talk` and `BattleTalk`; other
    dialogue-adjacent surfaces such as `TalkSubtitle` and `MiniTalk` still use
    the global engine path for now
  - there is no dedicated UI for these new config fields yet, so this is a
    backend/foundation pass first
  - DB reuse for `Talk` and `BattleTalk` is now intentionally keyed to the
    effective routed engine, which is correct but means the first use of a new
    dialogue override will not reuse rows created under the old global engine
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether the next pass should widen dialogue routing to
  `TalkSubtitle` / `MiniTalk` or add the first operator-facing UI for the
  dialogue override path

## Iteration 22 - Extend Dialogue Routing To Subtitle And MiniTalk

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - widen the first dialogue-family routing pass beyond `Talk` and
    `BattleTalk`
  - keep `TalkSubtitle` and `MiniTalk` aligned with the effective dialogue
    engine so DB lookup and persistence semantics stay coherent
- Files touched:
  - `NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs`
  - `NativeUI/AddonHandlers/SingleText/MiniTalkHandler.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - updated `TalkSubtitleHandler` to send live translation requests through
    `TranslationSurfaceGroup.Dialogue`
  - updated `TalkSubtitleHandler` DB lookup and persistence rows to use the
    effective routed dialogue engine id instead of the global engine id
  - updated `MiniTalkHandler` to send live translation requests through
    `TranslationSurfaceGroup.Dialogue`
  - updated `MiniTalkHandler` DB lookup and persistence rows to use the
    effective routed dialogue engine id instead of the global engine id
- Behavior-sensitive risks:
  - `TalkSubtitle` and `MiniTalk` still do not use runtime-only session
    context; this pass only aligns their engine routing and DB semantics
  - existing rows saved under the old global engine remain intentionally
    isolated from rows created under a dialogue override engine
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - add the first operator-facing UI for the dialogue override path so this
    routing foundation becomes configurable without editing the persisted
    config by hand

## Iteration 23 - Expose Dialogue LLM Override In Engine Settings

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - add the first operator-facing control for dialogue-family LLM routing
  - keep the new override selection aligned with the persisted id/key pair so
    it does not drift the way the primary engine selection used to
- Files touched:
  - `PluginUI/Tabs/TranslationEnginesTab.cs`
  - `Translators/LlmSurfaceGroupRoutingPolicy.cs`
  - `Properties/Resources.resx`
  - `Echoglossian.Tests/LlmSurfaceGroupRoutingPolicyTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a first-pass `Dialogue LLM override` section to the translation
    engine settings tab
  - users can now:
    - enable or disable the dialogue-family override
    - choose one LLM-backed override engine
    - configure that override engine in the same tab when it differs from the
      primary engine
  - added normalization for `DialogueLlmEngine` and
    `DialogueLlmEngineKey` so only valid LLM-backed engines remain selectable
    in this first-pass path
  - added a narrow policy test proving invalid non-LLM persisted override
    values normalize back to the default LLM fallback
- Behavior-sensitive risks:
  - this is still LLM-only by design; non-LLM engines remain intentionally
    excluded from the dialogue override list
  - localized `Resources.*.resx` files do not yet carry these new UI strings,
    so localized clients will temporarily fall back to the bundled English
    text for this new section
  - when the override engine matches the primary engine, the tab intentionally
    reuses the primary configuration UI instead of drawing the same settings
    twice
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether to keep widening the operator-facing override work or move
    back to metrics / controls for the same LLM routing path

## Iteration 24 - Show Dialogue Override State In The Translator Debugger

- Date: 2026-05-12
- Branch: `llm-translation-rework`
- Goal:
  - expose the effective runtime state of the dialogue-family LLM override in
    the existing debugger window
  - let operators see when the override is active versus silently falling back
    to the primary engine, without relying on logs
- Files touched:
  - `Translators/LlmSurfaceGroupRoutingPolicy.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `Echoglossian.cs`
  - `Echoglossian.Tests/LlmSurfaceGroupRoutingPolicyTests.cs`
  - `docs/commands/eglotranslatordebugger.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a debugger-facing `DialogueOverrideState` snapshot to the shared LLM
    routing policy
  - `Translator Debugger and Metrics` now shows:
    - current primary engine
    - current effective dialogue engine
    - whether the override is active or falling back to the primary engine
    - whether the selected override engine is not yet configured enough to be
      used safely
  - added narrow tests for:
    - active configured override state
    - incomplete override fallback state
- Behavior-sensitive risks:
  - this is informational only; it does not change routing behavior by itself
  - the debugger snapshot normalizes the persisted override selection before
    reading it, which is consistent with the new UI path but means malformed
    manual JSON edits will present as the normalized state
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - move to the next routing/product slice of the LLM rework, now that both
    configuration and runtime observability of the dialogue override exist

## Iteration 25 - OpenAI-Compatible Provider Variant Foundation

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - turn custom OpenAI-compatible support into an explicit variant of the
    existing OpenAI-family engine instead of relying on an implicit loose base
    URL field
  - centralize active OpenAI-family provider resolution so translator runtime,
    configuration validation, and metrics all agree on the same provider
    profile
- Files touched:
  - `Config.cs`
  - `Translators/OpenAI/OpenAiProviderVariantHelper.cs`
  - `Translators/OpenAI/OpenAIModelManager.cs`
  - `Translators/ChatGPTTranslator.cs`
  - `Translators/TranslationService.cs`
  - `PluginUI/Helpers/TranslationEngineConfigurationHelper.cs`
  - `Echoglossian.Tests/OpenAiProviderVariantHelperTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added explicit config for the OpenAI-family provider variant:
    - `OpenAiProviderVariant`
    - `CustomOpenAiCompatibleApiKey`
    - `CustomOpenAiCompatibleBaseUrl`
    - `CustomOpenAiCompatibleModel`
    - `UseLiveCustomOpenAiCompatibleModelList`
  - added `OpenAiProviderVariantHelper` to resolve the active OpenAI-family
    provider profile in one place
  - updated `ChatGPTTranslator` to use the resolved provider profile instead
    of assuming the official OpenAI path
  - updated OpenAI-family configuration readiness checks to validate the active
    provider profile, including the active model
  - generalized `OpenAIModelManager` so live model fetch can target any
    OpenAI-compatible base URL and provider label
  - updated translator metrics metadata so the debugger can distinguish
    `OpenAI` from `OpenAI-Compatible`
  - added narrow tests for official and custom provider-profile resolution
- Behavior-sensitive risks:
  - this foundation does not yet expose the custom provider variant in the
    operator-facing engine UI; existing users continue on the official OpenAI
    profile by default
  - custom provider profiles now require an explicit model to be considered
    configured, which is correct but may disable translation until the UI path
    lands and the field is filled intentionally
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - expose the OpenAI-family provider variant and custom-provider fields in
    `ChatGptEngineUI`, including live-model fetch against the configured custom
    endpoint

## Iteration 26 - OpenAI-Compatible Provider UI

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - expose the new OpenAI-family provider variant directly in the ChatGPT
    engine settings UI
  - make custom OpenAI-compatible providers usable without creating a separate
    engine family or polluting the official OpenAI profile
- Files touched:
  - `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
  - `Translators/OpenAI/OpenAIModelManager.cs`
  - `Properties/Resources.resx`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a provider selector to the ChatGPT/OpenAI engine settings:
    - `Official OpenAI`
    - `Custom OpenAI-Compatible`
  - routed API key, endpoint, and model editing to the active provider
    profile instead of always mutating the official OpenAI fields
  - added custom-provider guidance text and a safe manual-model fallback when
    a provider does not expose a usable `/models` response
  - added `Reload` support for live model fetch so operators do not need to
    toggle live fetch off and on after editing credentials or endpoint fields
  - hardened `OpenAIModelManager` so failed refresh attempts reset the shared
    model list instead of leaving stale provider models behind
- Behavior-sensitive risks:
  - the OpenAI-family model manager is still shared between the official and
    custom variants, so the UI explicitly resets the model list when the
    variant changes to avoid cross-provider list bleed
  - only `Resources.resx` was updated in this iteration; localized
    `Resources.*.resx` files still need follow-up and currently fall back to
    bundled English for the new provider strings
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - validate the operator-facing custom-provider flow in-game and then decide
    whether the next `#196` slice should be provider-specific diagnostics or
    broader LLM routing work

## Iteration 27 - OpenAI-Compatible Provider Debugger Diagnostics

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - make the custom OpenAI-compatible provider path observable in the
    `Translator Debugger and Metrics` window without requiring log inspection
  - surface enough provider and `/models` refresh state to troubleshoot the
    NanoGPT-style endpoint flow from `#196`
- Files touched:
  - `Translators/OpenAI/OpenAIModelManager.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `docs/commands/eglotranslatordebugger.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added an in-memory OpenAI-family live model refresh snapshot covering:
    - last refresh UTC
    - last provider label
    - last normalized endpoint
    - success or failure state
    - current shared model count
    - last failure detail
  - the model manager now updates that snapshot for:
    - missing key / endpoint validation failures
    - HTTP failures
    - malformed provider responses
    - zero supported text-model results
    - successful refreshes
  - `Translator Debugger and Metrics` now shows:
    - active OpenAI-family provider variant
    - active endpoint
    - active model
    - whether the active profile is configured
    - whether live model listing is enabled
    - the last live model refresh result and failure detail when applicable
- Behavior-sensitive risks:
  - this is observability-only; it does not change translation routing or
    provider selection behavior
  - the OpenAI-family live model snapshot is session-scoped and resets only as
    part of runtime lifecycle, not by clearing general translator metrics
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether to keep deepening `#196` with provider-specific polish or
    move back to broader LLM routing / retraduction work

## Iteration 28 - OpenAI-Compatible Provider Unavailable Messaging

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - make the OpenAI-compatible provider path fail with provider-aware
    configuration and unavailable messages instead of reusing the official
    OpenAI API-key-only wording
  - cover the active configuration gate and failure classification with narrow
    tests
- Files touched:
  - `Translators/OpenAI/OpenAiProviderVariantHelper.cs`
  - `Translators/ChatGPTTranslator.cs`
  - `GeneralHelpers/TranslationFailureTextClassifier.cs`
  - `Properties/Resources.resx`
  - `Echoglossian.Tests/OpenAiProviderConfigurationTests.cs`
  - `Echoglossian.Tests/TranslationFailureTextClassifierTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added provider-aware OpenAI-family helper methods for:
    - configuration warning text
    - unavailable translation text
  - `ChatGPTTranslator` now returns the custom OpenAI-compatible unavailable
    message when the selected provider profile cannot build a client
  - startup warnings for the custom provider now mention the real missing scope
    (`endpoint`, `API key`, and `model`) rather than only the API key
  - the shared translation-failure classifier now recognizes the custom
    OpenAI-compatible unavailable message as an `EngineUnavailable` failure
  - added tests proving:
    - the custom provider configuration gate succeeds only when the custom
      endpoint/key/model are present
    - the custom provider unavailable message is returned by the translator
    - that unavailable message is classified as an engine-unavailable failure
- Behavior-sensitive risks:
  - this changes only the user-facing unavailable text for the custom
    OpenAI-compatible provider path; the official OpenAI wording remains
    untouched
  - localized `Resources.*.resx` files still need follow-up and currently fall
    back to the base English text for the new provider-specific strings
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - reassess whether `#196` is complete enough to stop here and pivot back to
    the broader LLM rework backlog

## Iteration 29 - OpenAI-Compatible Provider Localization Coverage

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - close the remaining localization gap for the custom OpenAI-compatible
    provider flow so the new `#196` UI and failure text do not silently fall
    back to base English in localized builds
- Files touched:
  - `Properties/Resources.da.resx`
  - `Properties/Resources.de.resx`
  - `Properties/Resources.el.resx`
  - `Properties/Resources.es.resx`
  - `Properties/Resources.eu.resx`
  - `Properties/Resources.fr.resx`
  - `Properties/Resources.it.resx`
  - `Properties/Resources.pt-BR.resx`
  - `Properties/Resources.pt.resx`
  - `Properties/Resources.ru.resx`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - localized the custom-provider strings already introduced in the base
    `Resources.resx`, including:
    - provider-variant labels
    - provider description text
    - live-model fetch failure hint
    - manual model hint
    - provider-specific unavailable/configuration warnings
  - kept `Official OpenAI` and `Custom OpenAI-Compatible` as product labels
    while translating the surrounding operational text so localized UIs remain
    semantically correct without renaming provider families
- Behavior-sensitive risks:
  - this is text-only and does not alter provider selection, routing, or model
    refresh behavior
  - the custom-provider diagnostics shown in the debugger remain English-only
    for now because that window still uses hardcoded operator-facing text
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - polish the provider-specific UI wording so the custom provider does not
    keep reusing `ChatGPT API Key` labeling in the main engine configuration

## Iteration 30 - OpenAI-Compatible Provider UI Wording Polish

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - remove the most visible ChatGPT-specific wording leak from the custom
    OpenAI-compatible provider path so the engine configuration reads
    correctly when the operator is not using the official provider
- Files touched:
  - `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - the engine settings header now reflects the active provider variant:
    - official variant keeps `Settings for ChatGPT`
    - custom variant shows the localized `Custom OpenAI-Compatible` label
  - the API key field now uses:
    - `ChatGPT API Key` for the official variant
    - the generic localized `API Key` label for the custom provider
- Behavior-sensitive risks:
  - this is wording-only and does not alter how provider settings are stored,
    validated, or used to create the runtime client
  - the debugger still uses English operator-facing status text, which is a
    separate observability concern and not part of this UI wording pass
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether `#196` is complete enough for PR review or whether to do a
    dedicated follow-up for debugger localization and operator wording

## Iteration 31 - Translator Debugger Provider Localization

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - remove the remaining English-only operator text from the
    `Translator Debugger and Metrics` window for the dialogue override and
    OpenAI-compatible provider status flow
- Files touched:
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `Properties/Resources.resx`
  - `Properties/Resources.pt-BR.resx`
  - `Properties/Resources.pt.resx`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - moved the debugger text for:
    - dialogue override status
    - provider summary
    - endpoint/model/live-model status
    - model refresh status/failure lines
    into resource-backed keys with fallbacks
  - localized those debugger strings for:
    - base English
    - `pt-BR`
    - `pt`
  - stopped showing the raw `OpenAiProviderVariant` enum in the debugger and
    now render the localized provider-variant label instead
  - deliberately used `ResourceManager.GetString(...)` with fallbacks in the
    debugger window so this narrow pass did not require a broader regeneration
    sweep in `Resources.Designer.cs`
- Behavior-sensitive risks:
  - this is wording-only and does not change routing, provider validation, or
    model refresh behavior
  - locales other than `pt-BR` and `pt` still fall back to base English for
    these new debugger-only strings
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether `#196` is now complete enough for PR review or whether to
    keep polishing debugger/operator UX before leaving this branch

## Iteration 32 - Review Follow-Up: OpenAI Provider Runtime Refresh

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - address the first actionable PR `#202` review cluster around the
    OpenAI-family runtime by:
    - rebuilding the translation runtime when the custom provider changes
    - making debug masking safe for short API keys
    - disposing model-refresh HTTP objects correctly
- Files touched:
  - `GeneralHelpers/RuntimeConfigurationRefresh.cs`
  - `Translators/ChatGPTTranslator.cs`
  - `Translators/OpenAI/OpenAIModelManager.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - expanded the translation runtime signature so switching between
    `Official OpenAI` and `Custom OpenAI-Compatible`, or editing only the
    custom provider fields, now forces a runtime translator rebuild
  - replaced the unsafe debug log slicing in `ChatGPTTranslator` with a
    length-safe API-key masker that works for short local/custom tokens
  - wrapped the OpenAI-family model refresh request/response in `using`
    declarations so refresh attempts do not leak disposable HTTP objects
- Behavior-sensitive risks:
  - editing custom-provider settings now rebuilds the translation runtime
    immediately, which is the intended fix but may make misconfiguration more
    visible during active testing
  - the API-key masker is debug-only and does not alter provider auth
  - model refresh behavior is unchanged apart from correct disposal
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - address the remaining review comments about thread safety in runtime
    failure tracking and translation-failure caches

## Iteration 33 - Review Follow-Up: Failure Cache Thread Safety

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - address the actionable PR `#202` review comments about unsynchronized
    access to shared runtime failure-deduplication state
- Files touched:
  - `Echoglossian.cs`
  - `GeneralHelpers/TranslationFailureNotifications.cs`
  - `Cache/TranslationFailureCacheManager.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a dedicated lock for runtime translation-failure notification
    deduplication on the plugin instance
  - made the prune/check/update sequence for notification cooldowns atomic so
    concurrent async failures do not race through the dedupe map
  - added a shared lock to `TranslationFailureCacheManager` and serialized
    cache preload, update, lookup, transient-failure remember, and clear
    operations against the in-memory dictionaries
- Behavior-sensitive risks:
  - this does not change translation failure semantics or persistence rules;
    it only removes undefined concurrent access to the in-memory structures
  - lookups now briefly contend on a lock during failure-cache access, which is
    preferable to corrupting shared runtime state
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - address the remaining review follow-up on dialogue-context cache key
    robustness so context-aware translation requests cannot collide on
    delimiter-heavy source text

## Iteration 34 - Review Follow-Up: Dialogue Context Cache Key Robustness

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - remove delimiter-driven ambiguity from context-aware dialogue cache keys
    so different session/history combinations cannot collide when source text
    contains `|`, `:`, or `_`
- Files touched:
  - `Translators/Helpers/DialogueContextPromptHelper.cs`
  - `Echoglossian.Tests/DialogueContextPromptHelperTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - replaced the hand-concatenated context cache key with a serialized payload
    containing:
    - session namespace
    - session key
    - current text
    - source/target languages
    - prior-turn speaker/text pairs
  - updated helper tests to:
    - use explicit deterministic turn timestamps
    - verify the serialized key shape
    - cover a delimiter-heavy collision scenario that now stays distinct
- Behavior-sensitive risks:
  - existing in-memory context-aware cache entries from the previous key format
    will naturally miss after restart or hot reload, which is acceptable for a
    runtime-only cache
  - persistence semantics remain unchanged because these keys are not stored in
    the database
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - reassess the remaining PR `#202` review items and decide whether this
    branch is ready for another review pass

## Iteration 35 - Review Follow-Up: OpenAI Refresh Input Hygiene

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - address the next PR `#202` review cluster around custom-provider live model
    refresh by:
    - normalizing whitespace around the configured endpoint
    - removing the ambiguous `Task<Task<bool>>` refresh scheduling pattern
- Files touched:
  - `Translators/OpenAI/OpenAIModelManager.cs`
  - `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - normalized provider endpoints with `Trim().TrimEnd('/')` before:
    - building the `/models` URL
    - reporting refresh failures
  - replaced the `Task.Run(() => RefreshAsync(...))` official-provider path
    with direct async invocation
  - replaced the custom-provider `Task.Run(async () => ...)` branch with a
    dedicated async helper so the scheduled work is a single task with clearer
    intent and the custom fetch result still updates the UI flags
- Behavior-sensitive risks:
  - endpoint strings with leading/trailing spaces now normalize instead of
    failing later in the HTTP stack
  - live model refresh remains fire-and-forget from the UI, but the code path
    is now less ambiguous and easier to reason about during review/debugging
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - move the translator debugger command help text into `Resources` so the
    command registry stays localizable and consistent with the rest of the
    plugin command help

## Iteration 36 - Review Follow-Up: Translator Debugger Command Localization

- Date: 2026-05-13
- Branch: `llm-translation-rework`
- Goal:
  - close the last open PR `#202` review item by moving the
    `/eglotranslatordebugger` command help text out of hardcoded code and into
    `Resources`
- Files touched:
  - `Echoglossian.cs`
  - `Properties/Resources.resx`
  - `Properties/Resources.pt-BR.resx`
  - `Properties/Resources.pt.resx`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - replaced the hardcoded translator-debugger command help text with a
    `Resources.ResourceManager` lookup plus safe English fallback
  - added the new help string to:
    - base English resources
    - `pt-BR`
    - `pt`
- Behavior-sensitive risks:
  - this is command help text only; command registration and debugger behavior
    are unchanged
  - locales without a specific translation still fall back to base English
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - reassess PR `#202` review state and resolve the remaining open threads if
    the reviewer comments are fully covered

## Iteration 37 - Dialogue Override Configuration UI Availability

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - fix the dialogue-family LLM override UI so operators can configure the
    selected override engine directly from the override section even when that
    engine is not configured yet
- Files touched:
  - `PluginUI/Tabs/TranslationEnginesTab.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - stopped gating override-engine configuration rendering on
    `overrideConfigured`
  - the override section now:
    - still shows the warning when the selected override is not configured
    - still shows the “matches primary” note when the override engine is the
      same as the primary engine
    - but renders the selected override engine UI whenever it differs from the
      primary engine, so missing credentials/endpoint/model fields are actually
      reachable from the override workflow
- Behavior-sensitive risks:
  - this changes only the config UI flow for the dialogue override section
  - runtime routing, activation gating, and fallback-to-primary behavior remain
    unchanged; unconfigured overrides still do not become active until ready
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - verify in-game that all LLM override engines now expose their own
    configuration UI directly from the dialogue override section

## Iteration 38 - Translation Setup Tab Spacing Pass

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - improve the readability of the main translation setup tab by adding clearer
    visual separation between its top-level option groups without changing any
    configuration behavior
- Files touched:
  - `PluginUI/PluginUI.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a narrow section-header helper for the setup tab that renders:
    - a muted section title
    - a separator line
    - spacing below the separator
  - applied that section treatment to the four top-level groups:
    - target language
    - translation engine
    - translation activation
    - general settings
  - added extra vertical break spacing between those groups so the first tab
    no longer reads as one dense uninterrupted block
- Behavior-sensitive risks:
  - this is layout-only in the config UI and does not alter translation
    routing, activation, persistence, or runtime behavior
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - verify in-game that the added separation improves readability without
    making the first tab feel too tall or repetitive

## Iteration 39 - Translation Setup Group Spacing Follow-up

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - reduce the remaining crowding in the main setup tab and make the dialogue
    override block read like its own nested option group inside the engine
    section
- Files touched:
  - `PluginUI/PluginUI.cs`
  - `PluginUI/Tabs/TranslationEnginesTab.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - increased the vertical break spacing between top-level groups in the first
    setup tab
  - added extra spacing before the `Dialogue LLM override` block inside the
    translation engine section
  - converted the `Dialogue LLM override` label into the same muted
    header-plus-separator treatment used by the main setup groups so the block
    reads as a distinct nested section
- Behavior-sensitive risks:
  - this is still configuration-layout-only and does not alter engine routing,
    translation activation, or persistence behavior
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - verify in-game whether the first tab now has enough breathing room without
    forcing too much extra scrolling on smaller window sizes

## Iteration 40 - Issue 148 Structured LLM Plan

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - turn issue `#148` into a dedicated implementation document that separates
    structured glossary/metadata work from the broader umbrella LLM rework
- Files touched:
  - `docs/issue-148-structured-llm-plan.md`
  - `docs/llm-translation-improvements-plan.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - created a dedicated `#148` plan document covering:
    - structured request/response contracts
    - glossary strategy
    - dialogue metadata strategy
    - provider capability modes
    - persistence guardrails
    - phased rollout
  - linked the umbrella LLM plan to the new dedicated `#148` plan so the
    structured-quality work has an explicit home
- Behavior-sensitive risks:
  - documentation only
  - no runtime, persistence, or UI behavior changed in this iteration
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - start phase `148.1` by introducing the shared structured request/response
    contracts and the first validation helper for structured dialogue output

## Iteration 41 - Issue 148 Structured Contracts Foundation

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - start phase `148.1` with shared structured dialogue contracts and a narrow
    request-builder foundation that reuses the current runtime-only dialogue
    context path instead of inventing a parallel pipeline
- Files touched:
  - `Translators/StructuredDialogueProviderCapability.cs`
  - `Translators/StructuredDialogueContextTurn.cs`
  - `Translators/StructuredDialogueGlossaryEntry.cs`
  - `Translators/StructuredDialogueTranslationMetadata.cs`
  - `Translators/StructuredDialogueTranslationRequest.cs`
  - `Translators/StructuredDialogueTranslationResponse.cs`
  - `Translators/Helpers/StructuredDialogueCapabilityHelper.cs`
  - `Translators/Helpers/StructuredDialogueTranslationRequestBuilder.cs`
  - `Echoglossian.Tests/StructuredDialogueCapabilityHelperTests.cs`
  - `Echoglossian.Tests/StructuredDialogueTranslationRequestBuilderTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - introduced shared structured request/response record types for
    dialogue-family LLM work
  - introduced a provider-capability enum plus a first-pass capability helper
    for structured dialogue support
  - added a request builder that projects the existing `DialogueTranslationContext`
    into the new structured contract, including glossary rows and optional
    metadata hints
  - added tests covering the capability mapping and request-builder projection
- Behavior-sensitive risks:
  - no live translator behavior changed yet
  - this is shared foundation only, so any future provider wiring still has to
    choose where structured mode is attempted and how failures fall back
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - add the first structured dialogue response-validation helper so the future
    provider wiring has one shared acceptance gate instead of bespoke JSON-mode
    checks per engine

## Iteration 42 - Issue 148 Structured Response Validation

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - add the first shared structured dialogue response validation helper so the
    future provider wiring can reuse one acceptance gate for strict JSON
    payloads instead of open-coding response checks per engine
- Files touched:
  - `Translators/StructuredDialogueContextTurn.cs`
  - `Translators/StructuredDialogueGlossaryEntry.cs`
  - `Translators/StructuredDialogueTranslationMetadata.cs`
  - `Translators/StructuredDialogueTranslationRequest.cs`
  - `Translators/StructuredDialogueTranslationResponse.cs`
  - `Translators/StructuredDialogueResponseValidationResult.cs`
  - `Translators/Helpers/StructuredDialogueTranslationResponseValidator.cs`
  - `Echoglossian.Tests/StructuredDialogueTranslationResponseValidatorTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added JSON-property annotations to the shared structured dialogue contract
    types so the first provider wiring can serialize and parse stable snake_case
    payloads
  - introduced a shared structured response validation result type
  - introduced a strict JSON parse-and-validate helper that:
    - rejects wrapper prose instead of extracting JSON from it
    - rejects empty or synthetic translated text
    - can optionally require a translated speaker field
  - added tests covering:
    - successful strict JSON validation
    - wrapper/annotation leakage rejection
    - synthetic translation error rejection
    - required speaker rejection
- Behavior-sensitive risks:
  - no live provider path uses this helper yet
  - this is foundation only, but it intentionally sets the future structured
    path to be strict about wrapper prose instead of trying to guess the JSON
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - wire the first OpenAI-family structured dialogue path behind an explicit
    capability check and keep plain-text fallback in place

## Iteration 43 - Issue 148 First Live OpenAI Structured Dialogue Path

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - wire the first real structured dialogue path into `ChatGPTTranslator`
    without regressing the existing plain-text path when structured output is
    unsupported or malformed
- Files touched:
  - `Translators/ChatGPTTranslator.cs`
  - `Translators/Helpers/StructuredDialogueOpenAiToolHelper.cs`
  - `Echoglossian.Tests/StructuredDialogueOpenAiToolHelperTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added one reusable OpenAI-family helper that:
    - builds the narrow function-tool JSON schema
    - serializes the shared structured request payload
    - builds the first user prompt for the structured dialogue path
  - updated `ChatGPTTranslator` so the dialogue-context path now:
    - tries one forced function-tool call first
    - validates the returned JSON arguments through the shared structured
      response validator
    - falls back automatically to the old plain-text prompt path on any
      provider, schema, or parsing failure
  - kept persistence semantics unchanged:
    - only the translated dialogue text is remembered in the translator cache
    - no session-aware payload is persisted differently because of this path
- Behavior-sensitive risks:
  - some OpenAI-compatible endpoints may ignore forced tool calling or return
    malformed arguments; this cut intentionally treats that as a soft failure
    and falls back to the existing plain-text path
  - the first structured path only covers `ChatGPT` and only when dialogue
    context is already present
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - extend the same structured path shape to the next OpenAI-family-compatible
    translator after in-game validation confirms the fallback behavior is
    stable

## Iteration 44 - Issue 148 OpenRouter Structured Dialogue Path

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - extend the first live structured dialogue path to the next
    OpenAI-compatible HTTP provider without changing the legacy fallback
    behavior for incompatible upstreams
- Files touched:
  - `Translators/OpenRouterTranslator.cs`
  - `Translators/Helpers/StructuredDialogueOpenAiCompatiblePayloadHelper.cs`
  - `Echoglossian.Tests/StructuredDialogueOpenAiCompatiblePayloadHelperTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added one small shared helper for OpenAI-compatible HTTP JSON payloads that:
    - exposes the structured function-parameter schema as a JSON element
    - extracts matching `tool_calls[*].function.arguments`
    - falls back to `message.content` when tool calling is ignored
  - updated `OpenRouterTranslator` so the dialogue-context path now:
    - tries one forced function-tool request first
    - validates the returned structured payload via the shared validator
    - falls back automatically to the old plain-text request path when the
      upstream provider ignores tool calling, returns malformed JSON, or fails
- Behavior-sensitive risks:
  - some routed upstream models behind OpenRouter may partially support tool
    calling; this cut intentionally treats any malformed or missing structured
    payload as a soft failure and runs the legacy path instead
  - only the dialogue-context path attempts structured output
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - carry the same OpenAI-compatible structured path to `DeepSeek` or
    `LM Studio`, reusing the same JSON helper

## Iteration 45 - Issue 148 LM Studio Structured Dialogue Path

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - carry the same OpenAI-compatible structured dialogue path to the first
    local LLM backend without changing the legacy fallback semantics
- Files touched:
  - `Translators/LmStudioTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - updated `LmStudioTranslator` so the dialogue-context path now:
    - tries one forced function-tool request first
    - reuses the shared OpenAI-compatible JSON helper and structured response
      validator
    - falls back automatically to the old plain-text request path when the
      local backend ignores tool calling, returns malformed JSON, or fails
- Behavior-sensitive risks:
  - some local OpenAI-compatible servers may only partially support strict tool
    calling; this cut treats any malformed or missing structured payload as a
    soft failure and reruns the legacy path
  - only the dialogue-context path attempts structured output
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - carry the same OpenAI-compatible structured path to `DeepSeek`

## Iteration 46 - Issue 148 DeepSeek Structured Dialogue Path

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - carry the same OpenAI-compatible structured dialogue path to `DeepSeek`
    while preserving the existing plain-text fallback semantics
- Files touched:
  - `Translators/DeepSeekTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - updated `DeepSeekTranslator` so the dialogue-context path now:
    - tries one forced function-tool request first
    - reuses the shared structured request prompt helper, the OpenAI-compatible
      JSON extraction helper, and the structured response validator
    - falls back automatically to the old plain-text request path when the
      upstream provider ignores tool calling, returns malformed JSON, or fails
- Behavior-sensitive risks:
  - some DeepSeek-compatible models may only partially support strict tool
    calling; this cut treats any malformed or missing structured payload as a
    soft failure and reruns the legacy path
  - only the dialogue-context path attempts structured output
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - verify in-game across the currently wired structured providers before
    deciding whether to extend the same pattern to additional dialogue-family
    engines

## Iteration 47 - Issue 148 Gemini Structured Dialogue Path

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - add the first Gemini-specific structured dialogue path using the provider's
    documented JSON schema response format while preserving the current
    plain-text fallback
- Files touched:
  - `Translators/GeminiTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - updated `GeminiTranslator` so the dialogue-context path now:
    - tries one structured `responseFormat` request first
    - reuses the shared structured request payload serializer and structured
      response validator
    - falls back automatically to the existing plain-text path when Gemini
      rejects the schema, returns malformed JSON, or otherwise fails
- Provider notes:
  - based on the Gemini structured output documentation, this path uses
    `generationConfig.responseFormat.text.mimeType=application/json` plus the
    narrow dialogue schema
  - only the dialogue-context path attempts structured output
- Behavior-sensitive risks:
  - some configured Gemini models may claim support but still return malformed
    JSON or partial content; this cut treats all of that as a soft failure and
    reruns the legacy path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - evaluate whether `Ollama` should use its documented schema `format` path
    or stay on plain-text until there is a stronger in-game need

## Iteration 48 - Issue 148 Ollama Structured Dialogue Path

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - add the first Ollama-specific structured dialogue path using the official
    `format` JSON schema support while preserving the current plain-text
    fallback semantics
- Files touched:
  - `Translators/OllamaTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - updated `OllamaTranslator` so the dialogue-context path now:
    - tries one `/api/generate` request with `format=<json schema>` first
    - keeps using the existing translator endpoint instead of introducing a
      parallel chat-only code path
    - reuses the shared structured request payload serializer and structured
      response validator
    - falls back automatically to the existing plain-text path when the model
      ignores the schema, returns malformed JSON, or fails
- Provider notes:
  - this path follows the official Ollama structured output guidance for
    `format` with a JSON schema object
  - the structured attempt uses `options.temperature` for the schema request
- Behavior-sensitive risks:
  - some local models exposed through Ollama may not honor structured output
    consistently; this cut treats malformed or missing JSON as a soft failure
    and reruns the legacy path
  - only the dialogue-context path attempts structured output
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether `Claude` should stay disabled for structured mode until a
    provider-specific contract proves stable enough

## Iteration 49 - Issue 148 Claude Structured Dialogue Path

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - add the first Claude-specific structured dialogue path using Anthropic tool
    use with `input_schema`, while preserving the current plain-text fallback
- Files touched:
  - `Translators/Helpers/StructuredDialogueCapabilityHelper.cs`
  - `Translators/Helpers/StructuredDialogueAnthropicToolHelper.cs`
  - `Translators/ClaudeTranslator.cs`
  - `Echoglossian.Tests/StructuredDialogueAnthropicToolHelperTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - upgraded Claude from `Disabled` to `JsonSchema` in the structured-capability
    helper
  - added a narrow Anthropic helper to:
    - expose the stable tool name/description
    - reuse the shared structured schema
    - extract compact JSON from Claude `tool_use` blocks
  - updated `ClaudeTranslator` so the dialogue-context path now:
    - sends one tool-use request first with `tools` and `tool_choice`
    - validates the returned tool input with the shared structured response
      validator
    - falls back automatically to the existing plain-text path when no valid
      `tool_use` payload is returned
- Provider notes:
  - this path follows the Anthropic Messages API tool-use contract instead of
    relying on "JSON in prose"
  - only the dialogue-context path attempts structured output
- Behavior-sensitive risks:
  - some Claude models may still choose odd tool-use behavior or no tool use
    for some prompts; this cut treats that as a soft failure and reruns the
    legacy path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - re-evaluate the issue-148 plan/doc wording now that every major dialogue
    LLM family in the branch has at least one first structured path

## Iteration 50 - Issue 148 Dialogue Glossary Loader Foundation

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - add the first shared dialogue glossary file loader and runtime cache before
    wiring operator-facing config and actual glossary injection into requests
- Files touched:
  - `Translators/StructuredDialogueGlossaryLoadResult.cs`
  - `Translators/Helpers/StructuredDialogueGlossaryLoader.cs`
  - `Translators/StructuredDialogueGlossaryStore.cs`
  - `Echoglossian.Tests/StructuredDialogueGlossaryLoaderTests.cs`
  - `Echoglossian.Tests/StructuredDialogueGlossaryStoreTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a first-pass JSON glossary loader that accepts:
    - a root array of glossary rows
    - or an object document with an `entries` array
  - malformed rows are skipped instead of crashing the whole glossary load
  - added a shared in-memory glossary store with:
    - last-load snapshot state
    - filtered retrieval by source/target language
    - explicit clear/reset behavior
- Behavior-sensitive risks:
  - this cut does not inject glossary rows into any live translator request yet
  - language matching currently uses simple case-insensitive exact string
    comparison on optional language scopes
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - add config, debugger visibility, and a narrow operator-facing control for
    loading the dialogue glossary file

## Iteration 51 - Issue 148 Dialogue Glossary Runtime And Debugger Integration

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - connect the structured dialogue glossary foundation to config, runtime
    refresh, and operator-facing inspection without changing any provider
    prompt path yet
- Files touched:
  - `Config.cs`
  - `Echoglossian.cs`
  - `GeneralHelpers/RuntimeConfigurationRefresh.cs`
  - `Translators/StructuredDialogueGlossaryStore.cs`
  - `PluginUI/Tabs/TranslationEnginesTab.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added config fields for:
    - `EnableDialogueGlossaryInjection`
    - `DialogueGlossaryFilePath`
  - added a runtime-only glossary signature so config saves refresh the shared
    glossary store only when glossary settings change
  - glossary load and clear now happen safely at startup and during runtime
    config refresh
  - hardened glossary refresh against invalid path normalization failures
  - added a `Dialogue glossary` section in the engines tab with:
    - enable toggle
    - file-path input
    - reload button
    - clear button
    - inline snapshot feedback
  - added debugger visibility and reload/clear controls for glossary state
- Behavior-sensitive risks:
  - this cut still does not inject glossary rows into any live provider prompt
  - the runtime clears the shared glossary store whenever glossary injection is
    disabled or the path is blank
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - inject active glossary rows into the structured dialogue request path for
    the LLM providers already using issue-148 structured dialogue

## Iteration 52 - Issue 148 Structured Dialogue Glossary Injection

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - inject the active shared dialogue glossary rows into the structured
    request contract for the LLM providers already running issue-148
    structured dialogue
- Files touched:
  - `Translators/ChatGPTTranslator.cs`
  - `Translators/LmStudioTranslator.cs`
  - `Translators/OpenRouterTranslator.cs`
  - `Translators/DeepSeekTranslator.cs`
  - `Translators/GeminiTranslator.cs`
  - `Translators/OllamaTranslator.cs`
  - `Translators/ClaudeTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - each structured dialogue path now reads the active glossary rows from the
    shared store using the current source and target languages
  - those rows are passed into the existing structured request builder so the
    provider-specific serializers can include glossary data without inventing a
    second glossary pipeline
  - when glossary injection is disabled or unloaded, the store returns no rows
    and the structured request stays otherwise unchanged
- Behavior-sensitive risks:
  - glossary quality now directly affects structured dialogue requests when the
    feature is enabled, so malformed but loadable term choices can degrade
    output quality even though they no longer crash the runtime
  - this cut still keeps the plain-text fallback path intact for every engine
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether glossary activity should surface explicitly in the debugger
    metrics or per-request structured diagnostics

## Iteration 53 - Issue 148 Structured And Glossary Metrics

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - make issue-148 structured-path activity visible in the existing
    `Translator Debugger and Metrics` window without adding a parallel
    diagnostics subsystem
- Files touched:
  - `Translators/TranslatorMetricsCollector.cs`
  - `Echoglossian.Tests/TranslatorMetricsCollectorTests.cs`
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - extended the shared translator metrics collector with:
    - `StructuredRequestCount`
    - `StructuredSuccessCount`
    - `GlossaryAugmentedStructuredRequestCount`
    - `LastStructuredFailureReason`
  - updated the debugger summary and per-engine table to expose those counts
- Behavior-sensitive risks:
  - this cut only adds lightweight in-memory counters; it does not change DB
    semantics or request routing
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - emit the structured/glossary metrics from the individual structured
    translator paths

## Iteration 54 - Resourceize LLM Debugger And Glossary UI

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - remove user-facing hardcoded literals from the new LLM debugger, glossary,
    and OpenAI-compatible UI paths so those surfaces resolve through
    `Resources` instead of inline English strings
- Files touched:
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `PluginUI/Tabs/TranslationEnginesTab.cs`
  - `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
  - `Properties/Resources.resx`
  - `Properties/Resources.pt.resx`
  - `Properties/Resources.pt-BR.resx`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - replaced branch-added debugger window title, button labels, summaries,
    table headers, and glossary status strings with `Resources` lookups
  - replaced glossary section literals in the translation engines tab with
    resource-backed keys
  - removed fallback English literals from the OpenAI-compatible provider UI
    path in `ChatGptEngineUI`
  - added the missing base, `pt`, and `pt-BR` resource entries for the new
    glossary/debugger strings
- Behavior-sensitive risks:
  - non-Portuguese localized resource files still inherit the base resource for
    the newly added glossary/debugger keys until a broader localization pass is
    done
  - this cut does not change routing, persistence, or request semantics; it is
    strictly UI text plumbing
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - expand the new glossary/debugger resource keys into the remaining localized
    `.resx` files if we want parity beyond base fallback + Portuguese

## Iteration 55 - Enforce Direct Resource Access In UI

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - remove the remaining ad hoc UI text wrapper pattern from the branch work
    so plugin UI and notifications use `Resources.Key` directly, matching the
    repo rule and avoiding hidden fallback literals
- Files touched:
  - `PluginUI/TranslatorMetricsWindow.cs`
  - `PluginUI/Tabs/TranslationEnginesTab.cs`
  - `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
  - `PluginUI/Components/ModelDropdownUI.cs`
  - `PluginUI/Tabs/TooltipTab.cs`
  - `Properties/Resources.Designer.cs`
  - `Echoglossian.xml`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - removed the branch-local `GetText` / `GetUiString` helper usage from the
    LLM debugger, OpenAI-compatible provider UI, model dropdown, tooltip tab,
    and dialogue override UI paths
  - switched those call sites to direct `Resources.Key` access
  - regenerated `Resources.Designer.cs` so the new and existing keys are
    available through the strongly typed resource surface
  - kept `Echoglossian.xml` in sync with the validated code change so the
    committed XML documentation output matches the branch state
- Behavior-sensitive risks:
  - this cut is still presentation-only, but any missing strongly typed
    resource property would now fail at build time instead of silently falling
    back to inline English
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - make every selectable LLM engine use dynamic model-list refresh
    consistently when live model fetching is enabled

## Iteration 56 - Refresh Live LLM Model Lists Dynamically

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - make every selectable LLM engine refresh its live model catalog
    consistently from the provider API when live model fetching is enabled,
    instead of leaving some UIs stuck on predefined defaults
- Files touched:
  - `PluginUI/EngineConfigUI/LiveModelRefreshCoordinator.cs`
  - `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
  - `PluginUI/EngineConfigUI/ClaudeEngineUI.cs`
  - `PluginUI/EngineConfigUI/DeepSeekEngineUI.cs`
  - `PluginUI/EngineConfigUI/GeminiEngineUI.cs`
  - `PluginUI/EngineConfigUI/LibreTranslateEngineUI.cs`
  - `PluginUI/EngineConfigUI/LmStudioEngineUI.cs`
  - `PluginUI/EngineConfigUI/OllamaEngineUI.cs`
  - `PluginUI/EngineConfigUI/OpenRouterEngineUI.cs`
  - `PluginUI/Helpers/FieldValidationHelper.cs`
  - `PluginUI/Helpers/UIWarningHelpers.cs`
  - `Translators/LmStudio/LmStudioModelManager.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added a shared coordinator so live model refreshes trigger once per input
    signature instead of every frame
  - `Gemini` now actually binds its dropdown to `GeminiModelManager`
    whenever `Fetch live models` is enabled
  - `ChatGPT/OpenAI-family`, `Claude`, `DeepSeek`, `OpenRouter`, `Ollama`,
    and `LM Studio` now auto-refresh on first enable and when the relevant
    API key or endpoint changes, while still exposing `Reload`
  - `LM Studio` gained the same `ResetToDefault()` fallback surface already
    used by the other managers
  - removed the remaining `ResourceManager.GetString(... ) ?? ...` UI
    fallbacks found during the sweep so the branch stays aligned with direct
    `Resources.Key` usage
- Behavior-sensitive risks:
  - live model refresh is now more eager when credentials or endpoints change,
    so transient provider errors will show up sooner instead of waiting for a
    manual reload
  - the predefined static model lists remain as the fallback path when live
    fetch is disabled or a provider refresh fails
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether the debugger should expose per-engine live model refresh
    status beyond the current OpenAI-family diagnostics

## Iteration 57 - Refresh Engine Language Support Tables

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - refresh the per-language engine compatibility tables so the vendor-backed
    engines reflect their currently documented or publicly exposed language
    support instead of the older mixed hardcoded sets
- Files touched:
  - `LanguagesHandling/LanguageEngineSupport.cs`
  - `Echoglossian.Tests/LanguageEngineSupportTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - updated the `Microsoft` engine language set to the current official public
    `Translator Text` languages endpoint result
  - updated the `Amazon` engine language set to the current official Developer
    Guide language table, including newer targets such as `cy`, `es-MX`,
    `fr-CA`, and `fa-AF`
  - updated the `LibreTranslate` engine language set to the current public
    upstream instance list, including `he`, `hi`, `id`, `lt`, `lv`, `ms`,
    `th`, `ur`, and variant-specific Chinese codes
  - updated the `YandexCloud` / `YandexPublic` language table to the current
    official supported-languages page, including newer documented codes such as
    `gd`, `bua`, `kazlat`, `kbd`, `krc`, `kv`, `mdf`, `mhr`, `mrj`, `myv`,
    `tyv`, `udm`, and `uzbcyr`
  - refreshed the `DeepL` target language set to the broader current official
    supported-languages documentation, including the newer beta target
    languages and current regional variants
  - added normalization aliases so official vendor codes such as `zh-Hans`,
    `zh-Hant`, `fil`, `nb`, `sr-Cyrl`, and `tlh-Latn` still match the plugin's
    existing language codes
  - added regression tests for new vendor-backed support cases across DeepL,
    Microsoft, Amazon, LibreTranslate, and Yandex
- Behavior-sensitive risks:
  - some languages will now lose engines that are no longer in the current
    official vendor language tables
  - Microsoft and LibreTranslate support for Chinese variants now depends on
    the new normalization aliases instead of legacy duplicate hardcoded codes
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - consider whether Google's explicitly tracked common-code list should also
    be refreshed even though Google/GTranslate are still treated as broad
    support for rare variants

## Iteration 58 - Expand Target Language Dictionary Safely

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - turn the refreshed vendor language tables into actual selectable plugin
    target languages where the repo already has safe script coverage, while
    avoiding a blind add of entries that would immediately need missing font
    assets or duplicate an existing alias
- Files touched:
  - `LanguagesHandling/LanguagesDictionary.cs`
  - `GeneralHelpers/RuntimeLanguageHelper.cs`
  - `Echoglossian.Tests/RuntimeLanguageHelperTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - appended new selectable target languages at the end of
    `LanguagesDictionary`, including regional variants and newly covered
    vendor-backed codes that can reuse fonts already present in the repo
  - added the first safe batch for:
    `es-MX`, `fr-CA`, `fa-AF`, `bho`, `dsb`, `fo`, `hne`, `hsb`,
    `iu-Latn`, `kmr`, `ks`, `lug`, `lzh`, `mn-Cyrl`, `mn-Mong`, `mww`,
    `nso`, `otq`, `prs`, `run`, `sr-Cyrl`, `tlh-Latn`, `tlh-Piqd`,
    `yua`, `yue`, `bua`, `kazlat`, `kbd`, `krc`, `kv`, `mdf`, `mhr`,
    `mrj`, `myv`, `pap`, `tyv`, `udm`, and `uzbcyr`
  - kept `zh-Hans` / `zh-Hant` out of the selection list because the plugin
    already exposes `zh-CN` / `zh-TW`; instead, runtime aliases now normalize
    those script tags back onto the existing plugin-facing target codes
  - added runtime normalization coverage for the new Chinese script aliases
- Deferred on purpose:
  - `bo`, `ikt`, `iu`, and `mni` were not added in this cut because they need
    either missing font assets or a more careful script decision before they
    can be exposed safely
  - the repo also already references a few older Noto font files that are not
    present in this checkout (`NotoSansEthiopic-Medium.ttf`,
    `NotoSansNKo-Regular.ttf`, `NotoSansOlChiki-Regular.ttf`,
    `NotoSansThaana-Medium.ttf`, `NotoSansTibetan-Medium.ttf`); this cut did
    not try to silently paper over that broader asset gap
- Behavior-sensitive risks:
  - the target-language dropdown gets longer immediately, so user selection
    ordering changes even though the new entries were appended, not inserted
  - some new entries are only supported by a subset of engines, so their
    engine dropdown will look intentionally narrower than mainstream languages
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - reconcile the missing font assets already referenced in the repo before
    exposing the remaining deferred script-heavy languages

## Iteration 59 - Reconcile Downloadable Font Assets And Deferred Script Targets

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - close the safe part of the font-asset gap by teaching the plugin that the
    already-referenced non-CJK Noto fonts are downloadable assets too, then
    expose the deferred target languages that become safe once those assets are
    explicit
- Files touched:
  - `GeneralHelpers/AssetsManager.cs`
  - `Echoglossian.cs`
  - `LanguagesHandling/LanguagesDictionary.cs`
  - `Echoglossian.Tests/AssetsManagerTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - expanded the downloadable asset registry beyond CJK fonts to include:
    `NotoSansThaana-Medium.ttf`, `NotoSansEthiopic-Medium.ttf`,
    `NotoSansNKo-Regular.ttf`, `NotoSansOlChiki-Regular.ttf`,
    `NotoSansCanadianAboriginal-Regular.ttf`, and
    `NotoSerifTibetan-Regular.ttf`
  - aligned plugin startup so those font file names are treated as managed
    downloadable assets from the first runtime asset check, instead of looking
    like bundled files that just happen to be missing from disk
  - switched `dz` from the non-existent `NotoSansTibetan-Medium.ttf` reference
    to the official downloadable `NotoSerifTibetan-Regular.ttf`
  - appended the deferred-but-now-safe script-heavy target languages:
    `bo`, `iu`, and `ikt`
  - added asset-manager regression coverage for a non-CJK downloadable script
    font so this path no longer depends on CJK-only tests
- Deferred on purpose:
  - `mni` is still not exposed because the right script/font decision is not
    yet obvious enough to make safely in the same cut
- Behavior-sensitive risks:
  - users selecting `dz`, `bo`, `iu`, or `ikt` now correctly enter the
    downloadable-asset flow; if the new Noto assets are missing, the plugin
    will flag them as such instead of silently pointing at a bundled path that
    does not exist
  - the target-language list gets three more appended entries, but no existing
    ids were reordered
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - decide whether `mni` should be exposed through a safe script-specific font
    path, or remain intentionally unsupported until there is a better script
    strategy

## Iteration 60 - Reclassify Backlog Around Current LLM Fallout

- Date: 2026-05-15
- Branch: `llm-translation-rework`
- Goal:
  - refresh the versioned backlog after rereading the current open issues and
    their latest comments, so the branch keeps an explicit record of how the
    LLM rework now relates to live user reports
- Files touched:
  - `docs/github-issue-backlog.md`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - updated the backlog snapshot date to `2026-05-15`
  - promoted the new provider/runtime reports `#204` and `#203` into the top
    backlog bucket
  - regrouped `#174`, `#201`, `#176`, `#196`, and `#148` into one explicit
    active LLM / IA rework cluster
  - kept the quest/native-layout issues (`#189`, `#188`, `#187`, `#172`,
    `#181`) separate from the engine/runtime cluster instead of mixing them
    under one generic release-fallout bucket
- Validation:
  - docs-only update; no build or test run in this iteration
- Next cut:
  - keep the issue tracker and the active PR scope aligned as `#203` and `#204`
    become clearer

## Iteration 61 - Backport The OpenRouter Prompt-Expansion Guard Into The Shared Helper

- Date: 2026-05-16
- Branch: `llm-translation-rework`
- Goal:
  - close the `#204` prompt-expansion regression in the branch-local shared
    prompt helper, so the fix already merged into `v4-series` is not lost while
    the LLM rework continues on top
- Files touched:
  - `PluginUI/Helpers/PromptTemplateManager.cs`
  - `Echoglossian.Tests/TranslatorContractTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - changed `PromptTemplateManager.RenderPrompt(...)` to substitute
    `{sourceLanguage}` and `{targetLanguage}` before injecting `{text}`
  - added deterministic contract coverage proving that literal placeholder text
    inside the source dialogue is preserved instead of being reprocessed after
    insertion
  - added a second regression test confirming the normal placeholder-expansion
    path still resolves the standard prompt variables as expected
- Behavior-sensitive risks:
  - this helper now affects every translator path that shares
    `PromptTemplateManager`, not just `OpenRouter`, so the change intentionally
    broadens the fix to the entire shared prompt-expansion path
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - verify whether any other branch-local prompt helpers still bypass
    `PromptTemplateManager` and would therefore need the same ordering guard

## Iteration 62 - Prepare A Deterministic Testing-Channel Build For The LLM Rework

- Date: 2026-05-16
- Branch: `llm-translation-rework`
- Goal:
  - bump the branch-local plugin version to a deterministic testing-build value
    so the `DalamudPluginsD17` testing submission can point at a clearly newer
    rework build without inventing a parallel version-series scheme
- Files touched:
  - `Echoglossian.csproj`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - advanced the hardcoded version segments from `4.2601.0512.1000` to
    `4.2601.0516.1300`
  - kept the existing stable `01` series intact, so the distinction between
    stable and testing comes from the `DalamudPluginsD17` channel path
    (`stable/...` vs `testing/live/...`) rather than a fake testing-only
    version series
- Behavior-sensitive risks:
  - this is a real plugin version bump on the rework branch, so local builds
    and testing-channel installs will report `4.2601.0516.1300`
  - no GitHub release/tag is created for this testing-only submission; the
    official testing manifest will point directly at the branch commit
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- Next cut:
  - create `testing/live/Echoglossian` in the local `DalamudPluginsD17` fork
    and open the official testing-channel PR against `goatcorp`

## Iteration 63 - Refresh The Rework Branch On Top Of The Latest `v4-series`

- Date: 2026-07-08
- Branch: `llm-translation-rework`
- Goal:
  - merge the latest `origin/v4-series` into the rework branch, re-read the
    still-open PR review debt, and preserve the LLM rework codepaths while
    closing the immediate merge fallout before the remaining review fixes
- Files touched:
  - `Echoglossian.csproj`
  - `Echoglossian.Tests/Echoglossian.Tests.csproj`
  - `Properties/Resources.Designer.cs`
  - `Properties/Resources.resx`
  - `Properties/Resources.pt.resx`
  - `Properties/Resources.pt-BR.resx`
  - `Translators/ChatGPTTranslator.cs`
  - `docs/llm-translation-rework-iteration-log.md`
  - plus the upstream `origin/v4-series` merge payload
- What changed:
  - merged the current `origin/v4-series` state into `llm-translation-rework`
    and resolved the translator conflicts in favor of the newer rework
    implementations so the structured-dialogue and OpenAI-compatible paths were
    not overwritten by older upstream translator constructors
  - kept the upstream safety improvement for empty ChatGPT responses by guarding
    the first `completion.Content` read instead of assuming index `0` always
    exists
  - carried the latest release-date patch segment into the branch-local testing
    build version, landing on `4.2601.0531.1300`
  - unioned the new toast-routing and addon-probe resource keys from
    `v4-series` into the rework branch resource files, then manually restored
    the strongly typed `Resources` accessors required for that merged UI
  - added the missing `FluentAssertions` package reference to the test project
    so the structured-dialogue test suite could compile far enough to expose
    the remaining branch-local test drift instead of failing immediately on a
    missing assertion library
- Behavior-sensitive risks:
  - the merge intentionally keeps the rework translator codepaths as the source
    of truth, so any future comparison against `v4-series` needs to remember
    that the branch is not meant to fall back to the older prompt-only LLM
    translators
  - `Echoglossian.xml` was regenerated as part of the validated merge state and
    should travel with the merge checkpoint
- Validation:
  - `dotnet restore Echoglossian.sln`
  - `dotnet build Echoglossian.sln -c Debug --no-restore` : passed
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
    : initially failed because the fresh worktree had no built test assembly
  - `dotnet restore Echoglossian.Tests\Echoglossian.Tests.csproj`
  - `dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore`
    : currently fails on pre-existing branch-local test drift, now surfaced as:
    missing `Resources` imports in several tests, stale
    `TranslatorMetricsCollectorTests` constructor arguments, and stale
    `BuildPrompt` expectations in `TranslatorContractTests`
- Next cut:
  - commit this merge checkpoint, then fix the remaining PR `#202` review items
    and the now-exposed test drift in small validated slices, starting with the
    structured-dialogue capability mismatch and dialogue-session context bugs

## Iteration 64 - Realign The Branch Test Suite With The Current LLM Runtime

- Date: 2026-07-08
- Branch: `llm-translation-rework`
- Goal:
  - restore a reliable validation baseline after the merge checkpoint by
    fixing the branch-local test drift that no longer matched the current LLM
    runtime and structured-dialogue plumbing
- Files touched:
  - `Echoglossian.Tests/OpenAiProviderConfigurationTests.cs`
  - `Echoglossian.Tests/StructuredDialogueCapabilityHelperTests.cs`
  - `Echoglossian.Tests/TranslationFailureTextClassifierTests.cs`
  - `Echoglossian.Tests/TranslationServiceTests.cs`
  - `Echoglossian.Tests/TranslatorContractTests.cs`
  - `Echoglossian.Tests/TranslatorMetricsCollectorTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - added the missing `Resources` imports needed by the newer localized
    unavailable-message assertions
  - aligned `StructuredDialogueCapabilityHelperTests` with the branch runtime,
    which already routes Claude through the structured path instead of keeping
    it in the disabled bucket
  - removed stale `BuildPrompt(...)` expectations from
    `TranslatorContractTests` now that `ChatGPT` and `OpenRouter` share prompt
    rendering through `PromptTemplateManager` instead of exposing separate
    translator helpers
  - updated `TranslatorMetricsCollectorTests` for the current
    `Record(...)` signature
  - rewrote the failing `TranslationServiceTests` cases around empty and
    synthetic outputs so they assert the current transient-failure behavior
    rather than the older persistent-failure path
- Behavior-sensitive risks:
  - this cut is test-only; it does not change runtime translation behavior
  - the Claude capability expectation now explicitly documents the branch's
    current structured-dialogue intent, so if Claude is later re-deferred the
    code and tests will need to move together
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
    : passed (`319/319`)
- Next cut:
  - implement the remaining runtime review fixes for PR `#202`: dialogue
    session history-limit enforcement, live model-refresh signature scrubbing,
    and OpenAI-compatible structured payload lifetime cleanup

## Iteration 65 - Enforce The Active Dialogue History Limit Before Context Capture

- Date: 2026-07-08
- Branch: `llm-translation-rework`
- Goal:
  - fix the remaining `DialogueTranslationSessionStore.BuildContext(...)`
    review bug where lowering the configured history limit could still return
    stale prior turns above the new limit on the very next request
- Files touched:
  - `Translators/DialogueTranslationSessionStore.cs`
  - `Echoglossian.Tests/DialogueTranslationSessionStoreTests.cs`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - trimmed the retained in-memory turn list against the active `historyLimit`
    before copying `priorTurns` for the current request, instead of only after
    appending the new turn
  - added regression coverage proving that a session created under a larger
    history limit immediately respects a later smaller limit and only returns
    the newest retained turn
- Behavior-sensitive risks:
  - this only changes the in-memory runtime-only dialogue context returned to
    LLM requests when the configured limit shrinks; stable-limit behavior and
    session TTL pruning remain unchanged
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
    : passed (`320/320`)
- Next cut:
  - scrub raw API-key material out of live model-refresh signatures and clean
    up the OpenAI-compatible structured payload helper's JSON-document lifetime

## Iteration 66 - Scrub Live Model Refresh Signatures And Clone Structured Schema Elements

- Date: 2026-07-08
- Branch: `llm-translation-rework`
- Goal:
  - close the remaining PR `#202` LLM review debt by removing raw API-key
    material from live model-refresh cache signatures and fixing the
    OpenAI-compatible structured payload helper so it never returns a
    `JsonElement` backed by a disposed `JsonDocument`
- Files touched:
  - `PluginUI/EngineConfigUI/ChatGptEngineUI.cs`
  - `PluginUI/EngineConfigUI/ClaudeEngineUI.cs`
  - `PluginUI/EngineConfigUI/DeepSeekEngineUI.cs`
  - `PluginUI/EngineConfigUI/GeminiEngineUI.cs`
  - `PluginUI/EngineConfigUI/LmStudioEngineUI.cs`
  - `PluginUI/EngineConfigUI/OpenRouterEngineUI.cs`
  - `PluginUI/EngineConfigUI/LiveModelRefreshSignatureHelper.cs`
  - `Translators/Helpers/StructuredDialogueOpenAiCompatiblePayloadHelper.cs`
  - `Echoglossian.Tests/LiveModelRefreshSignatureHelperTests.cs`
  - `Echoglossian.Tests/StructuredDialogueOpenAiCompatiblePayloadHelperTests.cs`
  - `Echoglossian.xml`
  - `docs/llm-translation-rework-iteration-log.md`
- What changed:
  - introduced a shared live-refresh signature helper that normalizes ordinary
    inputs and reduces sensitive components to short stable hashes instead of
    embedding raw API keys in signature strings
  - updated the ChatGPT, Claude, DeepSeek, Gemini, LM Studio, and OpenRouter
    engine UIs to build their refresh signatures through that helper, with LM
    Studio only including the API-key hash when auth is enabled
  - changed
    `StructuredDialogueOpenAiCompatiblePayloadHelper.BuildFunctionParametersJsonElement()`
    to clone the root element before disposing the temporary parsed document
  - added regression coverage for both the secret-scrubbing signature behavior
    and the cloned `JsonElement` lifetime guarantee
- Behavior-sensitive risks:
  - live model-refresh caches will observe a one-time signature change because
    the format now stores hashed secret markers instead of raw values, but the
    refresh invalidation inputs remain behaviorally equivalent
  - the structured dialogue schema payload is unchanged on the wire; this only
    removes a disposed-document lifetime hazard for callers that hold the
    returned `JsonElement`
- Validation:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
    : passed (`323/323`)
- Next cut:
  - re-scan PR `#202` unresolved review state and decide whether any remaining
    debt is code, tests, or only GitHub thread follow-up

## Iteration 67 - Prepare The Local In-Game LLM Test Path And Operator Playbook

- Date: 2026-07-08
- Branch: `llm-translation-rework`
- Goal:
  - prepare the workstation for branch-local in-game LLM validation with a
    reversible config backup, a single-engine first-test setup, and an
    operator-focused playbook for acquiring provider credentials and running
    smoke and deep coverage passes
- Files touched:
  - `docs/llm-ingame-test-playbook.md`
  - `docs/llm-translation-rework-iteration-log.md`
- Local operator actions performed:
  - backed up the active plugin config from
    `%AppData%\XIVLauncher\pluginConfigs\Echoglossian.json`
  - switched only the selected engine in the local config from `Google` to
    `ChatGPT`, leaving the rest of the operator's translation and UI settings
    intact
- What changed:
  - added a dedicated in-game LLM test playbook that records the current local
    config state, clarifies which engines require API keys, links the official
    provider key-generation pages, and defines a recommended in-game smoke and
    deep test order
  - documented the local backup and restore flow so operator-side testing can
    move quickly without losing the pre-test config snapshot
- Behavior-sensitive risks:
  - the repo change is documentation-only, but the local operator config now
    points to `ChatGPT` and will remain activation-blocked until a valid
    OpenAI key is supplied and the plugin is reloaded
  - external edits to the config file require a plugin reload or game restart
    before the in-memory runtime matches disk
- Validation:
  - not run; repo changes are documentation-only
- Next cut:
  - perform the actual in-game smoke pass on `ChatGPT`, then expand into the
    deeper multi-engine matrix from the playbook

## Iteration 68 - Record The First Worktree In-Game Validation Results

- Date: 2026-07-10
- Branch: `llm-translation-rework`
- Goal:
  - capture the first real in-game validation outcome after redirecting
    Dalamud to the worktree build, and document the current interpretation of
    the observed queued-translation cancellations
- Files touched:
  - `docs/llm-ingame-test-playbook.md`
  - `docs/llm-translation-rework-iteration-log.md`
- Runtime findings recorded:
  - the game is now loading the worktree DLL from
    `C:\Dante\_dalamud\worktrees\Echoglossian\llm-translation-rework\bin\x64\Debug\win-x64\Echoglossian.dll`
  - `/eglotranslatordebugger` opens successfully on the rework build
  - the rework runtime has already translated and persisted output across
    multiple surfaces, including `Talk`, quest-family text,
    `AddonContextMenuTitle`, and `CharacterClass`
  - only two `QueuedTranslationBroker` `TaskCanceledException` events were
    observed in the worktree-backed session, both in prefetch-oriented paths
- What changed:
  - updated the in-game test playbook with the current workstation findings and
    a practical operator rule for treating isolated broker cancellations as
    benign unless they correlate with visible translation loss
- Behavior-sensitive risks:
  - the current benign classification depends on the observed session context:
    successful translation continued before and after the cancellations
  - if later sessions show repeated steady-state cancellations with missing
    visible translations, the broker path should be re-opened as a real bug
- Validation:
  - not run; repo changes are documentation-only
- Next cut:
  - continue deeper in-game coverage and only promote the broker cancellations
    to active bug work if they become user-visible or reproducible without
    reload or manual translation shutdown
