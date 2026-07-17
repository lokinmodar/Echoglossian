# Final Review Fix Wave Report

## Status

DONE

- Branch: `feature/dalamock-unified-previewer`
- Starting HEAD: `1e8b96c`
- Implementation commit: `e7c0444 fix: finalize deterministic previewer captures`
- Pre-existing `.superpowers/sdd/task-5-report.md` edits were not modified, staged, or committed.

## Implemented Fixes

### 1. Deterministic plugin-window layout and stabilization

- Added fixed logical layouts for Config (`1000x900`), DB Manager (`1200x800`), and Translator Metrics (`1100x480`).
- Enforced geometry both before draw and by exact ImGui window name after draw so hosted windows cannot retain first-use or saved geometry.
- Added a capture stability tracker that requires three consecutive frames with identical, non-empty integer bounds.
- Batch export waits for both overlay rendering and stable target bounds. Interactive capture remains pending until the same readiness condition is met.
- Interactive stabilization has an explicit frame limit; batch stabilization also retains the existing five-second time bound.

### 2. Explicit target-specific capture failure

- Added a non-empty crop guard for every plugin-window target.
- `null` or empty plugin-window bounds now throw an explicit target-specific failure instead of falling through to full-frame capture.
- Interactive failures are shown as screenshot failure status and do not claim a path was written.
- Batch/export failures occur before PNG or manifest output for the failed target.

### 3. CLI plugin-window export and manifest serialization

- Added `--capture-target <config-window|db-manager-window|translator-metrics-window>`.
- Kept semantics narrow: plugin-window targets require `--screenshot full`; existing `surface` and `batch` behavior is unchanged.
- Export requests carry the typed `PreviewCaptureTarget` through the existing `BatchScreenshotRunner` pipeline.
- Manifest entries now serialize the enum through `JsonStringEnumConverter`; focused tests parse actual JSON and assert `CaptureTarget` is a plugin-window string.
- Updated previewer operator documentation and examples.

### 4. Fail-soft optional database cloning

- SQLite, I/O, access, and invalid-operation clone failures now add a diagnostic and continue with `ClonedDatabasePath = null`.
- Partial database clone candidates are deleted.
- Any failure after workspace creation but before returning artifacts triggers best-effort recursive workspace cleanup before rethrowing.
- Config and overlay preview remain available when an optional database is corrupt or inaccessible.

### 5. Explicit unavailable preview imagery

- Removed preview use of `-1` texture handles.
- Added `PluginConfigWindowContext.ImagesAvailable`, defaulting to `true` for the live plugin path.
- Preview-created contexts use zero handles and set `ImagesAvailable = false`.
- About omits the image and shows an unavailable message; QR buttons are disabled and their popups cannot render invalid textures.
- Added the neutral `PreviewImageryUnavailableText` resource because no existing resource accurately described this state. No broad asset loader or placeholder texture infrastructure was introduced.

## Focused Test Evidence

The new tests were observed failing before their production contracts existed, then passing after implementation.

- CLI typed target parsing and invalid mode combinations.
- Actual manifest JSON serialization of `DbManagerWindow`.
- Explicit missing-window-crop rejection.
- Three-frame bounds stabilization, geometry-change reset, and missing-bounds failure limit.
- Corrupt SQLite fail-soft loading with no partial clone.
- Preview context zero texture handles with explicit imagery-unavailable state.

Focused/full previewer result after implementation: 81 passed, 0 failed, 0 skipped.

## Required Validation

All commands were run from the requested worktree after the final code changes.

1. `dotnet build Echoglossian.sln -c Debug --no-restore`
   - Passed: 0 errors.
