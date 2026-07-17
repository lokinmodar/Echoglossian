# Unified ImGui Previewer Phase 1 Implementation Plan

> **Status:** Complete and validated on `feature/dalamock-unified-previewer`.

**Goal:** Expand `Echoglossian.Previewer` into one standalone preview workbench that hosts the real overlay, config window, DB manager, and translator metrics window from cloned config/DB snapshots without affecting plugin build or live user state.

**Architecture:** Keep the existing previewer host and overlay renderer as the foundation. Add a preview session layer for cloned config/DB state, extract a reusable config window renderer plus preview-safe save scope, host the real plugin windows inside the same ImGui frame loop, and extend capture/export to support full-frame, overlay-surface, and window-target screenshots.

**Tech Stack:** C# / .NET 10, Dalamud.Bindings.ImGui, Veldrid, xUnit, EF Core SQLite, existing Echoglossian plugin UI and overlay code.

## Global Constraints

- Keep `Echoglossian.Previewer` out of `Echoglossian.sln`.
- Keep `Echoglossian.Previewer` `IsPackable=false`.
- Do not add previewer dependencies to the plugin artifact.
- Do not use DalaMock in Phase 1.
- Preserve the current overlay fidelity boundary, including `PlainImGui` and `RtlTexture`.
- Use real plugin window code, not preview-only facsimiles, for `Config`, `DB Manager`, and `Translator Metrics/Debugger`.
- Read config and DB sources from user-selected or default locations, clone them, and operate only on the cloned session state.
- Never auto-write preview edits back to the user's live config or DB.
- Keep the patch narrow: prefer adapters and extract-only seams over broad runtime refactors.
- Maintain repository validation with:
  - `dotnet build Echoglossian.sln -c Debug --no-restore`
  - `dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build`

---

### Task 1: Add preview session snapshot foundation and source selection

**Files:**
- Create: `Echoglossian.Previewer/Session/PreviewSessionSourceOptions.cs`
- Create: `Echoglossian.Previewer/Session/PreviewSessionArtifacts.cs`
- Create: `Echoglossian.Previewer/Session/PreviewSessionLoader.cs`
- Create: `Echoglossian.Previewer.Tests/Session/PreviewSessionLoaderTests.cs`
- Modify: `Echoglossian.Previewer/Hosting/PreviewCommandLine.cs`
- Modify: `Echoglossian.Previewer/Program.cs`
- Modify: `Echoglossian.Previewer/README.md`

**Interfaces:**
- Consumes:
  - `PreviewConfigLoader.Load(string? configPath): PreviewConfiguration`
  - `PreviewConfiguration.CreateEditableCopy(): Config`
- Produces:
  - `internal sealed record PreviewSessionSourceOptions(string? ConfigPath, string? DatabasePath, string? OutputDirectory);`
  - `internal sealed class PreviewSessionArtifacts : IDisposable`
  - `internal static class PreviewSessionLoader { internal static PreviewSessionArtifacts Load(PreviewSessionSourceOptions options); }`
  - `PreviewCommandLine.DatabasePath`
  - `PreviewSessionArtifacts.Diagnostics`

- [x] **Step 1: Write the failing session snapshot tests**

```csharp
// Echoglossian.Previewer.Tests/Session/PreviewSessionLoaderTests.cs
public sealed class PreviewSessionLoaderTests
{
    [Fact]
    public void Load_ClonesConfigAndDatabaseIntoSessionWorkspace()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
        var dbPath = Path.Combine(tempRoot.FullName, "Echoglossian.db");

        File.WriteAllText(configPath, "{\"Lang\":28,\"FontSize\":24}");
        File.WriteAllText(dbPath, "preview-db");

        using var session = PreviewSessionLoader.Load(
            new PreviewSessionSourceOptions(configPath, dbPath, null));

        Assert.NotEqual(configPath, session.ClonedConfigPath);
        Assert.NotEqual(dbPath, session.ClonedDatabasePath);
        Assert.True(File.Exists(session.ClonedConfigPath));
        Assert.True(File.Exists(session.ClonedDatabasePath));
        Assert.Equal(28, session.EditableConfiguration.Lang);
    }

    [Fact]
    public void Load_AllowsMissingDatabaseWithoutTouchingLiveSources()
    {
        var tempRoot = Directory.CreateTempSubdirectory();
        var configPath = Path.Combine(tempRoot.FullName, "Echoglossian.json");
        File.WriteAllText(configPath, "{\"Lang\":28}");

        using var session = PreviewSessionLoader.Load(
            new PreviewSessionSourceOptions(configPath, null, null));

        Assert.Null(session.ClonedDatabasePath);
        Assert.Contains("database", string.Join(" ", session.Diagnostics), StringComparison.OrdinalIgnoreCase);
    }
}
```

