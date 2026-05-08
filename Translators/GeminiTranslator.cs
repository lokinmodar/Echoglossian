// <copyright file="GeminiTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

public class GeminiTranslator : ITranslator
{
    private readonly string apiKey;
    private readonly HttpClient? httpClient;
    private readonly TimeSpan initialBackoff = TimeSpan.FromSeconds(1);
    private readonly int maxRetries = 3;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly float temperature = 0.1f;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    public GeminiTranslator(IPluginLog pluginLog, Config config)
    {
        this.apiKey = config.GeminiTranslatorApiKey ?? string.Empty;
        this.model = config.GeminiModel ?? "gemini-pro"; // Default model
        this.temperature = config.GeminiTemperature;
        this.pluginLog = pluginLog;

        if (string.IsNullOrWhiteSpace(this.apiKey))
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                Resources
                    .APIKeyIsEmptyOrInvalidGeminiTranslationWillNotBeAvailable);
            this.httpClient = null;
        }
        else
        {
            try
            {
                PluginRuntimeLog.Debug(
                    pluginLog,
                    $"GeminiTranslator: {this.model}, {this.apiKey[..20]}***{this.apiKey[^5..]}, {this.temperature}");

                this.httpClient = new HttpClient();
                this.httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            }
            catch (Exception ex)
            {
                PluginRuntimeLog.Error(
                    this.pluginLog,
                    $"Failed to initialize Gemini HTTP client: {ex.Message}");
                this.httpClient = null;
            }
        }
    }

    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return this.TranslateAsync(text, sourceLanguage, targetLanguage)
            .GetAwaiter().GetResult() ?? string.Empty;
    }

    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        if (this.httpClient == null)
        {
            return Resources.GeminiTranslationUnavailablePleaseCheckYourAPIKey;
        }

        var cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
        if (this.translationCache.TryGetValue(
                cacheKey,
                out var cachedTranslation))
        {
            return cachedTranslation;
        }

        return await this.translationCache.GetOrAddAsync(
            cacheKey,
            () => this.TranslateCoreAsync(
                text,
                sourceLanguage,
                targetLanguage,
                cacheKey)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Performs the actual Gemini translation request for one cache key.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <param name="cacheKey">The normalized cache key for this request.</param>
    /// <returns>The translated text or an error placeholder.</returns>
    private async Task<string?> TranslateCoreAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string cacheKey)
    {
        var fixedInputText = FixText(text);

        var prompt =
            @$"As an expert translator and cultural localization specialist with deep knowledge of video game localization, your task is to translate dialogues from the game Final Fantasy XIV from {sourceLanguage} to {targetLanguage}. This is not just a translation, but a full localization effort tailored for the Final Fantasy XIV universe. Please adhere to the following guidelines:

1. Preserve the original tone, humor, personality, and emotional nuances of the dialogue, considering the unique style and atmosphere of Final Fantasy XIV.
2. Adapt idioms, cultural references, and wordplay to resonate naturally with native {targetLanguage} speakers while maintaining the fantasy RPG context.
3. Maintain consistency in character voices, terminology, and naming conventions specific to Final Fantasy XIV throughout the translation.
4. Avoid literal translations that may lose the original intent or impact, especially for game-specific terms or lore elements.
5. Ensure the translation flows naturally and reads as if it were originally written in {targetLanguage}, while staying true to the game's narrative style.
6. Consider the context and subtext of the dialogue, including any references to the game's lore, world, or ongoing storylines.
7. If a word, phrase, or name has been translated in a specific way, maintain that translation consistently unless the context demands otherwise, respecting established localization choices for Final Fantasy XIV.
8. Pay attention to formal/informal speech patterns and adjust accordingly for the target language and cultural norms, considering the speaker's role and status within the game world.
9. Be mindful of character limits or text box constraints that may be present in the game, adapting the translation to fit if necessary.
10. Preserve any game-specific jargon, spell names, or technical terms according to the official localization guidelines for Final Fantasy XIV in the target language.

Text to translate: ""{fixedInputText}""

Please provide only the translated text in your response, without any explanations, additional comments, or quotation marks. Your goal is to create a localized version that captures the essence of the original Final Fantasy XIV dialogue while feeling authentic to {targetLanguage} speakers and seamlessly fitting into the game world.";

        for (var retry = 0; retry <= this.maxRetries; retry++)
        {
            try
            {
                var requestData = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = prompt
                                }
                            },
                        },
                    },
                    generationConfig = new
                    {
                        this.temperature,
                    },
                };

                var jsonContent = JsonConvert.SerializeObject(requestData);
                var httpContent = new StringContent(
                    jsonContent,
                    Encoding.UTF8,
                    "application/json");

                var baseUrl =
                    $"https://generativelanguage.googleapis.com/v1beta/models/{this.model}:generateContent?key={this.apiKey}";

                var response =
                    await this.httpClient.PostAsync(baseUrl, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    if (retry < this.maxRetries)
                    {
                        var backoff = this.initialBackoff * Math.Pow(2, retry);
                        PluginRuntimeLog.Warning(
                            this.pluginLog,
                            $"Gemini API request failed with status code {response.StatusCode}. Retrying in {backoff.TotalSeconds} seconds...");
                        await Task.Delay(backoff);
                        continue; // Retry
                    }

                    PluginRuntimeLog.Error(
                        this.pluginLog,
                        $"Gemini API request failed after {this.maxRetries} retries with status code {response.StatusCode}.");
                    return
                        $"[{Resources.TranslationError} Gemini API request failed with status code {response.StatusCode}]";
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var responseObject = JObject.Parse(responseString);

                var translatedText =
                    responseObject["candidates"]?[0]?["content"]?["parts"]?[0]?
                        ["text"]?.ToString().Trim();

                if (!string.IsNullOrEmpty(translatedText))
                {
                    translatedText = FixText(translatedText.Trim('"'));
                    if (TranslationResultGuard.IsPersistableTranslation(translatedText))
                    {
                        this.translationCache.Remember(cacheKey, translatedText);
                    }

                    return translatedText;
                }

                PluginRuntimeLog.Error(
                    this.pluginLog,
                    "Gemini API returned an empty translated text.");
                return
                    $"[{Resources.TranslationError} Gemini API returned an empty translated text.]";
            }
            catch (HttpRequestException httpEx)
            {
                if (retry < this.maxRetries)
                {
                    var backoff = this.initialBackoff * Math.Pow(2, retry);
                    PluginRuntimeLog.Warning(
                        this.pluginLog,
                        $"HTTP Error: {httpEx.Message}. Retrying in {backoff.TotalSeconds} seconds...");
                    await Task.Delay(backoff);
                }
                else
                {
                    PluginRuntimeLog.Error(
                        this.pluginLog,
                        $"{Resources.TranslationError} HTTP Error: {httpEx.Message}");
                    return
                        $"[{Resources.TranslationError} HTTP Error: {httpEx.Message}]";
                }
            }
            catch (JsonException jsonEx)
            {
                PluginRuntimeLog.Error(
                    this.pluginLog,
                    $"{Resources.TranslationError} JSON Error: {jsonEx.Message}");
                return
                    $"[{Resources.TranslationError} JSON Error: {jsonEx.Message}]";
            }
            catch (Exception ex)
            {
                PluginRuntimeLog.Error(
                    this.pluginLog,
                    $"{Resources.TranslationError} {ex.Message}");
                return $"[{Resources.TranslationError} {ex.Message}]";
            }
        }

        return string.Empty;
    }
}
