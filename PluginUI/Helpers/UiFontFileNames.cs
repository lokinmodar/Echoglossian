// <copyright file="UiFontFileNames.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
/// Defines the common font files used by the plugin and standalone previewer.
/// </summary>
public static class UiFontFileNames
{
    /// <summary>
    /// Gets the primary Latin font file name.
    /// </summary>
    public const string BaseFontFileName = "NotoSans-Medium.ttf";

    /// <summary>
    /// Gets the symbol font file name.
    /// </summary>
    public const string SymbolsFontFileName = "symbols.ttf";

    /// <summary>
    /// Gets the font used to initialize language-specific font stacks.
    /// </summary>
    public const string DummyFontFileName = "NotoSans-Regular.ttf";

    /// <summary>
    /// Gets the font used for the language-selection control.
    /// </summary>
    public const string LanguageComboFontFileName = "NotoSans-Medium-Custom2.otf";

    /// <summary>
    /// Gets the complementary Japanese variable-font file names.
    /// </summary>
    public static IReadOnlyList<string> ComplementaryFontFileNames { get; } =
    [
        "NotoSansJP-VF-3.ttf",
        "NotoSansJP-VF-4.ttf",
        "NotoSansJP-VF-5.ttf",
        "NotoSansJP-VF-6.ttf",
        "NotoSansJP-VF-7.ttf",
    ];

    /// <summary>
    /// Resolves one font file below a plugin or previewer root directory.
    /// </summary>
    /// <param name="rootDirectory">The root containing the <c>Font</c> directory.</param>
    /// <param name="fileName">The font file name.</param>
    /// <returns>The absolute or relative font path.</returns>
    public static string ResolvePath(string rootDirectory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return Path.Combine(rootDirectory, "Font", fileName);
    }
}