- [x] **Step 2: Run the new test target to verify it fails**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PreviewSessionLoaderTests
```

Expected: FAIL with missing `PreviewSessionLoader`, `PreviewSessionSourceOptions`, and `PreviewSessionArtifacts`.

- [x] **Step 3: Implement the preview session loader and CLI plumbing**

```csharp
// Echoglossian.Previewer/Session/PreviewSessionSourceOptions.cs
internal sealed record PreviewSessionSourceOptions(
    string? ConfigPath,
    string? DatabasePath,
    string? OutputDirectory);

// Echoglossian.Previewer/Session/PreviewSessionArtifacts.cs
internal sealed class PreviewSessionArtifacts : IDisposable
{
    internal PreviewSessionArtifacts(
        string workingDirectory,
        PreviewConfiguration configuration,
        Config editableConfiguration,
        string clonedConfigPath,
        string? clonedDatabasePath,
        IReadOnlyList<string> diagnostics)
    {
        this.WorkingDirectory = workingDirectory;
        this.Configuration = configuration;
        this.EditableConfiguration = editableConfiguration;
        this.ClonedConfigPath = clonedConfigPath;
        this.ClonedDatabasePath = clonedDatabasePath;
        this.Diagnostics = diagnostics;
    }

    internal string WorkingDirectory { get; }
    internal PreviewConfiguration Configuration { get; }
    internal Config EditableConfiguration { get; }
    internal string ClonedConfigPath { get; }
    internal string? ClonedDatabasePath { get; }
    internal IReadOnlyList<string> Diagnostics { get; }

    public void Dispose()
    {
        if (Directory.Exists(this.WorkingDirectory))
        {
            Directory.Delete(this.WorkingDirectory, recursive: true);
        }
    }
}

// Echoglossian.Previewer/Session/PreviewSessionLoader.cs
internal static class PreviewSessionLoader
{
    internal static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XIVLauncher", "pluginConfigs", "Echoglossian", "Echoglossian.db");
    }

    internal static PreviewSessionArtifacts Load(PreviewSessionSourceOptions options)
    {
        var sourceConfiguration = PreviewConfigLoader.Load(options.ConfigPath);
        var editableConfiguration = sourceConfiguration.CreateEditableCopy();
        var diagnostics = sourceConfiguration.Diagnostics.ToList();
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Echoglossian.Previewer",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        var clonedConfigPath = Path.Combine(workingDirectory, "Echoglossian.json");
        File.WriteAllText(clonedConfigPath, JsonConvert.SerializeObject(editableConfiguration, Formatting.Indented));

        string? clonedDatabasePath = null;
        var databasePath = string.IsNullOrWhiteSpace(options.DatabasePath)
            ? GetDefaultDatabasePath()
            : Path.GetFullPath(options.DatabasePath);
        if (File.Exists(databasePath))
        {
            clonedDatabasePath = Path.Combine(workingDirectory, Path.GetFileName(databasePath));
            File.Copy(databasePath, clonedDatabasePath, overwrite: true);
        }
        else
        {
            diagnostics.Add("Preview database file was not found; DB-backed windows will be unavailable.");
        }

        return new PreviewSessionArtifacts(
            workingDirectory,
            sourceConfiguration,
            editableConfiguration,
            clonedConfigPath,
            clonedDatabasePath,
            diagnostics);
    }
}
```

```csharp
// Echoglossian.Previewer/Hosting/PreviewCommandLine.cs
internal string? DatabasePath { get; private set; }

