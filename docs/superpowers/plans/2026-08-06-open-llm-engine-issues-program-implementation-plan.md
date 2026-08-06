# Open LLM translation-engine issues program Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve every currently open Echoglossian issue whose defect or requested improvement belongs to an LLM/AI translation engine, without using closed legacy issues as regression requirements and without blocking the game UI.

**Architecture:** Deliver seven issue-specific branches in dependency order. Keep `TranslationService` as the only translation orchestration service, preserve DB-first semantics, carry only immutable managed request data across awaits, and publish completed results through the existing source-generation lifecycle. Each branch is merged into `v4-series` before the next dependent branch starts.

**Tech Stack:** C#/.NET 10, Dalamud addon lifecycle APIs, EF Core, `HttpClient`, ImGui, Newtonsoft.Json/System.Text.Json, xUnit, DalaMock/Echoglossian.Mock where runtime integration requires it, Git/GitHub, and PowerShell.

## Global Constraints

- Scope only open issues [#214](https://github.com/lokinmodar/Echoglossian/issues/214), [#252](https://github.com/lokinmodar/Echoglossian/issues/252), [#209](https://github.com/lokinmodar/Echoglossian/issues/209), [#148](https://github.com/lokinmodar/Echoglossian/issues/148), [#176](https://github.com/lokinmodar/Echoglossian/issues/176), [#171](https://github.com/lokinmodar/Echoglossian/issues/171), and [#203](https://github.com/lokinmodar/Echoglossian/issues/203) as observed on 2026-08-06.
- Treat #12 as a tracker update only, watch #239 as an external dependency, and keep #212 out of scope except for separating DeepL evidence from the multi-engine #203 matrix.
- Do not reopen or encode behavior from closed pre-rework issues unless a current open issue or a new failing test independently reproduces it.
- Treat capture, DB lookup, translation, persistence, publication, overlay rendering, and native mutation as distinct stages.
- No provider call, DB access, file access, model-list refresh, prompt persistence, glossary refresh, or session operation may block a Dalamud framework or ImGui callback.
- Copy native inputs to immutable managed values before scheduling work. Never retain `AtkUnitBase*`, `AtkValue*`, `AtkResNode*`, `AtkTextNode*`, spans, or borrowed buffers across `await` or inside `Task.Run`.
- Background completion may update managed handler state only after `SourcePublicationLifecycle` accepts the captured generation. Native mutation remains on the framework callback and must re-resolve live pointers.
- Reuse the existing translation service, broker, caches, and failure cooldowns. An async task owner is allowed for lifecycle/exception ownership but must not become a second translation queue.
- Every async operation has cancellation for source supersession, configuration change, timeout, or plugin shutdown; every started task is observed.
- Preserve DB tables and lookup semantics. Add no migration unless an issue-specific plan explicitly proves one is necessary.
- Write the failing test first for each behavior change, then implement the smallest passing change.
- Use resx keys for all plugin UI and notifications; do not add inline user-facing fallback strings.
- Treat the issue branch as release-blocking until focused tests, solution build, unit tests, and any required Mock/in-game checks pass.

---

## Delivery map

| Order | Branch | Plan | Outcome |
|---:|---|---|---|
| 1 | `issue-214-first-dialogue-speaker-context` | [Issue 214 plan](2026-08-06-issue-214-first-dialogue-speaker-context-implementation-plan.md) | First-line speaker/glossary correctness plus non-blocking Talk/BattleTalk foundation. |
| 2 | `issue-252-llm-prompt-glossary-effectiveness` | [Issue 252 plan](2026-08-06-issue-252-llm-prompt-glossary-effectiveness-implementation-plan.md) | Prompt Save/Reset persistence, runtime refresh, explicit retranslation, and deterministic glossary protection. |
| 3 | `issue-209-bounded-local-llm-context` | [Issue 209 plan](2026-08-06-issue-209-bounded-local-llm-context-implementation-plan.md) | Configurable bounded runtime dialogue history for local LLMs. |
| 4 | `issue-148-structured-llm-contracts` | [Issue 148 plan](2026-08-06-issue-148-structured-llm-contracts-implementation-plan.md) | Provider-aware Auto/Structured/PlainText contracts with safe fallback. |
| 5 | `issue-176-local-llm-latency` | [Issue 176 plan](2026-08-06-issue-176-local-llm-latency-implementation-plan.md) | Phase metrics and removal of measurable local-engine overhead. |
| 6 | `issue-171-deepseek-runtime-auth` | [Issue 171 plan](2026-08-06-issue-171-deepseek-runtime-auth-implementation-plan.md) | DeepSeek authentication/runtime diagnosis without secret disclosure. |
| 7 | `issue-203-no-translation-matrix` | [Issue 203 plan](2026-08-06-issue-203-no-translation-matrix-implementation-plan.md) | Current-version engine/surface/mode verification matrix and remaining routing fixes. |

## Dependency rules

- #214 owns the safe async dialogue foundation. Later branches consume it and must not reintroduce direct DB reads or unowned tasks in addon callbacks.
- #252 owns prompt/glossary effectiveness and asynchronous UI persistence. #148 consumes its glossary snapshot and term-protection contracts.
- #209 owns context bounds and configuration. #148 and #176 consume those bounds rather than adding provider-local history controls.
- #148 owns provider capability selection and fallback state. #176 may tune measured overhead but must not create a second capability mechanism.
- #171 is isolated after the shared structured/metrics work so DeepSeek failures can be classified with the same contracts.
- #203 is last because it verifies the integrated matrix and should contain only matrix-discovered current defects, not speculative provider rewrites.

## Per-branch start and finish protocol

- [ ] Start from an updated `v4-series`, verify `git status --short` is clean, and create the exact issue branch from the table.
- [ ] Copy the corresponding issue plan into the branch history; do not combine implementation for another issue.
- [ ] Record the current issue body/comments and plugin version in the PR description so closure evidence is tied to the reworked architecture.
- [ ] Execute the issue plan task-by-task and commit each stable sub-scope with the issue number in the commit or PR context.
- [ ] Run the focused tests named in the issue plan.
- [ ] Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Expected result: build succeeds with no new warnings attributable to the branch and the full unit suite passes.

- [ ] If the branch changes addon lifecycle, native capture/publication, configuration-window hosting, or plugin startup/shutdown, also run:

```powershell
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

Expected result: hosted startup/shutdown and the exact mocked integration driven by the new test pass. Do not claim native capture coverage from startup-only tests.

- [ ] Run `git diff --check`, inspect `git status --short`, and commit `Echoglossian.xml` when the validated build changed it.
- [ ] Perform the plan's in-game matrix with `Echoglossian.log` open and verify the game remains responsive while the provider and DB are deliberately delayed.
- [ ] Open a PR back to `v4-series`; merge it before branching the next dependent issue.

## Program completion gate

- [ ] All seven issue PRs are merged or have explicit current-version evidence explaining why closure requires no code change.
- [ ] #12 is updated with links to the seven outcomes; #239 remains a dependency note rather than copied implementation scope.
- [ ] No open LLM issue is closed solely because an old closed issue appeared similar.
- [ ] A repository search confirms no dialogue addon callback contains synchronous EF queries, `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` and no native pointer is captured by background work.
- [ ] Suspended-provider and suspended-DB tests prove addon callbacks return before completion; stale-result tests prove superseded text is never published.
- [ ] Prompt, glossary, model refresh, provider call, and configuration save operations are owned, cancellable where applicable, and exception-observed.
- [ ] The final engine/surface/mode matrix is attached to #203 and the tracker.

