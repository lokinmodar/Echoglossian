# Issue 209 bounded local-LLM context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users bound or disable prior dialogue context for Ollama/LM Studio and other LLM dialogue engines without losing current-speaker metadata or blocking the game UI.

**Architecture:** Add one engine-independent `DialogueContextPolicy` derived from normalized configuration. The session store retains only the newest permitted turns and builds request snapshots under both a turn limit and an optional estimated token budget. Disabling history sets prior-turn capacity to zero but still emits the current dialogue request, speaker, and glossary contract established by #214/#252.

**Tech Stack:** C#/.NET 10, existing `Config`, ImGui/resx, `DialogueTranslationSessionStore`, pure token estimation, xUnit, and PowerShell.

## Global Constraints

- Branch from the merged #252 result as `issue-209-bounded-local-llm-context`.
- Use one shared policy for all LLM dialogue engines; do not add Ollama-only or LM-Studio-only session stores.
- Defaults preserve current behavior: enabled, three prior turns, 30-second TTL, estimated token budget disabled.
- “Disable context” means no prior turns. The current speaker/request metadata and glossary still travel through the dialogue-aware path.
- Token budgeting is explicitly an estimate, not a provider tokenizer claim.
- Bound configuration values on load and before runtime use; invalid old config must not create unbounded memory.
- Session state remains runtime-only and is cleared on relevant config/source/plugin transitions.
- UI edits persist through the async coordinator from #252 and apply on the next framework tick.

---

## File map

### New files

- `Translators/DialogueContextPolicy.cs` — normalized immutable context settings.
- `Translators/Helpers/DialogueContextBudgetHelper.cs` — deterministic estimated-token calculation and newest-first trimming.
- `Echoglossian.Tests/DialogueContextBudgetHelperTests.cs`.

### Modified files

- `Config.cs` — enabled, max prior turns, estimated token budget, and TTL fields.
- `PluginUI/Tabs/TranslationEnginesTab.cs` — LLM dialogue context controls.
- `GeneralHelpers/RuntimeConfigurationRefresh.cs` — signature and session reset on policy change.
- `Translators/DialogueTranslationSessionStore.cs` — consume `DialogueContextPolicy` and bounded snapshots.
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`.
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs` — remove hardcoded history/TTL constants and capture policy into each operation.
- `Echoglossian.Tests/DialogueTranslationSessionStoreTests.cs`.
- `Echoglossian.Tests/NativeDialogueHandlerLifecycleTests.cs`.
- `Properties/Resources.resx`, `Properties/Resources.en-US.resx`, `Properties/Resources.pt-BR.resx`, and `Properties/Resources.Designer.cs` — context setting labels/tooltips.

## Interfaces produced and consumed

```csharp
public readonly record struct DialogueContextPolicy(
    bool HistoryEnabled,
    int MaxPriorTurns,
    int EstimatedTokenBudget,
    TimeSpan SessionTtl)
{
    public static DialogueContextPolicy FromConfig(Config config);
}
```

Config defaults and bounds:

```csharp
public bool EnableDialogueContextHistory = true;
public int DialogueContextMaxPriorTurns = 3;          // normalize 0..12
public int DialogueContextEstimatedTokenBudget = 0;  // 0 disables; normalize 0..4096
public int DialogueContextSessionTtlSeconds = 30;     // normalize 5..300
```

Change the session API to consume the policy:

```csharp
public static DialogueTranslationContext BuildContext(
    string sessionNamespace,
    string sessionKey,
    string speakerName,
    string sourceText,
    DialogueContextPolicy policy,
    DateTime? observedAtUtc = null);
```

## Task 1: Define policy normalization and defaults

- [ ] Add config serialization tests proving absent fields deserialize to the current three-turn/30-second behavior.
- [ ] Add normalization tests for negative, excessive, and contradictory values. When history is disabled, effective max turns and budget are zero without destroying the user's stored numeric preferences.
- [ ] Implement `DialogueContextPolicy.FromConfig` as the only runtime normalization point and call matching config normalization from `SaveConfig`/startup.
- [ ] Add the policy fields to the runtime translation signature so changes clear sessions and rebuild the relevant managed runtime once.
- [ ] Run config/runtime contract tests and commit:

