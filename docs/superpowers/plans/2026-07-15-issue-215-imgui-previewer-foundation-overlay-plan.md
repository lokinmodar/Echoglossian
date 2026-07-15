# ImGui Previewer Foundation and Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Phase A of issue #215: a development-only Windows ImGui host that renders every currently registered Echoglossian translation overlay with the user's configuration, supports both text presentation paths, and exports reproducible PNG screenshots without affecting plugin build, packaging, or deploy.

**Architecture:** Keep the real draw-oriented overlay renderer in the `Echoglossian` assembly and call it from both Dalamud and a standalone Veldrid host. Isolate preview-only packages, native binaries, scenario state, and screenshot code in projects that are outside `Echoglossian.sln`. Introduce only two shared seams in this phase: an ImGui font runtime and a layout-aware RTL texture factory.

**Tech Stack:** .NET 10 Windows, `Dalamud.Bindings.ImGui`, `cimgui.dll`, Veldrid 4.9.0, Veldrid.SDL2 4.9.0, Veldrid.StartupUtilities 4.9.0, Veldrid.SPIRV 1.0.15, xUnit, Newtonsoft.Json, System.Drawing.Common.

## Global Constraints

- Work only on branch `issue-215-imgui-previewer`, based on `origin/v4-series` commit `af6e1f70fae0a0c747e1956860237f327b940cfa` or a reviewed newer base.
- Do not add `Echoglossian.Previewer` or `Echoglossian.Previewer.Tests` to `Echoglossian.sln`.
- Set `IsPackable=false` in both preview projects and keep all Veldrid references out of `Echoglossian.csproj`.
- Explicitly remove `Echoglossian.Previewer\**` and `Echoglossian.Previewer.Tests\**` from the main project's `Compile`, `EmbeddedResource`, and `None` items.
- Never save the source `Echoglossian.json`, write API keys to logs, or open the user's live plugin database.
- Reuse the existing `Config`, `TranslationOverlay`, `TranslationWindowConfig`, `TextImageRenderer`, `LanguagePresentationPolicy`, and RTL presentation cache.
- Do not simulate or draw native FFXIV UI. Simulated addon bounds are guides and renderer inputs only.
- Use the existing file header, XML documentation, braces, and `this.` conventions in every C# file.
- Commit `Echoglossian.xml` if shared production code changes regenerate it during validated builds.
- Keep issue #215 open after Phase A; configuration UI, translator metrics/debugger, and DB editor are Phases B and C.

---

### Task 1: Lock Build Isolation And Prove The Dalamud Binding

**Files:**

- Modify: `Echoglossian.csproj`
- Create: `Echoglossian.Previewer/Echoglossian.Previewer.csproj`
- Create: `Echoglossian.Previewer/Program.cs`
- Create: `Echoglossian.Previewer/Hosting/PreviewCommandLine.cs`
- Create: `Echoglossian.Tests/PreviewerIsolationTests.cs`

- [ ] Add failing isolation tests that load `Echoglossian.csproj`, `Echoglossian.sln`, and the preview project as XML/text and assert all of the following:
  - the main project removes both preview directories from `Compile`, `EmbeddedResource`, and `None`
  - the preview project is absent from `Echoglossian.sln`
  - the preview project declares `IsPackable=false`
  - `Echoglossian.csproj` has no Veldrid package references
  - the main project grants `InternalsVisibleTo` to `Echoglossian.Previewer`

- [ ] Run the new test and confirm it fails before project isolation exists:

  ```powershell
  dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~PreviewerIsolationTests
  ```

  Expected: at least one assertion fails because the preview project and exclusion contract do not exist.

- [ ] Add the main-project exclusions and friend assembly:

  ```xml
  <ItemGroup>
    <Compile Remove="Echoglossian.Previewer\**;Echoglossian.Previewer.Tests\**" />
    <EmbeddedResource Remove="Echoglossian.Previewer\**;Echoglossian.Previewer.Tests\**" />
    <None Remove="Echoglossian.Previewer\**;Echoglossian.Previewer.Tests\**" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Echoglossian.Previewer" />
  </ItemGroup>
  ```

