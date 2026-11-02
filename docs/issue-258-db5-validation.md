# Issue 258 DB-5 validation

Worktree: `C:\Dante\_dalamud\Echoglossian\.worktrees\issue-258-06-game-window`.

Branch: `issue-258-06-game-window`, targeting `v4-series`. Release preparation PR #291 was incorporated by fast-forward to `b60e40cbfd68ac5d2c7f9d7eb96b45f3126861d3` before delivery.

## Implementation

- The existing persistence coordinator owns GameWindow preload, admission, interactive writes, pending replacement, batching, commit and shutdown drain. No schema or translation pipeline changes were introduced.
- The shared cache is serialized for worker publication and provides candidate snapshots. Publication after commit preserves session generation and cannot repopulate a cleared cache during shutdown.
- Generic handlers retain original presentation while the committed startup projection loads. Rejected or failed preload retries after five seconds outside frame callbacks and stops on shutdown.
- Translation completion awaits commit before refresh. Supplemental presentation remains immediate, while its observed persistence waits asynchronously for capacity under the complete operation-scope guard. Successful supplemental persistence clears retry and returns without a new PreDraw; only failure schedules a delayed refresh. Accepted writes retain the coordinator drain deadline.
- Identical values generate no UPDATE or timestamp change. Batched operations see pending inserts while retaining the existing directional version fallback and persisted-row preference.

## Audit attribution

The baseline changes from 224 to 206 findings. DB-5 changes from 18 to zero: sixteen GameWindow/continuation findings are removed and two pre-existing StringArrayData operations are reassigned to their owning DB-6 domain, as explicitly requested. DB-6 changes from 15 to 17 solely because of that attribution correction. Neither structured lookup nor structured persistence is migrated here.

DB-7 changes from 124 to 122: the GameWindow compatibility caller now uses the coordinator and the scanner stops treating an existing in-memory query after async materialization as SQL. Other stage counts remain unchanged. Scanner regression tests distinguish async collection results and EF Local views from later real database queries.

## Automated proof

