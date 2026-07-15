// <copyright file="PreviewFontCatalog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;
using Echoglossian.PluginUI.Helpers;

namespace Echoglossian.Previewer.Fonts;

/// <summary>
/// Resolves the plugin's existing font stack for standalone previewing.
/// </summary>
public static class PreviewFontCatalog
{
    /// <summary>
    /// Resolves the selected language font and common plugin font files.
    /// </summary>
    /// <param name="selectedLanguage">The language selected by preview configuration.</param>
    /// <param name="fontSize">The configured plugin font size.</param>
    /// <param name="rootDirectory">The directory containing the <c>Font</c> directory.</param>
    /// <returns>The resolved preview font selection.</returns>
    public static PreviewFontSelection Resolve(
        LanguageInfo selectedLanguage,
        int fontSize,
        string? rootDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(selectedLanguage);

        var resolvedRoot = Path.GetFullPath(rootDirectory ?? AppContext.BaseDirectory);
        var generalFontPaths = new List<string>
        {
            UiFontFileNames.ResolvePath(resolvedRoot, UiFontFileNames.LanguageComboFontFileName),
            UiFontFileNames.ResolvePath(resolvedRoot, UiFontFileNames.SymbolsFontFileName),
            UiFontFileNames.ResolvePath(resolvedRoot, UiFontFileNames.BaseFontFileName),
        };

        generalFontPaths.AddRange(
            UiFontFileNames.ResolveComplementaryPaths(resolvedRoot));

        var specialFontPath = UiFontFileNames.ResolvePath(
            resolvedRoot,
            selectedLanguage.FontName);
        if (!generalFontPaths.Contains(specialFontPath, StringComparer.OrdinalIgnoreCase))
        {
            generalFontPaths.Add(specialFontPath);
        }

        var languageFontPaths = new List<string>
        {
            UiFontFileNames.ResolvePath(resolvedRoot, UiFontFileNames.DummyFontFileName),
            UiFontFileNames.ResolvePath(resolvedRoot, UiFontFileNames.SymbolsFontFileName),
            UiFontFileNames.ResolvePath(resolvedRoot, UiFontFileNames.BaseFontFileName),
        };
        if (!languageFontPaths.Contains(specialFontPath, StringComparer.OrdinalIgnoreCase))
        {
            languageFontPaths.Add(specialFontPath);
        }

        return new PreviewFontSelection(
            selectedLanguage,
            fontSize,
            specialFontPath,
            generalFontPaths,
            languageFontPaths);
    }
}

/// <summary>
/// Describes the resolved font files and selected language for a preview session.
/// </summary>
public sealed class PreviewFontSelection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewFontSelection" /> class.
    /// </summary>
    /// <param name="selectedLanguage">The selected language.</param>
    /// <param name="fontSize">The configured font size.</param>
    /// <param name="specialFontPath">The language-specific font path.</param>
    /// <param name="generalFontPaths">The plugin general font stack in merge order.</param>
    /// <param name="languageFontPaths">The plugin language font stack in merge order.</param>
    internal PreviewFontSelection(
        LanguageInfo selectedLanguage,
        int fontSize,
        string specialFontPath,
        IReadOnlyList<string> generalFontPaths,
        IReadOnlyList<string> languageFontPaths)
    {
        this.SelectedLanguage = selectedLanguage;
        this.FontSize = fontSize;
        this.SpecialFontPath = specialFontPath;
        this.GeneralFontPaths = generalFontPaths;
        this.LanguageFontPaths = languageFontPaths;
    }

    /// <summary>
    /// Gets the selected language definition.
    /// </summary>
    public LanguageInfo SelectedLanguage { get; }

    /// <summary>
    /// Gets the configured font size in pixels.
    /// </summary>
    public int FontSize { get; }

    /// <summary>
    /// Gets the selected language-specific font path.
    /// </summary>
    public string SpecialFontPath { get; }

    /// <summary>
    /// Gets the complete plugin font stack for output validation.
    /// </summary>
    public IReadOnlyList<string> FontPaths => this.GeneralFontPaths
        .Concat(this.LanguageFontPaths)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Gets the plugin general font stack in merge order.
    /// </summary>
    public IReadOnlyList<string> GeneralFontPaths { get; }

    /// <summary>
    /// Gets the plugin language-specific font stack in merge order.
    /// </summary>
    public IReadOnlyList<string> LanguageFontPaths { get; }
}
