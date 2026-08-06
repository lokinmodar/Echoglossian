# Issue 176 local LLM latency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Explain and remove Echoglossian-owned Ollama/LM Studio delay so measured warm-path overhead is small relative to provider generation time, while preserving sequential safety and UI responsiveness.

**Architecture:** Instrument end-to-end translation phases with aggregate timings, then optimize only measured plugin-owned costs. Consume #148's capability breaker to eliminate repeated structured-then-plain calls, remove artificial local-engine queue spacing while retaining the broker's single sequential pump, and benchmark streaming/response-reading variants behind parity tests before selecting them.

**Tech Stack:** C#/.NET 10, `Stopwatch`, existing `TranslatorMetricsCollector` and `QueuedTranslationBroker`, fake HTTP handlers, Ollama/LM Studio local APIs, xUnit, and PowerShell.

## Global Constraints

- Branch from the merged #148 result as `issue-176-local-llm-latency`.
- Do not promise sub-0.5-second total latency when the model/provider itself exceeds it; report provider versus plugin time separately.
- No per-request `HttpClient`, no parallel requests to a single local model, no busy polling, and no frame-based retry loop.
- Metrics are aggregate and content-free: no prompts, dialogue, glossary terms, URLs with secrets, or API keys.
- The local broker remains sequential; changing spacing to zero removes idle delay, not concurrency control.
- Any streaming implementation must pass output-parity, cancellation, malformed-stream, and resource-disposal tests and must measurably improve the benchmark before becoming default.
- All provider and DB work stays on the async foundation from prior issues.

---

## File map

### New files

- `Translators/TranslationPipelinePhase.cs` — queue, DB, provider, validation, persistence, publication phases.
- `Translators/TranslationPipelineTiming.cs` — immutable per-request aggregate input.
- `Echoglossian.Tests/LocalLlmTransportTests.cs` — fake-handler request count, connection reuse contract, cancellation, and optional stream parsing.
- `scripts/Measure-LocalLlmTranslation.ps1` — safe repeatable localhost benchmark producing content-free JSON/CSV summary.

### Modified files

