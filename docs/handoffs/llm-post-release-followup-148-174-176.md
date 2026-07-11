# LLM Post-Release Follow-Up Handoff (`#148`, `#174`, `#176`)

Snapshot date: 2026-07-11

## Scope

This handoff is for the remaining LLM work that stayed open after the official
stable publication of `v4.2601.0710.1250`.

Published and closed by that release:

- `#196` custom OpenAI-compatible provider support
- `#201` visible LLM quota / endpoint / provider failure feedback

Still open on purpose:

- `#174` retranslation and DB-reuse semantics
- `#176` local-LLM latency and overhead follow-up
- `#148` broader structured glossary and metadata follow-up

## Current branch and release state

- current branch: `v4-series`
- current `v4-series` head:
  - `13ec1cc` `docs: mark llm stable release as published`
- official stable publication:
  - `goatcorp/DalamudPluginsD17` [PR #9006](https://github.com/goatcorp/DalamudPluginsD17/pull/9006)
  - merged on `2026-07-11`
  - published version: `v4.2601.0710.1250`

Important interpretation:

- do **not** resume from `llm-translation-rework` as if it were still the
  active delivery branch
- PR `#202` already merged into `v4-series`
- the remaining work should now move on **fresh issue branches off the current
  `v4-series`**

Recommended branch names:

- `issue-174-dialogue-retranslation-semantics`
- `issue-176-local-llm-latency`
- `issue-148-structured-dialogue-followup`

## Read first in a new chat

1. `AGENTS.md`
2. this handoff
3. [docs/github-issue-backlog.md](../github-issue-backlog.md)
4. [docs/llm-translation-improvements-plan.md](../llm-translation-improvements-plan.md)
5. [docs/issue-148-structured-llm-plan.md](../issue-148-structured-llm-plan.md)
6. [docs/commands/eglotranslatordebugger.md](../commands/eglotranslatordebugger.md)
7. [docs/llm-ingame-test-playbook.md](../llm-ingame-test-playbook.md)
8. re-read the GitHub issue comments for `#148`, `#174`, and `#176`

## What the published LLM release already shipped

The official `4.2601.0710.1250` stable build already includes:

- dialogue-family LLM override routing
- runtime-only dialogue session context for `Talk` and `BattleTalk`
- compact local-LLM prompt work
- `Translator Debugger and Metrics` via `/eglotranslatordebugger`
- explicit `Retranslate Visible Dialogue And Persist` support for visible
  `Talk` / `BattleTalk`
- structured dialogue contracts, validation, and provider-capability helpers
- shared structured glossary loader/store and operator-facing glossary config
- structured dialogue provider paths across ChatGPT, Claude, Gemini, DeepSeek,
  LM Studio, Ollama, OpenRouter, and custom OpenAI-compatible providers
- live model refresh for the selectable LLM engines
- actionable LLM runtime failure notifications and provider-aware failure
  classification

The remaining issues are therefore **follow-up work on top of a shipped
baseline**, not greenfield design.

## Recommended execution order

Recommended order if you want the smallest-risk path:

1. `#174`
2. `#176`
3. `#148`

Why this order:

- `#174` is the most focused operator-facing gap and can likely land as a
  narrower fix without reopening the whole structured-dialogue architecture
- `#176` needs fresh measurement on the now-published build before more tuning
- `#148` is the broadest remaining architecture/product slice and should use
  the field data from the first two follow-ups

## Issue `#174` - current gap

### What exists already

- the debugger window exposes `Retranslate Visible Dialogue And Persist`
- `TalkHandler` and `BattleTalkHandler` can capture the currently visible line,
  request a fresh translation through the active dialogue engine, and persist
  the new result

Relevant code landmarks:

- [PluginRuntimeUi.cs](C:\Dante\_dalamud\Echoglossian\PluginUI\PluginRuntimeUi.cs)
- [TranslatorMetricsWindow.cs](C:\Dante\_dalamud\Echoglossian\PluginUI\TranslatorMetricsWindow.cs)
- [TalkHandler.cs](C:\Dante\_dalamud\Echoglossian\NativeUI\AddonHandlers\Talk\TalkHandler.cs)
- [BattleTalkHandler.cs](C:\Dante\_dalamud\Echoglossian\NativeUI\AddonHandlers\Talk\BattleTalkHandler.cs)
- [DbOperations.cs](C:\Dante\_dalamud\Echoglossian\DBHelpers\DbOperations.cs)
- [TranslationService.cs](C:\Dante\_dalamud\Echoglossian\Translators\TranslationService.cs)

### Why the issue is still open

The open issue comments still describe a broader operator problem:

- clearing the DB is a current workaround
- experimenting with different engines/models/prompts still leaves users
  unsure when old persisted rows are being reused
- the visible retranslation action only helps the **currently visible**
  `Talk` / `BattleTalk` line

That means the current shipped affordance is useful, but it does **not** fully
solve "translate already saved translated texts does not work".

### Good next cuts

Good narrow candidates:

1. add better operator diagnostics for "this line came from DB/cache/live
   translation/runtime-only context"
2. add a targeted dialogue-row invalidation or purge flow that is narrower and
   safer than "wipe the whole DB"
3. add a clearer operator-facing path for engine-experiment cleanup without
   redefining quest or canonical DB semantics

### Main risk

Do **not** solve `#174` by breaking the plugin's existing DB-first behavior for
quest, tooltip, or canonical surfaces. The follow-up should stay dialogue- and
LLM-focused unless a larger migration is explicitly intended.

## Issue `#176` - current gap

### What changed since the original report

The original LM Studio latency report predates the now-published LLM runtime
work:

- compact prompts landed
- dialogue session context landed
- local model request paths were refactored
- translator metrics and debugger visibility landed
- structured dialogue may now change some request shapes

So the old "1-2s overhead" report should **not** be treated as a current
baseline without fresh measurement on `4.2601.0710.1250`.

Relevant code landmarks:

- [TranslationService.cs](C:\Dante\_dalamud\Echoglossian\Translators\TranslationService.cs)
- [DialogueTranslationSessionStore.cs](C:\Dante\_dalamud\Echoglossian\Translators\DialogueTranslationSessionStore.cs)
- [DialogueContextPromptHelper.cs](C:\Dante\_dalamud\Echoglossian\Translators\Helpers\DialogueContextPromptHelper.cs)
- `Translators/LmStudioTranslator.cs`
- `Translators/OllamaTranslator.cs`
- [TranslatorMetricsWindow.cs](C:\Dante\_dalamud\Echoglossian\PluginUI\TranslatorMetricsWindow.cs)
- [llm-ingame-test-playbook.md](../llm-ingame-test-playbook.md)

### Recommended first move

Do **measurement first**, not blind optimization.

Recommended validation matrix on the published build:

- LM Studio and/or Ollama with a local model
- first visible dialogue line vs subsequent lines inside the same short-lived
  session
- `Talk` and `BattleTalk`
- overlay-only and native/swap modes only if the render path itself looks
  suspicious
- capture debugger metrics and any provider-reported token/usage data

Questions to answer before editing:

- is the current overhead still outside the model runtime, or was the original
  report mostly pre-rework behavior?
- is the main remaining cost prompt/context size, request setup, polling/cycle
  timing, or UI application latency?
- does the current session TTL/history window help quality enough to justify
  the measured latency on local models?

### Main risk

Do not tune `#176` by pushing context-aware outputs into persisted DB truth or
by adding hot-path retry/logging noise. The branch already drew that boundary
for a reason.

## Issue `#148` - current gap

### What exists already

The release shipped a real foundation for `#148`, not just planning docs:

- shared structured dialogue request/response contracts
- structured response parsing and validation
- provider-capability helpers
- operator-managed dialogue glossary loading and runtime refresh
- structured glossary injection
- debugger visibility for glossary and structured-request activity
- structured provider paths across the current LLM family

Relevant code landmarks:

- [issue-148-structured-llm-plan.md](../issue-148-structured-llm-plan.md)
- [StructuredDialogueTranslationRequestBuilder.cs](C:\Dante\_dalamud\Echoglossian\Translators\Helpers\StructuredDialogueTranslationRequestBuilder.cs)
- [StructuredDialogueTranslationMetadata.cs](C:\Dante\_dalamud\Echoglossian\Translators\StructuredDialogueTranslationMetadata.cs)
- `Translators/StructuredDialogueTranslationRequest.cs`
- `Translators/StructuredDialogueTranslationResponse.cs`
- `Translators/Helpers/StructuredDialogueCapabilityHelper.cs`
- `Translators/Helpers/StructuredDialogueTranslationResponseValidator.cs`
- `Translators/Helpers/StructuredDialogueGlossaryLoader.cs`
- `Translators/StructuredDialogueGlossaryStore.cs`
- [TranslationEnginesTab.cs](C:\Dante\_dalamud\Echoglossian\PluginUI\Tabs\TranslationEnginesTab.cs)
- [TranslatorMetricsWindow.cs](C:\Dante\_dalamud\Echoglossian\PluginUI\TranslatorMetricsWindow.cs)

### Why the issue is still open

Relative to the original issue body, the shipped foundation still leaves
follow-up scope:

- richer metadata derivation is still limited
- pronoun/subject hints are part of the contract, but they are not yet the
  center of a richer dialogue metadata builder
- plain-text glossary fallback for models that dislike strict structured mode
  is still a separate follow-up
- model/provider behavior still needs more real field validation and possibly a
  clearer operator-facing strategy model

### Good next cuts

The two strongest next slices are:

1. **Phase 148.4 style follow-up**:
   build richer dialogue metadata assembly and surface it clearly in the
   debugger
2. **Phase 148.6 style follow-up**:
   add plain-text glossary fallback for engines/models that do not behave well
   with strict schema/tool-based structured mode

If the goal is the smallest next slice, the metadata-builder path is probably
the cleaner continuation of what is already shipped.

### Main risk

Do not broaden `#148` first into quest, canonical game-window, or tooltip
surfaces. Keep the next follow-up dialogue-family scoped and preserve the
runtime-only/non-persistent rule for context-aware output.

## Validation

Required after code changes:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

Recommended in-game checks:

- `/eglotranslatordebugger` opens and reflects the new behavior
- `#174`: visible retranslate flow makes the operator outcome clearer
- `#176`: measured local-LLM latency is compared before/after with real data
- `#148`: glossary load/reload, structured request counts, and fallback
  behavior remain coherent

## Suggested opener for a new chat

Use something like:

> Continue from `docs/handoffs/llm-post-release-followup-148-174-176.md` on a
> fresh issue branch from `v4-series`. Re-read the GitHub issue comments for
> `#148`, `#174`, and `#176`, then propose the smallest next shippable slice.
