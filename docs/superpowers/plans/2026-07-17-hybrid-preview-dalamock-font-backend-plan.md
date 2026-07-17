# Hybrid Previewer DalaMock Font Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a hybrid plugin-window backend to `Echoglossian.Previewer` so `Config`, `DB Manager`, and `Translator Metrics / Debugger` can run through either the current standalone host or a DalaMock-hosted runtime, with `auto` fallback and no regression to plugin build, packaging, or normal runtime behavior.

**Architecture:** Keep overlays on the current preview-only rendering path and split plugin-window hosting behind an explicit backend interface. Reuse the current standalone preview window host as one backend, extract a reusable DalaMock bootstrap from `Echoglossian.Mock` as the second backend, and make the shell plus CLI own the requested/effective backend state, fallback reason, and screenshot manifest metadata.

**Tech Stack:** .NET 10 Windows, `Dalamud.Bindings.ImGui`, `Echoglossian.Previewer`, `Echoglossian.Mock`, DalaMock, xUnit, Newtonsoft.Json, Microsoft.Data.Sqlite.

## Global Constraints

- keep one previewer shell, not multiple windows or separate tools
- apply the new hosted path to real plugin windows only
- include all three real plugin windows in scope:
  - `Config`
  - `DB Manager`
  - `Translator Metrics / Debugger`
- allow interaction with cloned session state, not live user state
- expose backend choice both in CLI and in the previewer shell
- default behavior should try the hosted backend and fall back automatically
- fallback must remain visible and diagnosable, not silent
- keep the previewer outside `Echoglossian.sln`
- keep previewer dependencies out of normal plugin packaging
- keep `Echoglossian.Mock` and `Echoglossian.Mock.Tests` as local-only development/runtime complements
- preserve the current plugin entrypoint and release path
- all preview edits continue to target cloned session config and cloned session DB state only
- network or provider actions remain blocked in preview mode
- any runtime service that cannot be provided safely under the hosted backend must be stubbed, redirected, or cause a backend fallback
- one shell remains the only user-facing preview control surface
- backend choice must be visible in both CLI and shell
- fallback cannot silently degrade fidelity
- no preview of native game UI or native addon mutation
- no conversion of the entire previewer to a DalaMock-first architecture
- no removal of the current standalone plugin-window backend
- no change to plugin packaging or release flows
- no broad plugin dependency-injection rewrite
- no automatic write-back from preview session clones into the user's live config or DB
- no promise that overlay rendering will move to DalaMock in this slice
- keep the plugin path safe: if a shared production file needs to change, the smallest safe change wins, and production behavior must remain the same when the previewer is not running

---

## File Structure

### Previewer backend contract and shell selection

- Create: `Echoglossian.Previewer/PluginWindows/PluginWindowPreviewBackendMode.cs`
  - owns the requested mode enum: `Auto`, `Standalone`, `DalaMockHosted`
- Create: `Echoglossian.Previewer/PluginWindows/PluginWindowBackendStatus.cs`
  - owns requested mode, effective mode, availability, and fallback reason
- Create: `Echoglossian.Previewer/PluginWindows/IPluginWindowPreviewBackend.cs`
  - narrow backend contract for initialization, drawing, bounds, and cleanup
- Create: `Echoglossian.Previewer/PluginWindows/PluginWindowPreviewBackendFactory.cs`
  - creates `Standalone` or `DalaMockHosted` backends and applies `Auto` fallback
- Modify: `Echoglossian.Previewer/Hosting/PreviewCommandLine.cs`
  - parses `--plugin-window-backend`
- Modify: `Echoglossian.Previewer/Program.cs`
  - carries requested backend mode into session startup
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
  - exposes backend mode selector and diagnostics in the shell

### Standalone backend extraction

- Create: `Echoglossian.Previewer/PluginWindows/StandalonePluginWindowPreviewBackend.cs`
  - wraps the current preview-owned plugin-window behavior
- Modify: `Echoglossian.Previewer/UI/PreviewPluginWindowHost.cs`
  - either shrink into a helper owned by the standalone backend or disappear entirely
- Modify: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
  - switch plugin-window capture calls to backend-provided bounds

### Reusable hosted DalaMock session

- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginOptions.cs`
  - explicit config/db/state-root inputs for hosted preview sessions
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginSession.cs`
  - boots the real plugin under DalaMock and owns cleanup
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginSessionFactory.cs`
  - central shared bootstrap used by manual runner, tests, and previewer
- Modify: `Echoglossian.Mock/Program.cs`
  - reuse the new shared hosted bootstrap instead of open-coding startup
- Modify: `Echoglossian.Mock.Tests/TestBoot.cs`
  - reuse the new shared hosted bootstrap instead of open-coding startup
- Modify: `Echoglossian.Mock.Tests/Echoglossian.Mock.Tests.csproj`
  - add a project reference to `..\Echoglossian.Mock\Echoglossian.Mock.csproj`

### Hosted preview backend integration

- Create: `Echoglossian.Previewer/PluginWindows/DalaMockHostedPluginWindowPreviewBackend.cs`
  - adapts a hosted DalaMock session to the previewer backend contract
- Modify: `Echoglossian.Previewer/Program.cs`
  - create preview-owned hosted session inputs from cloned config/db state
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
  - add restart/fallback diagnostics for hosted window backend
- Modify: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
  - include requested/effective backend metadata in manifest generation

### Tests and docs

- Create: `Echoglossian.Previewer.Tests/PluginWindows/PreviewCommandLineBackendModeTests.cs`
- Create: `Echoglossian.Previewer.Tests/PluginWindows/PluginWindowPreviewBackendFactoryTests.cs`
- Create: `Echoglossian.Previewer.Tests/PluginWindows/PreviewShellBackendStatusTests.cs`
- Create: `Echoglossian.Previewer.Tests/Screenshots/BatchScreenshotRunnerBackendManifestTests.cs`
- Create: `Echoglossian.Mock.Tests/HostedPreviewPluginSessionTests.cs`
- Modify: `Echoglossian.Previewer/README.md`
  - document CLI + shell backend selection and fallback behavior
- Modify: `docs/handoffs/unified-imgui-previewer-next-step.md`
  - record implementation outcome and next debt

### Shared production files that may need narrow changes

- Modify only if required: `PluginUI/PluginConfigWindowContext.cs`
  - preserve production behavior while allowing hosted runtime closures when running under preview
- Modify only if required: `Echoglossian.xml`
  - commit if regenerated by validated builds

---

### Task 1: Add Plugin-Window Backend Mode Contract And CLI/Shell State

**Files:**
- Create: `Echoglossian.Previewer/PluginWindows/PluginWindowPreviewBackendMode.cs`
- Create: `Echoglossian.Previewer/PluginWindows/PluginWindowBackendStatus.cs`
- Modify: `Echoglossian.Previewer/Hosting/PreviewCommandLine.cs`
- Modify: `Echoglossian.Previewer/Program.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/PreviewCommandLineBackendModeTests.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/PreviewShellBackendStatusTests.cs`

**Interfaces:**
- Consumes:
  - `PreviewCommandLine.Parse(string[] args) : PreviewCommandLine`
  - `PreviewShell.GetRuntimeRestartWarning(Config configuration, int appliedLanguageId, int appliedFontSize) : string?`
- Produces:
  - `internal enum PluginWindowPreviewBackendMode`
  - `internal sealed record PluginWindowBackendStatus(PluginWindowPreviewBackendMode RequestedMode, PluginWindowPreviewBackendMode EffectiveMode, bool HostedRequested, bool HostedAvailable, string? FallbackReason)`
  - `PreviewCommandLine.PluginWindowBackendMode : PluginWindowPreviewBackendMode`

- [ ] **Step 1: Write the failing CLI parse tests**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

public sealed class PreviewCommandLineBackendModeTests
{
    [Theory]
    [InlineData("auto", PluginWindowPreviewBackendMode.Auto)]
    [InlineData("standalone", PluginWindowPreviewBackendMode.Standalone)]
    [InlineData("dalamock", PluginWindowPreviewBackendMode.DalaMockHosted)]
    public void Parse_plugin_window_backend_mode_maps_known_values(
        string rawValue,
        PluginWindowPreviewBackendMode expectedMode)
    {
        var commandLine = PreviewCommandLine.Parse(
            ["--plugin-window-backend", rawValue]);

        commandLine.PluginWindowBackendMode.Should().Be(expectedMode);
    }

    [Fact]
    public void Parse_plugin_window_backend_mode_rejects_unknown_values()
    {
        Action act = () => PreviewCommandLine.Parse(
            ["--plugin-window-backend", "bogus"]);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*plugin window backend*");
    }
}
```

- [ ] **Step 2: Write the failing shell-status tests**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

public sealed class PreviewShellBackendStatusTests
{
    [Fact]
    public void Backend_status_reports_no_fallback_when_requested_and_effective_match()
    {
        var status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Standalone,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: false,
            HostedAvailable: true,
            FallbackReason: null);

        status.FallbackReason.Should().BeNull();
        status.EffectiveMode.Should().Be(PluginWindowPreviewBackendMode.Standalone);
    }

    [Fact]
    public void Backend_status_retains_fallback_reason_when_auto_downgrades()
    {
        var status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Auto,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: true,
            HostedAvailable: false,
            FallbackReason: "DalaMock initialization failed");

        status.HostedRequested.Should().BeTrue();
        status.HostedAvailable.Should().BeFalse();
        status.FallbackReason.Should().Be("DalaMock initialization failed");
    }
}
```

- [ ] **Step 3: Run the new tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter "FullyQualifiedName~PreviewCommandLineBackendModeTests|FullyQualifiedName~PreviewShellBackendStatusTests"
```

