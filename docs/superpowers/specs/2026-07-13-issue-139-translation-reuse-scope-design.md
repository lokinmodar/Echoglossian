# #139 Translation Reuse Scope Design

## Objective

Make the current Dalamud client language a mandatory part of every persisted
translation reuse decision. A translation must never be applied, restored,
promoted, or used as a cache fallback when it was produced from a different
FFXIV client language.

The rule applies to every runtime surface, not just native UI handlers.

## Canonical Reuse Scope

Every translation lookup has these independent dimensions:

| Dimension | Source of truth | Rule |
| --- | --- | --- |
| Source client language | `IClientState.ClientLanguage` raw value | Must resolve to the same stored client-content identity. |
| Target language | selected plugin language, normalized | Must always match. |
| Translation engine | selected engine and `TranslateAlreadyTranslatedTexts` | Must match only when the toggle requests retranslation by the selected engine. |
| Game version | current game version | Existing version and source-hash rules remain in effect. |
| Source content | visible/canonical original payload | Must match whenever a source hash or complete original payload is available. |

The canonical source contract is a resolved client-language pair, not a
plugin-local `switch` that defaults unknown clients to English:

| Raw `ClientLanguage` value | Stored source identity | Provider source code |
| --- | --- | --- |
| `0` | `ja` | `ja` |
| `1` | `en` | `en` |
| `2` | `de` | `de` |
| `3` | `fr` | `fr` |
| `4` | `chs` | `zh-CN` |
| `5` | `cht` | `zh-CN` |
| `6` | `ko` | `ko` |
| `7` | `tc` | `zh-TW` |

