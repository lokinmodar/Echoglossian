// <copyright file="PreviewFontCatalogTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;
using Echoglossian.Previewer.Fonts;
using Echoglossian.PluginUI.Helpers;

using Xunit;

namespace Echoglossian.Previewer.Tests.Fonts;

/// <summary>
/// Covers the previewer's shared plugin font selection.
/// </summary>
public sealed class PreviewFontCatalogTests
{
    /// <summary>
    /// Ensures font resolution cannot escape the configured font directory.
    /// </summary>
    /// <param name="fileName">The unsafe font file name.</param>
    [Theory]
    [InlineData("..\\outside.ttf")]
    [InlineData("nested\\font.ttf")]
    [InlineData("nested/font.ttf")]
    [InlineData("C:\\outside.ttf")]
    public void ResolvePath_RejectsUnsafeFontFileNames(string fileName)
    {
        Assert.Throws<ArgumentException>(
            () => UiFontFileNames.ResolvePath("font-root", fileName));
    }

    /// <summary>
    /// Ensures the configured language font and shared font stack resolve to source files.
    /// </summary>
    /// <param name="languageCode">The selected language code.</param>
    /// <param name="fontFileName">The selected plugin font file.</param>
    [Theory]
    [InlineData("en", "NotoSans-Medium.ttf")]
    [InlineData("ar", "NotoSansArabic-Medium.ttf")]
    [InlineData("he", "NotoSansHebrew-Medium.ttf")]
    [InlineData("ja", "NotoSansCJKjp-Regular.otf")]
    [InlineData("ko", "NotoSansCJKkr-Regular.otf")]
    [InlineData("zh-CN", "NotoSansCJKsc-Regular.otf")]
    [InlineData("zh-TW", "NotoSansCJKtc-Regular.otf")]
    public void Resolve_UsesSelectedLanguageFontAndConfiguredSize(
        string languageCode,
        string fontFileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var language = new LanguageInfo(
            languageCode,
            languageCode,
            fontFileName,
            string.Empty,
            new List<int>());

        var selection = PreviewFontCatalog.Resolve(language, 31, repositoryRoot);

        Assert.Equal(31, selection.FontSize);
        Assert.EndsWith(Path.Combine("Font", fontFileName), selection.SpecialFontPath);
        Assert.All(selection.FontPaths, path => Assert.True(File.Exists(path), path));
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Echoglossian repository root.");
    }
}
