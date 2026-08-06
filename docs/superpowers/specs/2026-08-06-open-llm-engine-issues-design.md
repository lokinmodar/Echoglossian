# Open LLM Translation Engine Issues Design

**Date:** 2026-08-06

**Repository:** `lokinmodar/Echoglossian`

**Target branch:** `v4-series`

## Objective

Resolve every currently open Echoglossian issue that directly concerns an LLM
translation engine, while preserving the shared translation architecture, database
semantics, display-mode behavior, runtime stability, and game UI responsiveness.

No provider, database, file, model-list, prompt, glossary, or session operation may
block the game framework or ImGui render thread.

The work is ordered by technical dependency so that shared root causes are fixed
before provider-specific diagnostics and performance work.

## Scope

The implementation scope contains these issues that were open on 2026-08-06:

1. [#214](https://github.com/lokinmodar/Echoglossian/issues/214) — first dialogue line lacks speaker context.
2. [#252](https://github.com/lokinmodar/Echoglossian/issues/252) — custom prompt and glossary appear ineffective.
3. [#209](https://github.com/lokinmodar/Echoglossian/issues/209) — disable or limit dialogue context for local LLMs.
4. [#148](https://github.com/lokinmodar/Echoglossian/issues/148) — complete structured input/output, glossary, metadata, and compatible session support.
5. [#176](https://github.com/lokinmodar/Echoglossian/issues/176) — local LLM translation latency and prompt overhead.
6. [#171](https://github.com/lokinmodar/Echoglossian/issues/171) — DeepSeek reports translation unavailable despite configured credentials.
7. [#203](https://github.com/lokinmodar/Echoglossian/issues/203) — mixed report of engines and surfaces not translating.

Issue #12 is a tracker and is updated as the focused issues close. Issue #239 is
shared transport hardening and is only a dependency watch item. Issue #212 is not
in scope because DeepL is not an LLM engine, except where DeepL must be isolated
from the mixed reproduction matrix in #203.

Closed issues are not requirements, regression sources, or implementation scope.
They may not be reopened or promoted automatically.

Issues opened after this inventory require a separate scope decision and do not
silently expand this program.

## Non-goals

- No parallel translation service, provider queue, overlay path, or persistence path.
- No destructive database migration or automatic deletion of stored translations.
- No universal provider payload that assumes every OpenAI-compatible endpoint
  supports the same structured-output API.
- No guarantee that total translation time is below 0.5 seconds on every model and
  machine. Plugin-controlled latency and provider latency are measured separately.
- No real provider credentials in automated tests or diagnostic logs.
- No synchronous wait (`.Result`, `.Wait()`, or `GetAwaiter().GetResult()`) on an
  LLM, database, file, or configuration task from a game/UI runtime path.
- No unrelated quest layout, text reflow, or selection-dialog work folded into the
  LLM issue branches.

## Architectural boundaries

The existing `TranslationService` remains the only translation orchestrator and
`ITranslator` remains the common base contract. Provider request and response code
stays inside the concrete translator for that provider.

Typed dialogue support is added through a narrow optional capability rather than a
second pipeline. A dialogue-capable translator may return a
`DialogueTranslationResult` containing translated text, an optional translated
speaker name, provider/session metadata needed by the runtime, and structured-path
diagnostics. Translators that do not implement the optional capability continue to
return text through `ITranslator` unchanged.

Capture, lookup, translation, persistence, and presentation remain separate stages:

1. A handler captures the original text, speaker, surface, source language, target
   language, selected engine, and configuration generation into managed immutable
   data, enqueues the operation, and returns immediately.
2. The asynchronous pipeline performs the existing DB-first lookup with the
   captured reuse scope.
3. The runtime builds current-turn metadata and optional bounded history.
4. `TranslationService` selects the captured translator and request strategy.
5. The provider adapter translates and returns a validated result.
6. Persistence rules decide whether the result is runtime-only or reusable.
7. The handler publishes only if the captured operation is still current.

## Non-blocking game UI and async safety

Non-blocking execution is a program-wide acceptance invariant for every issue and
PR in this design.

### Capture boundary

Addon callbacks and ImGui `Draw` methods perform only bounded capture, state
comparison, and enqueue work. Native `AtkUnitBase`, `AtkValue`, node pointers,
spans, and borrowed buffers never cross an asynchronous boundary. Required text,
identifiers, language, engine, mode, and generation values are copied into managed
immutable request data before scheduling background work.

`Task.Run` must not be used to move native pointer access onto an arbitrary worker
thread. Native state is read and written only on the appropriate Dalamud framework
thread.

### Asynchronous pipeline

DB lookup logically remains before provider translation, but database I/O runs
away from the game/UI thread with a fresh context per operation and cancellation
where supported. A preloaded in-memory cache may satisfy the lookup immediately,
but it remains a mirror used to suppress I/O and does not redefine DB semantics.

Provider HTTP, model refresh, glossary file loading, configuration persistence, and
provider-managed session operations are asynchronous end-to-end. Runtime LLM paths
use `TranslateAsync`; the synchronous compatibility member on `ITranslator` is not
called by addon handlers, overlays, ImGui rendering, or framework callbacks.
Non-UI library awaits avoid capturing the game synchronization context.

Configuration UI changes update an in-memory snapshot immediately and queue
serialized persistence outside the render path. Glossary loading builds a new
immutable snapshot off-thread and swaps it atomically only after successful parsing.
One failed save or load is observed and reported without blocking subsequent frames.

### Cancellation, concurrency, and publication

Every request carries cancellation tied to source generation, visible-line change,
configuration generation, and plugin shutdown, plus a bounded provider timeout.
The existing broker/deduplication infrastructure prevents identical per-frame work
and bounds concurrent provider calls; this design does not create a second queue.
Fire-and-forget tasks are not allowed unless registered with an owner that observes
completion and exceptions.

Background completion publishes a managed result snapshot. Native mutation is then
marshalled to the framework thread, where the addon/node is resolved again and the
request identifier, source text, mode, language, engine, and configuration
generation are revalidated. No native pointer captured before `await` is reused.
Overlay drawing reads a thread-safe published snapshot and never awaits work.

## Dialogue context contract (#214)

Current-turn metadata and prior-turn history become distinct concepts.

Current-turn metadata is usable when any of the following is available:

- original speaker name;
- translation surface;
- source and target language;
- glossary entries selected for the request;
- other deterministic metadata derived from the current game payload.

Prior-turn history may be empty. An empty history must not suppress current speaker
metadata, structured translation, or glossary injection. `TranslationService` and
every `IDialogueContextAwareTranslator` must therefore gate the context path on
usable current metadata or history, not only `PriorTurns.Count > 0`.

The Talk handler already captures the speaker before translating the text. The
first request uses that speaker immediately; it does not wait for a later dialogue
line or introduce an artificial delay. BattleTalk follows the same contract with a
separate session key.

Acceptance requires a first-line request with an empty history to contain the
speaker and to produce the same glossary/pronoun behavior as a later line with the
same speaker.

## Prompt and glossary effectiveness (#252)

`PromptEditorUI` must report Save and Reset as configuration changes. The owning
engine panel queues persistence through the serialized non-blocking save path, and
runtime configuration refresh rebuilds the selected translator from the published
configuration snapshot. This rule applies to every prompt-aware LLM engine, not
only Gemini.

Saving or resetting a prompt clears the affected translator's in-memory request
cache through translator reconstruction. Existing DB rows are not deleted or
silently invalidated. The UI explains that a new prompt or glossary applies to new
translations and explicitly retranslated visible content. The existing visible
retranslation path must use the current prompt and glossary immediately.

Glossary enforcement has two layers:

1. The request includes the selected glossary entries in the structured or
   plain-text representation appropriate for the provider.
2. Exact glossary terms are protected deterministically before translation and
   restored to their configured targets after validated output.

Protection uses longest-match-first phrase matching and stable opaque markers.
Markers must round-trip unchanged; a response that damages a required marker is
invalid and follows the configured fallback policy. This guarantees entries such
as `source == target` without trusting the model to obey an instruction. Matching
must avoid changing substrings inside unrelated words.

Acceptance includes the exact terms `Scions` and `The Order of the Twin Adder`, a
first dialogue line, Save, Reset, translator refresh, and explicit retranslation of
an already stored line.

## Bounded dialogue context (#209)

Add shared LLM dialogue context settings with backward-compatible defaults:

- context enabled: `true`;
- maximum prior turns: `3`;
- maximum estimated context tokens: unlimited unless explicitly configured;
- existing runtime TTL: preserved unless a separate issue requires changing it.

A maximum-turn value of zero is equivalent to disabling prior-turn injection. The
speaker and other current-turn metadata remain available even when history is
disabled. A configured token budget trims the oldest turns first while retaining
the current request metadata. Token usage is explicitly labeled as an estimate
unless a provider supplies an exact tokenizer.

The session store remains in memory and is isolated by addon/surface, conversation
key, source language, engine, and model. Sessions are cleared on TTL expiry,
configuration generation change, engine/model change, plugin shutdown, or explicit
reset. No history is written to the translation database.

The settings are exposed using `.resx` strings and explain the latency/consistency
tradeoff for local models.

## Structured and session-capable LLM requests (#148)

### Request modes

Each LLM engine exposes a dialogue request mode:

- `Auto` (default): use the best supported provider strategy and downgrade after a
  deterministic incompatibility.
- `Structured`: attempt the provider's declared structured strategy and fall back
  safely when the endpoint or selected model rejects it.
- `PlainText`: do not send schema/tools; inject a compact plain-text glossary and
  metadata block.

The capability registry selects a provider adapter, not a blanket declaration that
all LLM engines support JSON schema. Provider families may use native response
schemas, tool/function calls, JSON format, or plain-text parsing as appropriate.

A deterministic structured incompatibility opens a circuit breaker scoped to the
current translator instance and provider/model signature. Subsequent requests use
the fallback directly instead of paying for two provider calls. The breaker resets
when relevant configuration changes.

### Structured contracts

The common request contract includes:

- source and target language for the current turn;
- original text and original speaker;
- bounded prior turns, each with its language metadata;
- selected glossary entries and comments;
- surface and deterministic current-turn metadata.

The response contract includes translated text and optional translated speaker.
Validation rejects missing required fields, empty/non-persistable text, damaged
protected glossary markers, and provider annotations outside the declared result.

Plain-text mode uses the same internal request data and a compact formatter. This
provides a supported fallback for models fine-tuned for textual glossaries, such as
SakuraLLM, without coupling core handlers to model names.

### Target-language changes

Target language is captured per request, not assumed to be fixed for the lifetime
of a conversation. A session may therefore contain turns with different targets.
Each history item records its target language, cache and DB identities use the
current request target, and stale results are rejected using the captured scope.

### Provider-managed sessions

Provider-managed conversation state is optional and opt-in. It is enabled only for
an adapter with a documented session API, initially the OpenAI Responses-style
adapter. Other providers continue using stateless requests with bounded local
history.

Provider session identifiers are runtime-only. They obey the same maximum-turn,
estimated-token, TTL, reset, engine/model, and shutdown rules as local history.
Glossary and system instruction refreshes occur when their configuration signature
changes. Failure of a provider-managed session falls back to a stateless request
without persisting provider identifiers.

The runtime never assumes that a remote provider prunes its own history. When the
local turn or token budget would be exceeded, it starts a new provider session and
seeds only the locally retained bounded context.

## Latency investigation and optimization (#176)

The existing translation metrics/debugger is extended rather than replaced. A
request correlation identifier records durations for:

- capture-to-queue;
- DB/cache lookup;
- internal queue wait;
- prompt/context/glossary construction;
- structured provider call;
- fallback provider call;
- validation and persistence;
- result-to-publication.

The metrics also record approximate prompt/context size, number of history turns,
structured strategy, fallback reason, cache/DB reuse, and provider/model identity.
They do not record credentials or full source/prompt/glossary content.

LM Studio and Ollama translator instances continue reusing their `HttpClient`.
Performance work must first reproduce the current release with the issue's local
model scenario. The implementation then removes only measured plugin overhead. A
plain-text request must perform one provider call. A structured-incompatible model
may pay for one discovery failure per translator signature, not one failure per
dialogue line.

The reported sub-0.5-second total remains an experience target for the reference
environment. The close criterion is evidence that plugin-controlled overhead is
measured, no artificial polling/delay dominates it, and remaining provider time is
reported separately.

## DeepSeek diagnosis and hardening (#171)

DeepSeek diagnostics distinguish:

- missing credentials;
- malformed endpoint URI;
- rejected credentials (`401`/`403`);
- rate limiting (`429`);
- unavailable model;
- transport/timeout failure;
- malformed or empty response;
- structured-mode incompatibility.

The translator must never slice or log any part of an API key. A short non-empty
key may be rejected by the provider but must not crash translator construction and
be misreported as a generic unavailable engine.

Model-list success is not proof that chat completion succeeds. Tests exercise the
translation endpoint contract separately, including URL composition, bearer auth,
request model, successful response parsing, and classified failures.

The remaining DeepSeek behavior is isolated from unrelated quest layout and
selection-dialog reports previously accumulated in #171. Confirmed unrelated
surface defects must be tracked in their appropriate issue rather than patched in
the DeepSeek translator.

## Mixed no-translation report (#203)

Issue #203 is treated as a reproduction matrix rather than one root cause. The
baseline scenario is English client text translated to Turkish. Each reported
engine is tested independently across the affected surfaces and the three display
modes:

- Google;
- Yandex;
- Gemini;
- DeepL.

For each cell, evidence records capture, selected engine, source/target contract,
provider invocation, accepted result, persistence/reuse decision, and native or
overlay publication. A failure is fixed in the layer where evidence first diverges.
Non-LLM findings remain isolated from LLM translator changes even though they are
needed to resolve the mixed issue report.

If the current release cannot reproduce a reported cell, the investigation and
requested evidence are documented. Silence from the reporter is not proof of a
fix. Closing as non-reproducible requires a maintainer decision backed by the
completed current-version matrix.

## Error handling and privacy

Provider failures use a shared classification vocabulary: configuration,
authentication, authorization, rate limit, endpoint/transport, timeout,
cancellation, capability, invalid output, and stale operation.

- User-facing messages use `.resx` resources and state a concrete next action.
- `PluginRuntimeLog` receives engine, model, status, stage, correlation identifier,
  latency, and approximate payload size.
- Credentials, raw prompts, glossary contents, and player text are not logged by
  default.
- Error placeholders and invalid results are never persisted as translations.
- Known deterministic failures use cooldown/circuit-breaker behavior and never
  retry every frame.
- Cancelled or stale operations do not mutate native UI, overlay state, DB rows, or
  active dialogue sessions.
- Background task exceptions are always observed by their owning operation and do
  not surface as unobserved task failures on the framework thread.

## Persistence rules

- Results that depend on prior turns or provider-managed session state remain
  runtime-only.
- Results that use only the current text, current speaker, deterministic glossary,
  and captured configuration may use the existing persistence path.
- Existing source, target, engine, game-version, and source-content compatibility
  semantics remain unchanged.
- Prompt or glossary edits do not destructively invalidate existing DB rows.
- Explicit visible retranslation is the supported way to replace an existing row
  using current LLM configuration.

No database migration is expected for this work. Configuration additions are
additive and use defaults that preserve current behavior.

## Testing and validation

Every bug fix begins with a failing unit test, controlled reproduction, or reusable
diagnostic that proves the reported behavior before the fix.

### Automated coverage

- First-line current-speaker context with no prior turns.
- Context disabled, zero turns, maximum turns, token-budget trimming, TTL, and
  session isolation.
- Prompt Save and Reset change propagation, config persistence, and translator
  reconstruction.
- Glossary load, language filtering, longest-match protection, damaged marker
  rejection, and `source == target` restoration.
- Structured request/response fixtures for each provider strategy.
- Plain-text glossary fallback and structured circuit-breaker reset.
- Typed text-plus-speaker result and text-only translator fallback.
- Per-request target language changes and stale-operation rejection.
- Provider-managed session creation, reuse, refresh, expiry, and stateless fallback.
- Translation metrics stage accounting without sensitive content.
- A deliberately suspended provider/DB task does not delay the calling addon or
  ImGui callback; the callback returns while the operation remains pending.
- Cancellation and plugin shutdown stop pending work without publishing or native
  mutation.
- Slow completion, engine/target changes, and out-of-order results cannot publish
  stale data.
- Native pointers and borrowed buffers are absent from queued request objects and
  are re-resolved on framework-thread publication.
- Concurrent identical frame captures deduplicate to one owned operation, and every
  scheduled task has observed completion and exception handling.
- DeepSeek URL, auth header, status classification, short key, secret-free logs,
  and response parsing.
- #203 engine/surface/display-mode routing with faked providers where practical.

No CI test calls a paid or credentialed provider. `HttpMessageHandler` test doubles
and provider response fixtures exercise the wire contracts.

Each issue PR runs:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Changes involving Talk, BattleTalk, addon lifecycle, or native UI payloads also run:

```powershell
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

### Runtime validation

The in-game checklist covers:

- first dialogue line and a multi-line sequence;
- Talk and BattleTalk session isolation;
- context off, bounded context, and default context;
- prompt Save, Reset, and visible retranslation;
- exact glossary terms on the first line;
- engine/model/target changes during a slow request;
- a deliberately slow provider while the game UI and plugin windows continue
  rendering and accepting input;
- cancellation caused by advancing dialogue, closing the addon, changing config,
  and unloading the plugin;
- native, overlay-only, and swap modes;
- structured-capable and structured-incompatible local models;
- LM Studio/Ollama latency stage metrics;
- DeepSeek success and classified failure presentation;
- the English-to-Turkish #203 matrix.

When DalaMock cannot drive the required game data or native payload, the PR states
the gap and retains the corresponding in-game step. Startup-only Mock coverage is
not described as proof of native capture or application.

## Issue execution and integration order

Each issue uses its own branch and PR into `v4-series`:

1. `issue-214-first-dialogue-speaker-context`
2. `issue-252-llm-prompt-glossary-effectiveness`
3. `issue-209-bounded-local-llm-context`
4. `issue-148-structured-llm-contracts`
5. `issue-176-local-llm-latency`
6. `issue-171-deepseek-runtime-auth`
7. `issue-203-no-translation-matrix`

Each branch starts from `v4-series` after the preceding dependency PR is merged.
Commits remain short and issue-labeled. A PR must include:

- reproduction evidence and root cause;
- the narrow implementation change;
- automated validation output;
- runtime validation completed or explicitly remaining;
- evidence that slow provider/DB work did not block the framework or ImGui thread;
- behavior-sensitive risks;
- issue closure evidence.

An issue closes only after its acceptance criteria are satisfied or a documented
current-version non-reproduction is explicitly accepted by the maintainer.

## Issue exit criteria

| Issue | Required exit evidence |
|---|---|
| #214 | First line uses captured speaker with empty history; capture enqueues and returns without waiting; no artificial delay; tests and Talk validation pass. |
| #252 | Prompt Save/Reset queues persistence and refreshes every LLM engine without blocking Draw; protected glossary terms survive first-line and explicit retranslation tests. |
| #209 | Context can be disabled and bounded by turns and estimated tokens; defaults preserve current behavior; sessions remain runtime-only and thread-safe. |
| #148 | Auto/Structured/PlainText strategies, provider-specific adapters, typed speaker/text, glossary fallback, per-turn target, and optional bounded provider session are asynchronous and validated. |
| #176 | Current-release reproduction has stage metrics; plugin-controlled overhead is identified and reduced where measurable; plain mode sends one asynchronous request and the UI remains responsive under a suspended provider. |
| #171 | DeepSeek success and classified failure contracts pass asynchronously; no key fragment is logged; current endpoint behavior is manually verified when credentials are available. |
| #203 | Every reported engine/surface/mode cell has a fix, a passing result, or an evidence-backed maintainer disposition; slow cells never block native or overlay rendering. |