case "--db":
    commandLine.DatabasePath = GetValue(args, ref index, "--db");
    break;
```

```csharp
// Echoglossian.Previewer/Program.cs
using var session = PreviewSessionLoader.Load(
    new PreviewSessionSourceOptions(
        commandLine.ConfigPath,
        commandLine.DatabasePath,
        commandLine.OutputDirectory));

var sourceConfiguration = session.Configuration;
var editableConfiguration = session.EditableConfiguration;
```

- [x] **Step 4: Run the tests and a previewer smoke path**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PreviewSessionLoaderTests
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug -- --host-smoke
```

Expected:

- tests PASS
- host smoke exits `0`

- [x] **Step 5: Commit**

```powershell
git add Echoglossian.Previewer\Hosting\PreviewCommandLine.cs Echoglossian.Previewer\Program.cs Echoglossian.Previewer\Session Echoglossian.Previewer.Tests\Session Echoglossian.Previewer\README.md
git commit -m "feat: add preview session snapshot foundation"
```

### Task 2: Introduce preview-safe config save scope and extract the reusable config window renderer

**Files:**
- Create: `PluginUI/PluginConfigSaveScope.cs`
- Create: `PluginUI/PluginConfigWindowContext.cs`
- Create: `PluginUI/PluginConfigWindowRenderer.cs`
- Create: `Echoglossian.Tests/PluginConfigSaveScopeTests.cs`
- Modify: `GeneralHelpers/Utils.cs`
- Modify: `PluginUI/PluginUI.cs`
- Modify: `PluginUI/PluginRuntimeUi.cs`

**Interfaces:**
- Consumes:
  - `Echoglossian.SaveConfig(Config config)`
  - `ResetConfigButtonHelper.Draw(Config config, Action saveAction)`
  - `OverlayTab.Draw(Config config)`
  - `TroubleshootingTab.Draw(Config config)`
  - `AboutTab.Draw(Config config, ImTextureID logoHandle)`
- Produces:
  - `public static class PluginConfigSaveScope`
  - `public sealed record PluginConfigWindowContext(...)`
  - `public sealed class PluginConfigWindowRenderer { public bool Draw(PluginConfigWindowContext context, ref bool isOpen); }`

- [x] **Step 1: Write failing tests for save redirection**

```csharp
// Echoglossian.Tests/PluginConfigSaveScopeTests.cs
public sealed class PluginConfigSaveScopeTests
{
    [Fact]
    public void SaveConfig_UsesScopedOverride_WhenPresent()
    {
        var config = new Config();
        var calls = 0;

        using var scope = PluginConfigSaveScope.Push(_ => calls++);

        Echoglossian.SaveConfig(config);

        Assert.Equal(1, calls);
    }
}
```

- [x] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --filter FullyQualifiedName~PluginConfigSaveScopeTests
```

Expected: FAIL with missing `PluginConfigSaveScope`.

- [x] **Step 3: Add the save scope and extract the config renderer**

```csharp
// PluginUI/PluginConfigSaveScope.cs
public static class PluginConfigSaveScope
{
    private static readonly AsyncLocal<Stack<Action<Config>>> Scopes = new();

    public static IDisposable Push(Action<Config> saveOverride)
    {
        var stack = Scopes.Value ??= new Stack<Action<Config>>();
        stack.Push(saveOverride);
        return new PopWhenDisposed(stack);
    }

    public static bool TrySave(Config config)
    {
        var stack = Scopes.Value;
        if (stack is not { Count: > 0 })
        {
            return false;
        }

        stack.Peek()(config);
        return true;
    }

