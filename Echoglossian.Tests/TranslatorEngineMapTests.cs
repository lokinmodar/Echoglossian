// <copyright file="TranslatorEngineMapTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the stable mapping between configured engines and translator implementations.
/// </summary>
public class TranslatorEngineMapTests
{
    /// <summary>
    ///     Ensures every supported engine resolves to the expected implementation name.
    /// </summary>
    [Fact]
    public void GetImplementationName_ReturnsExpectedNames()
    {
        var expectations = new (Echoglossian.TransEngines Engine, string Name)[]
        {
            (Echoglossian.TransEngines.Google, "GoogleTranslator"),
            (Echoglossian.TransEngines.Deepl, "DeepLTranslator"),
            (Echoglossian.TransEngines.ChatGPT, "ChatGPTTranslator"),
            (Echoglossian.TransEngines.YandexCloud, "YandexTranslator"),
            (Echoglossian.TransEngines.GTranslate, "GTranslateTranslator"),
            (Echoglossian.TransEngines.Amazon, "AmazonTranslateTranslator"),
            (Echoglossian.TransEngines.Microsoft, "MicrosoftTranslator"),
            (Echoglossian.TransEngines.Gemini, "GeminiTranslator"),
            (Echoglossian.TransEngines.DeepSeek, "DeepSeekTranslator"),
            (Echoglossian.TransEngines.Ollama, "OllamaTranslator"),
            (Echoglossian.TransEngines.LibreTranslate, "LibreTranslateTranslator"),
            (Echoglossian.TransEngines.YandexPublic, "YandexPublicTranslator"),
            (Echoglossian.TransEngines.OpenRouter, "OpenRouterTranslator"),
            (Echoglossian.TransEngines.LmStudio, "LmStudioTranslator"),
        };

        foreach (var (engine, name) in expectations)
        {
            var implementationName = TranslatorEngineMap.GetImplementationName(engine);
            Assert.Equal(name, implementationName);
        }
    }

    /// <summary>
    ///     Ensures sentinel values that do not map to a concrete translator still fail explicitly.
    /// </summary>
    [Fact]
    public void GetImplementationName_All_Throws()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            TranslatorEngineMap.GetImplementationName(Echoglossian.TransEngines.All));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Ensures every non-sentinel engine enum value is covered by the implementation map.
    /// </summary>
    [Fact]
    public void GetImplementationName_CoversAllConcreteEngines()
    {
        var concreteEngines = Enum.GetValues<Echoglossian.TransEngines>()
            .Where(static engine => engine != Echoglossian.TransEngines.All)
            .ToArray();

        foreach (var engine in concreteEngines)
        {
            var implementationName = TranslatorEngineMap.GetImplementationName(engine);
            Assert.False(string.IsNullOrWhiteSpace(implementationName));
        }
    }
}