Expected: FAIL because `PluginWindowPreviewBackendMode`, `PluginWindowBackendStatus`, and the CLI option do not exist yet.

- [ ] **Step 4: Add the backend mode enum and status record**

```csharp
namespace Echoglossian.Previewer.PluginWindows;

internal enum PluginWindowPreviewBackendMode
{
    Auto,
    Standalone,
    DalaMockHosted,
}

internal sealed record PluginWindowBackendStatus(
    PluginWindowPreviewBackendMode RequestedMode,
    PluginWindowPreviewBackendMode EffectiveMode,
    bool HostedRequested,
    bool HostedAvailable,
    string? FallbackReason);
```

- [ ] **Step 5: Extend CLI parsing with the new option**

```csharp
internal static PluginWindowPreviewBackendMode ParsePluginWindowBackendMode(string? rawValue)
{
    return rawValue?.Trim().ToLowerInvariant() switch
    {
        null or "" => PluginWindowPreviewBackendMode.Auto,
        "auto" => PluginWindowPreviewBackendMode.Auto,
        "standalone" => PluginWindowPreviewBackendMode.Standalone,
        "dalamock" => PluginWindowPreviewBackendMode.DalaMockHosted,
        _ => throw new ArgumentException(
            $"Preview plugin window backend '{rawValue}' is invalid. Use auto, standalone, or dalamock."),
    };
}
```

- [ ] **Step 6: Add shell-visible backend status fields without changing runtime behavior yet**

```csharp
private PluginWindowBackendStatus pluginWindowBackendStatus = new(
    PluginWindowPreviewBackendMode.Auto,
    PluginWindowPreviewBackendMode.Standalone,
    HostedRequested: true,
    HostedAvailable: false,
    FallbackReason: "Backend selection is not initialized.");

internal void SetPluginWindowBackendStatus(PluginWindowBackendStatus status)
{
    this.pluginWindowBackendStatus = status ??
        throw new ArgumentNullException(nameof(status));
}
```

- [ ] **Step 7: Render backend status in the shell fidelity summary**

```csharp
ImGui.TextUnformatted(
    $"Plugin window backend: requested={this.pluginWindowBackendStatus.RequestedMode}, effective={this.pluginWindowBackendStatus.EffectiveMode}");

if (!string.IsNullOrWhiteSpace(this.pluginWindowBackendStatus.FallbackReason))
{
    ImGui.TextWrapped(
        $"Plugin window backend fallback: {this.pluginWindowBackendStatus.FallbackReason}");
}
```

- [ ] **Step 8: Re-run the focused tests to verify they pass**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter "FullyQualifiedName~PreviewCommandLineBackendModeTests|FullyQualifiedName~PreviewShellBackendStatusTests"
```

Expected: PASS.

- [ ] **Step 9: Commit**

```powershell
git add Echoglossian.Previewer\Hosting\PreviewCommandLine.cs Echoglossian.Previewer\Program.cs Echoglossian.Previewer\UI\PreviewShell.cs Echoglossian.Previewer\PluginWindows Echoglossian.Previewer.Tests\PluginWindows
git commit -m "feat: add preview plugin-window backend selection contract"
```

### Task 2: Extract The Current Plugin-Window Host Into An Explicit Standalone Backend

**Files:**
- Create: `Echoglossian.Previewer/PluginWindows/IPluginWindowPreviewBackend.cs`
- Create: `Echoglossian.Previewer/PluginWindows/StandalonePluginWindowPreviewBackend.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewPluginWindowHost.cs`
- Modify: `Echoglossian.Previewer/Program.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Modify: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/StandalonePluginWindowPreviewBackendTests.cs`

**Interfaces:**
- Consumes:
  - `PreviewPluginWindowHost.TryGetStableCrop(PreviewCaptureTarget target) : Rectangle?`
  - `PreviewPluginWindowHost.Draw(PreviewWorkbenchState state) : void`
- Produces:
  - `internal interface IPluginWindowPreviewBackend : IDisposable`
  - `void Draw(PreviewWorkbenchState state)`
  - `Rectangle? TryGetStableCrop(PreviewCaptureTarget target)`
  - `PluginWindowBackendStatus Status { get; }`

- [ ] **Step 1: Write the failing standalone backend test**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

public sealed class StandalonePluginWindowPreviewBackendTests
{
    [Fact]
    public void Standalone_backend_reports_standalone_status()
    {
        using var backend = StandalonePluginWindowPreviewBackend.CreateForTests(
            dbManagerAvailable: true);

        backend.Status.RequestedMode.Should().Be(PluginWindowPreviewBackendMode.Standalone);
        backend.Status.EffectiveMode.Should().Be(PluginWindowPreviewBackendMode.Standalone);
        backend.Status.HostedAvailable.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the standalone backend test to verify it fails**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~StandalonePluginWindowPreviewBackendTests
```