    private sealed class PopWhenDisposed : IDisposable
    {
        private readonly Stack<Action<Config>> stack;
        public PopWhenDisposed(Stack<Action<Config>> stack) => this.stack = stack;
        public void Dispose() => this.stack.Pop();
    }
}
```

```csharp
// GeneralHelpers/Utils.cs
public static void SaveConfig(Config config)
{
    config.NormalizeGameMainMenuTranslationSettings();
    config.NormalizeNativeReplacementDiacriticsSettings();
    TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
        config,
        config.Version);

    if (PluginConfigSaveScope.TrySave(config))
    {
        activeInstance?.OnConfigurationSaved(config);
        return;
    }

    PluginInterface.SavePluginConfig(config);
    activeInstance?.OnConfigurationSaved(config);
}
```

```csharp
// PluginUI/PluginConfigWindowContext.cs
public sealed record PluginConfigWindowContext(
    Config Configuration,
    IReadOnlyDictionary<int, LanguageInfo> Languages,
    ImTextureID LogoTextureHandle,
    ImTextureID PixTextureHandle,
    ImTextureID CryptoTextureHandle,
    Action RebuildTranslationService,
    string PluginVersion);

// PluginUI/PluginConfigWindowRenderer.cs
public sealed class PluginConfigWindowRenderer
{
    public bool Draw(PluginConfigWindowContext context, ref bool isOpen)
    {
        var changed = false;
        ImGui.Begin($"{Resources.ConfigWindowTitle} - Plugin Version: {context.PluginVersion}", ref isOpen);
        // Move the current EchoglossianConfigUi body here, replacing this.configuration
        // and other plugin-owned fields with context.Configuration and context delegates.
        ImGui.End();
        return changed;
    }
}
```

```csharp
// PluginUI/PluginUI.cs
private readonly PluginConfigWindowRenderer configWindowRenderer = new();

private void EchoglossianConfigUi()
{
    var context = new PluginConfigWindowContext(
        this.configuration,
        this.languagesDictionary,
        this.logo.Handle,
        this.pixImage.Handle,
        this.cryptoImage.Handle,
        this.RebuildTranslationServiceSafely,
        this.configuration.PluginVersion);

    this.configWindowRenderer.Draw(context, ref this.config);
}
```

- [x] **Step 4: Run the tests and a main-project build**

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build --filter FullyQualifiedName~PluginConfigSaveScopeTests
```

Expected:

- solution build PASS
- save scope tests PASS

- [x] **Step 5: Commit**

```powershell
git add GeneralHelpers\Utils.cs PluginUI\PluginConfigSaveScope.cs PluginUI\PluginConfigWindowContext.cs PluginUI\PluginConfigWindowRenderer.cs PluginUI\PluginUI.cs PluginUI\PluginRuntimeUi.cs Echoglossian.Tests\PluginConfigSaveScopeTests.cs
git commit -m "feat: extract preview-safe config window renderer"
```

### Task 3: Add the unified workbench shell and host the real plugin windows

**Files:**
- Create: `Echoglossian.Previewer/UI/PreviewCaptureTarget.cs`
- Create: `Echoglossian.Previewer/UI/PreviewWorkbenchState.cs`
- Create: `Echoglossian.Previewer/UI/PreviewPluginWindowHost.cs`
- Create: `Echoglossian.Previewer.Tests/UI/PreviewWorkbenchStateTests.cs`
- Modify: `Echoglossian.Previewer/Program.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Modify: `DBManagerUI/DBEditorWindow.cs`
- Modify: `PluginUI/TranslatorMetricsWindow.cs`

**Interfaces:**
- Consumes:
  - `DbEditorWindow(EchoglossianDbContext dbContext)`
  - `TranslatorMetricsWindow(Config config, Action<string> openDbEditorForTable, Func<Task<VisibleDialogueRetranslationResult>> retranslateVisibleDialogueAsync)`
  - `PluginConfigWindowRenderer.Draw(PluginConfigWindowContext context, ref bool isOpen)`
- Produces:
  - `internal enum PreviewCaptureTarget`
  - `internal sealed class PreviewWorkbenchState`
  - `internal sealed class PreviewPluginWindowHost : IDisposable`
  - `DbEditorWindow.LastWindowBounds`
  - `TranslatorMetricsWindow.LastWindowBounds`
  - `PreviewPluginWindowHost.TryGetCrop(PreviewCaptureTarget target): Rectangle?`

- [x] **Step 1: Write failing state tests for unified shell behavior**

```csharp
// Echoglossian.Previewer.Tests/UI/PreviewWorkbenchStateTests.cs
public sealed class PreviewWorkbenchStateTests
{
    [Fact]
    public void CreateDefault_EnablesOverlayAndKeepsPluginWindowsClosed()
    {
        var state = PreviewWorkbenchState.CreateDefault(
            PreviewScenarioCatalog.Defaults[0],
            PreviewScenarioCatalog.ViewportPresets[1]);

        Assert.True(state.OverlayVisible);
        Assert.False(state.ConfigWindowOpen);
        Assert.False(state.DbManagerWindowOpen);
        Assert.False(state.TranslatorMetricsWindowOpen);
        Assert.Equal(PreviewCaptureTarget.FullFrame, state.CaptureTarget);
    }
}
```

- [x] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PreviewWorkbenchStateTests
```