- [ ] Create `Echoglossian.Previewer.csproj` targeting `net10.0-windows`, `win-x64`, and `x64`, with `IsPackable=false`, a project reference to `..\Echoglossian.csproj`, and preview-only references to Veldrid packages. Reference `Dalamud.Bindings.ImGui.dll` and `HexaGen.Runtime.dll` from `$(appdata)\XIVLauncher\addon\Hooks\dev` with `Private=true`; copy `cimgui.dll` from that directory to output.

- [ ] Implement command-line parsing with these stable commands and options:

  ```text
  --binding-smoke
  --host-smoke
  --config <absolute-or-relative-json-path>
  --scenario <surface-key>
  --viewport <width>x<height>
  --screenshot <full|surface|batch>
  --output <directory>
  ```

- [ ] Implement `--binding-smoke` as the smallest native-binding probe:

  ```csharp
  var context = ImGui.CreateContext();
  try
  {
      Console.WriteLine($"Dalamud ImGui binding OK: {ImGui.GetVersion()}");
  }
  finally
  {
      ImGui.DestroyContext(context);
  }
  ```

- [ ] Restore, build, and run the binding probe:

  ```powershell
  dotnet restore Echoglossian.Previewer\Echoglossian.Previewer.csproj
  dotnet build Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-restore
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --binding-smoke
  ```

  Expected: build succeeds and the final command prints a non-empty ImGui version without loading FFXIV or Dalamud.

- [ ] Re-run `PreviewerIsolationTests` and confirm it passes.

- [ ] Commit the isolation slice:

  ```powershell
  git add Echoglossian.csproj Echoglossian.Previewer Echoglossian.Tests\PreviewerIsolationTests.cs
  git commit -m "#215 Scaffold isolated ImGui previewer"
  ```

### Task 2: Add The Standalone Veldrid ImGui Host

**Files:**

- Create: `Echoglossian.Previewer/Hosting/PreviewHost.cs`
- Create: `Echoglossian.Previewer/Hosting/PreviewHostOptions.cs`
- Create: `Echoglossian.Previewer/Rendering/VeldridImGuiRenderer.cs`
- Create: `Echoglossian.Previewer/Rendering/VeldridTextureRegistry.cs`
- Create: `Echoglossian.Previewer/Rendering/PreviewTextureWrap.cs`
- Create: `Echoglossian.Previewer/Rendering/Shaders/imgui.vert.glsl`
- Create: `Echoglossian.Previewer/Rendering/Shaders/imgui.frag.glsl`
- Create: `Echoglossian.Previewer/THIRD-PARTY-NOTICES.md`

- [ ] Add `--host-smoke` behavior first: the command must create a 640x360 hidden-or-minimized host, draw one ImGui frame containing `ImGui.TextUnformatted("Echoglossian preview host")`, present it, then exit with code 0. Running it before the backend exists must fail to compile.

- [ ] Adapt the renderer structure from the MIT-licensed Veldrid `Veldrid.ImGui/ImGuiRenderer.cs` to `Dalamud.Bindings.ImGui`. Do not copy the AGPL Dalamud testbed backend. Record the Veldrid source URL, copyright, MIT license text, and adapted files in `THIRD-PARTY-NOTICES.md`.

- [ ] Implement context ownership, font texture recreation, vertex/index buffer growth, projection uniform updates, scissor rectangles, and draw command submission in `VeldridImGuiRenderer`. Reject unsupported ImGui callbacks with a descriptive exception instead of silently corrupting output.

- [ ] Implement SDL2 keyboard, mouse, wheel, text input, modifier, focus, display size, and delta-time updates in `PreviewHost`. Set the backend flags only for features actually implemented.

- [ ] Compile the GLSL shaders through Veldrid.SPIRV when the graphics device is created. Keep shader source in the preview project and embed it as resources so execution does not depend on the current working directory.

