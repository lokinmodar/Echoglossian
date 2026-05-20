// <copyright file="TranslatorContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.LibreTranslate;
using Echoglossian.PluginUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers deterministic translator-specific request and language-shaping contracts without live network calls.
/// </summary>
public class TranslatorContractTests
{
    /// <summary>
    ///     Ensures DeepL source-language normalization matches the current runtime contract.
    /// </summary>
    [Theory]
    [InlineData("English", "EN")]
    [InlineData("Japanese", "JA")]
    [InlineData("German", "DE")]
    [InlineData("French", "FR")]
    [InlineData("Unknown", "EN")]
    public void DeepL_FormatSourceLanguageCode_ReturnsExpectedCodes(string source, string expected)
    {
        var actual = DeepLTranslator.FormatSourceLanguageCode(source);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Ensures DeepL target-language normalization preserves the repo's language aliases.
    /// </summary>
    [Theory]
    [InlineData("en", "EN-US")]
    [InlineData("no", "NB")]
    [InlineData("pt", "PT-BR")]
    [InlineData("pt-PT", "PT-PT")]
    [InlineData("zh-CN", "ZH")]
    [InlineData("it", "IT")]
    [InlineData("de", "DE")]
    public void DeepL_FormatTargetLanguageCode_ReturnsExpectedCodes(string target, string expected)
    {
        var actual = DeepLTranslator.FormatTargetLanguageCode(target);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, DeepLTranslator.FormatFreeTargetLanguageCode(target));
    }

    /// <summary>
    ///     Ensures Google V0 URL construction keeps language codes and escapes text correctly.
    /// </summary>
    [Fact]
    public void Google_BuildV0Url_EncodesAndPreservesLanguageCodes()
    {
        var url = GoogleTranslator.BuildV0Url(
            "https://translate.google.com/m",
            "hello world",
            "en",
            "pt-BR");

        Assert.Contains("sl=en", url, StringComparison.Ordinal);
        Assert.Contains("tl=pt-BR", url, StringComparison.Ordinal);
        Assert.Contains("q=hello%20world", url, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures Google V2 URL construction preserves the selected output language and escapes the term.
    /// </summary>
    [Fact]
    public void Google_BuildV2Url_EncodesAndPreservesTargetLanguage()
    {
        var url = GoogleTranslator.BuildV2Url(
            "https://dictionaryextension-pa.googleapis.com/v1/dictionaryExtensionData",
            "Blue Magic Spellbook",
            "pt-BR",
            "api-key",
            "2");

        Assert.Contains("language=pt-BR", url, StringComparison.Ordinal);
        Assert.Contains("term=Blue%20Magic%20Spellbook", url, StringComparison.Ordinal);
        Assert.Contains("strategy=2", url, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures Microsoft request URLs keep the expected query shape.
    /// </summary>
    [Fact]
    public void Microsoft_BuildRequestUrl_ReturnsExpectedQueryShape()
    {
        var url = MicrosoftTranslator.BuildRequestUrl(
            "https://api.cognitive.microsofttranslator.com",
            "en",
            "pt-BR");

        Assert.Equal(
            "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from=en&to=pt-BR&category=general&profanityAction=NoAction&textType=plain",
            url);
    }

    /// <summary>
    ///     Ensures Yandex public API language-pair building uses the current hot-patch rules.
    /// </summary>
    [Theory]
    [InlineData("English", "pt", "en-pt-BR")]
    [InlineData("Français", "zh-CN", "fr-zh")]
    [InlineData("", "pt-PT", "pt")]
    public void YandexPublic_BuildLanguagePair_UsesExpectedNormalization(string fromLanguage, string toLanguage, string expected)
    {
        var actual = YandexPublicTranslator.BuildLanguagePair(fromLanguage, toLanguage);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Ensures LibreTranslate endpoint resolution follows the configured instance mode.
    /// </summary>
    [Fact]
    public void LibreTranslate_DetermineEndpoint_ResolvesConfiguredInstance()
    {
        var customConfig = new Config
        {
            LibreTranslateInstanceType = LibreTranslateInstanceType.Custom,
            LibreTranslateUrl = "https://example.test/",
        };

        var deConfig = new Config
        {
            LibreTranslateInstanceType = LibreTranslateInstanceType.De,
        };

        Assert.Equal(
            "https://example.test/translate",
            LibreTranslateTranslator.DetermineEndpoint(customConfig));
        Assert.Equal(
            "https://libretranslate.de/translate",
            LibreTranslateTranslator.DetermineEndpoint(deConfig));
    }

    /// <summary>
    ///     Ensures OpenRouter prompt expansion preserves placeholder-like text inside the translated input.
    /// </summary>
    [Fact]
    public void OpenRouter_BuildPrompt_DoesNotReprocessInsertedText()
    {
        var prompt = OpenRouterTranslator.BuildPrompt(
            "Translate from {sourceLanguage} to {targetLanguage}: {text}",
            "Keep literal {sourceLanguage} and {targetLanguage} tokens.",
            "en",
            "pt-BR");

        Assert.Equal(
            "Translate from en to pt-BR: Keep literal {sourceLanguage} and {targetLanguage} tokens.",
            prompt);
    }

    /// <summary>
    ///     Ensures OpenRouter prompt expansion falls back to the shared default template when the config prompt is blank.
    /// </summary>
    [Fact]
    public void OpenRouter_BuildPrompt_UsesDefaultTemplateWhenBlank()
    {
        var prompt = OpenRouterTranslator.BuildPrompt(
            "   ",
            "hello",
            "en",
            "pt-BR");

        Assert.Equal(
            PromptTemplateManager.DefaultPrompt
                .Replace("{sourceLanguage}", "en", StringComparison.Ordinal)
                .Replace("{targetLanguage}", "pt-BR", StringComparison.Ordinal)
                .Replace("{text}", "hello", StringComparison.Ordinal),
            prompt);
    }

    /// <summary>
    ///     Ensures ChatGPT prompt expansion preserves placeholder-like text inside the translated input.
    /// </summary>
    [Fact]
    public void ChatGpt_BuildPrompt_DoesNotReprocessInsertedText()
    {
        var prompt = ChatGPTTranslator.BuildPrompt(
            "Translate from {sourceLanguage} to {targetLanguage}: {text}",
            "Keep literal {sourceLanguage} and {targetLanguage} tokens.",
            "en",
            "pt-BR");

        Assert.Equal(
            "Translate from en to pt-BR: Keep literal {sourceLanguage} and {targetLanguage} tokens.",
            prompt);
    }

    /// <summary>
    ///     Ensures ChatGPT prompt expansion falls back to the shared default template when the config prompt is blank.
    /// </summary>
    [Fact]
    public void ChatGpt_BuildPrompt_UsesDefaultTemplateWhenBlank()
    {
        var prompt = ChatGPTTranslator.BuildPrompt(
            "   ",
            "hello",
            "en",
            "pt-BR");

        Assert.Equal(
            PromptTemplateManager.DefaultPrompt
                .Replace("{sourceLanguage}", "en", StringComparison.Ordinal)
                .Replace("{targetLanguage}", "pt-BR", StringComparison.Ordinal)
                .Replace("{text}", "hello", StringComparison.Ordinal),
            prompt);
    }
}
