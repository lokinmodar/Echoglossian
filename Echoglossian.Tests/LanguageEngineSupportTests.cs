// <copyright file="LanguageEngineSupportTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers language-to-engine support mapping for broad-coverage LLM engines and
///     vendor-backed language tables.
/// </summary>
public class LanguageEngineSupportTests
{
    /// <summary>
    ///     Ensures Claude is treated as a broad-coverage LLM engine and is added to language support lists.
    /// </summary>
    [Fact]
    public void ApplySupportTo_AddsClaudeToSupportedEngines()
    {
        Dictionary<int, LanguageInfo> languages = new()
        {
            [0] = new LanguageInfo("pt-BR", "Portuguese", "NotoSans-Medium.ttf", string.Empty, new List<int>()),
        };

        LanguageEngineSupport.ApplySupportTo(languages);

        Assert.Contains((int)Echoglossian.TransEngines.Claude, languages[0].SupportedEngines!);
    }

    /// <summary>
    ///     Ensures DeepL now exposes newer official target languages such as Afrikaans.
    /// </summary>
    [Fact]
    public void ApplySupportTo_AddsDeepLToAfrikaans()
    {
        Dictionary<int, LanguageInfo> languages = new()
        {
            [0] = new LanguageInfo("af", "Afrikaans", "NotoSans-Medium.ttf", string.Empty, new List<int>()),
        };

        LanguageEngineSupport.ApplySupportTo(languages);

        Assert.Contains((int)Echoglossian.TransEngines.Deepl, languages[0].SupportedEngines!);
    }

    /// <summary>
    ///     Ensures Microsoft language aliases still resolve for plugin codes such as traditional Chinese.
    /// </summary>
    [Fact]
    public void ApplySupportTo_AddsMicrosoftToTraditionalChineseViaZhHant()
    {
        Dictionary<int, LanguageInfo> languages = new()
        {
            [0] = new LanguageInfo("zh-TW", "Traditional Chinese", "NotoSans-Medium.ttf", string.Empty, new List<int>()),
        };

        LanguageEngineSupport.ApplySupportTo(languages);

        Assert.Contains((int)Echoglossian.TransEngines.Microsoft, languages[0].SupportedEngines!);
    }

    /// <summary>
    ///     Ensures Amazon language tables now reflect newer official target languages such as Welsh.
    /// </summary>
    [Fact]
    public void ApplySupportTo_AddsAmazonToWelsh()
    {
        Dictionary<int, LanguageInfo> languages = new()
        {
            [0] = new LanguageInfo("cy", "Welsh", "NotoSans-Medium.ttf", string.Empty, new List<int>()),
        };

        LanguageEngineSupport.ApplySupportTo(languages);

        Assert.Contains((int)Echoglossian.TransEngines.Amazon, languages[0].SupportedEngines!);
    }

    /// <summary>
    ///     Ensures LibreTranslate now reflects the current upstream Hebrew support.
    /// </summary>
    [Fact]
    public void ApplySupportTo_AddsLibreTranslateToHebrew()
    {
        Dictionary<int, LanguageInfo> languages = new()
        {
            [0] = new LanguageInfo("he", "Hebrew", "NotoSans-Medium.ttf", string.Empty, new List<int>()),
        };

        LanguageEngineSupport.ApplySupportTo(languages);

        Assert.Contains((int)Echoglossian.TransEngines.LibreTranslate, languages[0].SupportedEngines!);
    }

    /// <summary>
    ///     Ensures the Yandex language table includes the current documented Scottish Gaelic support.
    /// </summary>
    [Fact]
    public void ApplySupportTo_AddsYandexCloudToScottishGaelic()
    {
        Dictionary<int, LanguageInfo> languages = new()
        {
            [0] = new LanguageInfo("gd", "Scottish Gaelic", "NotoSans-Medium.ttf", string.Empty, new List<int>()),
        };

        LanguageEngineSupport.ApplySupportTo(languages);

        Assert.Contains((int)Echoglossian.TransEngines.YandexCloud, languages[0].SupportedEngines!);
        Assert.Contains((int)Echoglossian.TransEngines.YandexPublic, languages[0].SupportedEngines!);
    }
}
