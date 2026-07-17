// <copyright file="PreviewShell.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.Previewer.Configuration;
using Echoglossian.Previewer.Fonts;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;

namespace Echoglossian.Previewer.UI;

/// <summary>
/// Draws the interactive previewer shell around the shared overlay renderer.
/// </summary>
internal sealed class PreviewShell : IDisposable
{
    private readonly PreviewConfiguration sourceConfiguration;
    private readonly Config editableConfiguration;
    private readonly PreviewFontSelection fontSelection;
    private readonly IReadOnlyList<string> sessionDiagnostics;
    private readonly PreviewCanvas canvas;
    private readonly PreviewPluginWindowHost pluginWindowHost;
    private readonly PreviewWorkbenchState workbenchState;
    private readonly PreviewShellState state;
    private readonly int appliedLanguageId;
    private readonly int appliedFontSize;
    private PreviewCaptureRequest? pendingScreenshotRequest;
    private string screenshotStatus = string.Empty;
    private TranslationOverlayRenderResult lastRenderResult = new(
        false,
        Vector2.Zero,
        Vector2.Zero,
        TextPresentationBackendKind.PlainImGui);
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewShell" /> class.
    /// </summary>
    /// <param name="sourceConfiguration">The read-only source configuration.</param>
    /// <param name="editableConfiguration">The preview-owned editable configuration.</param>
    /// <param name="fontSelection">The resolved preview font selection.</param>
    /// <param name="sessionDiagnostics">The configuration and database session diagnostics.</param>
    /// <param name="renderer">The shared overlay renderer.</param>
    /// <param name="pluginWindowHost">The real plugin-window host.</param>
    /// <param name="scenario">The initial scenario.</param>
    /// <param name="viewport">The initial logical viewport.</param>
    internal PreviewShell(
        PreviewConfiguration sourceConfiguration,
        Config editableConfiguration,
        PreviewFontSelection fontSelection,
        IReadOnlyList<string> sessionDiagnostics,
        TranslationOverlayRenderer renderer,
        PreviewPluginWindowHost pluginWindowHost,
        PreviewScenario scenario,
        PreviewViewportPreset viewport)
    {
        this.sourceConfiguration = sourceConfiguration ??
            throw new ArgumentNullException(nameof(sourceConfiguration));
        this.editableConfiguration = editableConfiguration ??
            throw new ArgumentNullException(nameof(editableConfiguration));
        this.fontSelection = fontSelection ??
            throw new ArgumentNullException(nameof(fontSelection));
        this.sessionDiagnostics = sessionDiagnostics ??
            throw new ArgumentNullException(nameof(sessionDiagnostics));
        this.appliedLanguageId = editableConfiguration.Lang;
        this.appliedFontSize = fontSelection.FontSize;
        this.canvas = new PreviewCanvas(renderer);
        this.pluginWindowHost = pluginWindowHost ??
            throw new ArgumentNullException(nameof(pluginWindowHost));
        this.workbenchState = PreviewWorkbenchState.CreateDefault(
            scenario,
            viewport);
        this.state = PreviewShellState.FromScenario(
            scenario ?? throw new ArgumentNullException(nameof(scenario)),
            viewport ?? throw new ArgumentNullException(nameof(viewport)));
    }

    /// <summary>
    /// Draws one previewer shell frame.
    /// </summary>
    internal void Draw()
    {
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize, ImGuiCond.Always);
        ImGui.Begin(
            "Echoglossian Overlay Previewer",
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoCollapse);

        var controlWidth = 360f;
        ImGui.BeginChild("Controls", new Vector2(controlWidth, 0), true);
        this.DrawControls();
        ImGui.EndChild();

        ImGui.SameLine();

        var canvasSize = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("PreviewCanvas", canvasSize, true);
        var scenarioVisible = this.state.Visible;
        try
        {
            this.state.Visible = scenarioVisible && this.workbenchState.OverlayVisible;
            this.lastRenderResult = this.canvas.Draw(
                this.state,
                this.editableConfiguration,
                ImGui.GetContentRegionAvail());
        }
        finally
        {
            this.state.Visible = scenarioVisible;
        }

        ImGui.EndChild();

