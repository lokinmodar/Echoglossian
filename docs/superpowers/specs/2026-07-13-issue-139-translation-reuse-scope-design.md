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
| Source language | `IClientState.ClientLanguage` | Must match the language of the stored original payload. |
| Target language | selected plugin language, normalized | Must always match. |
| Translation engine | selected engine and `TranslateAlreadyTranslatedTexts` | Must match only when the toggle requests retranslation by the selected engine. |
| Game version | current game version | Existing version and source-hash rules remain in effect. |
| Source content | visible/canonical original payload | Must match whenever a source hash or complete original payload is available. |

`RuntimeLanguageHelper.GetCurrentGameLanguageCode()` is the canonical source
language identity for persistence and cache compatibility. New persisted rows
store that normalized code. Existing rows using `English`, `Deutsch`,
`French`, or `Japanese` remain readable through normalized comparison.

An empty or unrecognized stored source language is never reusable. This fails
closed: the text is captured and translated again instead of reusing an unsafe
row. An equal source hash does not override the client-language boundary.

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

1. Capture original text from the live client and attach the current normalized
   source-language code.
2. Resolve only a stored row in the canonical reuse scope.
3. On a cache miss, submit the source text through the shared translation
   service and persist the result with the full scope.
4. Render or mutate the surface using that resolved row.

Addon handlers may specialize only capture, native mutation, and layout. They
must not add literal source-to-target translations, language-specific fallback
maps, or custom compatibility rules.

### Shared Compatibility Guard

Add one shared helper that evaluates a stored source language, stored target
language, stored engine, and requested scope. All cache and persistence
lookups use it after their surface-specific identity filters. No handler may
implement a local source-language exception.

Provider calls continue to receive the current `ClientLanguage`-derived
language value expected by their adapters. This work does not globally replace
`ClientStateInterface.ClientLanguage.Humanize()` in translation requests;
those calls already derive from the live client and several providers retain
their own source-language mapping. Persisted identity uses the normalized
helper code instead.

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

No schema migration is required. Existing source language names are normalized
by `RuntimeLanguageHelper.LanguagesMatch`. Rows without a usable source
language remain in the database but are not selected by source-ambiguous
lookups. Fresh rows replace them naturally through the existing persistence
paths.

## Tests and Verification

Add regression tests that prove:

1. A row from English cannot be reused while the current source is Japanese,
   German, or French, even with the same target language and engine.
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

Run the standard build and full test suite. In-game validation covers an
English-to-RTL session followed by switching the FFXIV client language and
then the target language and engine, confirming that the previous translation
does not reappear on Character, ActionMenu, native GameWindow, ActionDetail,
ItemDetail, and dialogue surfaces.
