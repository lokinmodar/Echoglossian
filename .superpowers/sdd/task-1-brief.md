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