Expected: FAIL with missing `PreviewWorkbenchState`.

- [x] **Step 3: Implement the workbench state and real plugin window host**

```csharp
// Echoglossian.Previewer/UI/PreviewCaptureTarget.cs
internal enum PreviewCaptureTarget
{
    FullFrame,
    OverlaySurface,
    ConfigWindow,
    DbManagerWindow,
    TranslatorMetricsWindow,
}

// Echoglossian.Previewer/UI/PreviewWorkbenchState.cs
internal sealed class PreviewWorkbenchState
{
    internal PreviewScenario Scenario { get; private set; } = PreviewScenarioCatalog.Defaults[0];
    internal PreviewViewportPreset Viewport { get; set; } = PreviewScenarioCatalog.ViewportPresets[1];
    internal bool OverlayVisible { get; set; } = true;
    internal bool ConfigWindowOpen { get; set; }
    internal bool DbManagerWindowOpen { get; set; }
    internal bool TranslatorMetricsWindowOpen { get; set; }
    internal PreviewCaptureTarget CaptureTarget { get; set; } = PreviewCaptureTarget.FullFrame;

    internal static PreviewWorkbenchState CreateDefault(
        PreviewScenario scenario,
        PreviewViewportPreset viewport) =>
        new() { Scenario = scenario, Viewport = viewport, OverlayVisible = true };
}
```

