// <copyright file="PreviewFontCatalogTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;
using Echoglossian.Previewer;
using Echoglossian.Previewer.Fonts;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;

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
    /// Ensures rasterized general/original text uses the plugin base font.
    /// </summary>
    [Fact]
    public void ResolveRasterFontPath_GeneralText_UsesPluginBaseFont()
    {
        var repositoryRoot = FindRepositoryRoot();
        var language = new LanguageInfo(
            "ar",
            "Arabic",
            "NotoSansArabic-Medium.ttf",
            string.Empty,
            new List<int>());
        var selection = PreviewFontCatalog.Resolve(language, 18, repositoryRoot);
        var request = new TextLayoutRequest(
            "Original text",
            2,
            "ar",
            480f,
            1f,
            ShouldUseGeneralFont: true,
            Vector4.One,
            Vector4.Zero,
            TranslationOverlaySurfaceId.Talk,
            CenterAligned: false);

        var fontPath = selection.ResolveRasterFontPath(request);

        Assert.Equal(selection.GeneralRasterFontPath, fontPath);
        Assert.EndsWith(
            Path.Combine("Font", UiFontFileNames.BaseFontFileName),
            fontPath);
    }

    /// <summary>
    /// Ensures the preview font atlas always includes editable Latin text.
    /// </summary>
    [Fact]
    public void BuildGlyphRanges_IncludesDefaultLatinRange()
    {
        var ranges = PreviewFontRuntime.BuildGlyphRanges(
            title: string.Empty,
            text: string.Empty,
            exclusiveCharacters: string.Empty);

        Assert.Contains((ushort)'x', ranges);
        Assert.Contains((ushort)'é', ranges);
        Assert.Equal(0, ranges[^1]);
    }

    /// <summary>
    /// Ensures preview language resolution uses the plugin's complete language
    /// dictionary instead of a small preview-only subset.
    /// </summary>
    /// <param name="languageId">The configured plugin language identifier.</param>
    /// <param name="languageCode">The expected language code.</param>
    /// <param name="fontFileName">The expected selected plugin font file.</param>
    [Theory]
    [InlineData(2, "ar", "NotoSansArabic-Medium.ttf")]
    [InlineData(21, "zh-CN", "NotoSansCJKsc-Regular.otf")]
    [InlineData(22, "zh-TW", "NotoSansCJKtc-Regular.otf")]
    [InlineData(50, "ja", "NotoSansCJKjp-Regular.otf")]
    [InlineData(56, "ko", "NotoSansCJKkr-Regular.otf")]
    public void ResolvePreviewLanguage_UsesPluginLanguageDictionary(
        int languageId,
        string languageCode,
        string fontFileName)
    {
        var language = Program.ResolvePreviewLanguage(languageId);

        Assert.Equal(languageCode, language.Code);
        Assert.Equal(fontFileName, language.FontName);
    }

    /// <summary>
    /// Ensures preview language resolution falls back safely even if the
    /// historical English key is missing from the dictionary.
    /// </summary>
    [Fact]
    public void ResolvePreviewLanguage_MissingConfiguredIdAndEnglish_FallsBackToLowestKey()
    {
        var languages = new Dictionary<int, LanguageInfo>
        {
            [99] = new LanguageInfo("zz", "Zulu Test", "z.ttf", string.Empty, []),
            [7] = new LanguageInfo("aa", "A Test", "a.ttf", string.Empty, []),
        };

        var language = Program.ResolvePreviewLanguage(languages, languageId: 12345);

        Assert.Equal("aa", language.Code);
        Assert.Equal("a.ttf", language.FontName);
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