2. `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
   - Passed: 627 passed, 0 failed, 0 skipped.
3. `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug`
   - Passed: 81 passed, 0 failed, 0 skipped.
4. `dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --binding-smoke`
   - Passed: `Dalamud ImGui binding OK: 1.88`.
5. `dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke`
   - Passed with exit code 0.
6. `dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot full --capture-target config-window --scenario talk --viewport 1920x1080 --output artifacts\previewer\screenshots\final-validation-config-window`
   - Passed with one PNG and `manifest.json`.
   - Actual manifest value: `CaptureTarget=ConfigWindow`.
   - Actual PNG dimensions: `1000x900`, proving target crop rather than `1920x1080` full-frame output.

Additional runtime evidence:

- DB Manager export produced `1200x800` after post-draw named-window geometry enforcement.
- Translator Metrics export produced `1100x480`.
- DB Manager export with an unavailable snapshot exited 1 with an explicit error and left zero output files.

## Warnings and Risk

- Validation retains the branch's existing warning that the Multilingual App Toolkit is unavailable in this build environment.
- Restore/build retains the existing `NU1903` warning for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
- No task-specific test or build failures remain.
- Optional in-game regression check: open the live config About tab and QR popups to confirm imagery remains unchanged. The live context defaults `ImagesAvailable` to `true`, so runtime behavior is preserved by construction.

## Scope Review

- Changes are limited to the approved previewer, screenshot, session, plugin config UI, focused test, README, and generated `Echoglossian.xml` paths.
- `Properties/Resources.resx` and `Properties/Resources.Designer.cs` contain only the one necessary neutral unavailable-imagery string permitted by the localization exception.
- No broader plugin/runtime, persistence schema, plan, or unrelated documentation changes were made.

---

# Final Branch Review Fix Wave Round 2

## Status

DONE

- Branch: `feature/dalamock-unified-previewer`
- Starting HEAD: `e7c0444`
- Implementation commit: `7d65139 fix: complete previewer review fixes round 2`
- The pre-existing `.superpowers/sdd/task-5-report.md` edit was not modified, staged, or committed.

## Implemented Fixes

### 1. Interactive capture reset

- Added explicit `EndCapture`/tracker reset behavior and invoke it after successful plugin-window request consumption and stabilization failure.
- The active target now lives only in `PreviewCaptureStabilityTracker`, so clearing it immediately releases deterministic `SetWindowPos`/`SetWindowSize` enforcement.
- Completed stable bounds remain available for the downstream framebuffer crop calculation after layout is released; beginning or replacing a request clears that completed snapshot.

### 2. Startup language normalization

- Added one `NormalizePreviewLanguage` path used by both interactive and screenshot-export startup.
- Unknown IDs are replaced in `editableConfiguration.Lang` with English key `28`, or the deterministic lowest-key fallback when English is unavailable, before real plugin UI/rendering code receives the config.
- Existing valid IDs remain unchanged.

### 3. Restart-required runtime indication

- The interactive shell records the language ID and font size applied to startup runtime composition.
- The fidelity area displays `Restart the previewer to apply config language or font size changes.` whenever mutable config language or font size diverges from those applied values.
- No live font atlas or renderer composition rebuilding was introduced.

### 4. Export save isolation

- Centralized preview config save-scope creation in `PushPreviewConfigSaveScope`.
- Both interactive execution and screenshot export now install the same `PluginConfigSaveScope` that redirects renderer-side saves to the session config clone.
- Focused coverage verifies clone modification while the source config remains untouched.

### 5. Session diagnostics in the shell

- `PreviewShell` now receives `session.Diagnostics`, which includes configuration-loader and DB snapshot diagnostics.
- The fidelity/diagnostic area renders this session-level list instead of only `sourceConfiguration.Diagnostics`.

### 6. Clone-name collision prevention

- Session config and database clones now use fixed distinct names: `Echoglossian.preview.json` and `Echoglossian.preview.db`.
- A regression test loads config and SQLite sources with the same source basename and verifies both clones remain distinct and usable.

## TDD Evidence

The focused tests were observed failing before implementation with missing `End`/`Target`, `NormalizePreviewLanguage`, `GetRuntimeRestartWarning`, and `PushPreviewConfigSaveScope` contracts. After the first green cycle, review identified that resetting active capture also removed the crop needed by the downstream capture callback. The refined completed-bounds test was observed failing (`Expected: True`, `Actual: False`) before the tracker retained completed bounds separately from active layout state.

Final focused result: 37 passed, 0 failed, 0 skipped.

## Required Validation

All commands were run from the requested worktree after the production changes.

1. `dotnet build Echoglossian.sln -c Debug --no-restore`
   - Passed: 0 errors, 2 existing environment/package warnings.
2. `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`
   - Passed: 627 passed, 0 failed, 0 skipped.
3. `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug`
   - Passed: 89 passed, 0 failed, 0 skipped.
4. `dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --binding-smoke`
   - Passed: `Dalamud ImGui binding OK: 1.88`.
5. `dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke`
   - Passed with exit code 0.
6. Config-window export with `--config <temp-config-with-Lang-12345> --screenshot full --capture-target config-window --scenario talk --viewport 1920x1080`.
   - Passed with one PNG and `manifest.json`.
   - PNG dimensions: `1000x900`, confirming the real Config-window crop.
   - Manifest entry: `CaptureTarget=ConfigWindow`.
   - Source config remained exactly `{"Lang":12345,"FontSize":24}`, confirming source isolation.

## Warnings and Risk

- Validation retains the existing Multilingual App Toolkit unavailable warning.
- Validation retains the existing `NU1903` warning for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
- The restart-required message is intentionally preview-shell text rather than plugin UI text and does not alter live plugin resources or runtime composition.
- Interactive button-driven reset behavior is covered at the capture-state contract level and by successful target export; the final user interaction remains appropriate for a manual preview-shell smoke if desired.

## Scope Review

- Production changes are limited to the four approved previewer files: `Program.cs`, `PreviewSessionLoader.cs`, `PreviewPluginWindowHost.cs`, and `PreviewShell.cs`.
- Tests are limited to approved `Fonts`, `Session`, and `UI` previewer test folders.
- `Echoglossian.Previewer/README.md` and `Echoglossian.xml` did not require changes.
- No plugin UI/resource, persistence schema, or unrelated branch changes were included.

---

# Final Branch Review Fix Wave Round 3

## Status

DONE

- Branch: `feature/dalamock-unified-previewer`
- Starting HEAD: `7d65139e4b6fc4023a473d6bfae904fdc5c847a3`
- The pre-existing `.superpowers/sdd/task-5-report.md` edit was not modified, staged, or committed.

## What Changed

- Added shared preview language initialization for both interactive and export startup. It applies `LanguageEngineSupport.ApplySupportTo(...)`, normalizes `Lang`, and applies `LanguagePresentationPolicy.ApplyLanguageFlags(...)` before real preview UI construction.
- Changed batch plugin-window readiness to depend only on stable target bounds. Overlay and surface targets continue to require a successful overlay draw.
- Hardened interactive capture failure handling for invalid paths, I/O, access, GDI+ image-save, and Veldrid readback failures. Expected failures report through `SetLastScreenshotFailure(...)` and best-effort delete a partial PNG.
- Made preview session temporary-directory disposal ignore expected I/O and access-denied cleanup failures.
- Replaced interpolated SQLite clone connection strings with `SqliteConnectionStringBuilder` instances so semicolons in source or destination paths are preserved safely.

## Tests Run

1. `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PreviewFontCatalogTests|FullyQualifiedName~BatchScreenshotRunnerTests|FullyQualifiedName~PreviewSessionLoaderTests|FullyQualifiedName~InteractiveScreenshotCaptureTests"`
   - Passed: 41 passed, 0 failed, 0 skipped.
