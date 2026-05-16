// <copyright file="OpenRouterTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

public class OpenRouterTranslator : ITranslator
{
    private const string DefaultModel = "mistral";
    private const string DefaultOpenRouterUrl = "https://openrouter.ai/api/v1/";
    private readonly string apiKey;
    private readonly HttpClient httpClient;
    private readonly string model;
    private readonly string openRouterUrl;
    private readonly IPluginLog pluginLog;

    private readonly string prompt;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    public OpenRouterTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.model = string.IsNullOrWhiteSpace(config.OpenRouterModel)
            ? DefaultModel
            : config.OpenRouterModel!;
        this.temperature = config.OpenRouterTemperature;
        this.apiKey = config.OpenRouterApiKey ?? string.Empty;
        this.openRouterUrl = string.IsNullOrWhiteSpace(config.OpenRouterBaseUrl)
            ? DefaultOpenRouterUrl
            : config.OpenRouterBaseUrl!;
        this.prompt = string.IsNullOrWhiteSpace(config.OpenRouterPrompt)
            ? PromptTemplateManager.DefaultPrompt
            : config.OpenRouterPrompt!;

        if (string.IsNullOrWhiteSpace(this.apiKey))
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                Resources
                    .APIKeyIsEmptyOrInvalidChatGPTTranslationWillNotBeAvailable);
        }

        this.httpClient = new HttpClient
        {
            BaseAddress = new Uri(this.openRouterUrl),
        };

        this.httpClient.DefaultRequestHeaders.Add(
            "Authorization",
            $"Bearer {this.apiKey}");
        this.httpClient.DefaultRequestHeaders.Add(
            "HTTP-Referer",
            "https://your-plugin-site-or-github-url"); // Optional but recommended
        this.httpClient.DefaultRequestHeaders.Add(
            "X-Title",
            "Echoglossian Plugin");
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
        if (string.IsNullOrWhiteSpace(this.apiKey))
        {
            return Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
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
    ///     Performs the actual OpenRouter translation request for one cache key.
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
        var fullPrompt = BuildPrompt(
            this.prompt,
            text,
            sourceLanguage,
            targetLanguage);

        var request = new
        {
            this.model,
            messages = new[]
            {
                new { role = "user", content = fullPrompt },
            },
            this.temperature,
        };

        try
        {
            var response = await this.httpClient.PostAsJsonAsync(
                "chat/completions",
                request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var jsonResponse =
                await response.Content.ReadFromJsonAsync<OpenRouterResponse>().ConfigureAwait(false);

            var result =
                jsonResponse?.Choices?.FirstOrDefault()?.Message?.Content
                    ?.Trim() ?? string.Empty;

            result = result.Trim('"');

            if (!string.IsNullOrEmpty(result))
            {
                if (TranslationResultGuard.IsPersistableTranslation(result))
                {
                    this.translationCache.Remember(cacheKey, result);
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} {ex.Message}");
            return $"[{Resources.TranslationError} {ex.Message}]";
        }

        return string.Empty;
    }

    internal static string BuildPrompt(
        string? promptTemplate,
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        var normalizedTemplate = string.IsNullOrWhiteSpace(promptTemplate)
            ? PromptTemplateManager.DefaultPrompt
            : promptTemplate;

        return normalizedTemplate
            .Replace(
                "{sourceLanguage}",
                sourceLanguage,
                StringComparison.Ordinal)
            .Replace(
                "{targetLanguage}",
                targetLanguage,
                StringComparison.Ordinal)
            .Replace("{text}", FixText(text), StringComparison.Ordinal);
    }

    private class OpenRouterResponse
    {
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        public Message? Message { get; set; }
    }

    private class Message
    {
        public string? Role { get; set; }

        public string? Content { get; set; }
    }
}
