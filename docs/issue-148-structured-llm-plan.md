# Issue 148 Structured LLM Glossary and Metadata Plan

## Purpose

This document turns issue `#148` into a concrete implementation plan that fits
the current Echoglossian LLM rework.

Issue `#148` is not only about "JSON mode". It is really a request for a more
structured LLM translation contract that can support:

- glossary-aware translation
- cleaner machine-readable output
- richer speaker and dialogue metadata
- better pronoun and omitted-subject recovery
- eventual provider-specific session efficiencies

This plan intentionally builds on the current `llm-translation-rework` branch
instead of starting a parallel architecture.

## Source Issue Summary

The issue asks for structured input/output for LLM engines so the plugin can:

1. let users provide glossaries for names, skills, places, and terms
2. reduce annotation leakage such as "Here is your translation:"
3. pass metadata that helps the model infer omitted subjects
4. pass the speaker name in the same request
5. eventually support richer multi-turn flows and possible token savings via
   APIs such as `/v1/responses`

The issue also correctly highlights important constraints:

- not every model supports structured output well
- some models prefer plain-text glossary formats
- "OpenAI-compatible" providers do not all behave the same
- structured mode can cost more tokens

## Relationship To The Current LLM Rework

The current branch already delivered prerequisites that make `#148` more
practical:

- shared LLM error handling and actionable notifications
- a dedicated `Translator Debugger and Metrics` window
- compact local-LLM prompt work
- runtime-only dialogue session context
- dialogue-family LLM routing override
- `Custom OpenAI-Compatible` provider configuration and diagnostics

That means `#148` should now be treated as the next structured-quality layer
on top of a more stable LLM runtime.

## Goal

Introduce a structured LLM contract that improves translation quality and
operator control without breaking the current persistence model or forcing all
providers into one rigid request path.

## Non-Goals For The First Pass

- no requirement that every LLM engine support structured mode
- no global mandatory JSON mode for all providers
- no persistence of session-aware dialogue output
- no broad DB schema rewrite just to record every prompt/response detail
- no immediate support for mixed target languages inside one live dialogue
  session
- no forced migration away from the current plain-text path for engines that do
  not benefit from structure

## Design Principles

1. Structured mode must be optional and provider-aware.
2. The plain-text LLM path must remain available as fallback.
3. Glossary and metadata should improve live translation quality without
   redefining DB truth by themselves.
4. The first rollout should focus on dialogue-family surfaces where metadata is
   most valuable.
5. Provider quirks must be isolated behind shared helpers, not pushed into
   addon handlers.

## Recommended First Scope

Start with dialogue-family surfaces only:

- `Talk`
- `BattleTalk`
- later optionally `TalkSubtitle`
- later optionally `MiniTalk`

Do not start by applying structured mode to:

- `Journal`
- `JournalDetail`
- `ScenarioTree`
- `ToDoList`
- `ActionMenu`
- `MainCommand`
- tooltip/detail families

Those surfaces benefit more from deterministic cached rows and usually provide
less useful conversational metadata.

## Capability Matrix Direction

Structured mode should not be a single on/off switch with blind trust.

The runtime needs a provider-capability model with at least these distinctions:

- `Disabled`
  - engine never attempts structured mode
- `JsonSchema`
  - engine can accept a structured request and validate structured output
- `JsonObject`
  - engine can return JSON-like output but without strong schema guarantees
- `PlainTextGlossary`
  - engine prefers glossary augmentation but not strict JSON schema

This allows the plugin to support:

- OpenAI-family providers with JSON schema
- providers that only tolerate looser JSON-object output
- models such as SakuraLLM that may benefit from glossary injection without
  strict structured output

## Request Contract Direction

The plugin should standardize an internal structured request model before
deciding how each provider serializes it.

Suggested runtime contract:

```json
{
  "source_language": "ja-JP",
  "target_language": "en-US",
  "surface_family": "Talk",
  "speaker_original": "スフェーン",
  "speaker_role_hint": "npc",
  "text_original": "アレクサンドリアのみんなの笑顔を守るんだ。",
  "dialogue_context": [
    {
      "speaker_original": "スフェーン",
      "text_original": "..."
    }
  ],
  "glossary": [
    {
      "source": "スフェーン",
      "target": "Sphene",
      "comment": "Female name"
    }
  ],
  "metadata": {
    "npc_name_original": "スフェーン",
    "quest_name_original": null,
    "pronoun_hint": null,
    "subject_hint": null
  }
}
```

Not every provider has to receive this exact JSON payload. This is the
internal semantic contract the plugin should assemble before engine-specific
serialization.

## Response Contract Direction

The first structured response contract should stay narrow:

```json
{
  "speaker_translated": "Sphene",
  "text_translated": "I'll protect the smiles of everyone in Alexandria."
}
```

Optional later fields:

- `notes`
- `confidence`
- `glossary_hits`
- `language_detected`

These should not be required in the first pass.

## Glossary Strategy

The glossary feature needs its own phased rollout.

### First pass

- operator-managed glossary file or files
- runtime-loaded in memory
- simple additive structure:
  - source term
  - target term
  - optional comment
  - optional source/target language scope
- dialogue-family use first

### Format direction

Start with a plugin-native format that is easy to validate and merge, for
example JSON.

Later additions can include:

- import helpers for community-maintained glossary datasets
- provider-specific rendering:
  - JSON schema glossary entries
  - plain-text glossary lists for engines that prefer them

### Guardrails

