# Task 8: Captured Source Contract Report

## Boundary Changes

- Updated the owned live-client callers in `GenericAddonHandlerHelper`,
  `DbFirstStructuredStringArrayHelper`, `DbFirstGameWindowAddonHandler`,
  `ItemDetailPrefetchRuntime`, `TraitDetailPrefetchRuntime`,
  `QuestAddonHandlerBase`, `Glossian`, `CutSceneSelectStringHandler`,
  `TextGimmickHintHandler`, `QuestToastRuntime`, `ToastGuiCaptureRuntime`,
  `ToastGuiSupportedToastRuntime`, `AddonTextToastHandler`, `MiniTalkHandler`,
  `TalkSubtitleHandler`, `TalkHandler`, and `BattleTalkHandler`.
- Promoted `GenericAddonHandlerHelper` and
  `DbFirstStructuredStringArrayHelper` operation parameters to
  `SourceClientLanguage` and routed each service call through the captured
  contract overload.
- Used `sourceLanguage.PersistenceCode` for canonical StringArray persistence;
  provider selection remains within `TranslationService`.
- Kept existing provider-code locals only where required by persistence guards,
  never as a `TranslationService` source argument.

## TDD Evidence

- The required captured-source regression test was already committed by the
  concurrent Task 7 change (`2d79ebe`, `#139 Preserve async broker source
  scope`):
  `TranslateAsync_CapturedChsAfterResolverChangesToCht_PreservesSourceScope`.
  It injects a resolver changed from `chs` to `cht`, calls the captured-source
  overload, and asserts `chs` is used for failure identity while `zh-CN` is
  supplied to the translator.
- This task could not add a duplicate test or capture a new valid RED result:
  the public overload and its focused regression coverage were already present
  before Task 8 production edits.

## Validation

- Focused command attempted:
  `dotnet test .\\Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-restore --filter 'FullyQualifiedName~TranslationServiceTests'`.
- The command did not run tests because compilation failed with CS1503 at
  `NativeUI/AddonHandlers/ActionMenu/ActionMenuWindowHandler.cs:171`: it passes
  `sourceLanguage.ProviderCode` to the now contract-typed
  `GenericAddonHandlerHelper.TranslatePayloadAsync` parameter.
- `ActionMenuWindowHandler.cs` is outside the explicit Task 8 ownership list;
  it already has `SourceClientLanguage sourceLanguage` and requires the
  one-line change to pass `sourceLanguage` directly. No owned change can
  safely recover the captured `chs` or `cht` identity from `zh-CN`.

## Remaining Intentional String Overloads

- `TranslationService` string overloads are retained as compatibility adapters
  per the task brief. No owned live-client call site audited here passes a
  provider code or derived string to either overload.

## Final Completion

- Ownership was expanded to include `ActionMenuWindowHandler`. Its captured
  `SourceClientLanguage` now flows directly into
  `GenericAddonHandlerHelper.TranslatePayloadAsync`, resolving the initial
  CS1503 blocker without re-resolving or deriving the source identity.
- Focused GREEN: `TranslationServiceTests` passed, 21 of 21 tests.
- Build GREEN: `dotnet build .\\Echoglossian.sln -c Debug --no-restore`
  passed with 0 errors (the pre-existing multilingual-toolkit and NU1903
  warnings remain).
- Full-suite GREEN: `dotnet test .\\Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`
  passed, 548 of 548 tests.
- Final self-review confirmed every owned live-client service invocation uses
  `SourceClientLanguage`; `git diff --check` passed.