The values follow the FFXIV client-language ordering used by
[Lumina `Language`](https://github.com/NotAdam/Lumina/blob/master/src/Lumina/Data/Language.cs)
and [FFXIVClientStructs `ExcelLanguage`](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Common/Component/Excel/ExcelLanguage.cs),
with Lumina's leading `None` omitted by `ClientLanguage`.

`RuntimeLanguageHelper.TryResolveCurrentSourceLanguage(...)` is the sole
resolver for this pair. It resolves the known raw values above first, then uses
`ClientLanguage.ToCode()` only as a fail-closed-compatible extension point for
future host values. New persisted rows store the source identity, while
translation providers receive the provider source code. `chs`, `cht`, and `tc`
deliberately remain distinct stored identities even where two client values use
the same provider code; no cache or database row may cross those
client-language boundaries.

Existing rows using `English`, `Deutsch`, `French`, or `Japanese` remain
readable through normalized comparison.

An empty or unrecognized stored source identity is never reusable. An unknown
runtime client value also fails closed: the plugin does not reuse, persist, or
translate a row with a guessed source language. An equal source hash does not
override the client-language boundary.

## Engine Semantics

`TranslateAlreadyTranslatedTexts` retains its current meaning:

- `false`: a translation from any engine may be reused, provided source,
  target, version, and content are compatible.
- `true`: only rows from the active engine may be reused, allowing the active
  engine to retranslate content produced by another engine.

The engine policy does not weaken either source-language or target-language
matching.

## Implementation Design

### Uniform Translation Flow

Every surface follows one translation flow:

1. Resolve the live `ClientLanguage` to its stored source identity and provider
   source code, then capture original text with that pair.
2. Resolve only a stored row in the canonical reuse scope.
3. On a cache miss, submit the source text through the shared translation
   service and persist the result with the full scope.
4. Render or mutate the surface using that resolved row.

Addon handlers may specialize only capture, native mutation, and layout. They
must not add literal source-to-target translations, language-specific fallback
maps, or custom compatibility rules.

### Shared Compatibility Guard

Add one shared helper that evaluates a stored source identity, stored target
language, stored engine, and requested scope. All cache and persistence
lookups use it after their surface-specific identity filters. No handler may
implement a local source-language exception.

TranslationService receives the resolved provider source code, not
`ClientStateInterface.ClientLanguage.Humanize()`. Engine adapters may still
normalize that code for vendor-specific APIs, but no caller may pass a display
name or an enum number as a provider language. This keeps source identity
derived from the live client while making Chinese and Korean inputs usable by
code-oriented providers.

### Persistence and Cache Coverage

Apply the shared guard to:

- dialogue, toast, quest, mini-talk, subtitle, select-string, and text-gimmick
  database lookups in `DbOperations`;
- DB-first `GameWindow` and `StringArrayDatas` recovery and lookup candidates;
- action, item, trait, and reference-text canonical caches and persistence;
- text-to-text and reverse cache maps used by `ActionMenu` and Main Command.

Identity-only tooltip/reference fast paths must require the current source
language and exact source-content hash before returning a complete serialized
payload. A complete payload from a different source language must not be
layered over the live client payload.

### Handler-Specific Integration

- `CharacterStatus` must trust a projected canonical payload by structural
  changed-content coverage, not a fixed list of English section titles.
- `Character` title and tab labels must be captured into the canonical
  StringArrayData/GameWindow representation and translated through the shared
  flow. The existing English-to-Portuguese local fallback is removed rather
  than gated to English. This makes the header behavior identical for English,
  Japanese, German, and French clients and for every target language.
- `ActionMenu` must not reconstruct a non-English source label as `Lv.` when
  reversing a translated Portuguese level token. Reverse resolution must use
  the canonical original payload from the current source-language scope; if it
  is unavailable, it fails closed and the shared flow captures/translates the
  current client text.

## Data Compatibility

No schema migration is required. The existing source-language text columns
store the canonical source identities. Legacy source language names are
normalized by `RuntimeLanguageHelper.LanguagesMatch`; they map only to their
equivalent `ja`, `en`, `de`, or `fr` identities. Rows without a usable source
identity remain in the database but are not selected by source-ambiguous
lookups. Fresh rows replace them naturally through the existing persistence
paths.

## Tests and Verification

Add regression tests that prove:

1. A row from English cannot be reused while the current source is Japanese,
   German, French, `chs`, `cht`, `ko`, or `tc`, even with the same target
   language and engine.
2. A different target language is never reusable.
3. Engine reuse follows `TranslateAlreadyTranslatedTexts` without weakening
   source or target matching.
4. Legacy human-readable stored source names match their equivalent runtime
   language code.
5. Identity-only tooltip/reference retrieval rejects a mismatched source hash
   or source language.
6. `CharacterStatus` accepts non-English original section labels when enough
   same-slot text changes are present, and rejects unchanged payloads.
7. `Character` title and tabs are translated through a canonical row with no
   literal English-to-Portuguese fallback.
8. `ActionMenu` does not synthesize an English `Lv.` source label for a
   non-English game client.
9. Raw client values `4`, `5`, `6`, and `7` resolve respectively to stored
   identities `chs`, `cht`, `ko`, and `tc`, and provider codes `zh-CN`,
   `zh-CN`, `ko`, and `zh-TW`.
10. Two rows with source identities `chs` and `cht` cannot be reused across
    each other even though both provider calls use `zh-CN`.
11. An unknown raw client value has no translation scope and cannot trigger a
    provider call, cache reuse, persistence, or native mutation.

Run the standard build and full test suite. In-game validation covers an
English-to-RTL session followed by switching the FFXIV client language and
then the target language and engine, confirming that the previous translation
does not reappear on Character, ActionMenu, native GameWindow, ActionDetail,
ItemDetail, and dialogue surfaces. Where regional clients are available, run
the same matrix for client values `4`, `5`, `6`, and `7` and verify that
Chinese source identities do not cross-reuse.

## As-Built Status (2026-07-13)

`SourceClientLanguage` is the live source contract. Raw values `0` through `7`
persist as `ja`, `en`, `de`, `fr`, `chs`, `cht`, `ko`, and `tc`; provider codes
are `ja`, `en`, `de`, `fr`, `zh-CN`, `zh-CN`, `ko`, and `zh-TW`.
`TranslationReuseScope` uses the persistence identity, target, and selected
engine policy. `chs`, `cht`, and `tc` remain distinct persisted identities even
where provider codes overlap. The in-game matrix remains not run.