- [ ] Implement `VeldridTextureRegistry` with explicit registration and removal:

  ```csharp
  public nint Register(TextureView textureView);
  public bool Unregister(nint textureId);
  public TextureView Resolve(nint textureId);
  ```

  Use monotonically increasing non-zero IDs. Do not cast graphics resource pointers into ImGui texture IDs.

- [ ] Implement `PreviewTextureWrap : IDalamudTextureWrap` over a Veldrid `Texture`, `TextureView`, and registry ID. `Handle`, `Width`, `Height`, and `Size` must reflect the actual texture, and `Dispose()` must unregister and dispose owned resources exactly once.

- [ ] Run the host smoke probe:

  ```powershell
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug -- --host-smoke
  ```

  Expected: process exits with code 0 after creating and presenting one frame; no FFXIV process is required.

- [ ] Commit the host backend:

  ```powershell
  git add Echoglossian.Previewer
  git commit -m "#215 Add standalone Veldrid ImGui host"
  ```

### Task 3: Extract The Shared Translation Overlay Renderer

**Files:**

- Create: `PluginUI/Runtime/UiFontKind.cs`
- Create: `PluginUI/Runtime/IUiFontRuntime.cs`
- Create: `PluginUI/Runtime/DalamudUiFontRuntime.cs`
- Create: `UIOverlays/TranslationOverlay/TranslationOverlayRenderRequest.cs`
- Create: `UIOverlays/TranslationOverlay/TranslationOverlayRenderResult.cs`
- Create: `UIOverlays/TranslationOverlay/TranslationOverlayLayoutCalculator.cs`
- Create: `UIOverlays/TranslationOverlay/TranslationOverlayRenderer.cs`
- Modify: `UIOverlays/TranslationOverlay/TranslationOverlayDrawer.cs`
- Modify: `UIOverlays/TranslationOverlay/TranslationWindowConfig.cs`
- Modify: `Echoglossian.cs`
- Create: `Echoglossian.Tests/TranslationOverlayLayoutCalculatorTests.cs`
- Create: `Echoglossian.Tests/TranslationWindowConfigTests.cs`

- [ ] Add table-driven failing tests for `TranslationWindowConfig.ForSurface(Config, TranslationOverlaySurfaceId)` covering all 14 enum members and confirming the existing factory values for each surface. Registered Phase A scenarios will use the 12 runtime overlay surfaces; `ActionDetail` and `ItemDetail` remain available to later phases.

- [ ] Add failing pure layout tests for representative paths:
  - fixed-size `Talk`
  - expanding `BattleTalk` bounded by viewport fractions
  - centered auto-sized `MiniTalk`
  - no-background toast
  - position correction and viewport clamping

- [ ] Introduce the font seam with balanced push/pop semantics:

  ```csharp
  internal enum UiFontKind
  {
      General,
      Language,
  }

  internal interface IUiFontRuntime
  {
      IDisposable Push(UiFontKind fontKind);
  }
  ```

  `DalamudUiFontRuntime` delegates to the existing `UINewFontHandler` handles. The disposable returned by `Push` must always restore the prior ImGui font state.

- [ ] Add `TranslationWindowConfig.ForSurface(...)` as the single exhaustive switch over `TranslationOverlaySurfaceId`. Preserve all existing named factories and have the switch call them; do not duplicate their values.

- [ ] Move calculation that does not require active ImGui state into `TranslationOverlayLayoutCalculator`. Its input must include viewport size, simulated addon position/size, measured text size, title size, and `TranslationWindowConfig`; its output must include requested position, requested size, and content wrap width.

- [ ] Define the renderer boundary so preview and screenshot code receive exact rendered bounds:

  ```csharp
  internal sealed record TranslationOverlayRenderRequest(
      TranslationOverlay Overlay,
      TranslationWindowConfig WindowConfig,
      Vector2 ViewportPosition,
      Vector2 ViewportSize,
      Vector2 AddonPosition,
      Vector2 AddonSize,
      bool IsPreview);

  internal sealed record TranslationOverlayRenderResult(
      bool WasDrawn,
      Vector2 Position,
      Vector2 Size,
      TextPresentationBackendKind PresentationMode);
  ```

