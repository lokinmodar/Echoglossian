// <copyright file="TranslatorFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Creates translator instances for the configured translation engine.
/// </summary>
public static class TranslatorFactory
{
    /// <summary>
    ///     Creates one translator instance for the requested engine.
    /// </summary>
    /// <param name="engine">The translation engine to instantiate.</param>
    /// <param name="config">The active plugin configuration.</param>
    /// <param name="pluginLog">The plugin logger.</param>
    /// <returns>The created translator.</returns>
    /// <exception cref="NotSupportedException">
    ///     Thrown when the engine cannot be instantiated through this factory.
    /// </exception>
    public static ITranslator Create(
        Echoglossian.TransEngines engine,
        Config config,
        IPluginLog pluginLog)
    {
        return engine switch
        {
            Echoglossian.TransEngines.Google =>
                new GoogleTranslator(pluginLog, config),
            Echoglossian.TransEngines.Deepl =>
                new DeepLTranslator(
                    pluginLog,
                    config.DeeplTranslatorUsingApiKey,
                    config.DeeplTranslatorApiKey),
            Echoglossian.TransEngines.ChatGPT =>
                new ChatGPTTranslator(
                    pluginLog,
                    config.ChatGPTBaseUrl,
                    config.ChatGptApiKey,
                    config.OpenAILlmModel,
                    config.ChatGptTemperature,
                    config.ChatGptPrompt),
            Echoglossian.TransEngines.YandexCloud =>
                new YandexTranslator(pluginLog, config),
            Echoglossian.TransEngines.GTranslate =>
                new GTranslateTranslator(pluginLog, config),
            Echoglossian.TransEngines.Amazon =>
                new AmazonTranslateTranslator(pluginLog, config),
            Echoglossian.TransEngines.Microsoft =>
                new MicrosoftTranslator(pluginLog, config),
            Echoglossian.TransEngines.Gemini =>
                new GeminiTranslator(pluginLog, config),
            Echoglossian.TransEngines.DeepSeek =>
                new DeepSeekTranslator(pluginLog, config),
            Echoglossian.TransEngines.Ollama =>
                new OllamaTranslator(pluginLog, config),
            Echoglossian.TransEngines.LibreTranslate =>
                new LibreTranslateTranslator(pluginLog, config),
            Echoglossian.TransEngines.YandexPublic =>
                new YandexPublicTranslator(pluginLog, config),
            Echoglossian.TransEngines.OpenRouter =>
                new OpenRouterTranslator(pluginLog, config),
            Echoglossian.TransEngines.LmStudio =>
                new LmStudioTranslator(pluginLog, config),
            Echoglossian.TransEngines.Claude =>
                new ClaudeTranslator(pluginLog, config),
            _ => throw new NotSupportedException(
                $"Translation engine {engine} is not supported."),
        };
    }
}
