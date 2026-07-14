# Canonical Translation Data Compatibility

## Live Source Contract

Live source is resolved only from raw `IClientState.ClientLanguage` through
`RuntimeLanguageHelper`. Configuration, a hardcoded default, and a previously
visible translation are not source authority. `SourceClientLanguage` carries
both the persisted source identity and the provider input through live
operation boundaries; `TranslationService` selects `ProviderCode` internally.

| Raw value | Persisted identity | Provider code |
| --- | --- | --- |
| 0 | `ja` | `ja` |
| 1 | `en` | `en` |
| 2 | `de` | `de` |
| 3 | `fr` | `fr` |
| 4 | `chs` | `zh-CN` |
| 5 | `cht` | `zh-CN` |
| 6 | `ko` | `ko` |
| 7 | `tc` | `zh-TW` |

Unknown raw values fail closed before provider work, cache lookup, persistence,
or native mutation. New runtime code must pass `SourceClientLanguage` rather
than a provider code to a legacy string translation overload.

## Reuse And Persistence

`TranslationReuseScope` requires matching source, target, and engine policy
from `TranslateAlreadyTranslatedTexts`. Its callers combine that predicate with
their existing game-version and source-content checks. `chs`, `cht`, and `tc`
never cross-reuse, even when provider aliases overlap. `zh-CN` is provider
input, never a persisted client-source identity.

No EF schema or data migration belongs to #139: existing tables retain source
language. Do not perform a database-wide alias rewrite or implicit
deduplication, because canonical and legacy rows can collide and provenance
cannot be inferred safely. Read-time compatibility is limited to established
legacy English, German, French, and Japanese labels and their canonical codes,
under the complete current scope. A normal scoped upsert may promote the source
metadata of the compatible row it updates to its canonical code; that is not a
backfill and never assigns a source to an ambiguous row. Empty, unknown,
generic-provider, and ambiguous Chinese legacy origins remain stored but
non-reusable until a new live-client operation creates or updates a
source-proven row.

## Publication And Node Identity

Capture, publication, apply, stale recovery, and restore use visible text-node
`nodeId:ordinal` allocation. A filtered visible node consumes its ordinal.
Source transitions invalidate old state before publishing a new generation, so
stale async work cannot apply into a new source generation. Overlay-only flows
remain presentation-only and do not mutate native state.

## Evidence And Remaining Checks

Repository task reports record automated coverage for source resolution,
scope-aware reuse, captured async work, source-generation invalidation, and
bounded texture presentation. They do not record complete in-game acceptance.
The manual matrix in `docs/issue-139-canonical-language-validation.md` remains
the authority for unrun client/language, RTL, hover, and dense-GameWindow
checks.