- [ ] Extract the current `DrawTranslationWindow` body into `TranslationOverlayRenderer.Draw(TranslationOverlayRenderRequest)`. Preserve window IDs, style stack order, title rules, wrapping, scaling, viewport clamping, opacity, interaction flags, visibility rules, `PlainImGui`, and `RtlTexture` behavior.

- [ ] Change `TranslationOverlayDrawer.DrawTranslationWindow` into a thin adapter that builds the request from real Dalamud/game geometry and delegates to the shared renderer. Do not change addon lifecycle, translation capture, or overlay ownership.

- [ ] Initialize and dispose shared renderer/runtime dependencies in `Echoglossian.cs` without changing plugin constructor ordering visible to existing partial classes.

- [ ] Run focused and full production tests:

  ```powershell
  dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter "FullyQualifiedName~TranslationOverlayLayoutCalculatorTests|FullyQualifiedName~TranslationWindowConfigTests"
  dotnet build Echoglossian.sln -c Debug --no-restore
  dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
  ```

  Expected: focused tests pass, plugin build has 0 errors, and all existing tests pass.

- [ ] Commit the renderer extraction, including regenerated `Echoglossian.xml` if changed:

  ```powershell
  git add Echoglossian.cs Echoglossian.csproj Echoglossian.xml PluginUI\Runtime UIOverlays\TranslationOverlay Echoglossian.Tests
  git commit -m "#215 Extract shared translation overlay renderer"
  ```

### Task 4: Load User Configuration And Build Matching Fonts

**Files:**

- Create: `Echoglossian.Previewer.Tests/Echoglossian.Previewer.Tests.csproj`
- Create: `Echoglossian.Previewer.Tests/Configuration/PreviewConfigLoaderTests.cs`
- Create: `Echoglossian.Previewer.Tests/Fonts/PreviewFontCatalogTests.cs`
- Create: `Echoglossian.Previewer/Configuration/PreviewConfigLoader.cs`
- Create: `Echoglossian.Previewer/Configuration/PreviewConfiguration.cs`
- Create: `Echoglossian.Previewer/Fonts/PreviewFontCatalog.cs`
- Create: `Echoglossian.Previewer/Fonts/PreviewFontRuntime.cs`
- Modify: `Echoglossian.Previewer/Echoglossian.Previewer.csproj`

- [ ] Create an out-of-solution, non-packable xUnit project referencing the previewer project. Confirm the main-project exclusion test covers this directory.

- [ ] Add failing configuration tests for:
  - default path `%APPDATA%\XIVLauncher\pluginConfigs\Echoglossian.json`
  - explicit relative and absolute `--config` paths
  - missing file fallback to a new `Config`
  - malformed JSON returning a descriptive load failure without overwriting the source
  - deep-cloned preview config remaining independent after edits
  - redacted diagnostics that contain neither translator API keys nor full serialized config

- [ ] Implement `PreviewConfigLoader.Load(string? path)` with a result that contains the cloned `Config`, resolved source path, and non-secret diagnostics. Use the same Newtonsoft.Json conventions already accepted by the plugin config. Open files read-only with sharing enabled; never call plugin save methods.

- [ ] Add failing font catalog tests that map the selected language and configured font size to existing files under the repository `Font` directory, including Latin, Arabic/Hebrew RTL, Japanese, Korean, simplified Chinese, and traditional Chinese examples.

- [ ] Implement `PreviewFontCatalog` as a pure path-and-size resolver. Reuse current font selection data from `UINewFontHandler` by extracting constants or a small shared resolver; do not duplicate a second language-to-font table in the preview assembly.

- [ ] Implement `PreviewFontRuntime : IUiFontRuntime` with direct `ImGuiIO.Fonts.AddFontFromFileTTF` calls and the same base, symbols, language-specific, complementary, and special font files selected by the plugin. Build glyph ranges from scenario title/text plus the selected language's exclusive characters, then recreate the backend font texture.

