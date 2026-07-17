// <copyright file="BatchScreenshotRunner.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.Previewer.Configuration;
using Echoglossian.Previewer.Fonts;
using Echoglossian.Previewer.Hosting;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.UI;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Drawing;
using System.Numerics;
using System.Diagnostics;
using System.Text.Json;

namespace Echoglossian.Previewer.Screenshots;

/// <summary>
/// Runs deterministic preview screenshot captures.
/// </summary>
internal sealed class BatchScreenshotRunner
{
    private readonly PreviewConfiguration sourceConfiguration;
    private readonly Config editableConfiguration;
    private readonly PreviewFontSelection fontSelection;
    private readonly Func<PreviewPluginWindowHost>? pluginWindowHostFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchScreenshotRunner"/> class.
    /// </summary>
    /// <param name="sourceConfiguration">The loaded preview configuration source.</param>
    /// <param name="editableConfiguration">The isolated editable preview configuration.</param>
    /// <param name="fontSelection">The resolved preview fonts.</param>
    /// <param name="pluginWindowHostFactory">Creates preview-owned plugin windows when needed.</param>
    internal BatchScreenshotRunner(
        PreviewConfiguration sourceConfiguration,
        Config editableConfiguration,
        PreviewFontSelection fontSelection,
        Func<PreviewPluginWindowHost>? pluginWindowHostFactory = null)
    {
        this.sourceConfiguration = sourceConfiguration ??
            throw new ArgumentNullException(nameof(sourceConfiguration));
        this.editableConfiguration = editableConfiguration ??
            throw new ArgumentNullException(nameof(editableConfiguration));
        this.fontSelection = fontSelection ??
            throw new ArgumentNullException(nameof(fontSelection));
        this.pluginWindowHostFactory = pluginWindowHostFactory;
    }

    /// <summary>
    /// Captures all requested scenarios and writes a sidecar manifest.
    /// </summary>
    /// <param name="requests">The screenshot requests to run.</param>
    internal void Run(IReadOnlyList<ScreenshotRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            return;
        }

        var outputDirectory = ResolveSharedOutputDirectory(requests);
        Directory.CreateDirectory(outputDirectory);
        var entries = new List<ScreenshotManifestEntry>();

        foreach (var request in requests)
        {
            var capture = this.Capture(request);
            entries.Add(this.CreateManifestEntry(request, capture));
        }

        var manifest = new ScreenshotManifest(
            GetManifestConfigSourceLabel(this.sourceConfiguration),
            this.fontSelection.FontPaths.Select(Path.GetFileName).ToArray()!,
            this.fontSelection.FontSize,
            entries);
        var manifestPath = Path.Combine(outputDirectory, "manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Resolves the manifest-safe configuration source label without exposing
    /// local profile or directory information.
    /// </summary>
    /// <param name="sourceConfiguration">The loaded preview configuration.</param>
    /// <returns>A redacted configuration source label.</returns>
    internal static string GetManifestConfigSourceLabel(
        PreviewConfiguration sourceConfiguration)
    {
        ArgumentNullException.ThrowIfNull(sourceConfiguration);
        return sourceConfiguration.SourceLabel;
    }

    /// <summary>
    /// Resolves the batch output directory and rejects mixed destinations so
    /// the manifest cannot silently describe files written elsewhere.
    /// </summary>
    /// <param name="requests">The screenshot requests to validate.</param>
    /// <returns>The normalized shared output directory.</returns>
    internal static string ResolveSharedOutputDirectory(
        IReadOnlyList<ScreenshotRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            throw new ArgumentException(
                "At least one screenshot request is required.",
                nameof(requests));
        }

        var outputDirectory = Path.GetFullPath(requests[0].OutputDirectory);
        for (var index = 1; index < requests.Count; index++)
        {
            var candidateDirectory = Path.GetFullPath(requests[index].OutputDirectory);
            if (!string.Equals(
                outputDirectory,
                candidateDirectory,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "All screenshot requests must use the same output directory.",
                    nameof(requests));
            }
        }

        return outputDirectory;
    }

