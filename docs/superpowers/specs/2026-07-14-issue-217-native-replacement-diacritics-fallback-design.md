# Issue 217 Native Replacement Diacritics Fallback Design

## Summary

This spec defines a future refactor for the optional diacritics-removal
fallback used when Echoglossian writes translated text back into native FFXIV
UI surfaces that still depend on the game's own font.

The current runtime behavior is valid in principle and must be preserved:

- the fallback is opt-in
- the fallback applies only to native replacement flows
- overlay and texture-backed plugin presentation never use it

The problem is not semantics. The problem is that eligibility is currently
encoded through a hardcoded numeric language-id check in
`PluginUI/PluginUI.cs`, while several runtime paths still gate only on the raw
config toggle and display mode.

GitHub issue: [#217](https://github.com/lokinmodar/Echoglossian/issues/217)

## Problem

Current repo facts:

- `PluginUI/PluginUI.cs` exposes the toggle through a hardcoded list of target
  language ids.
- `GeneralHelpers/Utils.cs` creates a native replacement normalizer from the
  config toggle alone.
- `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs` decides quest-family
  diacritics removal from display mode plus the config toggle alone.
- `NativeUI/Helpers/QuestAddonWiring.cs` always injects a normalization
  delegate, leaving the actual callers to decide whether to use it.
- Talk, BattleTalk, MiniTalk, subtitle, toast, quest, main-menu, and DB-first
  game-window paths all have slightly different gating seams.

That leaves three architectural issues:

1. language eligibility is not modeled canonically in language metadata
2. numeric id checks are brittle and hard to audit
3. runtime hardening does not currently prove that only eligible languages can
   activate the normalizer path

## Goals

1. Preserve the current user-facing behavior exactly.
2. Replace scattered numeric id checks with explicit per-language metadata.
3. Centralize runtime eligibility checks so UI and handler code consume one
   canonical rule.
4. Keep overlay and texture-backed presentation explicitly out of scope.
5. Make the external-font-override story explicit: this remains an optional
   fallback, never a forced transform.

## Non-Goals

- no heuristic or auto-detected eligibility system
- no expansion of language coverage without explicit validation
- no changes to overlay, tooltip, or texture-backed rendering
- no DB migration or persistence changes
- no attempt to infer the user's external font mods

## Options Considered

### Option A: Keep the numeric id allowlist

Pros:

- smallest code delta
- zero migration work

Cons:

- continues hiding the rule in one UI file
- does not harden the runtime against stale config or non-UI callers
- keeps maintenance coupled to fragile ids instead of explicit language data

### Option B: Infer eligibility automatically from font support or Unicode decomposition

Pros:

- looks generic
- could catch some missed languages automatically

Cons:

- not reliable for letters such as `Ł/ł`, `Đ/đ`, `İ/ı`, `Ğ/ğ`
- cannot prove the real behavior of the game's native font across all addons
- breaks the intentional opt-in semantics for users who replaced the game font

### Option C: Store explicit eligibility in `LanguageInfo` and centralize the policy

Pros:

- keeps the rule curated and auditable
- decouples the feature from numeric ids
- lets UI and runtime share the same decision
- preserves current semantics without guesswork

Cons:

- requires touching `LanguagesDictionary.cs`
- still needs manual review when new languages are added

## Chosen Approach

Choose Option C.

The refactor should treat diacritics fallback eligibility as explicit language
metadata, not as inferred behavior.

The recommended design is:

- add one boolean property to `LanguageInfo`, for example
  `SupportsOptionalNativeReplacementDiacriticsFallback`
- set that property only on languages that were intentionally curated for the
  current native-font fallback behavior
- introduce one small shared policy helper that answers:
  - whether the selected language is eligible
  - whether the UI should expose the toggle
  - whether runtime native replacement paths may build or apply the normalizer

## Proposed Design

### 1. Model eligibility in language metadata

`LanguagesHandling/LanguageInfo.cs` gains a boolean property:

```csharp
public bool SupportsOptionalNativeReplacementDiacriticsFallback { get; set; }
```

`LanguagesHandling/LanguagesDictionary.cs` sets the property through object
initializers only for the validated language entries.

This keeps the rule attached to the language definition instead of an
unrelated UI branch.

### 2. Add one canonical policy helper

Create a small helper such as:

- `LanguagesHandling/NativeReplacementDiacriticsPolicy.cs`

Recommended API shape:

```csharp
internal static bool IsEligible(
    int languageId,
    IReadOnlyDictionary<int, LanguageInfo> languages)
```

Every surface-specific decision must be an explicit composition of:

- eligible language
- user toggle enabled
- native replacement display mode

### 3. Harden runtime gating, not only UI visibility

The refactor must not stop at `PluginUI.LangToRemoveDiacritics`.

At minimum, the shared policy must gate:

- `PluginUI/PluginUI.cs` toggle visibility
- `GeneralHelpers/Utils.cs` normalizer creation
- `NativeUI/AddonHandlers/Quest/QuestAddonModeHelpers.cs`
- any talk or toast handler path that currently checks only
  `RemoveDiacriticsWhenUsingReplacementTalkBTalk`
- DB-first game-window normalization through
  `TryCreateNativeReplacementTextNormalizer`

This ensures old config state or non-UI code paths cannot enable the
normalizer for a language that is not explicitly curated.

### 4. Preserve overlay and texture-backed exclusion

No overlay path should consult this policy.

The spec is explicit here:

- plugin overlays do not need fallback because Echoglossian controls the font
- plugin tooltip or texture-backed presentation does not need fallback because
  Echoglossian controls the font
- only native FFXIV replacement paths may consume the policy

### 5. Preserve the optional-user contract

Even when a language is eligible, the fallback must remain optional because
some users replace the game's native font externally.

Eligibility means "can expose the fallback safely", not "must always strip
diacritics".

## Initial Curated Coverage

The refactor should preserve the currently exposed language set on day one:

- `hr`
- `cs`
- `hu`
- `lv`
- `lt`
- `pl`
- `ro`
- `hbs`
- `sk`
- `tr`
- `tk`
- `uz`
- `vi`

Follow-up validation candidates worth auditing before any expansion:

- `bs`
- `az`
- `eo`
- `mt`
- `cy`
- `ig`
- `yo`

No candidate should be added only because it has diacritics. It must be
validated against real native replacement needs and current game-font
limitations.

## Risks

1. A UI-only refactor would leave runtime paths under-hardened.
2. A code-only refactor without explicit metadata would reintroduce hidden
   language lists elsewhere.
3. Expanding coverage without validation could degrade users who replaced the
   native game font externally.
4. Treating overlays and native replacement the same would regress the RTL and
   texture-backed work from issue `#139`.

## Exit Criteria

This future refactor is complete when:

- no numeric language-id gate remains for this feature
- language eligibility lives in `LanguageInfo` metadata
- UI visibility and runtime application both consume one shared policy
- non-eligible languages cannot apply the fallback even if the raw config
  toggle is true
- overlay and texture-backed paths remain unaffected
- the current curated language set behaves exactly as before
