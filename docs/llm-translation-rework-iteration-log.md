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