    /// <summary>
    /// Resolves the manifest-safe png label without exposing local directories.
    /// </summary>
    /// <param name="pngPath">The captured screenshot path.</param>
    /// <returns>A redacted screenshot file label.</returns>
    internal static string GetManifestPngPathLabel(string pngPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pngPath);
        var fileName = Path.GetFileName(pngPath);
        return string.IsNullOrWhiteSpace(fileName) ? "screenshot.png" : fileName;
    }

    private CapturedScreenshot Capture(ScreenshotRequest request)
    {
        var outputPath = Path.Combine(
            request.OutputDirectory,
            ScreenshotFileName.CreatePngName(
                request.Mode == ScreenshotMode.Batch ? ScreenshotMode.Full : request.Mode,
                request.Scenario.Key,
                request.Viewport));
        using PreviewHost host = new(
            new PreviewHostOptions
            {
                Width = request.Viewport.Width,
                Height = request.Viewport.Height,
                Title = "Echoglossian Screenshot Capture",
                StartHidden = true,
            });
        using var pluginWindowHost = this.CreatePluginWindowHost(request.CaptureTarget);
        var fontRuntime = new PreviewFontRuntime(
            this.fontSelection,
            string.Join(
                Environment.NewLine,
                PreviewScenarioCatalog.Defaults.Select(scenario => scenario.Title)),
            string.Join(
                Environment.NewLine,
                PreviewScenarioCatalog.Defaults.Select(scenario => scenario.TranslatedText)),
            host.RecreateFontDeviceTexture);
        using var composition = new PreviewOverlayRendererFactory(host).Create(
            this.editableConfiguration,
            fontRuntime,
            this.fontSelection);
        using var canvas = new PreviewCanvas(composition.Renderer);
        var state = PreviewShellState.FromScenario(request.Scenario, request.Viewport);
        state.ShowSimulatedAddonBounds = false;
        var workbenchState = PreviewWorkbenchState.CreateDefault(request.Scenario, request.Viewport);
        SetWindowCaptureTarget(workbenchState, request.CaptureTarget);
        var renderResult = new TranslationOverlayRenderResult(
            false,
            Vector2.Zero,
            Vector2.Zero,
            TextPresentationBackendKind.PlainImGui);

        Action draw = () =>
        {
            composition.BeginDrawFrame();
            renderResult = DrawCaptureFrame(
                canvas,
                state,
                this.editableConfiguration,
                request.Viewport);
            pluginWindowHost?.Draw(workbenchState);
        };
        var stopwatch = Stopwatch.StartNew();
        while (!renderResult.WasDrawn && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            host.RunFrame(draw);
            if (!renderResult.WasDrawn)
            {
                Thread.Sleep(25);
            }
        }

        if (!renderResult.WasDrawn)
        {
            throw new InvalidOperationException(
                $"Preview screenshot scenario did not render: {request.Scenario.Key}");
        }

        host.CaptureFramePng(
            draw,
            outputPath,
            () => this.CalculateCrop(request, renderResult, pluginWindowHost));

        return new CapturedScreenshot(outputPath, renderResult);
    }

    /// <summary>
    /// Resolves the physical crop for the requested preview target.
    /// </summary>
    /// <param name="request">The screenshot request.</param>
    /// <param name="renderResult">The overlay draw result.</param>
    /// <param name="pluginWindowHost">The optional real plugin-window host.</param>
    /// <returns>The requested crop, or <see langword="null" /> for the full frame.</returns>
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
            PreviewCaptureTarget.ConfigWindow => pluginWindowHost?.TryGetCrop(
                PreviewCaptureTarget.ConfigWindow),
            PreviewCaptureTarget.DbManagerWindow => pluginWindowHost?.TryGetCrop(
                PreviewCaptureTarget.DbManagerWindow),
            PreviewCaptureTarget.TranslatorMetricsWindow => pluginWindowHost?.TryGetCrop(
                PreviewCaptureTarget.TranslatorMetricsWindow),
            _ => null,
        };
    }

    /// <summary>
    /// Creates real plugin windows only for a plugin-window screenshot target.
    /// </summary>
    /// <param name="target">The requested capture target.</param>
    /// <returns>The plugin-window host, or <see langword="null" /> when unnecessary.</returns>
    private PreviewPluginWindowHost? CreatePluginWindowHost(PreviewCaptureTarget target)
    {
        if (target is not (PreviewCaptureTarget.ConfigWindow or
            PreviewCaptureTarget.DbManagerWindow or
            PreviewCaptureTarget.TranslatorMetricsWindow))
        {
            return null;
        }

        return this.pluginWindowHostFactory?.Invoke() ?? throw new InvalidOperationException(
            "Plugin-window screenshot capture requires a preview window host.");
    }

    /// <summary>
    /// Opens only the plugin window requested by a deterministic capture.
    /// </summary>
    /// <param name="state">The preview-owned workbench state.</param>
    /// <param name="target">The requested capture target.</param>
    private static void SetWindowCaptureTarget(
        PreviewWorkbenchState state,
        PreviewCaptureTarget target)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ConfigWindowOpen = target == PreviewCaptureTarget.ConfigWindow;
        state.DbManagerWindowOpen = target == PreviewCaptureTarget.DbManagerWindow;
        state.TranslatorMetricsWindowOpen = target == PreviewCaptureTarget.TranslatorMetricsWindow;
    }

    private ScreenshotManifestEntry CreateManifestEntry(
        ScreenshotRequest request,
        CapturedScreenshot capture)
    {
        return new ScreenshotManifestEntry(
            request.Scenario.Key,
            request.Scenario.SurfaceId.ToString(),
            request.Viewport.Width,
            request.Viewport.Height,
            request.Mode.ToString(),
            request.CaptureTarget.ToString(),
            capture.RenderResult.PresentationMode.ToString(),
            GetManifestPngPathLabel(capture.PngPath));
    }

    private static TranslationOverlayRenderResult DrawCaptureFrame(
        PreviewCanvas canvas,
        PreviewShellState state,
        Config configuration,
        PreviewViewportPreset viewport)
    {
        ImGui.SetNextWindowPos(Vector2.Zero, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(viewport.Width, viewport.Height), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.Begin(
            "ScreenshotCapture",
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoBringToFrontOnFocus);
        try
        {
            return canvas.Draw(
                state,
                configuration,
                new Vector2(viewport.Width, viewport.Height));
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
        }
    }

    private sealed record ScreenshotManifest(
        string ConfigSourceLabel,
        IReadOnlyList<string?> FontFileNames,
        int FontSize,
        IReadOnlyList<ScreenshotManifestEntry> Entries);

    private sealed record ScreenshotManifestEntry(
        string ScenarioKey,
        string SurfaceKey,
        int ViewportWidth,
        int ViewportHeight,
        string ScreenshotMode,
        string CaptureTarget,
        string PresentationMode,
        string PngPath);

    private sealed record CapturedScreenshot(
        string PngPath,
        TranslationOverlayRenderResult RenderResult);
}