Expected: FAIL because the backend interface and standalone backend do not exist yet.

- [ ] **Step 3: Define the backend interface**

```csharp
namespace Echoglossian.Previewer.PluginWindows;

internal interface IPluginWindowPreviewBackend : IDisposable
{
    PluginWindowBackendStatus Status { get; }

    bool DbManagerAvailable { get; }

    bool CaptureFailed { get; }

    void Draw(PreviewWorkbenchState state);

    void BeginCapture(PreviewCaptureTarget target);

    void EndCapture();

    Rectangle? TryGetStableCrop(PreviewCaptureTarget target);
}
```

- [ ] **Step 4: Wrap the current host in a standalone backend**

```csharp
internal sealed class StandalonePluginWindowPreviewBackend : IPluginWindowPreviewBackend
{
    private readonly PreviewPluginWindowHost host;

    private StandalonePluginWindowPreviewBackend(PreviewPluginWindowHost host)
    {
        this.host = host;
        this.Status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Standalone,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: false,
            HostedAvailable: true,
            FallbackReason: null);
    }

    public PluginWindowBackendStatus Status { get; }

    public bool DbManagerAvailable => this.host.DbManagerAvailable;

    public bool CaptureFailed => this.host.CaptureFailed;

    public void Draw(PreviewWorkbenchState state) => this.host.Draw(state);

    public void BeginCapture(PreviewCaptureTarget target) => this.host.BeginCapture(target);

    public void EndCapture() => this.host.EndCapture();

    public Rectangle? TryGetStableCrop(PreviewCaptureTarget target) =>
        this.host.TryGetStableCrop(target);

    public void Dispose() => this.host.Dispose();
}
```

- [ ] **Step 5: Add a small test-only constructor or factory**

```csharp
internal static StandalonePluginWindowPreviewBackend CreateForTests(bool dbManagerAvailable)
{
    return new StandalonePluginWindowPreviewBackend(
        PreviewPluginWindowHost.CreateForTests(dbManagerAvailable));
}
```

- [ ] **Step 6: Switch `Program` and `PreviewShell` to consume the interface**

```csharp
using var pluginWindowBackend = CreateStandalonePluginWindowPreviewBackend(
    editableConfiguration,
    languages,
    session.ClonedDatabasePath);

shell.SetPluginWindowBackendStatus(pluginWindowBackend.Status);
pluginWindowBackend.Draw(this.workbenchState);
```

- [ ] **Step 7: Route screenshot capture through the backend interface**

```csharp
if (PreviewPluginWindowHost.IsPluginWindowTarget(pendingRequest.CaptureTarget))
{
    if (this.pluginWindowBackend.CaptureFailed)
    {
        ...
    }

    if (this.pluginWindowBackend.TryGetStableCrop(
            pendingRequest.CaptureTarget) is null)
    {
        ...
    }
}
```

- [ ] **Step 8: Re-run the standalone backend test**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~StandalonePluginWindowPreviewBackendTests
```

Expected: PASS.

- [ ] **Step 9: Commit**

```powershell
git add Echoglossian.Previewer\PluginWindows Echoglossian.Previewer\Program.cs Echoglossian.Previewer\UI\PreviewShell.cs Echoglossian.Previewer\UI\PreviewPluginWindowHost.cs Echoglossian.Previewer\Screenshots\BatchScreenshotRunner.cs Echoglossian.Previewer.Tests\PluginWindows\StandalonePluginWindowPreviewBackendTests.cs
git commit -m "refactor: extract standalone preview plugin-window backend"
```

### Task 3: Extract A Reusable DalaMock Hosted Session Without Changing Plugin Runtime Behavior

**Files:**
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginOptions.cs`
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginSession.cs`
- Create: `Echoglossian.Mock/Hosting/HostedPreviewPluginSessionFactory.cs`
- Modify: `Echoglossian.Mock/Program.cs`
- Modify: `Echoglossian.Mock.Tests/Echoglossian.Mock.Tests.csproj`
- Modify: `Echoglossian.Mock.Tests/TestBoot.cs`
- Create: `Echoglossian.Mock.Tests/HostedPreviewPluginSessionTests.cs`

**Interfaces:**
- Consumes:
  - `MockContainer`
  - `PluginLoadSettings`
  - `global::Echoglossian.Echoglossian`
  - `StartedPlugin`
- Produces:
  - `public sealed record HostedPreviewPluginOptions(DirectoryInfo StateRoot, DirectoryInfo PluginSavePath, FileInfo ConfigPath, string? DatabasePath, bool CreateWindow)`
  - `public sealed class HostedPreviewPluginSession : IDisposable`
  - `public static Task<HostedPreviewPluginSession> StartAsync(HostedPreviewPluginOptions options, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Write the failing hosted-session smoke test**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Mock.Tests;

public sealed class HostedPreviewPluginSessionTests
{
    [Fact]
    public async Task StartAsync_uses_explicit_preview_owned_paths()
    {
        using var fixture = PreviewOwnedHostedSessionFixture.Create();

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            fixture.Options);

        session.StateRoot.FullName.Should().Be(fixture.Options.StateRoot.FullName);
        session.PluginSavePath.FullName.Should().Be(fixture.Options.PluginSavePath.FullName);
        session.ConfigPath.FullName.Should().Be(fixture.Options.ConfigPath.FullName);
    }
}
```

- [ ] **Step 2: Run the hosted-session smoke test to verify it fails**

Run:

```powershell
dotnet restore Echoglossian.Mock\Echoglossian.Mock.csproj
dotnet restore Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --filter FullyQualifiedName~HostedPreviewPluginSessionTests
```

Expected: FAIL because the hosted session types do not exist yet.

- [ ] **Step 3: Add the shared hosted options and session types**

```csharp
namespace Echoglossian.Mock.Hosting;