```powershell
git add -- Config.cs Translators/DialogueContextPolicy.cs GeneralHelpers/RuntimeConfigurationRefresh.cs Echoglossian.Tests
git commit -m "feat(#209): define bounded dialogue context policy"
```

## Task 2: Implement deterministic turn/token trimming

- [ ] Write pure tests asserting newest turns win, the turn limit is always enforced, a zero budget disables only budget trimming, and disabled history returns no prior turns.
- [ ] Define the estimate as `ceil(UTF-16 character count / 4.0)` plus a small documented per-turn speaker/format overhead. Test the exact formula.
- [ ] Ensure one oversized newest turn may be omitted rather than exceeding the budget; current request text is never counted as retained history and never removed.
- [ ] Implement `DialogueContextBudgetHelper.SelectPriorTurns` returning a new immutable/read-only list without mutating the input.
- [ ] Run focused tests and commit:

```powershell
git add -- Translators/Helpers/DialogueContextBudgetHelper.cs Echoglossian.Tests/DialogueContextBudgetHelperTests.cs
git commit -m "feat(#209): bound dialogue history by turns and budget"
```

## Task 3: Apply policy to the runtime session store

- [ ] Extend session-store tests for disabled history, turn bound, token bound, TTL expiry, namespace isolation, and policy reduction while a session already contains more turns.
- [ ] Replace raw `historyLimit`/`ttl` arguments with `DialogueContextPolicy` and trim retained state immediately on each access.
- [ ] Keep current speaker/source appended for future requests only when history is enabled. When disabled, do not accumulate source lines in memory.
- [ ] Ensure `GetSnapshots()` reports retained prior-turn count after policy trimming and contains no source text.
- [ ] Run session tests and commit:

```powershell
git add -- Translators/DialogueTranslationSessionStore.cs Echoglossian.Tests/DialogueTranslationSessionStoreTests.cs
git commit -m "fix(#209): enforce context limits in session storage"
```

## Task 4: Wire Talk/BattleTalk and configuration UI

- [ ] Add lifecycle tests proving both handlers capture `DialogueContextPolicy.FromConfig(config)` into the immutable source operation before starting async work; later config mutations must not change an in-flight request.
- [ ] Remove `DialogueSessionHistoryLimit` and `DialogueSessionTtl` constants from both handlers and pass the captured policy to `BuildContext`.
- [ ] Add a context subsection to `TranslationEnginesTab` with resx labels/tooltips. Disable numeric controls visually when history is off, but retain their values.
- [ ] Mark changes through the existing aggregated config-save path; do not perform file I/O or session enumeration during Draw.
- [ ] On applied policy signature change, clear `DialogueTranslationSessionStore` and cancel/supersede in-flight dialogue source generations so old-policy results cannot publish.
- [ ] Run lifecycle/UI contract tests and commit:

```powershell
git add -- NativeUI/AddonHandlers/Talk/TalkHandler.cs NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs PluginUI/Tabs/TranslationEnginesTab.cs GeneralHelpers/RuntimeConfigurationRefresh.cs Properties Echoglossian.Tests
git commit -m "feat(#209): expose safe dialogue context limits"
```

## Task 5: Validate and close #209

- [ ] Run focused tests, full build/tests, Mock tests, `git diff --check`, and include `Echoglossian.xml` if changed.
- [ ] With Ollama and LM Studio, verify history off, max turns 1/3/12, a small estimated budget, TTL expiry, rapid dialogue, and target-language changes. Confirm the first-line speaker/glossary behavior still works with history off.
- [ ] Deliberately suspend the local provider and confirm changing context settings or advancing dialogue never blocks the game UI and stale responses do not publish.
- [ ] Inspect memory/session snapshots to confirm bounded retention and no persisted dialogue history.
- [ ] Attach the effective-setting matrix to #209 and open the PR to `v4-series`.
