# Issue 258 UI-A Translation Toggle Evidence

Date: 2026-10-17

Branch: `issue-258-04-translation-toggle`

Base: `52f1838`

## Delivered

- One `TranslationActivationCoordinator` for the config checkbox, `/eglo translations on|off|toggle`, and the optional HUD button.
- Framework-thread enable and disable transitions with persistence ordering, enable validation, re-entry blocking, and failed-enable rollback.
- Disable cleanup for translation admissions, prefetch work, mutation-aware native handlers, overlays, hover presentation, toasts, nameplates, and addon registrations while preserving committed database and cache data.
- Enable reactivation through existing shared brokers, toast runtimes, nameplate runtime, addon handlers, and cached presentation paths.
- Opt-in draggable HUD with viewport clamping, persisted position, drag-versus-click suppression, and enabled, disabled, and transitioning visual states.
- Localized setting text, tooltips, command usage, and command feedback through `Resources`.
- No DTR integration.

## Proposed Asset Decision

Inspected `C:\Users\lokin\Downloads\translation-icon.svg`.

- SHA256: `C0E92DA9D48CD4058E0441DE33E53D68764BDA3EF8E7A534824292BE79976C3B`
- The file contains a `translation` title and one path, but no author, license, copyright, attribution, provenance, or redistribution terms.
- The SVG was not copied into the repository.
- The HUD uses Dalamud's existing `FontAwesomeIcon.Language` glyph instead.

## Automated Validation

- `dotnet build Echoglossian.sln -c Debug --no-restore`: PASS, 0 errors.
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`: PASS, 1,678 of 1,678.
- `dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore`: PASS, 0 errors. The existing DalaMock `NU1510` warning remains.
- `dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1`: PASS, 27 of 27.
- `git diff --check`: PASS.
- A second review of the complete `52f1838..HEAD` diff found no unresolved correctness issue.

## DalaMock Coverage and Limit

The hosted test drives the production `/eglo translations off` command, the Framework tick, persisted global state, and addon lifecycle unregistration. It also verifies startup handler registration only when global translation is enabled.

DalaMock cannot resolve `FFXIVClientStructs` `AtkStage.Instance`, so it cannot prove native Journal node restoration. The production cleanup now logs a per-handler restoration failure and continues the remaining disable contract. Unit coverage verifies that one restoration failure does not prevent later mutation-aware handlers from restoring. Actual native node and `AtkValue` restoration still requires in-game verification.

## Required In-Game Verification

1. Opt in to the HUD, drag it, reload the plugin, and confirm position persistence and viewport clamping after a resolution or display change.
2. Confirm enabled cyan/violet glow, disabled desaturated low-opacity presentation, and amber transition presentation.
3. Confirm a drag never toggles translation and rapid repeated clicks cannot re-enter a transition.
4. Confirm the config checkbox, HUD, and `/eglo translations on|off|toggle` stay synchronized.
5. Disable while native, overlay-only, and swap surfaces are visible. Confirm only Echoglossian-mutated native state is restored and overlays or hover text clear.
6. Disable during active dialogue, toast, nameplate, and prefetch work. Confirm no translated UI is applied after disable.
7. Re-enable with visible configured surfaces and confirm committed cached translations reappear without unnecessary translation work.
8. Confirm blocked enable feedback for missing assets or incomplete engine configuration.

UI-A is not release-eligible by itself. Stop after user testing and do not merge or publish without a separate explicit release request.
