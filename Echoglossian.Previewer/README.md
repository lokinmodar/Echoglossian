# Echoglossian ImGui Previewer

`Echoglossian.Previewer` is a development-only Windows host for previewing and
capturing Echoglossian translation overlay ImGui surfaces without launching
FFXIV or Dalamud. It is intentionally kept out of `Echoglossian.sln` and out of
normal plugin build, deploy, and packaging flows.

## Prerequisites

- Windows x64.
- .NET 10 SDK.
- XIVLauncher dev Dalamud hooks installed at:
  `%APPDATA%\XIVLauncher\addon\Hooks\dev`
- The dev hooks directory must contain `Dalamud.dll`,
  `Dalamud.Bindings.ImGui.dll`, `HexaGen.Runtime.dll`, and `cimgui.dll`.
- If the dev hooks live elsewhere, pass MSBuild property
  `DalamudLibPath=<absolute-path>` to restore/build/run commands.

The previewer copies the Dalamud binding files and `cimgui.dll` into only the
previewer output directory. Veldrid and preview native binaries must not enter
the plugin artifact.

## Build And Run

Run previewer commands independently from the main solution:

```powershell
dotnet restore Echoglossian.Previewer\Echoglossian.Previewer.csproj
dotnet build Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-restore
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --binding-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --scenario talk --viewport 1920x1080
```

With a custom Dalamud dev binding path:

```powershell
dotnet build Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-restore -p:DalamudLibPath="D:\Dalamud\Hooks\dev"
```

## Configuration

By default, the previewer attempts to read:

```text
%APPDATA%\XIVLauncher\pluginConfigs\Echoglossian.json
```

The file is opened read-only with sharing enabled, cloned into a preview-owned
session workspace, and never saved back. The previewer also creates a WAL-aware
SQLite snapshot of the optional database source in that workspace before later
preview phases can use it. If
either source is missing, or if the optional database snapshot cannot be
created, the previewer retains an isolated default or omits the database and
records a non-secret session diagnostic. To use other sources:

```powershell
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --config .\sample\Echoglossian.json
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --db .\sample\Echoglossian.db
```

The default database source is
`%APPDATA%\XIVLauncher\pluginConfigs\Echoglossian\Echoglossian.db`.
The previewer reads but does not modify the live database while creating its
snapshot.

## Unified Workbench

The interactive shell hosts the real plugin code for these surfaces alongside
the overlay scenarios:

- `Config`
- `DB Manager`
- `Translator Metrics / Debugger`

Use the window toggles to open each surface. DB Manager is disabled when the
session has no database snapshot. Preview edits and saves affect only the
cloned session files; they are never written back automatically to either live
source. There is no export-back flow in Phase 1.

### Plugin Window Backend Selection

Use `--plugin-window-backend auto|standalone|dalamock` to choose how `Config`,
`DB Manager`, and `Translator Metrics / Debugger` are hosted.

- `auto`: tries DalaMock first, then falls back to standalone with a visible
  diagnostic.
- `standalone`: always uses the previewer's existing plugin-window runtime.
- `dalamock`: requires the DalaMock-hosted runtime and reports startup failure
  instead of silently falling back.

The interactive shell provides the same selection and restarts only the
plugin-window backend when it changes. The selected backend affects plugin
windows only; overlay preview remains on the existing standalone pipeline.

## CLI Options

- `--binding-smoke`: creates and destroys a Dalamud ImGui context, then exits.
- `--host-smoke`: creates a hidden 640x360 Veldrid ImGui host, draws one frame,
  then exits.
- `--plugin-window-backend <auto|standalone|dalamock>`: selects the backend for
  real plugin windows; defaults to `auto`.
- `--config <path>`: loads an absolute or relative Echoglossian config JSON
  read-only.
- `--db <path>`: creates a WAL-aware snapshot of an absolute or relative
  Echoglossian database in the preview-owned session workspace.
- `--scenario <surface-key>`: selects a scenario; defaults to `talk`.
- `--viewport <width>x<height>`: selects the logical preview viewport; defaults
  to `1920x1080`.
- `--screenshot <full|surface|batch>`: exports screenshots instead of opening
  the interactive shell.
- `--capture-target <config-window|db-manager-window|translator-metrics-window>`:
  exports one real plugin window with `--screenshot full` after its fixed bounds
  remain stable across consecutive frames.
- `--output <directory>`: writes screenshots to a specific directory; defaults
  to `artifacts\previewer\screenshots\<timestamp>`.

