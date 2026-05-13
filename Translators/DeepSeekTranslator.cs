// <copyright file="DeepSeekTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Helpers;
using Echoglossian.PluginUI.Helpers;

namespace Echoglossian.Translators;

public class DeepSeekTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly string apiKey;
    private readonly string baseUrl;
    private readonly HttpClient? httpClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string promptTemplate;
    private readonly float temperature = 0.1f;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeepSeekTranslator" /> class.
    /// </summary>
    /// <param name="pluginLog">The plugin log instance for logging purposes.</param>
    /// <param name="config">The configuration settings for the DeepSeekTranslator.</param>
    public DeepSeekTranslator(IPluginLog pluginLog, Config config)
    {
        this.baseUrl = config.DeepSeekBaseUrl ?? "https://api.deepseek.com/v1";
        this.apiKey = config.DeepSeekTranslatorApiKey ?? string.Empty;
        this.model = config.DeepSeekModel ?? "deepseek-chat";
        this.temperature = config.DeepSeekTemperature;
        this.promptTemplate = string.IsNullOrWhiteSpace(config.DeepSeekPrompt)
            ? PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.DeepSeek)
            : config.DeepSeekPrompt;
        this.pluginLog = pluginLog;

        if (string.IsNullOrWhiteSpace(this.apiKey))
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                Resources
                    .APIKeyIsEmptyOrInvalidDeepSeekTranslationWillNotBeAvailable);
            this.httpClient = null;
        }
        else
        {
            try
            {
                PluginRuntimeLog.Debug(
                    pluginLog,
                    $"DeepSeekTranslator: {this.baseUrl}, {this.apiKey[..20]}***{this.apiKey[^5..]}, {this.temperature}");

                this.httpClient = new HttpClient
                {
                    BaseAddress = new Uri(this.baseUrl),
                };
                this.httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", this.apiKey);
                this.httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            }
            catch (Exception ex)
            {
                PluginRuntimeLog.Error(
                    this.pluginLog,
                    $"Failed to initialize DeepSeek HTTP client: {ex.Message}");
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
            return Resources
                .DeepSeekTranslationUnavailablePleaseCheckYourAPIKey;
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

    /// <inheritdoc />
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        if (this.httpClient == null)
        {
            return Resources
                .DeepSeekTranslationUnavailablePleaseCheckYourAPIKey;
        }

        if (!DialogueContextPromptHelper.HasUsableDialogueContext(dialogueContext))
        {
            return await this.TranslateAsync(
                text,
                sourceLanguage,
                targetLanguage).ConfigureAwait(false);
        }

        var cacheKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);
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
                cacheKey,
                dialogueContext)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Performs the actual DeepSeek translation request for one cache key.
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
        string cacheKey,
        DialogueTranslationContext? dialogueContext = null)
    {
        var prompt = this.BuildPrompt(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);

        try
        {
            var requestData = new
            {
                this.model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt,
                    },
                },
                this.temperature,
            };

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var httpContent = new StringContent(
                jsonContent,
                Encoding.UTF8,
                "application/json");

            var response = await this.httpClient.PostAsync(
                "chat/completions",
                httpContent);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var responseObject = JObject.Parse(responseString);

            var translatedText =
                responseObject["choices"]?[0]?["message"]?["content"]
                    ?.ToString().Trim();

            if (!string.IsNullOrEmpty(translatedText))
            {
                translatedText = translatedText.Trim('"');
                if (TranslationResultGuard.IsPersistableTranslation(translatedText))
                {
                    this.translationCache.Remember(cacheKey, translatedText);
                }

                return translatedText;
            }
        }
        catch (HttpRequestException httpEx)
        {
            PluginRuntimeLog.Error(
                this.pluginLog,
                $"{Resources.TranslationError} HTTP Error: {httpEx.Message}");
            return
                $"[{Resources.TranslationError} HTTP Error: {httpEx.Message}]";
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
            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} {ex.Message}");
            return $"[{Resources.TranslationError} {ex.Message}]";
        }

        return string.Empty;
    }

    private string BuildPrompt(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext? dialogueContext = null)
    {
        var prompt = PromptTemplateManager.RenderPrompt(
            this.promptTemplate,
            FixText(text),
            sourceLanguage,
            targetLanguage);

        if (!dialogueContext.HasValue)
        {
            return prompt;
        }

        return DialogueContextPromptHelper.AppendDialogueContext(
            prompt,
            dialogueContext.Value,
            FixText);
    }
}