2. `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore`
   - Passed: 98 passed, 0 failed, 0 skipped.

## Files Changed

- `Echoglossian.Previewer/Program.cs`
- `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- `Echoglossian.Previewer/Session/PreviewSessionLoader.cs`
- `Echoglossian.Previewer/Session/PreviewSessionArtifacts.cs`
- `Echoglossian.Previewer.Tests/Fonts/PreviewFontCatalogTests.cs`
- `Echoglossian.Previewer.Tests/Screenshots/BatchScreenshotRunnerTests.cs`
- `Echoglossian.Previewer.Tests/Screenshots/InteractiveScreenshotCaptureTests.cs`
- `Echoglossian.Previewer.Tests/Session/PreviewSessionLoaderTests.cs`
- `.superpowers/sdd/final-review-fix-report.md`

## Concerns

- The validation output retains existing `NU1903` warnings for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and the Multilingual App Toolkit unavailable warning. No task-specific failures or warnings remain.

---

# Final Branch Review Fix Wave Round 4

## What Changed

- Batch plugin-window crops now use the actual `CaptureFramePng(...)` offscreen texture dimensions, not swapchain framebuffer dimensions. Crop readback rejects any empty or out-of-bounds requested rectangle instead of silently intersecting it.
- Expected batch capture/output failures now use the same cleanup and contextual message helper as interactive capture. The failing target and PNG path are included, and a partial PNG is deleted best-effort before batch export fails.
- Preview DB context creation now uses `SqliteConnectionStringBuilder`, preserving semicolons in snapshot paths.
- Config, DB Manager, and Translator Metrics batch targets no longer draw an overlay capture window behind the requested plugin window.

## Tests Run

1. `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ScreenshotCropTests|FullyQualifiedName~BatchScreenshotRunnerTests|FullyQualifiedName~InteractiveScreenshotCaptureTests|FullyQualifiedName~PreviewWorkbenchStateTests"`
   - Passed: 43 passed, 0 failed, 0 skipped.
2. `dotnet build Echoglossian.sln -c Debug --no-restore`
   - Passed: 0 errors; existing Multilingual App Toolkit and `NU1903` warnings remain.
3. `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-build --no-restore`
   - Passed: 104 passed, 0 failed, 0 skipped.

## Files Changed

- `Echoglossian.Previewer/Hosting/PreviewHost.cs`
- `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- `Echoglossian.Previewer/Screenshots/VeldridScreenshotCapture.cs`
- `Echoglossian.Previewer/Program.cs`
- `Echoglossian.Previewer.Tests/Screenshots/ScreenshotCropTests.cs`
- `Echoglossian.Previewer.Tests/Screenshots/BatchScreenshotRunnerTests.cs`
- `Echoglossian.Previewer.Tests/Screenshots/InteractiveScreenshotCaptureTests.cs`
- `Echoglossian.Previewer.Tests/UI/PreviewWorkbenchStateTests.cs`
- `.superpowers/sdd/final-review-fix-report.md`

## Concerns

- Existing environment/package warnings remain: the Multilingual App Toolkit is unavailable and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 reports `NU1903`. No task-specific validation failures remain.
- The pre-existing `.superpowers/sdd/task-5-report.md` edit was not modified or included in this work.
