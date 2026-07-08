# LLM Translation Rework Handoff

Snapshot date: 2026-07-07

## Scope

This handoff is for the large LLM-focused workstream spanning:

- `#176` local LLM latency and prompt overhead
- `#174` retranslate-visible-text controls and DB semantics
- `#196` custom OpenAI-compatible provider support
- `#201` visible operator feedback for LLM/runtime failures
- `#148` structured dialogue, glossary, and metadata support

## Branch and PR state

- branch: `llm-translation-rework`
- latest local and remote branch commit:
  - `49bf066` `build: prepare llm rework testing version`
- open PR:
  - [#202](https://github.com/lokinmodar/Echoglossian/pull/202)
  - title: `feat: continue llm translation rework`
  - state: open, non-draft
- divergence versus `v4-series` at snapshot time:
  - `v4-series` unique commits: `38`
  - `llm-translation-rework` unique commits: `63`

Interpretation:

- this branch is large and old enough that it should be treated as a major
  integration effort, not a quick cherry-pick branch
- do not assume `#202` can merge cleanly without sync and review debt cleanup

## Product and architecture decisions already locked

These decisions were explicitly documented and should be treated as baseline:

- surface-group routing starts as **LLM-only override**
- local compact prompts are **per-engine**
- custom OpenAI-compatible support is an **OpenAI-family variant**
- session-aware translations stay **runtime-only and non-persistent**
- metrics surface in a dedicated **Translator Debugger and Metrics** command
  and window
- first operator retranslation control is **explicit retranslate visible text
  and persist**
- `BattleTalk` reuses the `Talk` session infrastructure, but with isolated
  session state

## What this branch already contains

The branch is not just one feature. It layers many validated iterations:

- LLM failure classification and visible notifications
- transient runtime-only suppression for repeated failure spam
- per-engine compact prompt defaults for local LLMs
- aggregated translator metrics and debugger window
- shared prompt wiring fixes across OpenAI-style engines
- runtime-only dialogue session context across supported LLM engines
- LLM-only dialogue surface override routing
- custom OpenAI-compatible provider variant and config UI
- structured dialogue contracts for issue `#148`
- glossary loading and dialogue glossary injection
- dynamic live model-list refresh for selectable LLM engines
- refreshed engine language-support tables and target-language expansion
- direct `Resources.Key` UI usage cleanup

The best branch-level memory source is the iteration log itself.

## Canonical docs to read before editing

Read these first:

1. [docs/llm-translation-improvements-plan.md](../llm-translation-improvements-plan.md)
2. [docs/translation-engines-architecture-and-flows.md](../translation-engines-architecture-and-flows.md)
3. [docs/translation-engine-backlog.md](../translation-engine-backlog.md)
4. [docs/llm-model-defaults-refresh-workflow.md](../llm-model-defaults-refresh-workflow.md)
5. [docs/github-issue-backlog.md](../github-issue-backlog.md)

Then read these directly from `llm-translation-rework`:

- `docs/llm-translation-rework-iteration-log.md`
- `docs/issue-148-structured-llm-plan.md`
- `docs/commands/eglotranslatordebugger.md`

## Open review debt on PR #202

As of this snapshot, `#202` still has unresolved review threads.

High-signal unresolved items:

1. `Translators/DialogueTranslationSessionStore.cs`
   - `BuildContext(...)` appears to include `priorTurns` before applying the
     effective `historyLimit`
2. `Echoglossian.Tests/StructuredDialogueCapabilityHelperTests.cs`
   - the tests still disagree with the production Claude structured-capability
     mapping
   - multiple review threads point at the same underlying mismatch
3. `PluginUI/EngineConfigUI/*EngineUI.cs`
   - live model refresh signatures still embed raw API keys for several LLM
     engines
   - this should move to a non-secret fingerprint or equivalent
4. `Translators/Helpers/StructuredDialogueOpenAiCompatiblePayloadHelper.cs`
   - `JsonDocument` disposal needs to be handled correctly after cloning the
     root element

Treat these as current actionable review debt until they are actually resolved
in the branch and in the PR threads.

## Important branch risks

### The branch is very wide

It touches runtime, config, tests, UI, resources, language tables, fonts, and
multiple translators at once.

### It has stale base assumptions

`v4-series` moved on after this branch diverged. Before resuming, verify that
already-shipped fixes on `v4-series` are not accidentally overwritten.

### It contains generated/localization churn

The branch includes large `Resources.*` and `Echoglossian.xml` changes. Keep
future edits narrow and intentional so the diff does not become unreadable.

### It must respect repo UI/localization rules

Do not reintroduce:

- `GetText` / `GetUiString` wrappers
- hardcoded user-facing strings
- secret duplication in refresh signatures or debug strings

## Recommended resume strategy

1. Switch to `llm-translation-rework`.
2. Read the branch iteration log before touching code.
3. Re-check PR `#202` review threads and resolve the remaining ones first.
4. Sync with `v4-series` before broad new work.
5. Decide whether to keep one large branch or split shippable slices by issue.

Good split candidates if you choose to decompose:

- `#201` notifications and failure-handling slice
- `#176` local prompt/context/runtime-cost slice
- `#196` custom OpenAI-compatible provider slice
- `#148` structured dialogue and glossary slice

## Validation

Required after code changes:

- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

Recommended extra checks on this branch:

- verify the debugger window opens and reports metrics coherently
- verify live model refresh works without leaking secrets into stored
  signatures
- verify `Talk` and `BattleTalk` runtime-only session behavior does not persist
  context-dependent outputs into DB-backed truth

## Merge rule

Do not assume `#202` is a near-merge PR. It should only move toward merge once:

- unresolved review threads are truly fixed
- the branch is resynced with `v4-series`
- the ship strategy is explicit, whether monolithic or issue-sliced