- [ ] Copy/link the required `Font/**` files to preview output from the preview project only. Verify this does not alter the main plugin's content items or package output.

- [ ] Run preview configuration and font tests:

  ```powershell
  dotnet restore Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj
  dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug
  ```

  Expected: all preview tests pass and test output contains no secrets from a synthetic config fixture.

- [ ] Commit config and fonts:

  ```powershell
  git add Echoglossian.Previewer Echoglossian.Previewer.Tests PluginUI\Helpers
  git commit -m "#215 Load preview config and matching fonts"
  ```

### Task 5: Preserve RTL Texture Presentation In The Preview Host

**Files:**

- Modify: `UIOverlays/TextPresentation/RtlTexturePresentationService.cs`
- Create: `Echoglossian.Previewer/Rendering/VeldridTextTextureFactory.cs`
- Create: `Echoglossian.Previewer.Tests/Rendering/VeldridTextTextureFactoryTests.cs`
- Modify: `Echoglossian.Tests/RtlTexturePresentationServiceTests.cs`

- [ ] Add a failing service test that injects a layout-aware factory and verifies the factory receives the `TextRasterLayout` produced by the existing `TextImageRenderer` together with the complete `TextureCreationRequest`.

- [ ] Change the existing private layout-aware constructor to `internal` while preserving the public `ITextureProvider` constructor and the current request-only test constructor. Keep its existing delegate shape:

  ```csharp
  Func<TextImageRenderer,
      TextImageRenderer.TextRasterLayout,
      TextureCreationRequest,
      CancellationToken,
      Task<IDalamudTextureWrap>>
  ```

  The production path must continue to use `ITextureProvider`; only the friend preview assembly supplies the Veldrid implementation.

- [ ] Add failing preview tests for RGBA upload orientation, reported dimensions, transparent pixels, disposal/unregistration, and cancellation before GPU upload. Keep GPU-independent pixel conversion in a pure helper so tests do not require a visible desktop.

- [ ] Implement `VeldridTextTextureFactory` by asking the passed `TextImageRenderer` to rasterize the passed layout, uploading the exact RGBA bytes into a Veldrid sampled texture, and returning `PreviewTextureWrap`.

- [ ] Wire the preview service into `TranslationOverlayRenderer` so `LanguagePresentationPolicy` selects `RtlTexture` for the same language/config inputs as the plugin. Do not add preview-specific presentation rules.

- [ ] Run RTL and preview rendering tests:

  ```powershell
  dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~RtlTexturePresentationServiceTests
  dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~VeldridTextTextureFactoryTests
  ```

  Expected: both suites pass; existing cache, cancellation, and disposal tests remain green.

- [ ] Commit RTL support:

  ```powershell
  git add UIOverlays\TextPresentation Echoglossian.Previewer Echoglossian.Previewer.Tests Echoglossian.Tests\RtlTexturePresentationServiceTests.cs
  git commit -m "#215 Render RTL overlay textures in previewer"
  ```

### Task 6: Add Overlay Scenarios And The Interactive Preview Shell

**Files:**

- Create: `Echoglossian.Previewer/Scenarios/PreviewScenario.cs`
- Create: `Echoglossian.Previewer/Scenarios/PreviewScenarioCatalog.cs`
- Create: `Echoglossian.Previewer/Scenarios/PreviewViewportPreset.cs`
- Create: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Create: `Echoglossian.Previewer/UI/PreviewCanvas.cs`
- Create: `Echoglossian.Previewer.Tests/Scenarios/PreviewScenarioCatalogTests.cs`
- Modify: `Echoglossian.Previewer/Program.cs`

- [ ] Add failing catalog tests asserting one deterministic default scenario for each currently registered runtime overlay surface:
  - `Talk`
  - `BattleTalk`
  - `TalkSubtitle`
  - `MiniTalk`
  - `CutSceneSelectString`
  - `TextGimmickHint`
  - `WideTextToast`
  - `ErrorToast`
  - `AreaToast`
  - `ClassChangeToast`
  - `QuestToast`
  - `ChatBubble`

