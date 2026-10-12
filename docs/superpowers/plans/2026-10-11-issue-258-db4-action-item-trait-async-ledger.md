# Issue 258 DB-4 Execution Ledger

## Handoff identity

- Target: Issue 258 DB-4 Action, Item, and Trait async persistence.
- Source branch: `issue-258-05-action-item-trait`.
- Base `origin/v4-series`: `3940d384dd65ccdba7be0dbe264ba73bcf5c4289`.
- Target pull request base: `v4-series`.
- Task: `.tasks/issue-258-db4-action-item-trait-async.md`.
- Master design: `docs/superpowers/specs/2026-09-01-issue-258-async-persistence-and-translation-toggle-design.md`.

The source branch was cut directly from the base above. Preparation commits add only the task, this ledger, and the workflow skill. Implementation must remain on the new delegated worktree branch descended from this source branch.

## Frozen scope

- Migrate runtime Action, Trait, and Item canonical reads/writes to the existing bounded `PersistenceCoordinator`.
- Preserve all current tables, rows, identities, fallback/promotion semantics, translation behavior, and display modes.
- Preserve current prefetch budgets: Action 6 per 2 seconds, Trait 6 per 2 seconds, Item 10 per 2 seconds.
- Visible cache misses keep original text, use `Interactive` persistence priority, and retain the existing 30-second admission cooldown.
- Prefetch persistence uses `Background` priority.
- Cache publication occurs only after a successful read or committed/unchanged write.
- Workers operate only on immutable managed snapshots and never mutate native UI state.

The audit currently groups 42 findings under DB-4. The accepted planning classification is:

- 24 persistence helper/operation findings that require runtime ownership review;
- 8 startup preload findings deferred to DB-8;
- 10 non-DB synchronization or completed-task findings that are not DB-4 refactor targets.

Do not chase the raw count to zero by changing startup preload, `AsyncSerialActionPump`, `TranslationOverlay.Semaphore`, or completed-task access in `TooltipHandler`.

## Slice ledger

| Slice | Required result | Status | Commit | Evidence |
|---|---|---|---|---|
| Preparation | Task, skill, and ledger committed | Complete | `811000ac242c8d23f7be7ee7defad210c480b182` | Clean preparation HEAD verified after repository-local worktree migration |
| 1. Canonical persistence | One coordinator-backed writer and async EF paths with semantic parity | Complete | `c4ded2d9360b15941434e7ff183ba3b7cc066b3c` | 43 real-SQLite writer cases plus 2 concurrent cache snapshot cases passed; existing canonical/cache regression filter 33 passed; independent canonical review fixed unlocked lookup snapshots and claimed-write coalescence; Debug solution/test builds pass |
| 2. Action and Trait prefetch | Background persistence with existing cadence and terminal ownership | Complete | `a7795be505564da1203c3a31907512a40cf29a3e` | 14 real-SQLite managed-worker cases plus 4 bounded operation-owner cases passed; 59 persistence/broker/validation regression cases passed; independent slice review found no correctness issues |
| 3. Item prefetch | Background persistence with existing source/HQ identity and cadence | Complete | `9249938fe60f4128616a2e972231a4f3a7263014` | 29 combined managed worker/owner/inventory cases passed (7 new Item worker cases); 11 Item and structured-tooltip regressions passed; standard/HQ ID normalization covered; existing EventItem/DeepDungeon dedicated-row reuse tests preserved |
| 4. Visible structured tooltips | Cache-first hit, nonblocking interactive miss, safe Framework-thread apply | Complete | `1e9b7425b915b8692ae3f534874d8ba9bf9791f0` | 36 focused tooltip/runtime contract cases passed, including all 19 DB-4 source/cadence/config/cooldown contracts; hosted DalaMock writer creation/disposal/rejected-write test passed; Debug unit and Mock test builds pass |
| Final validation | Full build/tests/audit/review, Debug DLL and SHA-256 | Pending |  |  |
| Pull request | Pushed branch and focused PR to `v4-series` | Pending |  |  |

Update this table in the same short commit that makes each slice stable. Do not mark a slice complete without recording its commit and concrete validation evidence.

## Commit boundaries

Execution checkout: `C:\Dante\_dalamud\Echoglossian\.worktrees\issue-258-db4-action-item-trait-execution`, branch `issue-258-db4-action-item-trait-execution`. Migration preserved the preparation HEAD and all pending changes. The old app-managed path is a compatibility junction only.

Slice 1 uses the same SQL candidates, language matching, completeness/time ordering, and merge policy for compatibility and async paths. Existing startup preload methods remain unchanged. Cache locks also guard ActionMenu snapshot projections; the writer keeps active write completions until terminal completion, including coordinator-claimed transactions. This ownership stores no translations and creates no additional queue.

