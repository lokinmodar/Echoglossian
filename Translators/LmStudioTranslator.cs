// <copyright file="LmStudioTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

/// <summary>
///     Translator implementation for LM Studio, using OpenAI-compatible local API.
/// </summary>
public class LmStudioTranslator : ITranslator
{
    private readonly HttpClient httpClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string prompt;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="LmStudioTranslator" /> class.
    /// </summary>
    /// <param name="pluginLog">Plugin log for diagnostic output.</param>
    /// <param name="config">User configuration containing model and credentials.</param>
    public LmStudioTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.model = config.LmStudioModel;
        this.temperature = config.LmStudioTemperature;
        this.prompt = config.LmStudioPrompt;

        var baseUrl = config.LmStudioBaseUrl?.TrimEnd('/') ??
                      "http://localhost:1234/v1";
        this.httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

        if (config.UseLmStudioAuth &&
            !string.IsNullOrWhiteSpace(config.LmStudioApiKey))
        {
            this.httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.LmStudioApiKey);
        }

        this.httpClient.DefaultRequestHeaders.Add(
            "User-Agent",
            "Echoglossian LmStudio Client");
    }

    /// <inheritdoc />
    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return this.TranslateAsync(text, sourceLanguage, targetLanguage)
            .GetAwaiter().GetResult() ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
        if (this.translationCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
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
    ///     Performs the actual LM Studio translation request for one cache key.
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
        var fixedText = FixText(text);
        var fullPrompt = this.prompt.Replace("{text}", fixedText)
            .Replace("{sourceLanguage}", sourceLanguage).Replace(
                "{targetLanguage}",
                targetLanguage);

        var request = new
        {
            this.model,
            this.temperature,
            messages = new[]
            {
                new { role = "user", content = fullPrompt },
            },
        };

        try
        {
            var response = await this.httpClient.PostAsJsonAsync(
                "chat/completions",
                request);
            response.EnsureSuccessStatusCode();

            var json =
                await response.Content.ReadFromJsonAsync<LmStudioResponse>();

            var result = json?.Choices?.FirstOrDefault()?.Message?.Content
                ?.Trim().Trim('"');

            if (!string.IsNullOrWhiteSpace(result))
            {
                result = FixText(result);
                if (TranslationResultGuard.IsPersistableTranslation(result))
                {
                    this.translationCache.Remember(cacheKey, result);
                }

                return result;
            }

            PluginRuntimeLog.Warning(this.pluginLog, "LmStudio returned empty translation.");
            return null;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                this.pluginLog,
                $"{Resources.TranslationError} LmStudio: {ex.Message}");
            return $"[{Resources.TranslationError} LmStudio: {ex.Message}]";
        }
    }

    private sealed class LmStudioResponse
    {
        public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        public Message? Message { get; set; }
    }

    private sealed class Message
    {
        public string? Role { get; set; }

        public string? Content { get; set; }
    }
}
