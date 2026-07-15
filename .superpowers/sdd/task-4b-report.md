# Task 4B Report

## Status

Implemented Task 4B Slice A. Canonical Action, Item, Trait, ReferenceText,
StringArrayData, ActionMenu, Character, and MainCommand callers now propagate a
captured `TranslationReuseScope`. Transitional target/engine and
source-explicit cache adapters were removed. No GameWindow cache key,
GameWindow persistence, canonical persistence predicate, or schema change was
made.

## TDD Evidence

- Baseline focused run: 42 tests passed.
- RED command: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~CanonicalTooltipIdentityLookupTests|FullyQualifiedName~ActionMenuWindowHandlerTests"`.
- RED result: expected `CS1501` and `CS7036` failures because the scope-based
  registry and ActionMenu seams did not exist.
- GREEN focused command: `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~ActionTooltipCacheManagerTests|FullyQualifiedName~CanonicalTooltipIdentityLookupTests|FullyQualifiedName~ActionMenuWindowHandlerTests|FullyQualifiedName~SourceScopedFallbackFlowTests|FullyQualifiedName~MainCommandCanonicalTextResolverTests|FullyQualifiedName~MainCommandTextPersistenceTests"`.
- GREEN focused result: 58 passed, 0 failed, 0 skipped.
- Added matching-source success and mismatching-source failure coverage for
  forward, reverse, canonical identity, registry identity, ActionMenu, and
  cache candidate queries.

## Files

- Cache contracts: `Cache/ActionTooltipCacheManager.cs`,
  `Cache/ItemTooltipCacheManager.cs`, `Cache/TraitCacheManager.cs`,
  `Cache/ReferenceTextCacheStore.cs`, `Cache/ReferenceTextCacheRegistry.cs`,
  `Cache/StringArrayDataCacheManager.cs`.
- DB callers: `DBHelpers/ActionItemTooltipDbOperations.cs`,
  `DBHelpers/ReferenceTextDbOperations.cs`.
- Runtime callers: `NativeUI/Helpers/ActionItemDetailUiRuntime.cs`,
  `NativeUI/Helpers/MainCommandCanonicalTextResolver.cs`,
  `NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs`,
  `NativeUI/AddonHandlers/Common/DbFirstGameWindowAddonHandler.cs`,
  `NativeUI/AddonHandlers/Character/CharacterTextNodeWindowHandlerBase.cs`,
  `NativeUI/AddonHandlers/Character/CharacterStatusSubWindowHandler.cs`,
  `NativeUI/AddonHandlers/MainMenu/MainCommandHandler.cs`, and
  `NativeUI/AddonHandlers/MainMenu/AddonContextMenuTitleHandler.cs`.
- Tests: `Echoglossian.Tests/ActionTooltipCacheManagerTests.cs`,
  `Echoglossian.Tests/CanonicalTooltipIdentityLookupTests.cs`,
  `Echoglossian.Tests/ActionMenuWindowHandlerTests.cs`,
  `Echoglossian.Tests/SourceScopedFallbackFlowTests.cs`,
  `Echoglossian.Tests/MainCommandCanonicalTextResolverTests.cs`, and
  `Echoglossian.Tests/MainCommandTextPersistenceTests.cs`.
- Generated API documentation: `Echoglossian.xml`.

## Validation

- `dotnet build Echoglossian.sln -c Debug --no-restore`: passed with 0 errors
  and the two existing environment/package warnings.
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`:
  479 passed, 0 failed, 0 skipped.
- `git diff --check`: passed.
- Slice B diff check: no changes in `Cache/GameWindowCacheManager.cs` or any
  canonical/GameWindow persistence helper.

## Commit

`#139 Migrate canonical cache callers to source scope`

## Remaining Risks

- The audit omitted the source-blind `MainCommandCanonicalTextResolver` callers;
  compiler-driven migration found them. They were migrated within Slice A
  because both live addon overrides already capture `SourceClientLanguage`.
- In-game verification should switch source languages across ActionMenu,
  Character StringArrayData, MainCommand, action/item/trait detail fallback,
  and native, overlay-only, and swap modes.
- Slice B still owns GameWindow cache identity and canonical persistence
  source predicates.
