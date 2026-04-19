// <copyright file="TranslatorFactoryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.LanguagesHandling;
using Echoglossian.Tests.TestDoubles;
using Echoglossian.Translators;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers concrete translator construction for every configured engine without invoking live translation requests.
/// </summary>
public class TranslatorFactoryTests
{
    /// <summary>
    ///     Ensures every concrete engine can be instantiated through the shared factory.
    /// </summary>
    [Fact]
    public void Create_ConstructsExpectedTranslatorForEveryConcreteEngine()
    {
        var config = CreateSafeConfig();
        var pluginLog = new NoOpPluginLog();
        Echoglossian.SelectedLanguage = new LanguageInfo(
            "pt",
            "Português Brasileiro; Brazilian Portuguese",
            "NotoSans-Medium.ttf",
            string.Empty,
            new List<int>());

        var concreteEngines = Enum.GetValues<Echoglossian.TransEngines>()
            .Where(static engine => engine != Echoglossian.TransEngines.All)
            .ToArray();

        foreach (var engine in concreteEngines)
        {
            var translator = TranslatorFactory.Create(engine, config, pluginLog);
            Assert.NotNull(translator);
            Assert.Equal(
                TranslatorEngineMap.GetImplementationName(engine),
                translator.GetType().Name);
        }
    }

    /// <summary>
    ///     Ensures sentinel values still fail explicitly during construction.
    /// </summary>
    [Fact]
    public void Create_All_Throws()
    {
        var config = CreateSafeConfig();
        var pluginLog = new NoOpPluginLog();

        var exception = Assert.Throws<NotSupportedException>(() =>
            TranslatorFactory.Create(Echoglossian.TransEngines.All, config, pluginLog));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Creates a config payload that keeps constructors off the network while satisfying required base settings.
    /// </summary>
    /// <returns>A test-safe configuration.</returns>
    private static Config CreateSafeConfig()
    {
        return new Config
        {
            AwsAccessKey = "test-access-key",
            AwsSecretKey = "test-secret-key",
            AwsRegion = "us-east-1",
            OpenRouterBaseUrl = "https://openrouter.ai/api/v1/",
            OpenRouterApiKey = string.Empty,
            LmStudioBaseUrl = "http://localhost:1234/v1",
            OllamaUrl = "http://localhost:11434",
            MicrosoftTranslatorEndpoint = "https://api.cognitive.microsofttranslator.com",
            YandexFreeApiKey = "test-yandex-key",
            ChatGPTBaseUrl = "https://api.openai.com/v1",
            DeepSeekBaseUrl = "https://api.deepseek.com/v1",
        };
    }
}
