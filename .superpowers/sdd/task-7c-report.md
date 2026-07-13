# Task 7C Report: Harden Cache and Persistence Source/Engine Identity

## Scope

Implemented only Task 7C production changes in:

- `Cache/GameWindowCacheManager.cs`
- `DBHelpers/ActionTooltipPersistenceHelper.cs`
- `DBHelpers/ItemTooltipPersistenceHelper.cs`
- `DBHelpers/TraitPersistenceHelper.cs`
- `DBHelpers/ReferenceTextPersistenceHelper.cs`
- `DBHelpers/StringArrayDataPersistenceHelper.cs`
- `DBHelpers/DbOperations.cs`

Direct regression coverage was added to the existing cache and persistence test
classes. No DB-first handler, translator/broker, UI texture, EF model, migration,
or schema file was edited by Task 7C.

## Root-Cause Trace

1. `GameWindowCacheManager` converted a nullable stored engine to `0` while
   indexing. The strict exact and scope fast paths then returned indexed values
   without applying `TranslationReuseScope.Matches`, so a legacy null engine was
   indistinguishable from strict engine `0`.
2. Action, item, trait, reference-text, and StringArrayData persistence finders
   hard-filtered `TranslationEngine` in SQL. That bypassed the shared
   engine-compatible reuse policy even though their cache callers already build
   a `TranslationReuseScope`.
3. Talk, BattleTalk, and quest write lookup queries compared source labels as
   exact strings. Canonical `en`, `de`, `fr`, and `ja` writes therefore failed
   to recognize equivalent legacy `English`, `Deutsch`, `French`, and
   `Japanese` rows.
4. `DbOperations.FindAndReturnGameWindow` had no production caller. It read
   configuration through the active plugin instance and selected the first
   addon/source/target/engine-compatible row without payload, class/job, or
   version identity.

## TDD Evidence

### RED

- The first focused behavior run produced 6 expected failures and 1 passing
  preservation test. It demonstrated the null-engine row returned for strict
  engine `0`, duplicate rows for all four legacy source labels, and the legacy
  GameWindow bypass still exposed.
- The fallback-policy test build was run with project references disabled to
  isolate the test API contract while concurrent work was temporarily
  uncompilable. It produced 10 expected `CS1501` errors because no Action,
  Item, Trait, Reference, or StringArray finder accepted an explicit reuse
  scope.

### GREEN

- New regressions: 10/10 passed.
- Direct cache/persistence classes: 73/73 passed.
- Full suite: 515/515 passed.

## Implementation

- GameWindow strict indexes now preserve nullable engine identity in their keys.
  Both exact and scoped indexed hits are revalidated with
  `TranslationReuseScope.Matches` before reuse.
- Canonical persistence helpers now expose explicit-scope finder overloads.
  Their source, target, and engine compatibility is evaluated by the shared
  scope predicate after SQL-safe content/version filtering.
- Existing finder overloads remain strict wrappers for backward compatibility.
- All insert/upsert lookup queries retain exact-engine identity, including the
  transitional StringArrayData legacy path. The tests confirm that engine `7`
  and engine `0` writes remain separate history rows.
- Talk, BattleTalk, and quest semantic write queries now apply
  `RuntimeLanguageHelper.LanguagesMatch` after their SQL filters. This recognizes
  only the established legacy-to-canonical source mappings and preserves `chs`,
  `cht`, `tc`, and unknown source identities as separate rows.
- The unused `FindAndReturnGameWindow` bypass and its obsolete test fixture
  scaffolding were removed. Canonical GameWindow lookup remains owned by the
  cache/persistence architecture with payload, class/job, and version identity.

## Compatibility and Risk

- Schema and migrations are unchanged.
- Existing game-version and class/job matching behavior is unchanged.
- Exact-engine upsert history is unchanged.
- Engine-compatible helper reads can enumerate multiple same-content history
  rows after SQL filtering, but these fallback queries are not frame-loop cache
  paths and remain bounded by stable entity/content/version identity.
- Runtime call sites outside Task 7C ownership retain the strict compatibility
  overload until their owning task passes an explicit scope; this task provides
  and verifies the policy-aware persistence contract requested for the helper
  layer.
- The shared worktree still contains unrelated, unstaged changes from other
  tasks. Task 7C staging and commit exclude those files, including the generated
  `Echoglossian.xml` changes produced by the combined working tree.

## Validation

Commands and results:

```text
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore --filter <Task 7C direct classes>
PASS: 73/73

dotnet build Echoglossian.sln -c Debug --no-restore
PASS: 0 errors, 79 existing/concurrent warnings

dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
PASS: 515/515

git diff --check
PASS
```

The build continues to report pre-existing warnings, including `NU1903` for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and existing nullable/obsolete API warnings.
No warning was introduced in a Task 7C-owned file.
