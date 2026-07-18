# Task 5 Report: Preserve RTL Texture Presentation In The Preview Host

## Status

Completed and committed.

## Files Changed

- `UIOverlays/TextPresentation/RtlTexturePresentationService.cs`
- `Echoglossian.Previewer/Rendering/VeldridTextTextureFactory.cs`
- `Echoglossian.Previewer/Rendering/PreviewTextureWrap.cs`
- `Echoglossian.Previewer/Rendering/VeldridTextureRegistry.cs`
- `Echoglossian.Previewer/Hosting/PreviewHost.cs`
- `Echoglossian.Previewer/Echoglossian.Previewer.csproj`
- `Echoglossian.Previewer.Tests/Rendering/VeldridTextTextureFactoryTests.cs`
- `Echoglossian.Previewer.Tests/Echoglossian.Previewer.Tests.csproj`
- `Echoglossian.Tests/RtlTexturePresentationServiceTests.cs`

## RED Evidence

- The new service test initially failed to compile because the layout-aware
  `RtlTexturePresentationService` constructor was private and overload
  resolution selected the public `ITextureProvider` constructor.
- The new preview tests initially failed to compile because
  `VeldridTextTextureFactory` did not exist.

## GREEN Evidence

- The layout-aware constructor is now internal with its required delegate
  shape. The public `ITextureProvider` constructor and request-only test
  constructor remain unchanged.
- `VeldridTextTextureFactory` rasterizes the supplied layout, converts the
  bitmap from ARGB to top-left RGBA bytes, uploads an
  `R8_G8_B8_A8_UNorm` sampled Veldrid texture, and returns
  `PreviewTextureWrap`.
- Preview tests cover RGBA orientation, dimensions, transparent alpha,
  cancellation before upload, and disposal/unregistration without a visible
  desktop or GPU device.
- `PreviewHost.CreateTextTextureFactory()` exposes the host-bound factory for
  Task 6 to inject into the existing `TranslationOverlayRenderer` path, which
  already resolves `RtlTexture` via shared `LanguagePresentationPolicy`.

## Commit

`38d8fd1 #215 Render RTL overlay textures in previewer`

## Validation Results

- `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~RtlTexturePresentationServiceTests`: passed, 15 tests.
- `dotnet test Echoglossian.Previewer.Tests\\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~VeldridTextTextureFactoryTests`: passed, 4 tests.
- `dotnet build Echoglossian.sln -c Debug --no-restore`: passed, 0 errors.
- `dotnet test Echoglossian.Tests\\Echoglossian.Tests.csproj -c Debug --no-build`: passed, 613 tests.

## Concerns

- Actual preview-shell construction of `RtlTexturePresentationService` and
  `TranslationOverlayRenderer` belongs to Task 6. It must inject
  `host.CreateTextTextureFactory().CreateTextureAsync`; it must not add
  preview-specific language or presentation rules.
- No visible-window or hardware-backed Veldrid smoke test was run. Pixel
  conversion and lifecycle behavior are covered by GPU-independent tests.

## Fix

### Review Finding

- P1: Preview RTL texture service was not wired into
  `TranslationOverlayRenderer` for Task 5.

### Files Changed

- `Echoglossian.Previewer/Hosting/PreviewOverlayRendererFactory.cs`
- `Echoglossian.Previewer.Tests/Rendering/VeldridTextTextureFactoryTests.cs`
- `Echoglossian.csproj`

### Commit

- `a74e05d0dd9a2caab93198c7e669347efe0b53aa #215 Wire preview RTL renderer composition`

### Commands Run

- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PreviewOverlayRendererFactory_Create_RtlRequestUsesVeldridTextureFactory`: passed, 1 test.
- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~VeldridTextTextureFactoryTests`: passed, 5 tests.
- `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~RtlTexturePresentationServiceTests`: passed, 15 tests.
- `dotnet build Echoglossian.sln -c Debug --no-restore`: passed, 0 errors.

### Result

- Added previewer-side composition that creates
  `RtlTexturePresentationService` with
  `host.CreateTextTextureFactory().CreateTextureAsync` and passes it to
  `TranslationOverlayRenderer`.
- Preserved the production `ITextureProvider` construction path.
- Added a previewer regression test proving an RTL render request reaches the
  supplied Veldrid texture creation delegate through the composed RTL service.

## Backend Diagnostics

### Commit

- `6f100fc feat: surface preview plugin-window backend diagnostics`

### Result

- Added requested, effective, and fallback backend fields to screenshot manifests.
- Added shell backend status and selection controls.

### Validation

- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~BatchScreenshotRunnerBackendManifestTests|FullyQualifiedName~PreviewShellBackendStatusTests"`: passed, 4/4.
- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore`: passed, 142/142.

## Backend Restart Fix Wave

### Root Cause

- The shell selector only updated display state. `Program` created its backend once before `PreviewHost.Run`, so restarting the previewer shell reused the original command-line backend mode.

### Implementation

- The shell emits a one-shot pending restart mode when the operator changes the backend selection.
- `PreviewHost.Run` accepts a continuation predicate so the interactive loop can exit without closing the preview window.
- `Program` consumes the request, updates the requested mode, disposes the current shell and backend, and recreates them over the existing preview-owned session, host, renderer, and font runtime.

### TDD Evidence

- RED: `PreviewShellBackendStatusTests` failed with `CS0117` because `PreviewShell.GetPluginWindowBackendRestartMode` did not exist.
- GREEN: the focused shell status and host contract tests passed after the restart path was added.

### Commands Run

- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PreviewShellBackendStatusTests|FullyQualifiedName~PreviewHostContractTests"`: passed, 6/6.
- `dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke`: passed, exit code 0.
- `dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore`: passed, 143/143.
- `git diff --check`: passed.

### Concern

- The pre-existing DalaMock `CreateDebouncer` incompatibility still prevents a local end-to-end hosted preview frame. Auto fallback remains visible; this fix does not alter that external blocker.