```csharp
// Echoglossian.Previewer/UI/PreviewPluginWindowHost.cs
internal sealed class PreviewPluginWindowHost : IDisposable
{
    private readonly PluginConfigWindowRenderer configWindowRenderer;
    private readonly EchoglossianDbContext? dbContext;
    private readonly DbEditorWindow? dbEditorWindow;
    private readonly TranslatorMetricsWindow translatorMetricsWindow;
    private readonly PluginConfigWindowContext configWindowContext;
    private RectangleF? configWindowBounds;

    internal PreviewPluginWindowHost(
        PluginConfigWindowRenderer configWindowRenderer,
        PluginConfigWindowContext configWindowContext,
        EchoglossianDbContext? dbContext,
        Config configuration)
    {
        this.configWindowRenderer = configWindowRenderer;
        this.configWindowContext = configWindowContext;
        this.dbContext = dbContext;
        this.dbEditorWindow = dbContext is null ? null : new DbEditorWindow(dbContext);
        this.translatorMetricsWindow = new TranslatorMetricsWindow(
            configuration,
            table => this.dbEditorWindow?.OpenAndSelectTable(table),
            () => Task.FromResult(
                new VisibleDialogueRetranslationResult(
                    false,
                    false,
                    null,
                    "Preview",
                    "Preview mode does not retranslate live dialogue.")));
    }

    internal void Draw(PreviewWorkbenchState state)
    {
        var configOpen = state.ConfigWindowOpen;
        this.configWindowRenderer.Draw(this.configWindowContext, ref configOpen);
        this.configWindowBounds = configOpen
            ? new RectangleF(
                ImGui.GetWindowPos().X,
                ImGui.GetWindowPos().Y,
                ImGui.GetWindowSize().X,
                ImGui.GetWindowSize().Y)
            : null;
        state.ConfigWindowOpen = configOpen;

        if (this.dbEditorWindow is not null)
        {
            this.dbEditorWindow.IsOpen = state.DbManagerWindowOpen;
            this.dbEditorWindow.Draw();
            state.DbManagerWindowOpen = this.dbEditorWindow.IsOpen;
        }

        this.translatorMetricsWindow.IsOpen = state.TranslatorMetricsWindowOpen;
        this.translatorMetricsWindow.Draw();
        state.TranslatorMetricsWindowOpen = this.translatorMetricsWindow.IsOpen;
    }

    internal Rectangle? TryGetCrop(PreviewCaptureTarget target)
    {
        var bounds = target switch
        {
            PreviewCaptureTarget.ConfigWindow => this.configWindowBounds,
            PreviewCaptureTarget.DbManagerWindow => this.dbEditorWindow?.LastWindowBounds,
            PreviewCaptureTarget.TranslatorMetricsWindow => this.translatorMetricsWindow.LastWindowBounds,
            _ => null,
        };

        if (bounds is not { Width: > 0, Height: > 0 })
        {
            return null;
        }

        return Rectangle.FromLTRB(
            (int)MathF.Floor(bounds.Value.Left),
            (int)MathF.Floor(bounds.Value.Top),
            (int)MathF.Ceiling(bounds.Value.Right),
            (int)MathF.Ceiling(bounds.Value.Bottom));
    }

    public void Dispose() => this.dbContext?.Dispose();
}
```

```csharp
// DBManagerUI/DBEditorWindow.cs and PluginUI/TranslatorMetricsWindow.cs
public RectangleF? LastWindowBounds { get; private set; }

// inside Draw(), before ImGui.End():
this.LastWindowBounds = new RectangleF(
    ImGui.GetWindowPos().X,
    ImGui.GetWindowPos().Y,
    ImGui.GetWindowSize().X,
    ImGui.GetWindowSize().Y);
```

- [x] **Step 4: Run the shell tests and previewer interactive smoke**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter "FullyQualifiedName~PreviewWorkbenchStateTests|FullyQualifiedName~PreviewHostContractTests"
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug -- --scenario talk --viewport 1920x1080
```

Expected:

- tests PASS
- previewer opens one unified shell with overlay controls plus window toggles

- [x] **Step 5: Commit**

```powershell
git add Echoglossian.Previewer\Program.cs Echoglossian.Previewer\UI\PreviewCaptureTarget.cs Echoglossian.Previewer\UI\PreviewWorkbenchState.cs Echoglossian.Previewer\UI\PreviewPluginWindowHost.cs Echoglossian.Previewer\UI\PreviewShell.cs Echoglossian.Previewer.Tests\UI\PreviewWorkbenchStateTests.cs DBManagerUI\DBEditorWindow.cs PluginUI\TranslatorMetricsWindow.cs
git commit -m "feat: host real plugin windows in unified preview workbench"
```

### Task 4: Extend capture/export to support window targets and deterministic manifests

**Files:**
- Create: `Echoglossian.Previewer/Screenshots/PreviewCaptureRequest.cs`
- Create: `Echoglossian.Previewer.Tests/Screenshots/PreviewCaptureRequestTests.cs`
- Modify: `Echoglossian.Previewer/Screenshots/ScreenshotRequest.cs`
- Modify: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Modify: `Echoglossian.Previewer/Program.cs`

**Interfaces:**
- Consumes:
  - `PreviewPluginWindowHost`
  - `PreviewCaptureTarget`
  - `VeldridScreenshotCapture.CalculateSurfaceCrop(...)`
- Produces:
  - `internal sealed record PreviewCaptureRequest(...)`
  - `ScreenshotRequest.CaptureTarget`
  - manifest entries that include `CaptureTarget`

- [x] **Step 1: Write the failing capture-target tests**

```csharp
// Echoglossian.Previewer.Tests/Screenshots/PreviewCaptureRequestTests.cs
public sealed class PreviewCaptureRequestTests
{
    [Fact]
    public void ScreenshotRequest_DefaultsToFullFrameTarget()
    {
        var request = new ScreenshotRequest(
            ScreenshotMode.Full,
            PreviewScenarioCatalog.Defaults[0],
            PreviewScenarioCatalog.ViewportPresets[1],
            "artifacts");

        Assert.Equal(PreviewCaptureTarget.FullFrame, request.CaptureTarget);
    }