Slice 2 serializes managed payload snapshots and captures source, target, engine, game version and translator resolution on Framework entry. Its managed workers join the existing broker and await registration plus final persistence. A bounded owner stores only operation Tasks and terminal times, preserving coalescence through translation and commit and cooling down failure for 30 seconds. Original Action/Trait tick budgets and sheet capture are unchanged. Existing static test/tool compatibility entrypoints remain but production no longer calls them. Mock `GetUIModule()` returns zero; managed worker tests do not prove native tooltip capture/application.

Keep at most four behavioral commits, plus narrow review fixes and the final ledger update:

1. `perf(#258): add async action item trait persistence`
2. `perf(#258): schedule action and trait prefetch asynchronously`
3. `perf(#258): schedule item tooltip prefetch asynchronously`
4. `perf(#258): make structured tooltip lookup nonblocking`

Push every validated stable commit. Do not accumulate the entire stage into one commit.

## Mandatory validation

Slice 3 keeps inventory, armory, equipment and hotbar capture on Framework, preserving the existing normal/HQ normalization and item-source dispatch. EventItem and DeepDungeon remain on the existing reference-text pipeline. Captured Item workers have no native pointers, live sheets, mutable configuration or language lookups.

Slice 4 removes synchronous fallback from all three visible canonical lookups. Cache misses retain original UI text and use the existing cooldown before Interactive admission with the already captured payload. Trait receives the same on-demand path as Action/Item. No native node, overlay renderer, semaphore, or display-mode implementation changes. The existing effective structured-tooltip mode remains Tooltip-only; DB-4 does not enable native/swap paths gated by the pinned dependency.

Mock coverage verifies plugin lifetime wiring and rejects writer admission after hosted disposal. `ScenarioGameGui.GetUIModule()` returns zero and does not emulate native tooltip nodes, agents, or player state; extending it to execute the complete real capture/application path would require native FFXIV structures and function dispatch beyond this persistence scope. Remaining live checks: Action/Trait hovers; normal/HQ Item, EventItem and DeepDungeon hover reuse; first-hover original then translated overlay; rapid hover/source/engine changes; disable/unload while work is pending; cache hits and frame-time behavior. Native/swap remain subject to their pre-existing safety gate.

- Focused real-SQLite persistence tests for each completed slice.
- Runtime contract tests proving migrated callers do not invoke synchronous DB helpers.
- `dotnet build Echoglossian.sln -c Debug --no-restore`
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
- `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore`
- `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1`
- `scripts\audit-sync-db-hotpaths.ps1`, followed by behavioral review of every remaining DB-4 finding.
- Independent final diff review before opening the PR.

If Mock/DalaMock cannot drive the native payload path, record the exact limitation and keep the remaining in-game verification explicit. Do not claim startup-only Mock coverage proves tooltip capture or application.

## Delivery and authorization gate

## Final review fixes

- Fresh independent review initially ran 115 focused proofs at `1e9b742` and found two P2 issues. Empty/source-equivalent provider responses now use the broker's existing quiet terminal cooldown instead of a generic exception. Proper-noun names remain valid when accompanied by translated descriptions, using the existing final payload validation; name-only source-equivalent output remains rejected.
- Six failure-response cases assert zero generic broker error logs. Nine new worker cases cover translated-description/proper-name persistence, cached proper-name reuse without work, and name-only rejection across Action/Trait/Item. The focused worker/validation batch passed 32 cases; review confirmed the assertions reach the production worker boundary.
- Full unit validation first found an existing vendored debouncer spelling mismatch with its source contract. Its parameter is now explicitly `System.Action`, equivalent to the previous `SysAction` alias; the test was preserved. After that correction, all 1628 then-current unit tests passed. Final expanded suite rerun follows this review-fix commit.
- Six newly introduced StyleCop parameter-layout warnings were removed; existing repository warnings remain.
- Audit baseline refresh changes only six retained compatibility query spellings. Audit passes 224 total findings. DB-4 retains 42: 24 compatibility/setup API findings with no migrated runtime callers, eight DB-8 startup preloads, and ten unchanged non-DB/completed-task findings. The generated inventory retains the existing DB-2 narrative and adds the DB-4 behavioral classification.

## Delivery and authorization gate

- Push the delegated execution branch and open a PR against `v4-series`.
- Attach the PR to the delegated task and report its URL.
- Report the exact `bin\x64\Debug\win-x64\Echoglossian.dll`, source commit, and SHA-256.
- Stop after PR creation and review fixes unless the user explicitly requests merge.
- Do not change version/release metadata, create tags or releases, or submit D17 manifests.
- A future DB-4 release also requires UI-A integration, successful user testing of the exact build, and a later explicit request authorizing that specific release.