Registered Phase A scenario keys are `talk`, `battle-talk`, `talk-subtitle`,
`mini-talk`, `cutscene-select-string`, `text-gimmick-hint`, `wide-text-toast`,
`error-toast`, `area-toast`, `class-change-toast`, `quest-toast`, and
`chat-bubble`.

Built-in viewport presets are `1280x720`, `1920x1080`, `2560x1440`, and
`3440x1440`; arbitrary positive `widthxheight` values are also accepted.

## Interactive Controls

The interactive shell opens a control panel and a scaled logical preview canvas.
Controls include:

- Surface scenario selector.
- Logical viewport selector.
- Overlay visibility.
- Optional simulated addon bounds guide.
- Editable title and body text.
- Editable addon bounds input values.
- Full-frame, selected overlay surface, Config, DB Manager, and Translator
  Metrics window screenshot buttons.
- Fidelity summary with config source, font file and size, logical viewport,
  selected presentation mode, and whether simulated addon bounds are shown.

The addon bounds guide is disabled by default for screenshots. It is only a
geometry input visualization and does not reproduce native FFXIV UI.

## Screenshots

Examples:

```powershell
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot full --scenario talk --viewport 1920x1080 --output artifacts\previewer\screenshots\full
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot surface --scenario talk --viewport 1920x1080 --output artifacts\previewer\screenshots\surface
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot batch --viewport 1920x1080 --output artifacts\previewer\screenshots\batch
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot full --capture-target config-window --scenario talk --viewport 1920x1080 --output artifacts\previewer\screenshots\config-window
```

Batch mode with a single viewport writes 12 overlay PNG files plus
`manifest.json`. Batch mode without `--viewport` runs all built-in viewports.
Use `--capture-target` or the interactive shell's target-specific buttons to
capture Config, DB Manager, or Translator Metrics windows. Each window uses a
fixed layout and must report stable non-empty bounds before capture; missing
bounds fail without writing a misleading target-specific image. The manifest
records scenario key, surface key, viewport, screenshot mode, capture target,
presentation mode, config source label, font file names, font size, and PNG
path. Plugin-window captures also record requested and effective backend modes
plus any fallback reason. Screenshot output is under `artifacts/` and ignored
by git.

## Presentation Modes

The previewer uses the shared overlay renderer and reports the presentation mode
returned by that renderer:

- `PlainImGui`: text is drawn through the active ImGui font atlas.
- `RtlTexture`: text is rasterized through the same RTL text image path and
  uploaded as a preview texture.

Arabic and Hebrew config selections exercise `RtlTexture`; default English
config exercises `PlainImGui`.

## Fidelity Boundary

Shared with the plugin:

- Echoglossian overlay ImGui renderer code.
- `Config`, `TranslationOverlay`, and `TranslationWindowConfig` inputs.
- Font file selection and configured font size.
- Logical viewport and simulated addon bounds inputs.
- Overlay layout, wrapping, opacity, placement, and clipping logic.
- RTL rasterization path and texture presentation behavior.

Not reproduced:

- Native FFXIV UI rendering.
- Native addon lifecycle, `AtkTextNode`, or `AtkValue` mutation.
- The game compositor, post-processing, color management, or HDR behavior.
- Real in-game addon geometry, animation, z-order, or occlusion.

Automated launch and screenshot checks validate process, renderer, and capture
mechanics. A human must still compare at least one `PlainImGui` and one
`RtlTexture` scenario in-game with the same config, viewport, text, font size,
and addon bounds before claiming 1:1 visual fidelity.

## Troubleshooting

- Missing `cimgui.dll`: install or refresh XIVLauncher dev hooks, or pass
  `-p:DalamudLibPath=<path>` to a directory containing `cimgui.dll`.
- Missing `Dalamud.Bindings.ImGui.dll` or `HexaGen.Runtime.dll`: verify the dev
  hooks path points at `%APPDATA%\XIVLauncher\addon\Hooks\dev` or the matching
  custom dev binding directory.
- Binding smoke fails before printing an ImGui version: the native binding files
  are not being copied to the previewer output, or a mismatched `cimgui.dll` is
  being loaded.
- Host smoke fails on startup: verify Windows x64, the .NET 10 SDK, GPU driver,
  and Veldrid/SDL2 native dependencies restored correctly.
- `auto` falls back to standalone: read the shell diagnostic and
  `PluginWindowBackendFallbackReason` in `manifest.json`. Select `dalamock`
  when a hosted startup failure must be surfaced without fallback.
- Config loads as defaults: confirm the path passed to `--config` exists and is
  valid JSON. Diagnostics intentionally do not include config contents or API
  keys.
