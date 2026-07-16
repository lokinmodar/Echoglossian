// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.LanguagesHandling;
using Echoglossian.Previewer.Configuration;
using Echoglossian.Previewer.Fonts;
using Echoglossian.Previewer.Hosting;
using Echoglossian.Previewer.Scenarios;
using Echoglossian.Previewer.Screenshots;
using Echoglossian.Previewer.UI;

using System.Drawing;

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
        var sourceConfiguration = PreviewConfigLoader.Load(commandLine.ConfigPath);
        var editableConfiguration = sourceConfiguration.CreateEditableCopy();
        var scenario = PreviewScenarioCatalog.ResolveScenario(commandLine.Scenario);
        var viewport = PreviewScenarioCatalog.ResolveViewport(
            commandLine.ViewportWidth,
            commandLine.ViewportHeight);
        var selectedLanguage = ResolvePreviewLanguage(editableConfiguration.Lang);
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
            fontRuntime);
        var shell = new PreviewShell(
            sourceConfiguration,
            editableConfiguration,
            fontSelection,
            composition.Renderer,
            scenario,
            viewport);

        var interactiveOutputDirectory = ResolveOutputDirectory(commandLine.OutputDirectory);
        host.Run(
            shell.Draw,
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
                        request.Viewport));
                Rectangle? crop = null;
                if (request.Mode == ScreenshotMode.Surface)
                {
                    crop = VeldridScreenshotCapture.CalculateSurfaceCrop(
                        shell.LastRenderResult,
                        request.Viewport.Width,
                        request.Viewport.Height,
                        request.SurfaceMargin,
                        framebufferScale: 1f);
                }

                host.CapturePng(outputPath, crop);
                shell.SetLastScreenshotPath(outputPath);
            });
    }

    /// <summary>
    ///     Runs hidden deterministic screenshot export.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    private static void RunScreenshotExport(PreviewCommandLine commandLine)
    {
        var sourceConfiguration = PreviewConfigLoader.Load(commandLine.ConfigPath);
        var editableConfiguration = sourceConfiguration.CreateEditableCopy();
        var selectedLanguage = ResolvePreviewLanguage(editableConfiguration.Lang);
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
                    outputDirectory));
            }
        }

        var runner = new BatchScreenshotRunner(
            sourceConfiguration,
            editableConfiguration,
            fontSelection);
        runner.Run(requests);
        Console.WriteLine($"Wrote {requests.Count} screenshot(s) to {outputDirectory}");
    }

    private static string ResolveOutputDirectory(string? outputDirectory)
    {
        return string.IsNullOrWhiteSpace(outputDirectory)
            ? ScreenshotFileName.CreateDefaultOutputDirectory(DateTimeOffset.UtcNow)
            : outputDirectory;
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
        return languageId switch
        {
            2 => new LanguageInfo(
                "ar",
                "Arabic",
                "NotoSansArabic-Medium.ttf",
                string.Empty,
                new List<int> { 0, 1 }),
            42 => new LanguageInfo(
                "he",
                "Hebrew",
                "NotoSansHebrew-Medium.ttf",
                string.Empty,
                new List<int>()),
            _ => new LanguageInfo(
                "en",
                "English",
                "NotoSans-Medium.ttf",
                string.Empty,
                new List<int>()),
        };
    }
}
