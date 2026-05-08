// <copyright file="ClaudeTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

/// <summary>
///     Translator implementation for Anthropic Claude using the Messages API.
/// </summary>
public class ClaudeTranslator : ITranslator
{
    private const string AnthropicVersion = "2023-06-01";
    private const string DefaultBaseUrl = "https://api.anthropic.com";
    private const string DefaultModel = "claude-sonnet-4-20250514";
    private const int MaxOutputTokens = 1024;

    private readonly string apiKey;
    private readonly string baseUrl;
    private readonly HttpClient? httpClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string promptTemplate;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClaudeTranslator" /> class.
    /// </summary>
    /// <param name="pluginLog">The plugin log instance for diagnostics.</param>
    /// <param name="config">The active plugin configuration.</param>
    public ClaudeTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.apiKey = config.ClaudeApiKey;
        this.baseUrl = string.IsNullOrWhiteSpace(config.ClaudeBaseUrl)
            ? DefaultBaseUrl
            : config.ClaudeBaseUrl;
        this.model = string.IsNullOrWhiteSpace(config.ClaudeModel)
            ? DefaultModel
            : config.ClaudeModel;
        this.temperature = config.ClaudeTemperature;
        this.promptTemplate = string.IsNullOrWhiteSpace(config.ClaudePrompt)
            ? PromptTemplateManager.DefaultPrompt
            : config.ClaudePrompt;

        if (string.IsNullOrWhiteSpace(this.apiKey))
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                Resources.APIKeyIsEmptyOrInvalidClaudeTranslationWillNotBeAvailable);
            this.httpClient = null;
            return;
        }

        try
        {
            this.httpClient = new HttpClient
            {
                BaseAddress = new Uri(this.baseUrl.TrimEnd('/') + "/"),
            };
            this.httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            this.httpClient.DefaultRequestHeaders.Add("x-api-key", this.apiKey);
            this.httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"Failed to initialize Claude HTTP client: {ex.Message}");
            this.httpClient = null;
        }
    }

    /// <inheritdoc/>
    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
        return this.TranslateAsync(text, sourceLanguage, targetLanguage)
            .GetAwaiter().GetResult() ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        if (this.httpClient is null)
        {
            return Resources.ClaudeTranslationUnavailablePleaseCheckYourAPIKey;
        }

        string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
        if (this.translationCache.TryGetValue(cacheKey, out string? cachedTranslation))
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
    ///     Performs the actual Claude Messages API request for one cache key.
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
        string fullPrompt = this.promptTemplate
            .Replace("{text}", text, StringComparison.Ordinal)
            .Replace("{sourceLanguage}", sourceLanguage, StringComparison.Ordinal)
            .Replace("{targetLanguage}", targetLanguage, StringComparison.Ordinal);

        var requestData = new
        {
            model = this.model,
            max_tokens = MaxOutputTokens,
            temperature = this.temperature,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = fullPrompt,
                },
            },
        };

        try
        {
            string jsonContent = JsonConvert.SerializeObject(requestData);
            using StringContent httpContent = new(
                jsonContent,
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage response = await this.httpClient.PostAsync(
                "v1/messages",
                httpContent).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            JObject responseObject = JObject.Parse(responseString);

            string translatedText = string.Join(
                string.Empty,
                responseObject["content"]?
                    .OfType<JObject>()
                    .Where(static block => string.Equals(block["type"]?.ToString(), "text", StringComparison.Ordinal))
                    .Select(static block => block["text"]?.ToString())
                    .Where(static blockText => !string.IsNullOrWhiteSpace(blockText)) ?? []);

            translatedText = translatedText.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(translatedText))
            {
                translatedText = FixText(translatedText);
                if (TranslationResultGuard.IsPersistableTranslation(translatedText))
                {
                    this.translationCache.Remember(cacheKey, translatedText);
                }

                return translatedText;
            }
        }
        catch (HttpRequestException httpEx)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} HTTP Error: {httpEx.Message}");
            return $"[{Resources.TranslationError} HTTP Error: {httpEx.Message}]";
        }
        catch (JsonException jsonEx)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} JSON Error: {jsonEx.Message}");
            return $"[{Resources.TranslationError} JSON Error: {jsonEx.Message}]";
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} {ex.Message}");
            return $"[{Resources.TranslationError} {ex.Message}]";
        }

        return string.Empty;
    }
}
