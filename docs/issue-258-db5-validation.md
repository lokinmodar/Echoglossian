# Issue 258 DB-5 validation

Worktree: `C:\Dante\_dalamud\Echoglossian\.worktrees\issue-258-06-game-window`.

Branch: `issue-258-06-game-window`, targeting `v4-series`. Release preparation PR #291 was incorporated by fast-forward to `b60e40cbfd68ac5d2c7f9d7eb96b45f3126861d3` before delivery.

## Implementation

- The existing persistence coordinator owns GameWindow preload, admission, interactive writes, pending replacement, batching, commit and shutdown drain. No schema or translation pipeline changes were introduced.
- The shared cache is serialized for worker publication and provides candidate snapshots. Publication after commit preserves session generation and cannot repopulate a cleared cache during shutdown.
- Generic handlers retain original presentation while the committed startup projection loads. Rejected or failed preload retries after five seconds outside frame callbacks and stops on shutdown.
- Translation completion awaits commit before refresh. Supplemental presentation remains immediate, while its observed persistence waits asynchronously for capacity under the complete operation-scope guard. Retry state is cleared only after commit. Accepted writes retain the coordinator drain deadline.
- Identical values generate no UPDATE or timestamp change. Batched operations see pending inserts while retaining the existing directional version fallback and persisted-row preference.

## Audit attribution

The baseline changes from 224 to 206 findings. DB-5 changes from 18 to zero: sixteen GameWindow/continuation findings are removed and two pre-existing StringArrayData operations are reassigned to their owning DB-6 domain, as explicitly requested. DB-6 changes from 15 to 17 solely because of that attribution correction. Neither structured lookup nor structured persistence is migrated here.

DB-7 changes from 124 to 122: the GameWindow compatibility caller now uses the coordinator and the scanner stops treating an existing in-memory query after async materialization as SQL. Other stage counts remain unchanged. Scanner regression tests distinguish async collection results and EF Local views from later real database queries.

## Automated proof

- `scripts\audit-sync-db-hotpaths.ps1`: passed, 206 findings, zero DB-5 findings.
- `dotnet build Echoglossian.sln -c Debug --no-restore`: passed.
- `dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore`: passed.
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`: 1,718 passed after F1/F2 corrections.
- `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore`: passed.
- `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1`: 32 passed after F1/F2 corrections.
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
