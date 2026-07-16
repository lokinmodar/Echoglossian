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
using Echoglossian.Previewer.UI;

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

            if (!commandLine.BindingSmoke && !commandLine.HostSmoke)
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

        host.Run(shell.Draw);
    }

    /// <summary>
    ///     Resolves preview language metadata without constructing the live plugin.
    /// </summary>
    /// <param name="languageId">The configured language identifier.</param>
    /// <returns>The language metadata used by font and RTL preview paths.</returns>
    private static LanguageInfo ResolvePreviewLanguage(int languageId)
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
