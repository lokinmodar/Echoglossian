# Localization Incident Post-Mortem: Crowdin Data Integrity and Runtime Culture

- **Incident window:** August 1-8, 2026
- **Recovery date:** August 7, 2026
- **Runtime fix:** [PR #260](https://github.com/lokinmodar/Echoglossian/pull/260)
- **Status:** Translation data recovered; runtime fix and permanent guardrails in progress
- **Audience:** Echoglossian maintainers and localization administrators

## Executive Summary

During the migration from neutral-language resource names to locale-specific
`.resx` names, Echoglossian experienced two related but technically separate
failures:

1. Crowdin exported untranslated entries as English source text. Those
   source-filled localized files were later imported back into Crowdin and
   approved, displacing correct historical translations as the active
   variants.
2. The plugin normalized its configured locale but did not assign that culture
   to `Echoglossian.Properties.Resources.Culture`. Most UI lookups therefore
   depended on Dalamud's thread UI culture and could silently fall back to the
   English root resource even when the correct locale satellite assembly was
   packaged.

No historical translation data was permanently deleted. Crowdin retained the
previous translations as alternate variants and in translation memory. On
August 7, maintainers corrected the export policy, restored the historical
variants, removed only the bad active approvals, and verified representative
translations. PR #260 makes resource lookup use the plugin's configured locale
explicitly and adds regression coverage.

The filename pattern `Resources.%locale%.resx` was not the cause and remains
required. Region-specific locales such as `pt-BR` and `pt-PT` must remain
distinct. The incident resulted from unsafe synchronization semantics, an
unreviewed source-filled export/import cycle, and a missing runtime culture
assignment.

## Impact

- Existing translations appeared to disappear from Crowdin because English
  source-equivalent variants became the approved variants.
- Generated localized `.resx` files expanded from partial translation files to
  near-complete copies of the English source file.
- Plugin UI could render in English on user systems even when a translated
  satellite assembly existed and the user selected a supported plugin locale.
- The generated localization PR became unsafe to merge as a recovery vehicle.
- Maintainer time was required to reconstruct the event, restore approvals,
  audit translations, and verify build artifacts.

The incident affected plugin-owned UI resources. It did not change translator
engine output, database contents, or FFXIV native UI translation data.

## Detection

The incident was detected through user-visible loss of translations and an
unexpected generated localization diff. The strongest forensic signals were:

- A localized file suddenly contained every source key instead of only its
  translated subset.
- Most localized values became byte-for-byte equal to the English source.
- Correct Crowdin translations still existed, but as unapproved historical
  variants.
- Packaged locale satellite assemblies existed while runtime UI lookup still
  returned English.

These signals should have been automated invariants rather than manual
forensics.

## Timeline

| Date | Event | Evidence |
| --- | --- | --- |
| 2026-08-01 | Runtime resource files moved toward locale-specific names. | Commit `a7ba2c6` (`chore(i18n): use locale-specific resx`). |
| 2026-08-03 20:19 -03:00 | Crowdin mapping was changed to export `Resources.%locale%.resx`. | Commit `bec0043` (`chore: update crowdin.yml`). |
| 2026-08-03 20:24 -03:00 | The first bad pt-BR export expanded the localized file and filled missing translations with English. | Commit `f93c46e` (`New translations resources.resx (Portuguese, Brazilian)`). |
| 2026-08-05 | A later generated update retained the corrupted shape. | Commit `f597ab0`. |
| 2026-08-06 | Source-equivalent values from repository files were imported into Crowdin and approved. | Crowdin translation history: for example, correct pt-BR `ConfigWindowTitle` variant ID `16221` from August 3 remained, while English variant ID `29669` was created and approved on August 6. |
| 2026-08-07 | [PR #253](https://github.com/lokinmodar/Echoglossian/pull/253) was opened from `l10n_v4-series`. Its final diff contained only indentation changes and did not constitute translation recovery. | GitHub PR metadata and patch review. |
| 2026-08-07 | Crowdin export/import settings were corrected, historical translations were restored, and bad active approvals were removed without deleting translation history. | Crowdin project settings, translation history, and post-recovery approval audit. |
| 2026-08-08 | Runtime investigation confirmed that configured culture normalization did not set `Resources.Culture`; `ca` and `nl` also lacked neutral-to-locale mappings. | Startup path and resource lookup review. |
| 2026-08-08 | Runtime correction, mappings, and regression tests were committed and submitted in [PR #260](https://github.com/lokinmodar/Echoglossian/pull/260). | Commit `98c887f` (`fix(i18n): apply plugin resource culture`). |

## Technical Root Cause A: Crowdin Data Integrity

The intended repository mapping was valid:

```yaml
preserve_hierarchy: true

files:
  - source: /Properties/Resources.resx
    translation: /Properties/Resources.%locale%.resx
    skip_untranslated_strings: true
```

However, the effective Crowdin project export behavior initially did not skip
untranslated strings. A localized file therefore received English source text
for every untranslated key. For pt-BR:

- The known-good file at `bec0043` had 188 entries, 183 of which were genuine
  Portuguese values. The 356 absent keys were expected to fall back to the root
  English resource at runtime.
- The generated file at `f93c46e` had 544 entries, and all 544 values matched
  the English source.
- A later version had 547 entries with only 21 values different from the
  English source.

The source-filled repository files were subsequently treated as translation
input. Continuous or manual repository-to-Crowdin translation import created
new English variants and made them active approvals. Correct translations did
not vanish from storage; they were no longer the selected variants.

### Five Whys

1. **Why did translations appear to disappear?** English source-equivalent
   variants became the approved translations.
2. **Why were those variants present?** Complete source-filled localized files
   were imported from the repository into Crowdin.
3. **Why were localized files source-filled?** The effective Crowdin export did
   not omit untranslated strings during the initial synchronization.
4. **Why did the bad files become authoritative?** Repository-to-Crowdin
   translation import remained available after Crowdin had become the
   translation source of truth.
5. **Why was the cycle not stopped before approval?** There was no automated
   diff gate for mass source equality, abnormal file expansion, or approval
   count regression.

## Technical Root Cause B: Runtime Resource Loading

The plugin stored a `DefaultPluginCulture` and normalized neutral codes to the
locale-specific names used by its resource files. Before the fix, normalization
did not apply the resulting `CultureInfo` to the generated resource class:

```csharp
Echoglossian.Properties.Resources.Culture
```

As a result, calls such as `Resources.ConfigWindowTitle` used the current
thread's UI culture. Dalamud's thread culture is not guaranteed to equal the
locale chosen in the plugin configuration. `ResourceManager` could therefore
fall through to `Resources.resx` and display English.

The locale migration also omitted explicit normalization for Catalan and Dutch:
`ca -> ca-ES` and `nl -> nl-NL`.

PR #260 centralizes the behavior so startup and configuration changes:

1. normalize the persisted locale;
2. construct the matching `CultureInfo`;
3. assign it to `Resources.Culture`; and
4. use that exact culture for subsequent strongly typed resource lookups.

The regression test verifies that `fr` becomes `fr-FR`, that
`Resources.Culture` is set, and that the localized resource set is loaded
without relying on parent fallback.

### Five Whys

1. **Why did users still see English with locale files installed?** Strongly
   typed resource lookups used the thread UI culture instead of the plugin
   culture.
2. **Why did they use the thread culture?** `Resources.Culture` remained null.
3. **Why was it null?** Startup normalized the configuration value but never
   applied it to the generated resource class.
4. **Why did filename migration expose the gap?** Exact locale filenames made
   the distinction between configured locale, thread locale, and fallback
   chain observable.
5. **Why was this not detected before release?** Build checks verified resource
   compilation and packaging, but no test verified a real strongly typed lookup
   under a configured plugin culture.

## Contributing Control Gaps

- Crowdin UI settings and `crowdin.yml` behavior were assumed to be equivalent
  without verifying the effective exported artifact.
- The same repository path could act as both Crowdin output and later Crowdin
  input, creating a feedback loop.
- A generated localization PR was not protected by content-based invariants.
- Partial localized `.resx` files were mistaken for incomplete artifacts,
  although omission is the correct way to preserve English runtime fallback.
- Locale naming, build packaging, configuration normalization, and runtime
  lookup were validated independently instead of end to end.
- No maintained runbook explained when the one-time import must stop.

This was a process and systems failure. No single maintainer action was
sufficient to cause the incident by itself.

## Recovery Performed

On August 7, 2026, maintainers:

1. Set the Crowdin project to omit untranslated strings and export only
   translated, approved values.
2. Disabled ongoing repository-to-Crowdin translation import. The one-time
   import remains a bootstrap operation only.
3. Reapproved known-good historical variants from August 3 across the ten
   languages that contained translations.
4. Imported known-good localized resources from commit `bec0043` where needed
   to reconnect translations to recreated source string IDs.
5. Removed only currently approved incident-window and source-equivalent
   English variants. Historical variants and translation memory were retained.
6. Normalized XML entities and audited all active approvals.
7. Verified that no currently approved translation was created during the bad
   import window and that no approved translation equaled its English source.
8. Verified representative values, including:

   - pt-BR `ConfigWindowTitle`: `Configuração do Echoglossian`
   - pt-BR `ConfigTab1Name`: `Configurações de diálogos`
   - fr `ConfigTab1Name`: `Paramètres de conversation`

### Post-Recovery Approved Phrase Counts

The source catalog contained 540 phrases at audit time.

| Locale | Approved phrases |
| --- | ---: |
| ca | 0 |
| da | 190 |
| de | 195 |
| el | 194 |
| en | 0 |
| es-ES | 194 |
| eu | 196 |
| fr | 193 |
| it | 191 |
| nl | 0 |
| pt-BR | 215 |
| pt-PT | 202 |
| ru | 198 |

Zero is valid for a configured target with no translations; it must not be
materialized as a localized file full of English values.

## Corrective and Preventive Actions

| ID | Priority | Status | Owner | Action and acceptance evidence |
| --- | --- | --- | --- | --- |
| C1 | P0 | Complete | Crowdin administrator | Enable `skipUntranslatedStrings`, `exportTranslatedOnly`, and `exportApprovedOnly`. Confirmed in Crowdin project settings on 2026-08-07. |
| C2 | P0 | Complete | Crowdin administrator | Restore historical approvals and remove only bad active variants. Acceptance: the post-recovery audit found zero approved incident-window variants and zero approved source-equivalent translations. |
| C3 | P0 | Complete | Repository administrator | Keep **Always import new translations from the repository** disabled; use one-time import only for bootstrap. |
| C4 | P0 | In PR #260 | Plugin maintainers | Apply the configured `CultureInfo` to `Resources.Culture`; add `ca` and `nl` mappings and exact-resource-set regression coverage. |
| C5 | P0 | Complete | Plugin maintainers | Verify packaging with a Plogon-like Release build. Acceptance: 13 of 13 expected locale satellite assemblies were present and matched. |
| P1 | P0 | Open | Plugin maintainers | Add a repeatable CI or repository script that rejects mass source-equivalent localized values, identical locale files, and abnormal localized-file expansion. It must run before the next Crowdin-generated PR is merged. |
| P2 | P0 | Open | Crowdin administrator | Audit effective Crowdin settings after every integration reconnection or configuration change. Save the expected flags and an API response or screenshot in the PR evidence. |
| P3 | P0 | Open | Repository administrator | Close or replace stale PR #253. Accept a replacement only when its translation diff passes all review gates below. |
| P4 | P1 | Open | Test infrastructure maintainer | Update vendored DalaMock to the current Dalamud API. Acceptance: `Echoglossian.Mock.Tests` builds and passes after addressing `IFramework.CreateDebouncer(TimeSpan, Action)`, `IGameObject.CurrentDistance`, and `IGameObject.NextDistance`. |
| P5 | P1 | Open | Crowdin administrator | Decide whether the `en` target locale should be removed because English is already the source language. Document the decision. |
| P6 | P1 | Open | Plugin maintainers | Whenever a Crowdin target locale is added, add or verify configuration normalization and a localized lookup test before enabling export. |
| P7 | P2 | Open | Plugin maintainers | Decide the long-term role of `MultilingualResources/*.xlf`. Keep XLIFF optional unless an explicit migration establishes it as the single source of truth. |

## Permanent Safety Invariants

These invariants apply to every future localization change:

1. `Properties/Resources.resx` is the only English source file.
2. Crowdin exports localized files to
   `Properties/Resources.%locale%.resx`; `%locale%` must not be replaced with a
   two-letter placeholder.
3. Localized files are intentionally partial. An absent key falls back to the
   English root resource at runtime.
4. After bootstrap, Crowdin is the source of truth for translated values.
   Repository translations must not be continuously or manually imported back
   into Crowdin.
5. **Always import new translations from the repository** remains off.
6. Do not use **Sync Translations to Crowdin** during routine synchronization.
   Use the normal source synchronization action only.
7. Every supported runtime locale must have an explicit normalization path and
   a matching satellite assembly.
8. Plugin startup must set `Resources.Culture` before the first localized UI or
   notification lookup.
9. A generated localization PR is untrusted until its content diff and build
   artifacts pass the checks below.

## Safe Localization Runbook

### A. Change English Source Strings

1. Add, edit, or remove source keys only in `Properties/Resources.resx`.
2. Use `Resources.Key` directly for plugin UI and notifications.
3. Do not copy English values into localized files to make them look complete.
4. Commit the source change before asking Crowdin to synchronize it.

### B. Synchronize Crowdin

1. Confirm the integration targets the intended repository branch.
2. For a new integration only, enable **One-time translation import after the
   branch is connected** if existing repository translations must seed Crowdin.
3. Keep **Always import new translations from the repository** off.
4. Confirm effective settings:

   - `skipUntranslatedStrings = true`
   - `exportTranslatedOnly = true`
   - `exportApprovedOnly = true`
5. Run normal source synchronization. Do not run **Sync Translations to
   Crowdin** after bootstrap.
6. Wait for translators and approval; then request/export translations to the
   generated branch.

The GitHub integration field **Branches to Sync Automatically** applies to
future branch discovery. A pattern such as `*translate*` does not retroactively
change the existing `v4-series` branch configuration.

### C. Review a Crowdin-Generated PR

Reject or stop the PR if any of these signals appear:

- a localized file gains nearly every English key without a corresponding jump
  in approved translations;
- a large percentage of localized values equals the English source;
- two or more locale files become identical;
- a locale file changes from a partial resource into a complete source mirror;
- an unexpected locale or two-letter filename appears;
- approved phrase counts drop unexpectedly;
- the diff is only formatting after a supposed translation recovery.

Before merge:

1. Compare changed keys and values, not only file counts.
2. Spot-check at least one established translation per changed locale.
3. Compare Crowdin approved counts with emitted localized entry counts.
4. Confirm `Resources.resx` was not modified by a translation export.
5. Confirm the PR contains actual recovered or new localized values.

PR #253 must not be merged as the recovery: its reviewed diff does not contain
the restored translations.

### D. Validate Build and Runtime

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
.\scripts\test-plogon-like-build.ps1
```

Then verify:

1. Every expected locale has a matching
   `<locale>/Echoglossian.resources.dll` satellite assembly.
2. Selecting a plugin locale assigns the exact normalized culture to
   `Resources.Culture`.
3. A known translated key returns its localized value while an absent key
   falls back to English.
4. At least one user-visible UI label or notification is checked in-game when
   runtime localization behavior changes.

The 2026-08-08 validation passed the main build and 1,107 unit tests. A local
Plogon-like Release build produced all 13 expected satellite assemblies. Hosted
Mock validation remained blocked by the pre-existing DalaMock API mismatch
listed in action P4; startup-only Mock tests would not replace the localized
lookup regression in any case.

## Rollback Procedure

If translation corruption is suspected:

1. Stop translation exports and disable repository-to-Crowdin translation
   import. Do not delete files, variants, or translation memory.
2. Preserve evidence: generated branch/PR, affected commit hashes, Crowdin
   settings, string history, approval counts, and timestamps.
3. Identify the last known-good repository commit before source-filled files.
4. Compare localized entry counts and source-equality rates before and after
   that commit.
5. In Crowdin, restore correct historical variants as the active approvals.
   Prefer reapproval over deletion.
6. Remove approval only from variants proven to be part of the incident window
   or equal to the English source. Preserve alternates and TM.
7. Reimport known-good localized resources only when needed to reconnect
   translations to current source string IDs, and inspect the import preview.
8. Audit every active approved variant after recovery.
9. Generate a fresh export branch. Do not reuse a branch containing the bad
   artifacts.
10. Apply the review and build/runtime gates above before merge.

If runtime lookup is wrong but Crowdin data is intact, do not rename or rewrite
localized files first. Verify, in order:

1. configured locale normalization;
2. `Resources.Culture` assignment;
3. satellite assembly packaging;
4. exact resource-set lookup; and
5. English fallback for an intentionally absent key.

## Lessons

- Partial localized resources are correct and safer than English-padded files.
- A successful build proves compilation and packaging, not that runtime culture
  selection is correct.
- Translation platforms preserve useful history, so recovery should prefer
  reapproval and audits over destructive cleanup.
- A localization integration is bidirectional data movement unless explicitly
  constrained; every direction needs an owner and a stopping rule.
- Locale-specific naming solves ambiguity but must be implemented across
  Crowdin mapping, configuration normalization, resource lookup, packaging, and
  tests as one end-to-end contract.

## Related References

- [Localization build flow](localization-build-flow.md)
- [Crowdin project](https://crowdin.com/project/echoglossian)
- [Stale generated localization PR #253](https://github.com/lokinmodar/Echoglossian/pull/253)
- [Runtime culture fix PR #260](https://github.com/lokinmodar/Echoglossian/pull/260)
