// <copyright file="TranslatorEngineMap.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Exposes the stable engine-to-implementation mapping without instantiating translators.
/// </summary>
internal static class TranslatorEngineMap
{
    /// <summary>
    ///     Gets the expected implementation name for the requested translation engine.
    /// </summary>
    /// <param name="engine">The translation engine.</param>
    /// <returns>The implementation type name.</returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown when the engine is not mapped to a concrete implementation.
    /// </exception>
    internal static string GetImplementationName(Echoglossian.TransEngines engine)
    {
        return engine switch
        {
            Echoglossian.TransEngines.Google => "GoogleTranslator",
            Echoglossian.TransEngines.Deepl => "DeepLTranslator",
            Echoglossian.TransEngines.ChatGPT => "ChatGPTTranslator",
            Echoglossian.TransEngines.YandexCloud => "YandexTranslator",
            Echoglossian.TransEngines.GTranslate => "GTranslateTranslator",
            Echoglossian.TransEngines.Amazon => "AmazonTranslateTranslator",
            Echoglossian.TransEngines.Microsoft => "MicrosoftTranslator",
            Echoglossian.TransEngines.Gemini => "GeminiTranslator",
            Echoglossian.TransEngines.DeepSeek => "DeepSeekTranslator",
            Echoglossian.TransEngines.Ollama => "OllamaTranslator",
            Echoglossian.TransEngines.LibreTranslate => "LibreTranslateTranslator",
            Echoglossian.TransEngines.YandexPublic => "YandexPublicTranslator",
            Echoglossian.TransEngines.OpenRouter => "OpenRouterTranslator",
            Echoglossian.TransEngines.LmStudio => "LmStudioTranslator",
            Echoglossian.TransEngines.Claude => "ClaudeTranslator",
            _ => throw new NotSupportedException($"Translation engine {engine} is not supported."),
        };
    }
}
