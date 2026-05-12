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
