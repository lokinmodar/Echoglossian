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
using System.Text.Json.Serialization;

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
        WriteOutputsAtomically(
            outputDirectory,
            stagingDirectory =>
            {
                var entries = new List<ScreenshotManifestEntry>();
                foreach (var request in requests)
                {
                    var stagedRequest = request with { OutputDirectory = stagingDirectory };
                    var stagedOutputPath = GetOutputPath(stagedRequest);
                    CapturedScreenshot capture;
                    try
                    {
                        capture = this.Capture(stagedRequest, stagedOutputPath);
                    }
                    catch (Exception exception) when (Program.IsExpectedInteractiveScreenshotFailure(exception))
                    {
                        throw new InvalidOperationException(
                            Program.CreateScreenshotFailureMessage(
                                request.CaptureTarget,
                                GetOutputPath(request),
                                exception),
                            exception);
                    }

                    entries.Add(this.CreateManifestEntry(request, capture));
                }

                var manifest = new ScreenshotManifest(
                    GetManifestConfigSourceLabel(this.sourceConfiguration),
                    this.fontSelection.FontPaths.Select(Path.GetFileName).ToArray()!,
                    this.fontSelection.FontSize,
                    entries);
                var manifestPath = Path.Combine(stagingDirectory, "manifest.json");
                try
                {
                    File.WriteAllText(manifestPath, SerializeManifest(manifest));
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    throw new InvalidOperationException(
                        CreateOutputFailureMessage(
                            "Manifest write",
                            Path.Combine(outputDirectory, "manifest.json"),
                            exception),
                        exception);
                }
            });
    }

    /// <summary>
    /// Writes a complete batch to a private staging directory before publishing
    /// it to the requested output directory.
    /// </summary>
    /// <param name="outputDirectory">The final screenshot output directory.</param>
    /// <param name="writeStagedOutputs">Writes all batch outputs into the supplied staging directory.</param>
    internal static void WriteOutputsAtomically(
        string outputDirectory,
        Action<string> writeStagedOutputs,
        Action<string, string>? moveFile = null,
        Action<string>? deleteFile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(writeStagedOutputs);

        string stagingDirectory;
        try
        {
            Directory.CreateDirectory(outputDirectory);
            stagingDirectory = Path.Combine(
                outputDirectory,
                $".echoglossian-previewer-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            throw new InvalidOperationException(
                CreateOutputFailureMessage(
                    "Screenshot output preparation",
                    outputDirectory,
                    exception),
                exception);
        }

        var preserveStagingDirectory = false;
        try
        {
            writeStagedOutputs(stagingDirectory);
            PublishStagedOutputs(stagingDirectory, outputDirectory, moveFile, deleteFile);
        }
        catch (BatchPublicationException exception) when (
            exception.RollbackIncomplete)
        {
            preserveStagingDirectory = true;
            throw;
        }
        finally
        {
            if (!preserveStagingDirectory)
            {
                TryDeleteStagingDirectory(stagingDirectory);
            }
        }
    }

    /// <summary>
    /// Publishes staged outputs while restoring prior generated outputs if a
    /// later replacement fails.
    /// </summary>
    /// <param name="stagingDirectory">The private staging directory containing complete outputs.</param>
    /// <param name="outputDirectory">The final screenshot output directory.</param>
    /// <param name="moveFile">Moves one file and is injectable for failure testing.</param>
    internal static void PublishStagedOutputs(
        string stagingDirectory,
        string outputDirectory,
        Action<string, string>? moveFile = null,
        Action<string>? deleteFile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var move = moveFile ?? File.Move;
        var delete = deleteFile ?? File.Delete;
        var backups = new List<(string BackupPath, string DestinationPath)>();
        var publishedPaths = new List<string>();

        try
        {
            var stagedFiles = Directory.EnumerateFiles(
                    stagingDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var rollbackDirectory = Path.Combine(stagingDirectory, "rollback");
            Directory.CreateDirectory(rollbackDirectory);

            foreach (var stagedFile in stagedFiles)
            {
                var destinationPath = Path.Combine(outputDirectory, Path.GetFileName(stagedFile));
                if (!File.Exists(destinationPath))
                {
                    continue;
                }

                var backupPath = Path.Combine(rollbackDirectory, Path.GetFileName(stagedFile));
                move(destinationPath, backupPath);
                backups.Add((backupPath, destinationPath));
            }

            foreach (var stagedFile in stagedFiles)
            {
                var destinationPath = Path.Combine(outputDirectory, Path.GetFileName(stagedFile));
                move(stagedFile, destinationPath);
                publishedPaths.Add(destinationPath);
            }
        }
        catch (Exception exception)
        {
            if (publishedPaths.Count == 0 &&
                backups.Count == 0)
            {
                throw new InvalidOperationException(
                    CreateOutputFailureMessage(
                        "Screenshot publication",
                        outputDirectory,
                        exception),
                    exception);
            }

            var rollbackIncomplete = false;
            foreach (var publishedPath in publishedPaths)
            {
                if (!TryDeleteFile(delete, publishedPath))
                {
                    rollbackIncomplete = true;
                }
            }

            var unrecoveredBackups = new List<string>();
            foreach (var backup in backups.AsEnumerable().Reverse())
            {
                if (!TryDeleteFile(delete, backup.DestinationPath))
                {
                    rollbackIncomplete = true;
                }

                if (!TryMoveFile(move, backup.BackupPath, backup.DestinationPath))
                {
                    unrecoveredBackups.Add(backup.BackupPath);
                }
            }

            if (rollbackIncomplete || unrecoveredBackups.Count > 0)
            {
                throw new BatchPublicationException(
                    outputDirectory,
                    stagingDirectory,
                    exception);
            }

            throw new InvalidOperationException(
                CreateOutputFailureMessage(
                    "Screenshot publication",
                    outputDirectory,
                    exception),
                exception);
        }
    }

    /// <summary>
    /// Creates a path-qualified message for an output operation failure.
    /// </summary>
    /// <param name="operation">The output operation that failed.</param>
    /// <param name="outputPath">The affected output path.</param>
    /// <param name="exception">The underlying I/O failure.</param>
    /// <returns>A contextual output failure message.</returns>
    internal static string CreateOutputFailureMessage(
        string operation,
        string outputPath,
        Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(exception);
        return $"{operation} to {outputPath} failed: {exception.Message}";
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

    /// <summary>
    /// Resolves target-specific manifest fields without representing an
    /// unrendered overlay as a plugin-window capture surface.
    /// </summary>
    /// <param name="target">The captured target.</param>
    /// <param name="overlaySurfaceKey">The rendered overlay surface identifier.</param>
    /// <param name="presentationMode">The rendered overlay presentation mode.</param>
    /// <returns>The manifest surface and presentation values for the target.</returns>
    internal static (string SurfaceKey, string PresentationMode) GetManifestTargetMetadata(
        PreviewCaptureTarget target,
        string overlaySurfaceKey,
        string presentationMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(overlaySurfaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentationMode);
        return PreviewPluginWindowHost.IsPluginWindowTarget(target)
            ? ("NotApplicable", "NotApplicable")
            : (overlaySurfaceKey, presentationMode);
    }

    /// <summary>
    /// Captures one deterministic screenshot request to its resolved output path.
    /// </summary>
    /// <param name="request">The request to capture.</param>
    /// <param name="outputPath">The absolute or relative destination PNG path.</param>
    /// <returns>The capture metadata used by the manifest.</returns>
    private CapturedScreenshot Capture(ScreenshotRequest request, string outputPath)
    {
        var captureLayoutSize = PreviewPluginWindowHost.GetCaptureLayoutSize(
            request.CaptureTarget);
        using PreviewHost host = new(
            new PreviewHostOptions
            {
                Width = Math.Max(request.Viewport.Width, captureLayoutSize.Width),
                Height = Math.Max(request.Viewport.Height, captureLayoutSize.Height),
                Title = "Echoglossian Screenshot Capture",
                StartHidden = true,
            });
        using var pluginWindowHost = this.CreatePluginWindowHost(request.CaptureTarget);
        if (pluginWindowHost is not null)
        {
            pluginWindowHost.BeginCapture(request.CaptureTarget);
        }

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
            if (!PreviewPluginWindowHost.IsPluginWindowTarget(request.CaptureTarget))
            {
                renderResult = DrawCaptureFrame(
                    canvas,
                    state,
                    this.editableConfiguration,
                    request.Viewport);
            }

            pluginWindowHost?.Draw(workbenchState);
        };
        var stopwatch = Stopwatch.StartNew();
        while (!IsCaptureReady(
                request.CaptureTarget,
                renderResult.WasDrawn,
                IsCaptureTargetReady(request.CaptureTarget, pluginWindowHost)) &&
            stopwatch.Elapsed < TimeSpan.FromSeconds(5) &&
            pluginWindowHost?.CaptureFailed != true)
        {
            host.RunFrame(draw);
            if (!IsCaptureReady(
                    request.CaptureTarget,
                    renderResult.WasDrawn,
                    IsCaptureTargetReady(request.CaptureTarget, pluginWindowHost)))
            {
                Thread.Sleep(25);
            }
        }

        if (!PreviewPluginWindowHost.IsPluginWindowTarget(request.CaptureTarget) &&
            !renderResult.WasDrawn)
        {
            throw new InvalidOperationException(
                $"Preview screenshot scenario did not render: {request.Scenario.Key}");
        }

        if (!IsCaptureReady(
                request.CaptureTarget,
                renderResult.WasDrawn,
                IsCaptureTargetReady(request.CaptureTarget, pluginWindowHost)))
        {
            throw new InvalidOperationException(
                $"Preview screenshot target did not produce stable bounds: " +
                request.CaptureTarget);
        }

        host.CaptureFramePng(
            draw,
            outputPath,
            sourceTextureSize => this.CalculateCrop(
                request,
                renderResult,
                pluginWindowHost,
                ImGui.GetIO().DisplaySize,
                sourceTextureSize));

        return new CapturedScreenshot(outputPath, renderResult);
    }

    /// <summary>
    /// Resolves the physical crop for the requested preview target.
    /// </summary>
    /// <param name="request">The screenshot request.</param>
    /// <param name="renderResult">The overlay draw result.</param>
    /// <param name="pluginWindowHost">The optional real plugin-window host.</param>
    /// <param name="displaySize">The logical ImGui display dimensions.</param>
    /// <param name="sourceTextureSize">The physical offscreen capture texture dimensions.</param>
    /// <returns>The requested crop, or <see langword="null" /> for the full frame.</returns>
    private Rectangle? CalculateCrop(
        ScreenshotRequest request,
        TranslationOverlayRenderResult renderResult,
        PreviewPluginWindowHost? pluginWindowHost,
        Vector2 displaySize,
        Vector2 sourceTextureSize)
    {
        var crop = request.CaptureTarget switch
        {
            PreviewCaptureTarget.OverlaySurface => VeldridScreenshotCapture.CalculateSurfaceCrop(
                renderResult,
                request.Viewport.Width,
                request.Viewport.Height,
                request.SurfaceMargin,
                framebufferScale: 1f),
            PreviewCaptureTarget.ConfigWindow => CalculateWindowCrop(
                pluginWindowHost?.TryGetStableCrop(PreviewCaptureTarget.ConfigWindow),
                displaySize,
                sourceTextureSize),
            PreviewCaptureTarget.DbManagerWindow => CalculateWindowCrop(
                pluginWindowHost?.TryGetStableCrop(PreviewCaptureTarget.DbManagerWindow),
                displaySize,
                sourceTextureSize),
            PreviewCaptureTarget.TranslatorMetricsWindow => CalculateWindowCrop(
                pluginWindowHost?.TryGetStableCrop(PreviewCaptureTarget.TranslatorMetricsWindow),
                displaySize,
                sourceTextureSize),
            _ => null,
        };

        return PreviewPluginWindowHost.IsPluginWindowTarget(request.CaptureTarget)
            ? RequireWindowCrop(crop, request.CaptureTarget)
            : crop;
    }

    /// <summary>
    /// Converts a logical plugin-window crop into physical batch capture-source pixels.
    /// </summary>
    /// <param name="logicalCrop">The plugin window crop in logical ImGui coordinates.</param>
    /// <param name="displaySize">The logical ImGui display dimensions.</param>
    /// <param name="framebufferSize">The physical capture-source dimensions.</param>
    /// <returns>The physical crop, or <see langword="null" /> when no window bounds are available.</returns>
    internal static Rectangle? CalculateWindowCrop(
        Rectangle? logicalCrop,
        Vector2 displaySize,
        Vector2 framebufferSize)
    {
        return CalculateOffscreenWindowCrop(
            logicalCrop,
            displaySize,
            framebufferSize);
    }

    /// <summary>
    /// Converts logical plugin-window bounds using the actual offscreen texture
    /// that will be read back by deterministic batch capture.
    /// </summary>
    /// <param name="logicalCrop">The plugin window crop in logical ImGui coordinates.</param>
    /// <param name="displaySize">The logical ImGui display dimensions.</param>
    /// <param name="sourceTextureSize">The physical offscreen texture dimensions.</param>
    /// <returns>The physical crop, or <see langword="null" /> when no window bounds are available.</returns>
    internal static Rectangle? CalculateOffscreenWindowCrop(
        Rectangle? logicalCrop,
        Vector2 displaySize,
        Vector2 sourceTextureSize)
    {
        return Program.CalculateInteractiveWindowCrop(
            logicalCrop,
            displaySize,
            sourceTextureSize);
    }

    /// <summary>
    /// Requires a non-empty plugin-window crop instead of treating it as a full frame.
    /// </summary>
    /// <param name="crop">The resolved physical crop.</param>
    /// <param name="target">The requested plugin-window target.</param>
    /// <returns>The non-empty crop.</returns>
    /// <exception cref="InvalidOperationException">The target has no usable bounds.</exception>
    internal static Rectangle RequireWindowCrop(
        Rectangle? crop,
        PreviewCaptureTarget target)
    {
        if (crop is not { Width: > 0, Height: > 0 } validCrop)
        {
            throw new InvalidOperationException(
                $"Preview screenshot target has no stable capture bounds: {target}");
        }

        return validCrop;
    }

    /// <summary>
    /// Serializes the deterministic screenshot manifest.
    /// </summary>
    /// <param name="manifest">The manifest to serialize.</param>
    /// <returns>The indented JSON manifest.</returns>
    internal static string SerializeManifest(ScreenshotManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(manifest, options);
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
    /// Determines whether the requested target is ready for capture.
    /// </summary>
    /// <param name="target">The requested capture target.</param>
    /// <param name="pluginWindowHost">The optional plugin-window host.</param>
    /// <returns><see langword="true" /> when no stabilization is needed or stable bounds exist.</returns>
    private static bool IsCaptureTargetReady(
        PreviewCaptureTarget target,
        PreviewPluginWindowHost? pluginWindowHost)
    {
        return !PreviewPluginWindowHost.IsPluginWindowTarget(target) ||
            pluginWindowHost?.TryGetStableCrop(target) is not null;
    }

    /// <summary>
    /// Determines whether a capture target has met its target-specific readiness condition.
    /// </summary>
    /// <param name="target">The requested capture target.</param>
    /// <param name="overlayWasDrawn"><see langword="true" /> when the overlay rendered successfully.</param>
    /// <param name="hasStableWindowBounds"><see langword="true" /> when the requested plugin window has stable bounds.</param>
    /// <returns><see langword="true" /> when the target is ready to capture; otherwise, <see langword="false" />.</returns>
    internal static bool IsCaptureReady(
        PreviewCaptureTarget target,
        bool overlayWasDrawn,
        bool hasStableWindowBounds)
    {
        return PreviewPluginWindowHost.IsPluginWindowTarget(target)
            ? hasStableWindowBounds
            : overlayWasDrawn;
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
        var metadata = GetManifestTargetMetadata(
            request.CaptureTarget,
            request.Scenario.SurfaceId.ToString(),
            capture.RenderResult.PresentationMode.ToString());
        return new ScreenshotManifestEntry(
            request.Scenario.Key,
            metadata.SurfaceKey,
            request.Viewport.Width,
            request.Viewport.Height,
            request.Mode.ToString(),
            request.CaptureTarget,
            metadata.PresentationMode,
            GetManifestPngPathLabel(capture.PngPath));
    }

    /// <summary>
    /// Deletes a private staging directory without replacing the capture or
    /// manifest exception that caused cleanup.
    /// </summary>
    /// <param name="stagingDirectory">The private staging directory to remove.</param>
    private static void TryDeleteStagingDirectory(string stagingDirectory)
    {
        try
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Deletes one partial published output without replacing its original
    /// publication exception.
    /// </summary>
    /// <param name="path">The output path to remove.</param>
    private static bool TryDeleteFile(Action<string> deleteFile, string path)
    {
        try
        {
            deleteFile(path);
            return !File.Exists(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Restores one backed-up output without replacing the original failed
    /// publication exception.
    /// </summary>
    /// <param name="moveFile">The file move operation used for publication.</param>
    /// <param name="sourcePath">The backup source path.</param>
    /// <param name="destinationPath">The original output path.</param>
    private static bool TryMoveFile(
        Action<string, string> moveFile,
        string sourcePath,
        string destinationPath)
    {
        try
        {
            if (File.Exists(sourcePath))
            {
                moveFile(sourcePath, destinationPath);
                return true;
            }

            return false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Represents a failed publication whose prior outputs could not all be
    /// restored from the private staging directory.
    /// </summary>
    internal sealed class BatchPublicationException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BatchPublicationException"/> class.
        /// </summary>
        /// <param name="outputDirectory">The requested final output directory.</param>
        /// <param name="recoveryDirectory">The preserved private backup directory.</param>
        /// <param name="innerException">The original publication failure.</param>
        internal BatchPublicationException(
            string outputDirectory,
            string recoveryDirectory,
            Exception innerException)
            : base(
                $"Screenshot publication to {outputDirectory} failed and rollback is incomplete. " +
                $"Previous outputs remain in {recoveryDirectory}.",
                innerException)
        {
            this.RecoveryDirectory = recoveryDirectory;
        }

        /// <summary>
        /// Gets the preserved directory that contains unrecovered backups.
        /// </summary>
        internal string RecoveryDirectory { get; }

        /// <summary>
        /// Gets a value indicating that at least one backup was not restored.
        /// </summary>
        internal bool RollbackIncomplete => true;
    }

    /// <summary>
    /// Resolves the deterministic PNG destination for one screenshot request.
    /// </summary>
    /// <param name="request">The request whose output path is needed.</param>
    /// <returns>The destination PNG path.</returns>
    private static string GetOutputPath(ScreenshotRequest request)
    {
        return Path.Combine(
            request.OutputDirectory,
            ScreenshotFileName.CreatePngName(
                request.Mode == ScreenshotMode.Batch ? ScreenshotMode.Full : request.Mode,
                request.Scenario.Key,
                request.Viewport,
                request.CaptureTarget));
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

    internal sealed record ScreenshotManifest(
        string ConfigSourceLabel,
        IReadOnlyList<string?> FontFileNames,
        int FontSize,
        IReadOnlyList<ScreenshotManifestEntry> Entries);

    internal sealed record ScreenshotManifestEntry(
        string ScenarioKey,
        string SurfaceKey,
        int ViewportWidth,
        int ViewportHeight,
        string ScreenshotMode,
        PreviewCaptureTarget CaptureTarget,
        string PresentationMode,
        string PngPath);

    private sealed record CapturedScreenshot(
        string PngPath,
        TranslationOverlayRenderResult RenderResult);
}