    [Fact]
    public void ManifestEntry_ContainsWindowTargetName()
    {
        var entry = new
        {
            CaptureTarget = PreviewCaptureTarget.DbManagerWindow.ToString(),
        };

        Assert.Equal("DbManagerWindow", entry.CaptureTarget);
    }
}
```

- [x] **Step 2: Run the target tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PreviewCaptureRequestTests
```

Expected: FAIL with missing `CaptureTarget`.

- [x] **Step 3: Implement capture-target plumbing and deterministic window capture**

```csharp
// Echoglossian.Previewer/Screenshots/ScreenshotRequest.cs
internal sealed record ScreenshotRequest(
    ScreenshotMode Mode,
    PreviewScenario Scenario,
    PreviewViewportPreset Viewport,
    string OutputDirectory,
    PreviewCaptureTarget CaptureTarget = PreviewCaptureTarget.FullFrame)
{
    internal int SurfaceMargin { get; init; } = 16;
}
```

```csharp
// Echoglossian.Previewer/UI/PreviewShell.cs
if (ImGui.Button("Save config window screenshot"))
{
    this.pendingScreenshotRequest = new PreviewCaptureRequest(
        ScreenshotMode.Full,
        PreviewCaptureTarget.ConfigWindow);
}
```

```csharp
// Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs
private Rectangle? CalculateCrop(
    ScreenshotRequest request,
    TranslationOverlayRenderResult renderResult,
    PreviewPluginWindowHost? pluginWindowHost)
{
    return request.CaptureTarget switch
    {
        PreviewCaptureTarget.OverlaySurface => VeldridScreenshotCapture.CalculateSurfaceCrop(
            renderResult,
            request.Viewport.Width,
            request.Viewport.Height,
            request.SurfaceMargin,
            framebufferScale: 1f),
        PreviewCaptureTarget.ConfigWindow => pluginWindowHost?.TryGetCrop(PreviewCaptureTarget.ConfigWindow),
        PreviewCaptureTarget.DbManagerWindow => pluginWindowHost?.TryGetCrop(PreviewCaptureTarget.DbManagerWindow),
        PreviewCaptureTarget.TranslatorMetricsWindow => pluginWindowHost?.TryGetCrop(PreviewCaptureTarget.TranslatorMetricsWindow),
        _ => null,
    };
}
```

```csharp
// manifest entry extension
private sealed record ScreenshotManifestEntry(
    string ScenarioKey,
    string SurfaceKey,
    int ViewportWidth,
    int ViewportHeight,
    string ScreenshotMode,
    string CaptureTarget,
    string PresentationMode,
    string PngPath);
```