- [ ] Add viewport presets for 1280x720, 1920x1080, 2560x1440, and 3440x1440. Each scenario must define stable addon position/size, translated text, optional speaker/title, visibility, and selected surface ID.

- [ ] Implement a left control panel for surface, viewport, config source, title, body text, addon bounds, and screenshot actions. Render a right preview canvas at the selected logical resolution, scaled uniformly to available host space.

- [ ] Draw simulated addon bounds as an optional labeled guide outside the shared overlay renderer. The guide must be disabled by default for screenshots and must never resemble or claim to reproduce native FFXIV UI.

- [ ] Call `TranslationWindowConfig.ForSurface`, construct the real `TranslationOverlay`, and invoke `TranslationOverlayRenderer.Draw` each frame. Keep scenario edits in preview-owned state; do not mutate the loaded source config object after cloning.

- [ ] Display a visible fidelity summary: config source, font file/size, logical viewport, selected presentation mode, and whether the scenario uses simulated addon bounds.

- [ ] Run catalog tests and launch one interactive LTR and one RTL scenario:

  ```powershell
  dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PreviewScenarioCatalogTests
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug -- --scenario talk --viewport 1920x1080
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug -- --scenario talk --viewport 1920x1080 --config "$env:APPDATA\XIVLauncher\pluginConfigs\Echoglossian.json"
  ```

  Expected: the shell opens without FFXIV, controls remain interactive, and the overlay follows the selected config and viewport. Close each interactive run manually after inspection.

- [ ] Commit scenarios and shell:

  ```powershell
  git add Echoglossian.Previewer Echoglossian.Previewer.Tests
  git commit -m "#215 Add interactive overlay preview scenarios"
  ```

### Task 7: Export Full, Surface, And Batch Screenshots

**Files:**