- `Translators/TranslatorMetricsCollector.cs` and `PluginUI/TranslatorMetricsWindow.cs` — per-phase aggregate p50/p95/count display.
- `NativeUI/Helpers/QueuedTranslationBroker.cs` — enqueue timestamp and zero local idle spacing.
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`, `BattleTalkHandler.cs` — DB/provider/persistence/publication phase marks.
- `Translators/OllamaTranslator.cs`, `LmStudioTranslator.cs` — transport timings and selected response-reading optimization.
- `Echoglossian.Tests/TranslatorMetricsCollectorTests.cs`, `QueuedTranslationBrokerTests.cs`, and handler lifecycle tests.
- `Properties/Resources.resx`, `Properties/Resources.en-US.resx`, `Properties/Resources.pt-BR.resx`, and `Properties/Resources.Designer.cs` for metric labels.

## Task 1: Add content-free phase metrics

- [ ] Add collector tests for phase counts, p50/p95 aggregation, bounded sample retention, cancellation/failure outcomes, reset, and concurrent recording.
- [ ] Define phases as `QueueWait`, `DatabaseLookup`, `ProviderRequest`, `ResponseValidation`, `Persistence`, and `ManagedPublication`; define total separately.
- [ ] Record monotonic durations with `Stopwatch.GetTimestamp`; never derive phase duration from wall-clock `DateTime`.
- [ ] Add aggregate fields to the debugger UI with resx labels. Drawing reads snapshots only and performs no I/O.
- [ ] Instrument Talk/BattleTalk and the broker at existing stage boundaries. Do not add per-line log output.
- [ ] Run metrics/lifecycle tests and commit:

```powershell
git add -- Translators/TranslationPipelinePhase.cs Translators/TranslationPipelineTiming.cs Translators/TranslatorMetricsCollector.cs PluginUI/TranslatorMetricsWindow.cs NativeUI/Helpers/QueuedTranslationBroker.cs NativeUI/AddonHandlers/Talk Properties Echoglossian.Tests
git commit -m "feat(#176): measure translation pipeline phases"
```

## Task 2: Remove known avoidable local wait and double requests

- [ ] Add a broker timing test with two local requests and assert the second starts immediately after the first completes, while an online LLM retains its configured pacing.
- [ ] Change `ResolveMinimumRequestSpacing` for Ollama/LM Studio to `TimeSpan.Zero`; keep the single pump and existing timeout/failure cooldown.
- [ ] Add fake-provider tests proving a known incompatible structured model makes at most one structured attempt until #148's breaker expires, and subsequent Auto requests make one plain request.
- [ ] Assert PlainText always makes one request and Structured never silently makes two.
- [ ] Run broker/capability/local transport tests and commit:

```powershell
git add -- NativeUI/Helpers/QueuedTranslationBroker.cs Translators/TranslationService.cs Echoglossian.Tests/QueuedTranslationBrokerTests.cs Echoglossian.Tests/LocalLlmTransportTests.cs
git commit -m "perf(#176): remove avoidable local request delay"
```

## Task 3: Build a repeatable localhost benchmark

- [ ] Add `scripts/Measure-LocalLlmTranslation.ps1` with explicit parameters for provider, base URL, model, iterations, warmups, source/target, and output path. Reject non-loopback URLs unless the caller explicitly opts in.
- [ ] Record cold/warm total, provider elapsed, request count, response size, success classification, p50/p95, and configured structured mode. Do not write prompt text or credentials.
- [ ] Make the script UTF-8-safe on Windows and return nonzero on connection/config/response failure.
- [ ] Document usage in the script comment help and run `Get-Help` plus a fake/local smoke test.
- [ ] Commit:

```powershell
git add -- scripts/Measure-LocalLlmTranslation.ps1
git commit -m "test(#176): add local LLM latency benchmark"
```

## Task 4: Evaluate and select response transport optimizations

- [ ] Add injectable `HttpMessageHandler` constructor seams for Ollama/LM Studio tests without changing production construction.
- [ ] Test that each translator reuses its single `HttpClient`, sends cancellation to `SendAsync`, disposes responses, and produces identical cleaned text for buffered and candidate streaming fixtures.
- [ ] Benchmark these exact candidates after warmup: current buffered non-stream response; `SendAsync(..., ResponseHeadersRead, token)` with async content read; Ollama NDJSON streaming; LM Studio SSE streaming if the configured server/model supports it.
- [ ] Select a candidate as default only if output/error parity passes and its median or p95 end-to-end time improves by at least 10% or 100 ms over 20 warm requests. Otherwise keep buffered transport and record “no transport change” in the PR.
- [ ] If streaming wins, implement bounded incremental parsing with cancellation and a maximum response size. Publish only the final validated text; do not repaint partial tokens per frame in this issue.
- [ ] Commit the chosen code/tests, or commit only the benchmark evidence/tests when no transport change qualifies:

```powershell
git add -- Translators/OllamaTranslator.cs Translators/LmStudioTranslator.cs Echoglossian.Tests/LocalLlmTransportTests.cs docs
git commit -m "perf(#176): optimize measured local transport overhead"
```

## Task 5: Validate and close #176

- [ ] Run focused metrics/broker/transport tests, full build/tests, Mock tests if handler instrumentation changed hosted behavior, `git diff --check`, and include `Echoglossian.xml` if changed.
- [ ] Reproduce the issue's LM Studio Qwen-class setup when available and run at least 20 warm requests in PlainText and Auto. Record request count and phase p50/p95.
- [ ] Acceptance: UI callbacks return immediately; no plugin-added 250 ms idle gap remains for local engines; compatible warm requests use one provider call; measured non-provider overhead is documented and has no unexplained 1–2 second segment.
- [ ] If total remains above 0.5 seconds, show whether provider generation/queueing accounts for it rather than claiming an Echoglossian fix that measurements do not support.
- [ ] Attach the content-free before/after report to #176 and open the PR to `v4-series`.