- `scripts\audit-sync-db-hotpaths.ps1`: passed, 206 findings, zero DB-5 findings.
- `dotnet build Echoglossian.sln -c Debug --no-restore`: passed.
- `dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore`: passed.
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`: 1,753 passed after the Tooltip height follow-up.
- `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore`: passed.
- `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1`: 33 passed after the review and Tooltip corrections.
- `dotnet build Echoglossian.csproj -c Release --no-restore`: passed. Existing repository warnings remain; no new GameWindow analyzer warning remains.

SQLite tests exercise actual commit, rollback, no-op SQL suppression, snapshot isolation, identity separation, coalescence, preload recovery and accepted-write drain. Hosted Mock validation exercises the production persistence facade, database/cache publication and writer retirement. It does not drive native addon capture or application.

## Required human validation

Use the Debug DLL from `bin\x64\Debug\win-x64\Echoglossian.dll`, version `4.2601.1031.2017`. The PR delivery records its exact source commit and SHA-256.

1. Open ActionMenu, `_MainCommand`, context menus and another GameWindow surface with an existing persisted translation. Confirm reuse without a provider request, including class/job and submenu changes.
2. Translate a previously unseen payload, close/reopen the addon and reload the plugin. Confirm persistence, independent payload variants and correct source/target/engine reuse.
3. Exercise overlay-only, native and swap modes, including global translation off/on. Confirm original restoration, hover presentation and absence of stale text after scope changes.
4. Unload with pending translations/writes and reload. Confirm accepted writes survive and no old completion mutates the new session.

Native layout, payload capture/application and frame-time behavior require this in-game check. Stop here for human validation; no merge or release publication is authorized.

## Review corrections F1 and F2

F1 now observes supplemental persistence completion rather than discarding its result. Admission and retry preserve the captured `sourceOperation` and its retry deadline while capacity is unavailable. Completion clears retry only after commit; failure rearms a delayed refresh without blocking callbacks. Existing in-flight tracking includes handler lifetime and operation generation so cleanup and reopening the same payload cannot strand a newer operation behind an older completion.

Hosted tests block the production writer, fill every interactive slot, verify rejection and non-blocking pending persistence, release capacity and assert the eventual SQLite commit and cache publication. Separate cases cover scope retirement, reopening the same payload in a new generation, duplicate admission suppression, and delayed refresh after failure. These tests exercise managed payload persistence; native capture and application still require the new in-game test.

F2 stops erasing an identifier from the auditor's file-level provenance set when another method materializes an async collection under that name. The new fixture reuses `rows` as an async collection alias in one method and an `EchoglossianDbContext` parameter in another, and requires the later synchronous query to remain detected. The baseline is unchanged at 206 findings, with zero DB-5 findings.

## Tooltip growth regression correction

The Debug build at `45ff806` could repeatedly refresh a supplemental payload after successful persistence. The payload had already been applied, so an unnecessary PreDraw could re-enter Tooltip reflow with an expanded live layout as its new baseline. This affected Gil hover tooltips in both Journal rewards and the HUD/main-menu Gil indicator. Success now clears cooldown and returns without requesting another refresh. Failure retains the existing delayed retry and refresh.

Tooltip and ContextMenu dedicated persistence now return an observable task instead of reporting success before their database delegates complete. An absent dedicated store remains distinct from a failed dedicated save: failure never falls back to the generic GameWindow table. `ErrorSavingData`, empty results and `No data to save.` are failures. Scoped completion rejects retired operations; Tooltip's post-commit cache publication also uses the atomic ownership guard. Accepted database writes may finish without publishing a retired operation into the live cache.

Supplemental admission in both dedicated handlers applies the same persistence filter to the same ordered, serialized translated texts used by the database, including ContextMenu normalization. Known non-persistable markers are rejected before starting persistence, preventing an intentional `No data to save.` from creating a recurring supplemental retry.

Focused tests exercise actual handler delegates held by task-completion sources, success/error statuses, retired ownership, SQLite insert/update with denied cache publication, delayed refresh after failure, and repeated successful supplemental completions with zero refresh requests. These managed tests do not prove native Tooltip dimensions; in-game confirmation remains required.

Retest with the new Debug DLL after moving the cursor off all tooltips, closing Journal/main-menu surfaces and reloading the plugin:

1. Open a Journal quest with a Gil reward. Keep the cursor over Gil for 15 to 20 seconds. The native tooltip background must settle once and remain the same size.
2. Move off the reward and hover it again several times. Repeat the same hold and re-entry checks on the HUD/main-menu Gil indicator. Neither location may grow on stable hover or progressively widen across re-entry.
3. Switch between both locations, close/reopen the surfaces and reload the plugin once more. Confirm the translated text remains correct and the tooltip returns to a normal size after leaving the hover.

No merge or release is authorized before the user validates this new DLL.

## Tooltip height stabilization follow-up

The user confirmed the width correction in `644dd9e`, but reported intermittent retained height. A reused Tooltip can receive entirely new game text while still retaining geometry mutated by the plugin. Shared payload-transition cleanup now invokes the addon-local layout hook independently of whether selective text restoration found any old translated strings. A boolean hook result requests recapture only when text or owned layout changed; the default hook remains inert for other addon owners.

Tooltip restores only its recorded applied layout snapshots before recapture, preserving the game's newly replaced text. Its reflow enables compact wrapped text height and compact detached height baselines, matching the existing MiniTalk helper path. Width options and formulas are unchanged. Retained snapshot baselines and the existing container formulas preserve measured translated height plus the minimum eight-pixel background padding; there is no fixed height or cap on legitimate multiline growth. Successful supplemental persistence still does not request a new PreDraw.

Focused tests exercise cleanup dispatch when no old translated text remains, unchanged stable payloads, missing mutation ownership, inert non-Tooltip hooks, compact short/long/short container heights, long measured text plus padding and unchanged widths. Native sizing functions are FFXIVClientStructs member-function calls, so these tests cover managed payload dispatch and production geometry formulas rather than native text measurement or actual dimensions. Mock startup/persistence tests do not remove that in-game gap.

Retest from a clean tooltip state with the new Debug DLL:

1. Move off all tooltips, close Journal/main-menu surfaces and reload the plugin. Hover the Journal Gil reward for 15 to 20 seconds, then repeat on the HUD/main-menu Gil indicator. Confirm width stays stable and short content has no retained blank vertical area.
2. Open a longer translated help tooltip with several lines, then immediately return to each Gil location. Repeat short/long/short several times without reloading between transitions. Long content must fit without clipping, and the short tooltips must return to a stable compact height.
3. Leave/re-enter each hover and close/reopen the surfaces. Confirm normal restoration and no repeated success-refresh growth. Compare height for the same short text across repetitions; longer translations may legitimately need more height.

PR #292 remains blocked on renewed human validation. No merge or release is authorized.

Validation note: the first full height-follow-up run passed 1,752 tests and failed the existing `QuestPlatePersistenceWriterTests.TryPersist_ExistingRow_MergesAndPublishesAfterCommit` at its cache assertion. That test passed in isolation, followed by a complete rerun with all 1,753 tests passing. This is recorded as a pre-existing flake under the maintainer's requested validation procedure; no QuestPlate code or test was changed, and its underlying cause was not corrected in this Tooltip patch.
