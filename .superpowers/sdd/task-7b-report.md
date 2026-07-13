# Task 7B Report: Scope Broker and Failure Cache by Source

## Status

Implemented the Task 7B source-scope fix within the production ownership defined
by `task-7b-brief.md`. No DB-first handler, DB operation helper, cache manager,
or texture code was edited or staged by this task.

## Root Cause Trace

The action-detail, reference-text, and accepted-quest prefetch runtimes built
shared broker keys from payload identity only. A pending or completed result
could therefore be reused after the client source changed. Their completion
callbacks also consulted live target/engine configuration, allowing a result
captured under one operation scope to be persisted under another.

The translation failure path used the outbound provider language as persistent
source identity. Raw client sources 4 (`chs`) and 5 (`cht`) both map to provider
code `zh-CN`, so a failure from one source could suppress the other source.

## Implementation

- Each prefetch runtime now captures one immutable `TranslationReuseScope` and
  includes its source persistence code, target, engine, and engine-policy flag
  in broker and related cooldown identities alongside the existing payload.
- Completion callbacks receive and persist with the captured scope instead of
  re-reading mutable source, target, or engine configuration.
- Translation requests receive the captured `SourceClientLanguage`; provider
  calls use `ProviderCode`, while failure lookup and persistence use the
  canonical `PersistenceCode`.
- Unknown or mismatched source identity fails closed before broker/failure
  lookup, provider invocation, or persistence.
- Existing shared broker, translation service, and persistence infrastructure
  is reused; no parallel queue, cache, or service-locator path was introduced.

## TDD Evidence

Tests were authored before production edits. The first RED execution in the
shared worktree could not compile because concurrent DB-first task edits were
temporarily incomplete. To preserve strict test-first evidence without editing
that work, the test-only patch was replayed against a clean `HEAD` archive.

RED command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~PrefetchBrokerSourceScopeTests|FullyQualifiedName~TranslationServiceTests.Translate_ChineseClientSources_SeparateFailureIdentity|FullyQualifiedName~TranslationServiceTests.Translate_UnknownCapturedSource_PerformsNoWork"
```

RED result: 10 failed, 0 passed. Failures identified the missing source-scoped
broker key builders, immutable-scope callbacks, accepted-quest scoped row
creation, and captured-source translation-service overload.

Focused green result for the same filter: 10 passed, 0 failed.

Broader focused command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~TranslationServiceTests|FullyQualifiedName~QueuedTranslationBrokerTests|FullyQualifiedName~TranslationFailureCacheManagerTests|FullyQualifiedName~PrefetchBrokerSourceScopeTests"
```

Broader focused result: 31 passed, 0 failed.

The regression tests prove distinct source A/B work identity for action,
reference, and quest broker calls; every `TranslationReuseScope` field affects
the key; callbacks carry captured scope; accepted-quest persistence uses the
captured source/target/engine; `chs` and `cht` retain separate failure identity
while both send `zh-CN`; and unknown sources perform no failure-cache or
provider activity.

## Validation

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
git diff --check
```

- Build: passed with 0 errors. The existing Multilingual App Toolkit warning
  and SQLitePCLRaw 2.1.11 `NU1903` warning remain.
- Full suite: 515 passed, 0 failed, 0 skipped.
- Diff check: passed.

## Risks and Follow-up

No persistence schema or UI mutation behavior changed. The source transition
behavior should still be smoke-tested in game by switching between distinct
client source languages during action/reference/accepted-quest prefetch,
including raw sources 4 and 5. Unrelated shared-worktree modifications were
excluded from this task's staged scope and commit.

## Review Findings Follow-up

The P1 async gap was confirmed in `TranslationService`: every string-based
`TranslateAsync` overload reached the terminal path with no captured source
contract. Captured-source overloads now share one async core across default,
surface, dialogue-context, and combined routing. A delayed `chs` operation
continues to send provider code `zh-CN` while failure lookup and persistence
retain `chs`, even after the live resolver changes to `cht`. Unknown,
inconsistent, or source/scope-mismatched captured contracts fail before
resolver, broker, failure-cache, provider, or persistence activity.

The P2 reflection and signature-only tests were replaced with production-path
dispatch tests. ActionDetail, ReferenceText, and AcceptedQuest now call a
narrow shared orchestrator that performs the production scoped-key lookup,
shared broker queue, cache-hit completion, and immutable-scope callback. Tests
schedule otherwise-identical `chs` and `cht` payloads, mutate live
source/target/engine policy before completion, and assert both queued and cached
callbacks persist their captured scopes. Invalid source entry is asserted not
to reach broker lookup or queueing.

RED command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~PrefetchBrokerSourceScopeTests|FullyQualifiedName~TranslationServiceTests.TranslateAsync_CapturedChsAfterResolverChangesToCht_PreservesSourceScope|FullyQualifiedName~TranslationServiceTests.TranslateAsync_UnknownOrMismatchedCapturedSource_PerformsNoWork"
```

RED result: test compilation failed on the intentionally missing production
prefetch dispatch delegates/result type and family dispatch methods. No
production file had been edited at that point.

Focused GREEN result for the same command: 6 passed, 0 failed.

Broader affected command:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~TranslationServiceTests|FullyQualifiedName~PrefetchBrokerSourceScopeTests|FullyQualifiedName~QueuedTranslationBrokerTests|FullyQualifiedName~TranslationFailureCacheManagerTests"
```

Broader affected result: 29 passed, 0 failed.

Fresh validation after the review fixes:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
git diff --check
```

- Build: passed with 0 errors; the existing Multilingual App Toolkit and
  SQLitePCLRaw 2.1.11 `NU1903` warnings remain.
- Full suite: 523 passed, 0 failed, 0 skipped.
- Diff check: passed.