public sealed record HostedPreviewPluginOptions(
    DirectoryInfo StateRoot,
    DirectoryInfo PluginSavePath,
    FileInfo ConfigPath,
    string? DatabasePath,
    bool CreateWindow);

public sealed class HostedPreviewPluginSession : IAsyncDisposable, IDisposable
{
    public HostedPreviewPluginSession(
        MockContainer container,
        global::Echoglossian.Echoglossian plugin,
        DirectoryInfo stateRoot,
        DirectoryInfo pluginSavePath,
        FileInfo configPath)
    {
        this.Container = container;
        this.Plugin = plugin;
        this.StateRoot = stateRoot;
        this.PluginSavePath = pluginSavePath;
        this.ConfigPath = configPath;
    }

    public MockContainer Container { get; }
    public global::Echoglossian.Echoglossian Plugin { get; }
    public DirectoryInfo StateRoot { get; }
    public DirectoryInfo PluginSavePath { get; }
    public FileInfo ConfigPath { get; }
}
```

- [ ] **Step 4: Move DalaMock plugin bootstrap into a factory shared by runner and tests**

```csharp
public static class HostedPreviewPluginSessionFactory
{
    public static async Task<HostedPreviewPluginSession> StartAsync(
        HostedPreviewPluginOptions options,
        CancellationToken cancellationToken = default)
    {
        var container = new MockContainer(
            new MockDalamudConfiguration
            {
                CreateWindow = options.CreateWindow,
                GamePath = ResolveSqpackDirectory(),
                PluginSavePath = options.PluginSavePath,
            },
            builder => { },
            [],
            false);

        var loader = container.GetPluginLoader();
        var mockPlugin = loader.AddPlugin(typeof(global::Echoglossian.Echoglossian));
        var settings = new PluginLoadSettings(options.StateRoot, options.ConfigPath)
        {
            AssemblyLocation = typeof(global::Echoglossian.Echoglossian).Assembly.Location,
        };

        await loader.StartPlugin(mockPlugin, settings);
        ...
    }
}
```

- [ ] **Step 5: Make `Echoglossian.Mock/Program.cs` use the shared factory**

```csharp
await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
    new HostedPreviewPluginOptions(
        stateRoot,
        pluginSavePath,
        configPath,
        DatabasePath: null,
        CreateWindow: true));

session.Container.GetMockUi().Run();
```

- [ ] **Step 6: Make `TestBoot` use the shared factory instead of re-implementing startup**

```csharp
await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
    new HostedPreviewPluginOptions(
        stateRoot,
        pluginSavePath,
        configPath,
        DatabasePath: null,
        CreateWindow: false));

return new StartedPlugin(
    session.Container,
    session.Plugin,
    stateRoot,
    pluginSavePath,
    configPath);
```

- [ ] **Step 7: Re-run the hosted-session smoke test**

Run:

```powershell
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --filter FullyQualifiedName~HostedPreviewPluginSessionTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Mock Echoglossian.Mock.Tests
git commit -m "refactor: extract reusable DalaMock hosted preview session"
```

### Task 4: Add The DalaMock Hosted Preview Backend With Safe `Auto` Fallback

**Files:**
- Create: `Echoglossian.Previewer/PluginWindows/DalaMockHostedPluginWindowPreviewBackend.cs`
- Create: `Echoglossian.Previewer/PluginWindows/PluginWindowPreviewBackendFactory.cs`
- Modify: `Echoglossian.Previewer/Echoglossian.Previewer.csproj`
- Modify: `Echoglossian.Previewer/Program.cs`
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/PluginWindowPreviewBackendFactoryTests.cs`
- Test: `Echoglossian.Mock.Tests/HostedPreviewPluginSessionTests.cs`

