// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.EFCoreSqlite;
using Echoglossian.LanguagesHandling;
using Echoglossian.PluginUI;
using Echoglossian.Previewer.Configuration;
using Echoglossian.Previewer.Fonts;
using Echoglossian.Previewer.Hosting;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.Previewer.Session;
using Echoglossian.Previewer.UI;
using Echoglossian.UIOverlays.TranslationOverlay;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using System.Drawing;
using System.Numerics;

namespace Echoglossian.Previewer;

/// <summary>
///     Provides the standalone previewer process entry point.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Runs the requested standalone previewer command.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A process exit code.</returns>
    internal static int Main(string[] args)
    {
        try
        {
            var commandLine = PreviewCommandLine.Parse(args);

            if (commandLine.BindingSmoke)
            {
                RunBindingSmoke();
            }

            if (commandLine.HostSmoke)
            {
                RunHostSmoke();
            }

            if (!string.IsNullOrWhiteSpace(commandLine.ScreenshotMode))
            {
                RunScreenshotExport(commandLine);
            }
            else if (!commandLine.BindingSmoke && !commandLine.HostSmoke)
            {
                RunInteractivePreview(commandLine);
            }

            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    ///     Creates and destroys an ImGui context through the installed Dalamud
    ///     native binding.
    /// </summary>
    private static void RunBindingSmoke()
    {
        var context = ImGui.CreateContext();

        try
        {
            Console.WriteLine($"Dalamud ImGui binding OK: {ImGui.GetVersion()}");
        }
        finally
        {
            ImGui.DestroyContext(context);
        }
    }

    /// <summary>
    ///     Creates a standalone host and presents one ImGui frame.
    /// </summary>
    private static void RunHostSmoke()
    {
        using PreviewHost host = new(
            new PreviewHostOptions
            {
                Width = 640,
                Height = 360,
                Title = "Echoglossian Preview Host Smoke",
                StartHidden = true,
            });

        host.RunFrame(
            static () => ImGui.TextUnformatted("Echoglossian preview host"));
    }

    /// <summary>
    ///     Runs the interactive preview shell.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    private static void RunInteractivePreview(PreviewCommandLine commandLine)
    {
        using var session = PreviewSessionLoader.Load(
            new PreviewSessionSourceOptions(
                commandLine.ConfigPath,
                commandLine.DatabasePath,
                commandLine.OutputDirectory));
        var sourceConfiguration = session.Configuration;
        var editableConfiguration = session.EditableConfiguration;
        var scenario = PreviewScenarioCatalog.ResolveScenario(commandLine.Scenario);
        var viewport = PreviewScenarioCatalog.ResolveViewport(
            commandLine.ViewportWidth,
            commandLine.ViewportHeight);
        var languages = Echoglossian.CreateLanguagesDictionary();
        var selectedLanguage = ResolvePreviewLanguage(
            languages,
            editableConfiguration.Lang);
        if (!languages.ContainsKey(editableConfiguration.Lang))
        {
            editableConfiguration.Lang = languages.First(
                candidate => ReferenceEquals(candidate.Value, selectedLanguage)).Key;
        }

        Echoglossian.SelectedLanguage = selectedLanguage;
        var fontSelection = PreviewFontCatalog.Resolve(
            selectedLanguage,
            editableConfiguration.FontSize);

        using PreviewHost host = new(
            new PreviewHostOptions
            {
                Width = 1400,
                Height = 900,
                Title = "Echoglossian Overlay Previewer",
            });
        var fontRuntime = new PreviewFontRuntime(
            fontSelection,
            scenario.Title,
            scenario.TranslatedText,
            host.RecreateFontDeviceTexture);
        using var composition = new PreviewOverlayRendererFactory(host).Create(
            editableConfiguration,
            fontRuntime,
            fontSelection);
        using var pluginWindowHost = CreatePreviewPluginWindowHost(
            editableConfiguration,
            languages,
            session.ClonedDatabasePath);
        using var shell = new PreviewShell(
            sourceConfiguration,
            editableConfiguration,
            fontSelection,
            composition.Renderer,
            pluginWindowHost,
            scenario,
            viewport);
        using var configSaveScope = PluginConfigSaveScope.Push(
            config => SavePreviewConfiguration(session.ClonedConfigPath, config));

        var interactiveOutputDirectory = ResolveOutputDirectory(commandLine.OutputDirectory);
        host.Run(
            () =>
            {
                composition.BeginDrawFrame();
                shell.Draw();
            },
            () =>
            {
                if (!shell.TryConsumeScreenshotRequest(
                    interactiveOutputDirectory,
                    out var request))
                {
                    return;
                }

                var outputPath = Path.Combine(
                    request.OutputDirectory,
                    ScreenshotFileName.CreatePngName(
                        request.Mode,
                        request.Scenario.Key,
                        request.Viewport,
                        request.CaptureTarget));
                try
                {
                    var crop = CalculateInteractiveCrop(
                        request,
                        shell.LastRenderResult,
                        pluginWindowHost,
                        ImGui.GetIO().DisplaySize,
                        host.FramebufferSize);

                    host.CapturePng(outputPath, crop);
                    shell.SetLastScreenshotPath(outputPath);
                }
                catch (InvalidOperationException exception)
                {
                    shell.SetLastScreenshotFailure(exception.Message);
                }
            });
    }

    /// <summary>
    ///     Runs hidden deterministic screenshot export.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    private static void RunScreenshotExport(PreviewCommandLine commandLine)
    {
        using var session = PreviewSessionLoader.Load(
            new PreviewSessionSourceOptions(
                commandLine.ConfigPath,
                commandLine.DatabasePath,
                commandLine.OutputDirectory));
        var sourceConfiguration = session.Configuration;
        var editableConfiguration = session.EditableConfiguration;
        var languages = Echoglossian.CreateLanguagesDictionary();
        var selectedLanguage = ResolvePreviewLanguage(languages, editableConfiguration.Lang);
        Echoglossian.SelectedLanguage = selectedLanguage;
        var fontSelection = PreviewFontCatalog.Resolve(
            selectedLanguage,
            editableConfiguration.FontSize);
        var outputDirectory = ResolveOutputDirectory(commandLine.OutputDirectory);
        var mode = ParseScreenshotMode(commandLine.ScreenshotMode);
        var requestedViewport = PreviewScenarioCatalog.ResolveViewport(
            commandLine.ViewportWidth,
            commandLine.ViewportHeight);
        var scenarios = mode == ScreenshotMode.Batch
            ? PreviewScenarioCatalog.Defaults
            : [PreviewScenarioCatalog.ResolveScenario(commandLine.Scenario)];
        var viewports = mode == ScreenshotMode.Batch &&
            commandLine.ViewportWidth is null &&
            commandLine.ViewportHeight is null
            ? PreviewScenarioCatalog.ViewportPresets
            : [requestedViewport];
        var requests = new List<ScreenshotRequest>();
        foreach (var viewport in viewports)
        {
            foreach (var scenario in scenarios)
            {
                requests.Add(new ScreenshotRequest(
                    mode,
                    scenario,
                    viewport,
                    outputDirectory,
                    commandLine.CaptureTarget ?? GetCaptureTarget(mode)));
            }
        }

        var runner = new BatchScreenshotRunner(
            sourceConfiguration,
            editableConfiguration,
            fontSelection,
            () => CreatePreviewPluginWindowHost(
                editableConfiguration,
                languages,
                session.ClonedDatabasePath));
        runner.Run(requests);
        Console.WriteLine($"Wrote {requests.Count} screenshot(s) to {outputDirectory}");
    }

    private static string ResolveOutputDirectory(string? outputDirectory)
    {
        return string.IsNullOrWhiteSpace(outputDirectory)
            ? ScreenshotFileName.CreateDefaultOutputDirectory(DateTimeOffset.UtcNow)
            : outputDirectory;
    }

    /// <summary>
    /// Creates a context over the preview-owned database snapshot.
    /// </summary>
    /// <param name="databasePath">The optional snapshot path.</param>
    /// <returns>The snapshot context, or <see langword="null" /> when unavailable.</returns>
    private static EchoglossianDbContext? CreatePreviewDbContext(string? databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            return null;
        }

        var options = new DbContextOptionsBuilder<EchoglossianDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;
        return new EchoglossianDbContext(options);
    }

    /// <summary>
    /// Creates real plugin windows over preview-owned configuration and database state.
    /// </summary>
    /// <param name="configuration">The preview-owned editable configuration.</param>
    /// <param name="languages">The available preview languages.</param>
    /// <param name="databasePath">The optional preview database snapshot.</param>
    /// <returns>The preview-owned plugin-window host.</returns>
    private static PreviewPluginWindowHost CreatePreviewPluginWindowHost(
        Config configuration,
        IReadOnlyDictionary<int, LanguageInfo> languages,
        string? databasePath)
    {
        var configWindowContext = CreatePreviewPluginWindowContext(
            configuration,
            languages);
        return new PreviewPluginWindowHost(
            new PluginConfigWindowRenderer(),
            configWindowContext,
            CreatePreviewDbContext(databasePath),
            configuration);
    }

    /// <summary>
    /// Creates config-window dependencies with explicit unavailable preview imagery.
    /// </summary>
    /// <param name="configuration">The preview-owned editable configuration.</param>
    /// <param name="languages">The available preview languages.</param>
    /// <returns>The preview-safe config-window context.</returns>
    internal static PluginConfigWindowContext CreatePreviewPluginWindowContext(
        Config configuration,
        IReadOnlyDictionary<int, LanguageInfo> languages)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(languages);
        return new PluginConfigWindowContext(
            configuration,
            languages,
            default,
            default,
            default,
            static () => { },
            configuration.PluginVersion)
        {
            ImagesAvailable = false,
            PushGeneralFont = static () => NoOpDisposable.Instance,
            ApplyLanguageRuntimeChanges = static (_, _) => { },
        };
    }

    /// <summary>
    /// Persists config-window edits only to the preview-owned config clone.
    /// </summary>
    /// <param name="configPath">The preview-owned config path.</param>
    /// <param name="configuration">The edited preview configuration.</param>
    private static void SavePreviewConfiguration(string configPath, Config configuration)
    {
        File.WriteAllText(
            configPath,
            JsonConvert.SerializeObject(configuration, Formatting.Indented));
    }

    internal static Rectangle? CalculateInteractiveSurfaceCrop(
        ScreenshotRequest request,
        TranslationOverlayRenderResult renderResult,
        Vector2 displaySize,
        Vector2 framebufferSize)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(renderResult);
        if (request.Mode != ScreenshotMode.Surface &&
            request.CaptureTarget != PreviewCaptureTarget.OverlaySurface)
        {
            return null;
        }

        if (displaySize.X <= 0f ||
            displaySize.Y <= 0f ||
            framebufferSize.X <= 0f ||
            framebufferSize.Y <= 0f)
        {
            return Rectangle.Empty;
        }

        var displayWidth = checked((int)MathF.Ceiling(displaySize.X));
        var displayHeight = checked((int)MathF.Ceiling(displaySize.Y));
        var framebufferScale = new Vector2(
            framebufferSize.X / displaySize.X,
            framebufferSize.Y / displaySize.Y);
        return VeldridScreenshotCapture.CalculateSurfaceCrop(
            renderResult,
            displayWidth,
            displayHeight,
            request.SurfaceMargin,
            framebufferScale);
    }

    private static ScreenshotMode ParseScreenshotMode(string? value)
    {
        return value switch
        {
            "full" => ScreenshotMode.Full,
            "surface" => ScreenshotMode.Surface,
            "batch" => ScreenshotMode.Batch,
            _ => throw new ArgumentException(
                "Previewer screenshot mode must be full, surface, or batch."),
        };
    }

    /// <summary>
    ///     Resolves preview language metadata without constructing the live plugin.
    /// </summary>
    /// <param name="languageId">The configured language identifier.</param>
    /// <returns>The language metadata used by font and RTL preview paths.</returns>
    internal static LanguageInfo ResolvePreviewLanguage(int languageId)
    {
        var languages = Echoglossian.CreateLanguagesDictionary();
        return ResolvePreviewLanguage(languages, languageId);
    }

    internal static LanguageInfo ResolvePreviewLanguage(
        IReadOnlyDictionary<int, LanguageInfo> languages,
        int languageId)
    {
        ArgumentNullException.ThrowIfNull(languages);
        if (languages.TryGetValue(languageId, out var language))
        {
            return language;
        }

        if (languages.TryGetValue(28, out var englishLanguage))
        {
            return englishLanguage;
        }

        KeyValuePair<int, LanguageInfo>? fallbackLanguage = null;
        foreach (var candidate in languages)
        {
            if (fallbackLanguage == null ||
                candidate.Key < fallbackLanguage.Value.Key)
            {
                fallbackLanguage = candidate;
            }
        }

        return fallbackLanguage?.Value ?? throw new InvalidOperationException(
            "Preview language dictionary is empty.");
    }

    /// <summary>
    /// Resolves an interactive screenshot crop from its explicit capture target.
    /// </summary>
    /// <param name="request">The interactive screenshot request.</param>
    /// <param name="renderResult">The latest overlay draw result.</param>
    /// <param name="pluginWindowHost">The real plugin-window host.</param>
    /// <param name="displaySize">The logical ImGui display dimensions.</param>
    /// <param name="framebufferSize">The physical framebuffer dimensions.</param>
    /// <returns>The requested crop, or <see langword="null" /> for the full frame.</returns>
    private static Rectangle? CalculateInteractiveCrop(
        ScreenshotRequest request,
        TranslationOverlayRenderResult renderResult,
        PreviewPluginWindowHost pluginWindowHost,
        Vector2 displaySize,
        Vector2 framebufferSize)
    {
        var crop = request.CaptureTarget switch
        {
            PreviewCaptureTarget.OverlaySurface => CalculateInteractiveSurfaceCrop(
                request,
                renderResult,
                displaySize,
                framebufferSize),
            PreviewCaptureTarget.ConfigWindow => CalculateInteractiveWindowCrop(
                pluginWindowHost.TryGetStableCrop(PreviewCaptureTarget.ConfigWindow),
                displaySize,
                framebufferSize),
            PreviewCaptureTarget.DbManagerWindow => CalculateInteractiveWindowCrop(
                pluginWindowHost.TryGetStableCrop(PreviewCaptureTarget.DbManagerWindow),
                displaySize,
                framebufferSize),
            PreviewCaptureTarget.TranslatorMetricsWindow => CalculateInteractiveWindowCrop(
                pluginWindowHost.TryGetStableCrop(PreviewCaptureTarget.TranslatorMetricsWindow),
                displaySize,
                framebufferSize),
            _ => null,
        };

        return PreviewPluginWindowHost.IsPluginWindowTarget(request.CaptureTarget)
            ? BatchScreenshotRunner.RequireWindowCrop(crop, request.CaptureTarget)
            : crop;
    }

    /// <summary>
    /// Converts logical ImGui plugin-window bounds into physical framebuffer pixels.
    /// </summary>
    /// <param name="logicalCrop">The plugin window crop in logical ImGui coordinates.</param>
    /// <param name="displaySize">The logical ImGui display dimensions.</param>
    /// <param name="framebufferSize">The physical framebuffer dimensions.</param>
    /// <returns>The physical crop, or <see langword="null" /> when no window bounds are available.</returns>
    internal static Rectangle? CalculateInteractiveWindowCrop(
        Rectangle? logicalCrop,
        Vector2 displaySize,
        Vector2 framebufferSize)
    {
        if (logicalCrop is not { } crop)
        {
            return null;
        }

        if (displaySize.X <= 0f ||
            displaySize.Y <= 0f ||
            framebufferSize.X <= 0f ||
            framebufferSize.Y <= 0f)
        {
            return Rectangle.Empty;
        }

        var framebufferScale = new Vector2(
            framebufferSize.X / displaySize.X,
            framebufferSize.Y / displaySize.Y);
        return Rectangle.FromLTRB(
            checked((int)MathF.Floor(crop.Left * framebufferScale.X)),
            checked((int)MathF.Floor(crop.Top * framebufferScale.Y)),
            checked((int)MathF.Ceiling(crop.Right * framebufferScale.X)),
            checked((int)MathF.Ceiling(crop.Bottom * framebufferScale.Y)));
    }

    /// <summary>
    /// Maps command-line screenshot modes to their default capture targets.
    /// </summary>
    /// <param name="mode">The selected screenshot mode.</param>
    /// <returns>The matching capture target.</returns>
    private static PreviewCaptureTarget GetCaptureTarget(ScreenshotMode mode)
    {
        return mode == ScreenshotMode.Surface
            ? PreviewCaptureTarget.OverlaySurface
            : PreviewCaptureTarget.FullFrame;
    }

    /// <summary>
    /// Provides a no-op scope when the standalone default font is already active.
    /// </summary>
    private sealed class NoOpDisposable : IDisposable
    {
        /// <summary>Gets the shared no-op scope.</summary>
        internal static NoOpDisposable Instance { get; } = new();

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
