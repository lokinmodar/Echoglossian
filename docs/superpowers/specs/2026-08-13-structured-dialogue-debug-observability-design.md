# Structured Dialogue Debug Observability Design

**Date:** 2026-08-13

**Repository:** `lokinmodar/Echoglossian`

## Objective

Add one shared debug-only structured-dialogue observability layer that proves,
for every LLM structured-dialogue attempt:

- what prompt shape was prepared;
- which effective capability decisions were applied;
- which provider route and model were used;
- whether glossary/context were included;
- whether the request succeeded, validated, or downgraded.

The design must reinforce the existing LLM capability matrix rather than
introducing new provider-specific policy drift.

## Problem

Current logs show only part of the structured-dialogue pipeline:

- `TranslationService: TranslateAsync called...`
- dialogue-context summary
- some structured fallback diagnostics on failure

That leaves important gaps:

1. we cannot prove from logs that the structured prompt payload was actually
   emitted;
2. we cannot tell whether glossary entries were included in the structured
   request;
3. we cannot tell which effective capability decision was applied for each
   parameter such as `temperature` or `reasoning_effort`;
4. we cannot tell what raw structured payload came back before validation;
5. success paths are less observable than failure paths.

This has created uncertainty around whether fixes are architectural or merely
local bandaids.

## Goals

- Keep diagnostics in normal debug logging only. No extra diagnostic file.
- Emit one shared structured logging shape across all structured LLM
  translators:
  - ChatGPT / OpenAI
  - OpenRouter
  - DeepSeek
  - Gemini
  - Claude
  - Ollama
  - LM Studio
- Show the effective capability decision used at runtime, including whether a
  parameter was:
  - sent as configured;
  - omitted due to unsupported or default-only policy;
  - forced to an explicit transport-specific disable such as
    `reasoning_effort=none`.
- Show whether glossary/context were present in the structured request.
- Show bounded sanitized previews for request and response content.
- Preserve the capability matrix as the only policy authority.

## Non-Goals

- No new persisted diagnostics table or file.
- No broad refactor into one universal provider executor in this slice.
- No raw prompt dumps, full provider bodies, API keys, bearer tokens, or
  complete glossary contents in logs.
- No new UI.
- No replacement of existing translator classes.

## Existing Foundations

The repository already has the right building blocks:

- shared structured request builder:
  - `StructuredDialogueTranslationRequestBuilder`
- shared OpenAI-style schema and prompt helpers:
  - `StructuredDialogueOpenAiToolHelper`
  - `StructuredDialogueOpenAiCompatiblePayloadHelper`
- shared validation path:
  - `StructuredDialogueTranslationResponseValidator`
- shared structured fallback formatting:
  - `StructuredDialogueDiagnosticsHelper`
- shared capability matrix:
  - static catalog
  - SQLite overlay rules and observations
  - resolver / policy service

The new work should extend those shared seams rather than adding free-form
per-provider logs.

## Recommended Approach

Extend the existing structured diagnostics helper into one shared debug
observability surface with three standardized log phases:

1. `structured-start`
2. `structured-success`
3. `structured-fallback`

Each structured translator continues to own transport-specific request
construction, but every translator must emit the same debug contract before and
after its provider call.

This keeps policy central and transport local:

- **policy** stays in the capability matrix and shared request logic;
- **transport adaptation** stays in each translator where SDK or HTTP request
  shapes differ.

That means a future model incompatibility should be fixed by:

- updating the shared static catalog or learned DB overlay;
- or adjusting one transport adapter to honor the same shared policy;

not by inventing new policy branches inside each translator.

## Shared Debug Contract

### 1. `structured-start`

Emit immediately before the provider request is sent.

Required fields:

- `provider`
- `endpointScope`
- `route`
- `model`
- `capability`
- `sessionNamespace`
- `priorTurns`
- `glossaryCount`
- `glossaryApplied`
- `speakerMetadataPresent`
- `addresseeMetadataPresent`
- `requestPromptLength`
- `requestJsonLength` when a serialized request body exists
- `promptPreview`
- `sourcePreview`
- `capabilityDecisions`

`capabilityDecisions` must use normalized tokens derived from the effective
matrix decision. Representative examples:

- `temperature=sent(configured)`
- `temperature=omitted(default-only)`
- `temperature=omitted(unsupported)`
- `reasoning_effort=explicit-none(unsupported)`
- `reasoning_effort=sent(configured)`
- `reasoning_effort=omitted(unknown)`