- Create: `Echoglossian.Previewer/Screenshots/ScreenshotMode.cs`
- Create: `Echoglossian.Previewer/Screenshots/ScreenshotRequest.cs`
- Create: `Echoglossian.Previewer/Screenshots/ScreenshotFileName.cs`
- Create: `Echoglossian.Previewer/Screenshots/VeldridScreenshotCapture.cs`
- Create: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- Create: `Echoglossian.Previewer.Tests/Screenshots/ScreenshotFileNameTests.cs`
- Create: `Echoglossian.Previewer.Tests/Screenshots/ScreenshotCropTests.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Modify: `Echoglossian.Previewer/Program.cs`

- [ ] Add failing tests for deterministic, Windows-safe file names and for clamping a `TranslationOverlayRenderResult` crop rectangle to the logical viewport. Include zero-size, partially off-screen, and HiDPI-scaled cases.

- [ ] Implement full-frame capture by copying the rendered color target into a CPU-readable staging texture, mapping it after GPU completion, normalizing row pitch and channel order, and writing PNG with `System.Drawing.Common`.

- [ ] Implement selected-surface capture using the exact `Position` and `Size` returned by `TranslationOverlayRenderer.Draw`. Add a configurable 8-pixel logical margin and clamp it to the rendered target.

- [ ] Implement batch mode over all 12 scenarios and requested viewport presets. Disable controls, addon guides, and debug overlays in captured frames. Render at the logical target resolution rather than the resized desktop-window resolution.

- [ ] Default output to `artifacts/previewer/screenshots/<timestamp>` and allow `--output`. Write a sidecar `manifest.json` containing surface key, viewport, config source path without config contents, font file names/sizes, presentation mode, and PNG path.

- [ ] Run screenshot tests and a deterministic batch export:

  ```powershell
  dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter "FullyQualifiedName~ScreenshotFileNameTests|FullyQualifiedName~ScreenshotCropTests"
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug -- --screenshot batch --viewport 1920x1080 --output artifacts\previewer\screenshots\validation
  Get-ChildItem artifacts\previewer\screenshots\validation -Filter *.png
  ```

  Expected: 12 non-empty PNG files plus `manifest.json`; output remains ignored by git.

- [ ] Open at least one LTR full-frame PNG, one LTR surface crop, and one RTL surface crop to verify orientation, transparency/background, font rendering, and crop bounds.

- [ ] Commit screenshot support:

  ```powershell
  git add Echoglossian.Previewer Echoglossian.Previewer.Tests
  git commit -m "#215 Add reproducible overlay screenshots"
  ```

### Task 8: Document, Validate, And Record The Manual Fidelity Gate

**Files:**

- Create: `Echoglossian.Previewer/README.md`
- Modify: `docs/superpowers/specs/2026-07-12-imgui-previewer-design.md`
- Modify: `docs/superpowers/plans/2026-07-15-issue-215-imgui-previewer-foundation-overlay-plan.md`

- [ ] Document prerequisites, the dev Dalamud binding path, independent restore/build/run commands, config auto-detection, all CLI options, interactive controls, screenshot output, presentation modes, and troubleshooting for missing `cimgui.dll` or bindings.

- [ ] Document the fidelity boundary precisely: plugin ImGui code, config, fonts, viewport inputs, layout, and RTL rasterization are shared; native game UI, game compositor color management, and real addon geometry are not reproduced.

- [ ] Run production validation from a clean command sequence:

  ```powershell
  dotnet restore Echoglossian.sln
  dotnet build Echoglossian.sln -c Debug --no-restore
  dotnet restore Echoglossian.Tests\Echoglossian.Tests.csproj
  dotnet build Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-restore
  dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
  ```

  Expected: plugin build has 0 errors and the full existing test suite passes. Existing warnings may remain but no new preview-related warning may appear in the production build.

- [ ] Run preview validation independently:

  ```powershell
  dotnet restore Echoglossian.Previewer\Echoglossian.Previewer.csproj
  dotnet build Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-restore
  dotnet restore Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj
  dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --binding-smoke
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke
  dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot batch --viewport 1920x1080 --output artifacts\previewer\screenshots\final-validation
  ```

  Expected: both projects build, preview tests pass, both smoke probes exit 0, and the batch contains 12 PNG files plus a manifest.

- [ ] Compare one `PlainImGui` scenario and one `RtlTexture` scenario in-game against preview screenshots using the same `Echoglossian.json`, logical viewport, text, font size, and simulated addon bounds. Record differences caused by real addon geometry or game compositor separately from renderer regressions.

- [ ] If FFXIV is unavailable during automated implementation, mark only this manual comparison checkbox as pending in the plan and GitHub issue. Do not claim 1:1 validation until the comparison is completed.

- [ ] Review `git diff --check`, `git status --short`, generated XML changes, package references, solution membership, and ignored screenshot output. Confirm the plugin artifact contains no Veldrid assemblies, preview executable, or preview native binary.

- [ ] Commit final documentation and validation notes:

  ```powershell
  git add Echoglossian.Previewer\README.md docs\superpowers\specs\2026-07-12-imgui-previewer-design.md docs\superpowers\plans\2026-07-15-issue-215-imgui-previewer-foundation-overlay-plan.md Echoglossian.xml
  git commit -m "#215 Document ImGui previewer workflow"
  ```

## Phase Completion Criteria

Phase A is complete only when:

- the main plugin build and tests pass without preview dependencies entering its package
- the standalone host starts without FFXIV or a running Dalamud process
- all 12 registered translation overlay surfaces render through the shared renderer
- `PlainImGui` and `RtlTexture` scenarios both render successfully
- user config loading is read-only and no diagnostics expose secrets
- full-frame, surface-crop, and batch screenshots are generated deterministically
- the manual in-game comparison is either recorded as passed or explicitly left as the only pending fidelity gate

Configuration UI, translator metrics/debugger, ActionDetail/ItemDetail, and DB editor preview extraction remain tracked by issue #215 for subsequent plans.