- glossary injection must stay optional
- bad glossary rows should not crash translation
- malformed glossary entries should surface clear debugger warnings

## Metadata Strategy

The most valuable metadata for the first pass is dialogue-specific:

- `speaker_original`
- optional speaker type hint:
  - player
  - npc
  - unknown
- recent dialogue turns already captured by the runtime session
- optional active target language

Later metadata candidates:

- quest title
- map or zone name
- target addressee hints
- gender/pronoun hints when derivable safely

The first implementation should avoid inventing metadata that the plugin cannot
derive reliably.

## Persistence Semantics

Structured mode must not quietly corrupt DB assumptions.

Recommended first-pass rules:

- glossary-aware but no-session output may still be persisted normally when the
  output is accepted as the live translation result
- session-aware output remains runtime-only, matching the current LLM rework
  rule
- the DB should not be extended in the first pass just to store raw structured
  payloads
- if we later need to distinguish structured-vs-plain output in persistence,
  do it with additive metadata, not a persistence rewrite

## Provider Strategy

### Phase 1 providers

Focus first on providers already inside the OpenAI-style family in this branch:

- `ChatGPT` / official OpenAI
- `Custom OpenAI-Compatible`
- `OpenRouter`
- `DeepSeek`
- `LM Studio`
- `Ollama` where compatible with the selected local model path

### Deferred or cautious providers

- `Claude`
  - the issue itself reports schema non-compliance on some Claude models
- `Gemini`
  - supported later if its structured behavior proves stable enough
- non-OpenAI-style providers with bespoke request semantics

## Operator-Facing Configuration Direction

Recommended initial controls:

- `Enable structured dialogue mode`
- `Structured dialogue mode strategy`
  - `Auto`
  - `Force structured`
  - `Plain text only`
- `Glossary file path`
- `Enable dialogue glossary injection`
- `Enable speaker metadata hints`

These should live alongside the relevant LLM configuration surfaces, not as a
brand-new disconnected subsystem.

## Debugger And Metrics Requirements

The current `Translator Debugger and Metrics` window should grow to show
structured-mode state clearly.

Recommended additions:

- whether structured mode was attempted
- provider capability mode:
  - `Disabled`
  - `JsonSchema`
  - `JsonObject`
  - `PlainTextGlossary`
- whether glossary injection was active
- whether speaker metadata was attached
- whether the response passed structured validation
- structured parse failure reason, when applicable

This is important because `#148` will otherwise look like "translation quality
changed mysteriously" rather than a visible structured-mode feature.

## Implementation Phases

### Phase 148.1 - Shared structured request and response contracts

Build:

- internal request envelope model
- internal response model
- capability enum / routing helpers

Do not yet wire every provider.

### Phase 148.2 - Structured output parsing and validation

Build:

- robust JSON extraction/parsing helper
- strict validation for required fields
- safe fallback to plain-text path when validation fails

This phase directly attacks annotation leakage and malformed output.

### Phase 148.3 - Dialogue glossary infrastructure

Build:

- glossary file model
- loader
- validation
- in-memory cache
- debugger visibility for glossary load state

### Phase 148.4 - Dialogue metadata assembly

Build:

- shared metadata builder for dialogue-family surfaces
- speaker metadata assembly from visible runtime state
- handoff into structured-capable translators

### Phase 148.5 - OpenAI-family structured provider path

Build:

- provider-specific serializer for OpenAI-style structured requests
- response parser/validator
- structured capability toggle in the engine UI

This is the first real end-to-end delivery point for the issue.

### Phase 148.6 - Plain-text glossary fallback path

Build:

- glossary rendering for engines that do not support strict JSON schema but can
  still benefit from term injection

This is where models like SakuraLLM belong conceptually.

### Phase 148.7 - Experimental multi-turn provider optimizations

Evaluate later:

- `/v1/responses`
- provider-managed conversation state
- glossary refresh every `N` turns

This should remain explicitly experimental and should not block the main
structured contract rollout.

## Recommended First Deliverable

The safest first deliverable for `#148` is:

1. structured dialogue mode for OpenAI-family providers only
2. strict narrow response schema:
   - `speaker_translated`
   - `text_translated`
3. optional glossary injection
4. optional speaker metadata hints
5. debugger visibility for structured-mode success/failure
6. plain-text fallback when:
   - provider capability is absent
   - schema validation fails
   - operator disables structured mode

That gives immediate user-visible value without requiring every provider or
every surface to support the same complexity on day one.

## Risks

- token cost may increase if structured payloads become too verbose
- provider compatibility may be less reliable than the marketing suggests
- glossary misuse can degrade quality rather than improve it
- poorly chosen metadata can mislead the model
- overextending to non-dialogue surfaces too early could damage cache and DB
  semantics

## Success Criteria

`#148` should be considered on the right track when all of the following are
true:

- structured-capable providers can translate dialogue with a validated response
  contract
- glossary entries can be supplied without breaking fallback behavior
- annotation leakage drops materially on the structured path
- the debugger clearly explains when structured mode was used or skipped
- the plain-text path still works cleanly for unsupported engines/models
- no DB or session-persistence regression is introduced

## Recommended Next Step

The next implementation cut for `#148` should be:

1. add the shared structured request/response contracts
2. add structured response parsing and validation helpers
3. wire the first provider through the existing OpenAI-family branch

That is the smallest useful slice that makes the issue concrete without trying
to solve every glossary, provider, and multi-turn edge case at once.