        ImGui.End();
        this.pluginWindowHost.Draw(this.workbenchState);
    }

    /// <summary>
    /// Consumes a screenshot request made by the interactive controls.
    /// </summary>
    /// <param name="outputDirectory">The screenshot output directory.</param>
    /// <param name="request">The consumed request.</param>
    /// <returns><see langword="true"/> when a request was pending.</returns>
    internal bool TryConsumeScreenshotRequest(
        string outputDirectory,
        out ScreenshotRequest request)
    {
        if (this.pendingScreenshotRequest is not { } pendingRequest)
        {
            request = null!;
            return false;
        }

        if (PreviewPluginWindowHost.IsPluginWindowTarget(
                pendingRequest.CaptureTarget))
        {
            if (this.pluginWindowHost.CaptureFailed)
            {
                this.pendingScreenshotRequest = null;
                this.pluginWindowHost.EndCapture();
                this.screenshotStatus =
                    $"Screenshot failed: {pendingRequest.CaptureTarget} " +
                    "did not produce stable bounds.";
                request = null!;
                return false;
            }

            if (this.pluginWindowHost.TryGetStableCrop(
                    pendingRequest.CaptureTarget) is null)
            {
                request = null!;
                return false;
            }

            this.pluginWindowHost.EndCapture();
        }

        this.pendingScreenshotRequest = null;
        request = new ScreenshotRequest(
            pendingRequest.Mode,
            this.state.CreateScenarioSnapshot(),
            this.state.Viewport,
            outputDirectory,
            pendingRequest.CaptureTarget);
        return true;
    }

    /// <summary>
    /// Gets the most recent overlay render result.
    /// </summary>
    internal TranslationOverlayRenderResult LastRenderResult => this.lastRenderResult;

    /// <summary>
    /// Gets a warning when mutable config no longer matches the applied startup runtime.
    /// </summary>
    /// <param name="configuration">The current preview-owned configuration.</param>
    /// <param name="appliedLanguageId">The language identifier applied at startup.</param>
    /// <param name="appliedFontSize">The font size applied at startup.</param>
    /// <returns>A restart warning when runtime values are stale; otherwise, <see langword="null" />.</returns>
    internal static string? GetRuntimeRestartWarning(
        Config configuration,
        int appliedLanguageId,
        int appliedFontSize)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.Lang != appliedLanguageId ||
            configuration.FontSize != appliedFontSize
            ? "Restart the previewer to apply config language or font size changes."
            : null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.canvas.Dispose();
        this.disposed = true;
    }

    /// <summary>
    /// Records the latest interactive screenshot output path.
    /// </summary>
    /// <param name="path">The saved PNG path.</param>
    internal void SetLastScreenshotPath(string path)
    {
        this.screenshotStatus = $"Last screenshot: {path}";
    }

    /// <summary>
    /// Records an interactive screenshot failure without claiming output was written.
    /// </summary>
    /// <param name="message">The capture failure message.</param>
    internal void SetLastScreenshotFailure(string message)
    {
        this.screenshotStatus = $"Screenshot failed: {message}";
    }

    private void DrawControls()
    {
        ImGui.TextUnformatted("Workbench");
        this.DrawToggle(
            "Overlay visible",
            this.workbenchState.OverlayVisible,
            value => this.workbenchState.OverlayVisible = value);
        this.DrawToggle(
            "Config",
            this.workbenchState.ConfigWindowOpen,
            value => this.workbenchState.ConfigWindowOpen = value);

        if (!this.pluginWindowHost.DbManagerAvailable)
        {
            ImGui.BeginDisabled();
        }

        this.DrawToggle(
            "DB Manager",
            this.workbenchState.DbManagerWindowOpen,
            value => this.workbenchState.DbManagerWindowOpen = value);

        if (!this.pluginWindowHost.DbManagerAvailable)
        {
            ImGui.EndDisabled();
            ImGui.TextDisabled("DB snapshot unavailable");
        }

        this.DrawToggle(
            "Translator Metrics / Debugger",
            this.workbenchState.TranslatorMetricsWindowOpen,
            value => this.workbenchState.TranslatorMetricsWindowOpen = value);

        ImGui.Separator();
        ImGui.TextUnformatted("Scenario");
        this.DrawScenarioCombo();
        this.DrawViewportCombo();
        ImGui.Checkbox("Visible", ref this.state.Visible);
        ImGui.Checkbox("Show simulated addon bounds", ref this.state.ShowSimulatedAddonBounds);

        ImGui.Separator();
        ImGui.TextUnformatted("Content");
        ImGui.InputText("Title", ref this.state.Title, 256);
        ImGui.InputTextMultiline(
            "Body text",
            ref this.state.BodyText,
            4096,
            new Vector2(-1f, 110f));

        ImGui.Separator();
        ImGui.TextUnformatted("Addon bounds guide");
        ImGui.DragFloat("X", ref this.state.AddonX, 1f, 0f, this.state.Viewport.Width);
        ImGui.DragFloat("Y", ref this.state.AddonY, 1f, 0f, this.state.Viewport.Height);
        ImGui.DragFloat("Width", ref this.state.AddonWidth, 1f, 1f, this.state.Viewport.Width);
        ImGui.DragFloat("Height", ref this.state.AddonHeight, 1f, 1f, this.state.Viewport.Height);

        ImGui.Separator();
        ImGui.TextUnformatted("Screenshot actions");
        if (ImGui.Button("Save full screenshot"))
        {
            this.QueueScreenshot(
                ScreenshotMode.Full,
                PreviewCaptureTarget.FullFrame);
        }

        if (ImGui.Button("Save surface screenshot"))
        {
            this.QueueScreenshot(
                ScreenshotMode.Surface,
                PreviewCaptureTarget.OverlaySurface);
        }

        if (ImGui.Button("Save config window screenshot"))
        {
            this.QueueScreenshot(
                ScreenshotMode.Full,
                PreviewCaptureTarget.ConfigWindow);
        }

        if (!this.pluginWindowHost.DbManagerAvailable)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Save DB Manager window screenshot"))
        {
            this.QueueScreenshot(
                ScreenshotMode.Full,
                PreviewCaptureTarget.DbManagerWindow);
        }

        if (!this.pluginWindowHost.DbManagerAvailable)
        {
            ImGui.EndDisabled();
        }

        if (ImGui.Button("Save Translator Metrics window screenshot"))
        {
            this.QueueScreenshot(
                ScreenshotMode.Full,
                PreviewCaptureTarget.TranslatorMetricsWindow);
        }

        if (!string.IsNullOrEmpty(this.screenshotStatus))
        {
            ImGui.TextWrapped(this.screenshotStatus);
        }

        ImGui.Separator();
        this.DrawFidelitySummary();
    }

    /// <summary>
    /// Queues a screenshot and starts deterministic plugin-window stabilization when needed.
    /// </summary>
    /// <param name="mode">The screenshot mode.</param>
    /// <param name="target">The requested capture target.</param>
    private void QueueScreenshot(
        ScreenshotMode mode,
        PreviewCaptureTarget target)
    {
        this.pluginWindowHost.EndCapture();
        if (PreviewPluginWindowHost.IsPluginWindowTarget(target))
        {
            switch (target)
            {
                case PreviewCaptureTarget.ConfigWindow:
                    this.workbenchState.ConfigWindowOpen = true;
                    break;
                case PreviewCaptureTarget.DbManagerWindow:
                    this.workbenchState.DbManagerWindowOpen = true;
                    break;
                case PreviewCaptureTarget.TranslatorMetricsWindow:
                    this.workbenchState.TranslatorMetricsWindowOpen = true;
                    break;
            }

            this.pluginWindowHost.BeginCapture(target);
            this.screenshotStatus =
                $"Waiting for stable {target} bounds before capture.";
        }

        this.pendingScreenshotRequest = new PreviewCaptureRequest(mode, target);
    }

    private void DrawScenarioCombo()
    {
        if (ImGui.BeginCombo("Surface", this.state.DisplayName))
        {
            foreach (var scenario in PreviewScenarioCatalog.Defaults)
            {
                var selected = this.state.Key == scenario.Key;
                if (ImGui.Selectable(scenario.DisplayName, selected))
                {
                    this.state.ApplyScenario(scenario);
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawViewportCombo()
    {
        if (ImGui.BeginCombo("Viewport", this.state.Viewport.Key))
        {
            foreach (var preset in PreviewScenarioCatalog.ViewportPresets)
            {
                var selected = this.state.Viewport.Key == preset.Key;
                if (ImGui.Selectable(preset.Key, selected))
                {
                    this.state.Viewport = preset;
                    this.workbenchState.Viewport = preset;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawFidelitySummary()
    {
        ImGui.TextUnformatted("Fidelity summary");
        ImGui.TextWrapped(
            $"Config source: {this.sourceConfiguration.SourceLabel}");
        ImGui.TextWrapped(
            $"Font: {Path.GetFileName(this.fontSelection.SpecialFontPath)} / {this.fontSelection.FontSize}px");
        ImGui.TextUnformatted(
            $"Logical viewport: {this.state.Viewport.Width}x{this.state.Viewport.Height}");
        ImGui.TextUnformatted($"Presentation mode: {this.lastRenderResult.PresentationMode}");
        ImGui.TextUnformatted(
            $"Uses simulated addon bounds: {this.state.ShowSimulatedAddonBounds}");
        var restartWarning = GetRuntimeRestartWarning(
            this.editableConfiguration,
            this.appliedLanguageId,
            this.appliedFontSize);
        if (restartWarning is not null)
        {
            ImGui.TextWrapped(restartWarning);
        }

        foreach (var diagnostic in this.sessionDiagnostics)
        {
            ImGui.TextWrapped(diagnostic);
        }
    }

    /// <summary>
    /// Draws a property-backed workbench toggle.
    /// </summary>
    /// <param name="label">The toggle label.</param>
    /// <param name="value">The current value.</param>
    /// <param name="update">Applies a changed value.</param>
    private void DrawToggle(string label, bool value, Action<bool> update)
    {
        if (ImGui.Checkbox(label, ref value))
        {
            update(value);
        }
    }
}

/// <summary>
/// Holds preview-owned mutable shell state.
/// </summary>
internal sealed class PreviewShellState
{
    /// <summary>Gets the selected scenario key.</summary>
    internal string Key { get; private set; } = string.Empty;

    /// <summary>Gets the selected scenario display name.</summary>
    internal string DisplayName { get; private set; } = string.Empty;

    /// <summary>Gets the selected surface identifier.</summary>
    internal TranslationOverlaySurfaceId SurfaceId { get; private set; }

    /// <summary>Gets or sets the selected viewport.</summary>
    internal PreviewViewportPreset Viewport { get; set; } = PreviewScenarioCatalog.ViewportPresets[1];

    /// <summary>Gets or sets a value indicating whether the overlay is visible.</summary>
    internal bool Visible;

    /// <summary>Gets or sets a value indicating whether the bounds guide is shown.</summary>
    internal bool ShowSimulatedAddonBounds;

    /// <summary>Gets or sets the preview title.</summary>
    internal string Title = string.Empty;

    /// <summary>Gets or sets the preview body text.</summary>
    internal string BodyText = string.Empty;

    /// <summary>Gets or sets the simulated addon X coordinate.</summary>
    internal float AddonX;

    /// <summary>Gets or sets the simulated addon Y coordinate.</summary>
    internal float AddonY;

    /// <summary>Gets or sets the simulated addon width.</summary>
    internal float AddonWidth;

    /// <summary>Gets or sets the simulated addon height.</summary>
    internal float AddonHeight;

    /// <summary>Gets the current addon bounds.</summary>
    internal PreviewAddonBounds AddonBounds => new(
        this.AddonX,
        this.AddonY,
        this.AddonWidth,
        this.AddonHeight);

    /// <summary>
    /// Creates shell state from a scenario and viewport.
    /// </summary>
    /// <param name="scenario">The initial scenario.</param>
    /// <param name="viewport">The initial viewport.</param>
    /// <returns>The initialized shell state.</returns>
    internal static PreviewShellState FromScenario(
        PreviewScenario scenario,
        PreviewViewportPreset viewport)
    {
        var state = new PreviewShellState
        {
            Viewport = viewport,
        };
        state.ApplyScenario(scenario);
        return state;
    }

    /// <summary>
    /// Applies a scenario to preview-owned state.
    /// </summary>
    /// <param name="scenario">The selected scenario.</param>
    internal void ApplyScenario(PreviewScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        this.Key = scenario.Key;
        this.DisplayName = scenario.DisplayName;
        this.SurfaceId = scenario.SurfaceId;
        this.Visible = scenario.Visible;
        this.ShowSimulatedAddonBounds = scenario.ShowsSimulatedAddonBounds;
        this.Title = scenario.Title ?? string.Empty;
        this.BodyText = scenario.TranslatedText;
        this.AddonX = scenario.AddonBounds.X;
        this.AddonY = scenario.AddonBounds.Y;
        this.AddonWidth = scenario.AddonBounds.Width;
        this.AddonHeight = scenario.AddonBounds.Height;
    }

    /// <summary>
    /// Creates a scenario record from the current interactive state.
    /// </summary>
    /// <returns>The current scenario snapshot.</returns>
    internal PreviewScenario CreateScenarioSnapshot()
    {
        return new PreviewScenario(
            this.Key,
            this.DisplayName,
            this.SurfaceId,
            this.AddonBounds,
            this.BodyText,
            this.Title,
            this.Visible,
            this.ShowSimulatedAddonBounds);
    }
}