**Interfaces:**
- Consumes:
  - `HostedPreviewPluginSessionFactory.StartAsync(...)`
  - `IPluginWindowPreviewBackend`
  - `PluginWindowPreviewBackendMode`
- Produces:
  - `internal sealed class DalaMockHostedPluginWindowPreviewBackend : IPluginWindowPreviewBackend`
  - `internal static class PluginWindowPreviewBackendFactory`
  - `Task<(IPluginWindowPreviewBackend Backend, PluginWindowBackendStatus Status)> CreateAsync(...)`

- [ ] **Step 1: Write the failing backend factory tests**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.PluginWindows;

public sealed class PluginWindowPreviewBackendFactoryTests
{
    [Fact]
    public async Task CreateAsync_auto_falls_back_to_standalone_when_hosted_boot_fails()
    {
        var result = await PluginWindowPreviewBackendFactory.CreateForTestsAsync(
            PluginWindowPreviewBackendMode.Auto,
            static () => throw new InvalidOperationException("synthetic hosted failure"));

        result.Status.EffectiveMode.Should().Be(PluginWindowPreviewBackendMode.Standalone);
        result.Status.FallbackReason.Should().Contain("synthetic hosted failure");
    }

    [Fact]
    public async Task CreateAsync_dalamock_does_not_silently_fallback_when_hosted_boot_fails()
    {
        Func<Task> act = async () => await PluginWindowPreviewBackendFactory.CreateForTestsAsync(
            PluginWindowPreviewBackendMode.DalaMockHosted,
            static () => throw new InvalidOperationException("synthetic hosted failure"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*synthetic hosted failure*");
    }
}
```

- [ ] **Step 2: Run the backend factory tests to verify they fail**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PluginWindowPreviewBackendFactoryTests
```

Expected: FAIL because the factory and hosted backend do not exist yet.

- [ ] **Step 3: Reference `Echoglossian.Mock` from the previewer project**

```xml
<ItemGroup>
  <ProjectReference Include="..\Echoglossian.Mock\Echoglossian.Mock.csproj" />
</ItemGroup>
```

- [ ] **Step 4: Implement the hosted backend over the shared hosted session**

```csharp
internal sealed class DalaMockHostedPluginWindowPreviewBackend : IPluginWindowPreviewBackend
{
    private readonly HostedPreviewPluginSession session;
    private readonly StandalonePluginWindowPreviewBackend fallbackRenderer;

    public DalaMockHostedPluginWindowPreviewBackend(
        HostedPreviewPluginSession session,
        StandalonePluginWindowPreviewBackend fallbackRenderer)
    {
        this.session = session;
        this.fallbackRenderer = fallbackRenderer;
        this.Status = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.DalaMockHosted,
            PluginWindowPreviewBackendMode.DalaMockHosted,
            HostedRequested: true,
            HostedAvailable: true,
            FallbackReason: null);
    }

    public PluginWindowBackendStatus Status { get; }
    ...
}
```

- [ ] **Step 5: Implement `Auto` fallback logic in the backend factory**

```csharp
internal static async Task<(IPluginWindowPreviewBackend Backend, PluginWindowBackendStatus Status)> CreateAsync(
    PluginWindowPreviewBackendMode requestedMode,
    Func<Task<DalaMockHostedPluginWindowPreviewBackend>> createHostedBackend,
    Func<IPluginWindowPreviewBackend> createStandaloneBackend)
{
    if (requestedMode == PluginWindowPreviewBackendMode.Standalone)
    {
        var backend = createStandaloneBackend();
        return (backend, backend.Status);
    }

    try
    {
        var hostedBackend = await createHostedBackend();
        return (hostedBackend, hostedBackend.Status);
    }
    catch (Exception ex) when (requestedMode == PluginWindowPreviewBackendMode.Auto)
    {
        var standaloneBackend = createStandaloneBackend();
        var fallbackStatus = new PluginWindowBackendStatus(
            PluginWindowPreviewBackendMode.Auto,
            PluginWindowPreviewBackendMode.Standalone,
            HostedRequested: true,
            HostedAvailable: false,
            FallbackReason: ex.Message);
        return (standaloneBackend, fallbackStatus);
    }
}
```

- [ ] **Step 6: Thread the selected backend mode into `Program` startup**

```csharp
var backendCreation = await PluginWindowPreviewBackendFactory.CreateAsync(
    commandLine.PluginWindowBackendMode,
    () => CreateDalaMockHostedPluginWindowBackendAsync(
        editableConfiguration,
        languages,
        session),
    () => CreateStandalonePluginWindowPreviewBackend(
        editableConfiguration,
        languages,
        session.ClonedDatabasePath));
```

- [ ] **Step 7: Re-run the backend factory tests**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~PluginWindowPreviewBackendFactoryTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Previewer\Echoglossian.Previewer.csproj Echoglossian.Previewer\Program.cs Echoglossian.Previewer\PluginWindows Echoglossian.Previewer.Tests\PluginWindows Echoglossian.Mock Echoglossian.Mock.Tests
git commit -m "feat: add DalaMock hosted preview plugin-window backend"
```

### Task 5: Surface Hosted/Effective Backend In The Shell And Screenshot Manifest

**Files:**
- Modify: `Echoglossian.Previewer/UI/PreviewShell.cs`
- Modify: `Echoglossian.Previewer/Hosting/PreviewCommandLine.cs`
- Modify: `Echoglossian.Previewer/Screenshots/BatchScreenshotRunner.cs`
- Modify: `Echoglossian.Previewer/Screenshots/ScreenshotManifest.cs` or the current manifest-owning type
- Test: `Echoglossian.Previewer.Tests/Screenshots/BatchScreenshotRunnerBackendManifestTests.cs`
- Test: `Echoglossian.Previewer.Tests/PluginWindows/PreviewShellBackendStatusTests.cs`

**Interfaces:**
- Consumes:
  - `PluginWindowBackendStatus`
  - `IPluginWindowPreviewBackend.TryGetStableCrop(PreviewCaptureTarget target) : Rectangle?`
- Produces:
  - manifest fields for requested/effective plugin-window backend
  - shell UI controls for selecting backend mode interactively

- [ ] **Step 1: Write the failing manifest test**

```csharp
using FluentAssertions;
using Xunit;

namespace Echoglossian.Previewer.Tests.Screenshots;

public sealed class BatchScreenshotRunnerBackendManifestTests
{
    [Fact]
    public void SerializeManifest_plugin_window_capture_records_requested_and_effective_backend()
    {
        var manifest = BatchScreenshotRunner.CreateManifestForTests(
            captureTarget: PreviewCaptureTarget.ConfigWindow,
            requestedBackend: PluginWindowPreviewBackendMode.Auto,
            effectiveBackend: PluginWindowPreviewBackendMode.Standalone,
            fallbackReason: "DalaMock initialization failed");

        manifest.RequestedPluginWindowBackend.Should().Be("Auto");
        manifest.EffectivePluginWindowBackend.Should().Be("Standalone");
        manifest.PluginWindowBackendFallbackReason.Should().Be("DalaMock initialization failed");
    }
}
```

- [ ] **Step 2: Run the manifest test to verify it fails**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~BatchScreenshotRunnerBackendManifestTests
```

Expected: FAIL because the manifest fields do not exist yet.

- [ ] **Step 3: Add backend fields to the screenshot manifest model**

```csharp
internal sealed record ScreenshotManifest(
    ...,
    string RequestedPluginWindowBackend,
    string EffectivePluginWindowBackend,
    string? PluginWindowBackendFallbackReason);
```

- [ ] **Step 4: Populate manifest backend fields from the active plugin-window backend status**

```csharp
var manifest = new ScreenshotManifest(
    ...,
    RequestedPluginWindowBackend: backendStatus.RequestedMode.ToString(),
    EffectivePluginWindowBackend: backendStatus.EffectiveMode.ToString(),
    PluginWindowBackendFallbackReason: backendStatus.FallbackReason);
```

- [ ] **Step 5: Add interactive shell control for backend mode selection**

```csharp
if (ImGui.BeginCombo(
        "Plugin window backend",
        this.requestedPluginWindowBackendMode.ToString()))
{
    DrawBackendSelectable(PluginWindowPreviewBackendMode.Auto);
    DrawBackendSelectable(PluginWindowPreviewBackendMode.Standalone);
    DrawBackendSelectable(PluginWindowPreviewBackendMode.DalaMockHosted);
    ImGui.EndCombo();
}
```

- [ ] **Step 6: Add explicit hosted restart warning text**

```csharp
if (this.requestedPluginWindowBackendMode != this.pluginWindowBackendStatus.EffectiveMode)
{
    ImGui.TextWrapped(
        $"Plugin window backend is running as {this.pluginWindowBackendStatus.EffectiveMode}. Requested mode was {this.pluginWindowBackendStatus.RequestedMode}.");
}
```

- [ ] **Step 7: Re-run the manifest test**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --filter FullyQualifiedName~BatchScreenshotRunnerBackendManifestTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Previewer\UI\PreviewShell.cs Echoglossian.Previewer\Hosting\PreviewCommandLine.cs Echoglossian.Previewer\Screenshots Echoglossian.Previewer.Tests\Screenshots Echoglossian.Previewer.Tests\PluginWindows\PreviewShellBackendStatusTests.cs
git commit -m "feat: surface preview plugin-window backend diagnostics"
```

### Task 6: Validate End-To-End Safety, Update Docs, And Lock In The No-Plugin-Break Regression Guard

**Files:**
- Modify: `Echoglossian.Previewer/README.md`
- Modify: `docs/handoffs/unified-imgui-previewer-next-step.md`
- Modify: `docs/superpowers/specs/2026-07-17-preview-hybrid-dalamock-font-backend-design.md` only if the implementation changes the approved design materially
- Modify: `Echoglossian.xml` if regenerated

**Interfaces:**
- Consumes:
  - all prior task outputs
- Produces:
  - updated operator docs for CLI and shell backend selection
  - recorded validation evidence and remaining debt

- [ ] **Step 1: Document CLI and shell backend selection in the previewer README**

```md
### Plugin window backend selection

Use `--plugin-window-backend auto|standalone|dalamock` to choose how
`Config`, `DB Manager`, and `Translator Metrics / Debugger` are hosted.

- `auto`: try DalaMock first, then fall back to standalone
- `standalone`: always use the previewer's existing plugin-window runtime
- `dalamock`: require the DalaMock-hosted runtime
```

- [ ] **Step 2: Update the handoff with what shipped and what still remains**

```md
## Hosted plugin-window backend

Phase 2 added a hybrid plugin-window backend with:

- CLI and shell selection for `auto`, `standalone`, and `dalamock`
- DalaMock-hosted runtime for `Config`, `DB Manager`, and `Translator Metrics / Debugger`
- explicit fallback diagnostics in the shell and screenshot manifest
```

- [ ] **Step 3: Run the production-safe build validation**

Run:

```powershell
dotnet build Echoglossian.sln -c Debug --no-restore
```

Expected: PASS with 0 errors. Previewer and mock projects must remain outside the solution.

- [ ] **Step 4: Run the main production test suite**

Run:

```powershell
dotnet test Echoglossian.Tests\Echoglossian.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

Expected: PASS. Any failure here is a plugin regression and blocks completion.

- [ ] **Step 5: Run the previewer tests**

Run:

```powershell
dotnet test Echoglossian.Previewer.Tests\Echoglossian.Previewer.Tests.csproj -c Debug --no-restore -p:VSTestMaxCpuCount=1
```

Expected: PASS.

- [ ] **Step 6: Run the DalaMock rail**

Run:

```powershell
dotnet restore Echoglossian.Mock\Echoglossian.Mock.csproj
dotnet restore Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj
dotnet build Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-restore
dotnet test Echoglossian.Mock.Tests\Echoglossian.Mock.Tests.csproj -c Debug --no-build -p:VSTestMaxCpuCount=1
```

Expected: PASS.

- [ ] **Step 7: Run the previewer host smoke and one hosted-backend smoke path**

Run:

```powershell
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --host-smoke
dotnet run --project Echoglossian.Previewer\Echoglossian.Previewer.csproj -c Debug --no-build -- --plugin-window-backend auto --screenshot full --capture-target config-window --scenario talk --viewport 1920x1080 --output artifacts\previewer\hosted-backend-validation
```

Expected: host smoke exits `0`; screenshot command writes `manifest.json` and one PNG, and the manifest records the requested/effective backend.

- [ ] **Step 8: Commit**

```powershell
git add Echoglossian.Previewer\README.md docs\handoffs\unified-imgui-previewer-next-step.md docs\superpowers\specs\2026-07-17-preview-hybrid-dalamock-font-backend-design.md Echoglossian.xml
git commit -m "docs: validate hybrid previewer hosted backend workflow"
```

## Self-Review

### Spec Coverage

- shell stays single and overlays stay separate: Tasks 1, 2, 4, and 5
- all three real plugin windows in scope: Tasks 2, 4, and 5
- DalaMock-hosted runtime over cloned state only: Tasks 3 and 4
- CLI and shell backend selection: Tasks 1 and 5
- explicit `auto` fallback and non-silent diagnostics: Tasks 4 and 5
- screenshot manifest backend metadata: Task 5
- no-plugin-break safety net and full validation: Task 6

No spec requirement is left without a task.

### Placeholder Scan

- No `TBD`, `TODO`, or deferred “handle later” language remains in task steps.
- Every task includes explicit files, interfaces, commands, and expected outcomes.

### Type Consistency

- Backend mode type is consistently `PluginWindowPreviewBackendMode`
- status type is consistently `PluginWindowBackendStatus`
- backend contract is consistently `IPluginWindowPreviewBackend`
- hosted bootstrap type is consistently `HostedPreviewPluginSessionFactory`

These names are stable across all tasks.
