// <copyright file="TranslatorContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators;
using Echoglossian.Translators.LibreTranslate;

using Dalamud.Plugin.Services;
using Serilog;
using Serilog.Events;
using System.Globalization;

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
    ///     Ensures recoverable Google response-shape failures are logged as
    ///     warnings instead of errors.
    /// </summary>
    [Fact]
    public void Google_LogRecoverableResponseFailure_UsesWarning()
    {
        var pluginLog = new CapturingPluginLog();

        GoogleTranslator.LogRecoverableResponseFailure(
            pluginLog,
            "Dzemael Darkhold",
            200,
            "OK",
            "{ \"status\": 404 }");

        Assert.Empty(pluginLog.ErrorMessages);
        Assert.Single(pluginLog.WarningMessages);
        Assert.Contains(
            "returned no translateResponse.translateText",
            pluginLog.WarningMessages[0],
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures prompt variable expansion does not reprocess placeholders that
    ///     happen to appear inside the source text itself.
    /// </summary>
    [Fact]
    public void PromptTemplateManager_RenderPrompt_DoesNotReprocessInsertedText()
    {
        var template = "From {sourceLanguage} to {targetLanguage}: {text}";
        var text = "Keep literal {sourceLanguage} and {targetLanguage} tokens.";

        var prompt = PromptTemplateManager.RenderPrompt(
            template,
            text,
            "English",
            "Portuguese");

        Assert.Equal(
            "From English to Portuguese: Keep literal {sourceLanguage} and {targetLanguage} tokens.",
            prompt);
    }

    /// <summary>
    ///     Ensures prompt variable expansion still resolves all standard placeholders.
    /// </summary>
    [Fact]
    public void PromptTemplateManager_RenderPrompt_ReplacesStandardPlaceholders()
    {
        var prompt = PromptTemplateManager.RenderPrompt(
            "Translate {text} from {sourceLanguage} to {targetLanguage}.",
            "hello",
            "English",
            "Portuguese");

        Assert.Equal(
            "Translate hello from English to Portuguese.",
            prompt);
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

    private sealed class CapturingPluginLog : IPluginLog
    {
        private readonly List<string> errorMessages = [];
        private readonly List<string> warningMessages = [];

        /// <summary>
        ///     Gets captured error messages.
        /// </summary>
        public IReadOnlyList<string> ErrorMessages => this.errorMessages;

        /// <summary>
        ///     Gets captured warning messages.
        /// </summary>
        public IReadOnlyList<string> WarningMessages => this.warningMessages;

        /// <summary>
        ///     Gets the inert Serilog logger required by the interface.
        /// </summary>
        public ILogger Logger { get; } = new LoggerConfiguration().CreateLogger();

        /// <summary>
        ///     Gets or sets the minimum log level accepted by this logger.
        /// </summary>
        public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Verbose;

        /// <inheritdoc/>
        public void Fatal(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Fatal(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Error(string messageTemplate, params object[] values)
        {
            this.errorMessages.Add(Format(messageTemplate, values));
        }

        /// <inheritdoc/>
        public void Error(Exception? exception, string messageTemplate, params object[] values)
        {
            this.errorMessages.Add(Format(messageTemplate, values));
        }

        /// <inheritdoc/>
        public void Warning(string messageTemplate, params object[] values)
        {
            this.warningMessages.Add(Format(messageTemplate, values));
        }

        /// <inheritdoc/>
        public void Warning(Exception? exception, string messageTemplate, params object[] values)
        {
            this.warningMessages.Add(Format(messageTemplate, values));
        }

        /// <inheritdoc/>
        public void Information(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Information(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Info(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Info(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Debug(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Debug(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Verbose(string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Verbose(Exception? exception, string messageTemplate, params object[] values)
        {
        }

        /// <inheritdoc/>
        public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values)
        {
        }

        private static string Format(string messageTemplate, object[] values)
        {
            return values.Length == 0
                ? messageTemplate
                : string.Format(
                    CultureInfo.InvariantCulture,
                    messageTemplate,
                    values);
        }
    }
}
