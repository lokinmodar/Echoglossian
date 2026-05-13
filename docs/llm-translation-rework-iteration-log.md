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
