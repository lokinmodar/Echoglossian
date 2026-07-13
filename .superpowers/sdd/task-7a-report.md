# Task 7A Report: Scope Native Runtime State by Source

## Status

Implemented Task 7A within the production ownership declared by
`task-7a-brief.md`. No translator, DB helper, cache-manager, prefetch-runtime,
or UI texture production files were edited for this task.

## Root-cause trace

1. `DbFirstGameWindowAddonHandler.OnPreDrawEvent` returned early for an
   unresolved source and could short-circuit on `runtimeState` without comparing
   the current source.
2. `DbFirstGameWindowRuntimeState` stored payload identity and payloads, but no
   canonical source persistence identity.
3. DB-first in-flight and failure-cooldown dictionaries shared the same payload
   key, which contained addon, target, engine, version, and payload but omitted
   source and the engine-policy flag.
4. The non-preloaded `GameWindow` DB fallback duplicated source/target checks
   and always required exact engine equality instead of applying the existing
   `TranslationReuseScope` policy.
5. Talk, BattleTalk, TalkSubtitle, and MiniTalk local runtime caches compared
   visible source strings only. Their failure and prior-result state could
   therefore survive a client-source transition when visible text was
   identical.
6. Unknown-source branches returned without first clearing overlays and
   restoring tracked plugin-owned native mutations.

## TDD evidence

RED was run before production edits:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter FullyQualifiedName~NativeRuntimeSourceScopeTests
```

Expected compile failures proved the missing behavior: the runtime-state
constructor had no source parameter, `MatchesSource` did not exist, and the
source-scope, work-key, and fallback-policy seams did not exist. The command
exited `1` with `CS1729`, `CS1061`, and `CS0103` errors in
`NativeRuntimeSourceScopeTests.cs`.

GREEN focused validation:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~NativeRuntimeSourceScopeTests|FullyQualifiedName~DbFirstPreDrawRefreshPolicyTests|FullyQualifiedName~ActionMenuWindowHandlerTests"
```

Result: `33` passed, `0` failed, `0` skipped.

The regressions cover:

- identical DB-first payload state rejecting an `en -> de` source change;
- unresolved source requiring runtime invalidation;
- dialogue local-cache matching rejecting identical speaker/text from another
  source;
- DB-first in-flight/cooldown work keys differing by source;
- engine-agnostic fallback accepting another engine only when matching-engine
  policy is disabled, while strict policy rejects it.

## Implementation

- `DbFirstGameWindowRuntimeState` now stores canonical source persistence
  identity and exposes the source match/invalidation decision used before
  mode-switch reuse and pre-draw short-circuiting.
- Unknown or changed source clears resolved DB-first state, removes hover state,
  and restores native content only when `runtimeState` proves this handler wrote
  native content.
- DB-first work keys now include the complete captured
  `TranslationReuseScope`, including source and strict/compatible engine policy.
- Non-preloaded DB fallback candidates now use `TranslationReuseScope.Matches`
  while retaining addon, game version, class/job, and serialized payload checks.
- Talk, BattleTalk, TalkSubtitle, and MiniTalk bubble state now store canonical
  current-source identity. Failure and prior-result state also carry source
  identity where applicable.
- Source is resolved before stale replacement-to-original mapping. Changed or
  unknown source invalidates request IDs, restores tracked plugin-owned native
  mutations, and clears overlays before the handler fails closed.
- Async dialogue callbacks compare the operation-captured source with stored
  request state; they do not resolve source from global runtime state.

## Files

Production:

- `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`
- `NativeUI/AddonHandlers/Talk/TalkHandler.cs`
- `NativeUI/AddonHandlers/Talk/BattleTalkHandler.cs`
- `NativeUI/AddonHandlers/Talk/TalkSubtitleHandler.cs`
- `NativeUI/AddonHandlers/SingleText/MiniTalkHandler.cs`

Tests and report:

- `Echoglossian.Tests/NativeRuntimeSourceScopeTests.cs`
- `.superpowers/sdd/task-7a-report.md`

## Validation

```text
dotnet build Echoglossian.sln -c Debug --no-restore
```

Result: succeeded with `0` errors and `79` warnings across the full concurrent
worktree. Warning classes included existing nullable and obsolete-API warnings,
the unavailable Multilingual App Toolkit, and the
`SQLitePCLRaw.lib.e_sqlite3 2.1.11` advisory `NU1903`.

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
```

Result: `515` passed, `0` failed, `0` skipped.

```text
git diff --check
```

Result: exit `0`; Git emitted line-ending conversion notices only.

## Concerns and in-game verification

- Runtime native UI acceptance was not available in the test environment.
  Verify Talk, BattleTalk, TalkSubtitle, MiniTalk, and a DB-first GameWindow in
  native, overlay-only, and swap modes across `en -> de` and known -> unknown
  source transitions.
- Confirm old native text/layout is restored only when the plugin previously
  mutated it, and that old overlays/hover content disappear immediately.
- Confirm raw client source identities `4` (`chs`) and `5` (`cht`) do not reuse
  each other's live state despite sharing a provider code elsewhere.
- The worktree contained concurrent changes from other tasks. They were left
  untouched and are not part of the Task 7A commit.