The key rule is that these tokens describe the **effective runtime decision**,
not merely the static catalog entry.

### 2. `structured-success`

Emit after the provider response is parsed and validated successfully, before
the translated text is returned.

Required fields:

- `provider`
- `endpointScope`
- `route`
- `model`
- `capability`
- `glossaryApplied`
- `rawPayloadLength`
- `translatedLength`
- `translatedPreview`
- `rawPayloadPreview`

This is what proves "what came back" without dumping the entire response body.

### 3. `structured-fallback`

Keep the existing fallback diagnostics, but extend them to include enough
shared request context to correlate with `structured-start`:

- `provider`
- `endpointScope`
- `route`
- `model`
- `capability`
- `stage`
- `reason`
- `status`
- `excerpt`
- `capabilityDecisions`
- `glossaryApplied`

This keeps failure logs structurally consistent with success logs.

## Sanitization Rules

All previews must be bounded and sanitized.

Requirements:

- redact API keys, bearer tokens, `authorization`, `apiKey`, `token`,
  `password`, and similar assignments;
- normalize whitespace into one line;
- cap previews to a short fixed length;
- log glossary count and presence, not full glossary contents;
- log prompt and response previews only after sanitization;
- never log raw provider JSON bodies in full.

The current helper already sanitizes excerpts. The same style must be reused
for request and success previews.

## Provider Integration Pattern

Every structured translator should follow the same sequencing:

1. build normalized structured request object;
2. compute effective capability decisions from the shared capability matrix;
3. build transport-specific request payload;
4. emit `structured-start`;
5. send request;
6. extract raw structured payload;
7. validate payload;
8. emit `structured-success` or `structured-fallback`;
9. return translated text or downgrade to the plain-text path.

### ChatGPT / OpenAI

ChatGPT remains the only current translator with `reasoning_effort` in the
structured path. That is acceptable as a transport-specific detail, as long as:

- the decision source remains the shared capability matrix;
- the debug logs explicitly show whether the runtime sent:
  - configured reasoning effort,
  - explicit `none`,
  - or omission.

This is not considered a bandaid because the policy does not live in a
ChatGPT-only ad hoc branch. The transport mapping is local, but the decision
authority remains shared.

### Other Structured Translators

Gemini, OpenRouter, DeepSeek, Claude, Ollama, and LM Studio should emit the
same structured debug lifecycle even if they currently only consume a subset of
capability decisions such as temperature.

That keeps observability and future compatibility expansion uniform.

## Why This Is Not A Per-Model Bandaid

The architectural rule is:

- translators may differ in **how** they encode a supported or unsupported
  parameter for their transport;
- translators must not differ in **whether** a parameter is considered
  supported.

Support state belongs to:

- static catalog defaults;
- DB overlay rules;
- learned observations promoted into rules;
- one shared resolver.

The new debug layer should reveal that chain clearly at runtime. If a future
model fails, the intended correction path is to update the capability rule or
promotion logic, not to scatter more hidden conditional logic.

## Testing Strategy

Add or extend unit tests for:

- `StructuredDialogueDiagnosticsHelper`
  - start message format
  - success message format
  - fallback message extensions
  - sanitization and truncation
- capability decision token formatting
  - sent
  - omitted unsupported
  - omitted default-only
  - explicit disable such as `none`
- ChatGPT structured reasoning diagnostics
  - unsupported rule produces `explicit-none`
- representative non-ChatGPT structured diagnostics
  - temperature omission/sending tokens

The tests should focus on formatted debug behavior and not require live
provider calls.

## Validation

Implementation validation should include:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- targeted test execution for diagnostics and capability-policy tests
- one in-game debug-log check confirming:
  - `structured-start` appears for a Talk interaction;
  - the log shows `glossaryCount` and `capabilityDecisions`;
  - either `structured-success` or `structured-fallback` follows with the same
    provider/model/route context.

## Risks

- adding too much noisy debug output can make logs harder to scan;
- prompt previews that are too long can become quasi-dumps;
- each translator has slightly different transport plumbing, so the shared
  contract must stay simple enough to adopt consistently.

The mitigation is to keep one compact structured line per phase with bounded
preview sizes.

## Recommended Implementation Scope

Keep the patch narrow:

- extend `StructuredDialogueDiagnosticsHelper`;
- add any small shared formatter types needed for capability decision tokens;
- wire the shared helper into all structured translators;
- add targeted unit tests;
- avoid larger executor refactors in this slice.