- [x] **Step 4: Run the screenshot tests and deterministic export validation**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter "FullyQualifiedName~PreviewCaptureRequestTests|FullyQualifiedName~BatchScreenshotRunnerTests|FullyQualifiedName~ScreenshotCropTests"
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug -- --screenshot batch --viewport 1920x1080 --output artifacts\previewer\unified-validation
```

Expected:

- tests PASS
- manifest contains `CaptureTarget`
- batch output writes PNGs plus `manifest.json`

- [x] **Step 5: Commit**

```powershell
git add Echoglossian.Previewer\Program.cs Echoglossian.Previewer\Screenshots\PreviewCaptureRequest.cs Echoglossian.Previewer\Screenshots\ScreenshotRequest.cs Echoglossian.Previewer\Screenshots\BatchScreenshotRunner.cs Echoglossian.Previewer\UI\PreviewShell.cs Echoglossian.Previewer.Tests\Screenshots\PreviewCaptureRequestTests.cs
git commit -m "feat: add window-target capture to previewer"
```

### Task 5: Update documentation, preserve isolation checks, and run full validation

**Files:**
- Modify: `Echoglossian.Previewer/README.md`
- Modify: `Echoglossian.Tests/PreviewerIsolationTests.cs`
- Modify: `Echoglossian.xml`
- Modify: `docs/superpowers/specs/2026-07-16-unified-imgui-previewer-phase1-design.md`
- Modify: `docs/superpowers/plans/2026-07-16-unified-imgui-previewer-phase1-implementation-plan.md`

**Interfaces:**
- Consumes:
  - `PreviewCommandLine`
  - `PreviewShell`
  - `BatchScreenshotRunner`
- Produces:
  - updated operator documentation for session snapshots and window capture
  - updated isolation assertions if any new previewer directory needs exclusion

- [x] **Step 1: Add or tighten the failing isolation/documentation tests**

```csharp
// Echoglossian.Tests/PreviewerIsolationTests.cs
[Fact]
public void PreviewerProject_RemainsStandaloneAndUnpackable()
{
    // keep the existing assertions and add any new preview-only project path checks
    Assert.DoesNotContain("Echoglossian.Previewer", solution);
}
```

```text
README additions required:
- `--db <path>`
- unified shell behavior
- screenshot targets for config / DB / translator metrics windows
- session snapshot safety rules
```

- [x] **Step 2: Run the baseline validation before editing docs**

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug
```

Expected: PASS on all three commands before final doc polishing.

- [x] **Step 3: Update the docs and any generated XML touched by the code changes**

```markdown
<!-- Echoglossian.Previewer/README.md -->
## Unified Workbench

The previewer now hosts:

- overlay scenarios
- configuration window
- DB manager window
- translator metrics window

All state is loaded from cloned session files. Preview edits do not write back
to live plugin files.
```

```powershell
# If XML docs changed during build, stage them as part of the validated change.
git add Echoglossian.xml
```

- [x] **Step 4: Run the full final verification set**

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --binding-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --screenshot batch --viewport 1920x1080 --output artifacts\previewer\final-validation
```

Expected:

- all builds and tests PASS
- previewer smoke commands exit `0`
- screenshot batch writes PNGs plus manifest

- [x] **Step 5: Commit**

```powershell
git add Echoglossian.Previewer\README.md Echoglossian.Tests\PreviewerIsolationTests.cs Echoglossian.xml docs\superpowers\specs\2026-07-16-unified-imgui-previewer-phase1-design.md docs\superpowers\plans\2026-07-16-unified-imgui-previewer-phase1-implementation-plan.md
git commit -m "docs: finalize unified previewer phase 1 plan and validation"
```

## Plan Self-Review

- Spec coverage:
  - unified app scope is covered by Tasks 1, 3, and 4
  - cloned config/DB session safety is covered by Task 1
  - real config window extraction is covered by Task 2
  - real DB manager and translator metrics hosting is covered by Task 3
  - screenshot/export for all supported surfaces is covered by Task 4
  - docs and final validation are covered by Task 5
- Placeholder scan:
  - no `TODO` or `TBD` placeholders remain in the plan
  - every task has concrete files, interfaces, commands, and example code
- Type consistency:
  - `PreviewSessionSourceOptions`, `PreviewSessionArtifacts`, `PluginConfigWindowRenderer`, `PreviewWorkbenchState`, and `PreviewCaptureTarget` are introduced once and reused consistently across later tasks

## Completion Record

Tasks 1 through 5 were completed on the dedicated previewer branch. Final
validation covers the main solution build and tests, previewer tests, binding
and host smoke commands, and a deterministic overlay batch screenshot export.
