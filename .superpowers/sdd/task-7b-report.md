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
