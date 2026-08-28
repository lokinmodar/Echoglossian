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
    /// Ensures preview startup configures downloaded-font asset state and
    /// reports a missing selected language font.
    /// </summary>
    [Fact]
    public void InitializePreviewAssets_MissingDownloadedLanguageFont_ReportsDiagnostic()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var originalAssetsPath = global::Echoglossian.AssetsManager.AssetsPath;
        var originalAssetFiles = global::Echoglossian.AssetsManager.AssetFiles;
        var configuration = new Config();
        var language = new LanguageInfo(
            "ja",
            "Japanese",
            "NotoSansCJKjp-Regular.otf",
            string.Empty,
            []);

        try
        {
            var diagnostics = PreviewFontCatalog.InitializePreviewAssets(
                language,
                configuration,
                tempDirectory.FullName);

            Assert.Equal(Path.Combine(tempDirectory.FullName, "Font"), global::Echoglossian.AssetsManager.AssetsPath);
            Assert.Contains("NotoSansCJKjp-Regular.otf", global::Echoglossian.AssetsManager.AssetFiles);
            Assert.False(configuration.PluginAssetsDownloaded);
            Assert.Contains(
                diagnostics,
                diagnostic => diagnostic.Contains("NotoSansCJKjp-Regular.otf", StringComparison.Ordinal) &&
                    diagnostic.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            global::Echoglossian.AssetsManager.AssetsPath = originalAssetsPath;
            global::Echoglossian.AssetsManager.AssetFiles = originalAssetFiles;
            tempDirectory.Delete(recursive: true);
        }
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
    /// Ensures startup language resolution repairs an unknown configured identifier.
    /// </summary>
    [Fact]
    public void NormalizePreviewLanguage_UnknownConfiguredId_WritesFallbackKey()
    {
        var languages = new Dictionary<int, LanguageInfo>
        {
            [28] = new LanguageInfo("en", "English", "en.ttf", string.Empty, []),
            [7] = new LanguageInfo("aa", "A Test", "a.ttf", string.Empty, []),
        };
        var configuration = new Config { Lang = 12345 };

        var language = Program.NormalizePreviewLanguage(languages, configuration);

        Assert.Equal(28, configuration.Lang);
        Assert.Same(languages[28], language);
    }

    /// <summary>
    /// Ensures startup language resolution preserves a valid configured identifier.
    /// </summary>
    [Fact]
    public void NormalizePreviewLanguage_KnownConfiguredId_PreservesKey()
    {
        var languages = new Dictionary<int, LanguageInfo>
        {
            [28] = new LanguageInfo("en", "English", "en.ttf", string.Empty, []),
            [7] = new LanguageInfo("aa", "A Test", "a.ttf", string.Empty, []),
        };
        var configuration = new Config { Lang = 7 };

        var language = Program.NormalizePreviewLanguage(languages, configuration);

        Assert.Equal(7, configuration.Lang);
        Assert.Same(languages[7], language);
    }

    /// <summary>
    /// Ensures preview startup applies the same engine support and language presentation flags as the plugin.
    /// </summary>
    [Fact]
    public void InitializePreviewLanguageRuntime_AppliesSupportAndPresentationFlags()
    {
        var previousLanguageInt = global::Echoglossian.Echoglossian.LanguageInt;
        var previousSelectedLanguage = global::Echoglossian.Echoglossian.SelectedLanguage;
        var previousLanguages = global::Echoglossian.Echoglossian.LangDict;
        var configuration = new Config { Lang = 21 };

        try
        {
            var (languages, selectedLanguage) = Program.InitializePreviewLanguageRuntime(configuration);

            Assert.Same(languages[21], selectedLanguage);
            Assert.Same(languages, global::Echoglossian.Echoglossian.LangDict);
            Assert.Equal(21, global::Echoglossian.Echoglossian.LanguageInt);
            Assert.Same(selectedLanguage, global::Echoglossian.Echoglossian.SelectedLanguage);
            Assert.Contains(2, selectedLanguage.SupportedEngines!);
            Assert.True(configuration.OverlayOnlyLanguage);
            Assert.False(configuration.UnsupportedLanguage);
        }
        finally
        {
            global::Echoglossian.Echoglossian.LanguageInt = previousLanguageInt;
            global::Echoglossian.Echoglossian.SelectedLanguage = previousSelectedLanguage;
            global::Echoglossian.Echoglossian.LangDict = previousLanguages;
        }
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
