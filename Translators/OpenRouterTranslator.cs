// <copyright file="OpenRouterTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

public class OpenRouterTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private const string DefaultModel = "mistral";
    private const string DefaultOpenRouterUrl = "https://openrouter.ai/api/v1/";
    private readonly string apiKey;
    private readonly HttpClient httpClient;
    private readonly string model;
    private readonly string openRouterUrl;
    private readonly IPluginLog pluginLog;

    private readonly string promptTemplate;
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
        this.promptTemplate = string.IsNullOrWhiteSpace(config.OpenRouterPrompt)
            ? PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.OpenRouter)
            : config.OpenRouterPrompt;

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

    /// <inheritdoc />
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        if (string.IsNullOrWhiteSpace(this.apiKey))
        {
            return Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
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
        string cacheKey,
        DialogueTranslationContext? dialogueContext = null)
    {
        if (dialogueContext.HasValue)
        {
            var structuredTranslation =
                await this.TryTranslateStructuredDialogueAsync(
                    text,
                    sourceLanguage,
                    targetLanguage,
                    cacheKey,
                    dialogueContext.Value).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(structuredTranslation))
            {
                return structuredTranslation;
            }
        }

        var prompt = this.BuildPrompt(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);

        var request = new
        {
            this.model,
            messages = new[]
            {
                new { role = "user", content = prompt },
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

    /// <summary>
    ///     Attempts the first OpenAI-compatible structured dialogue path for
    ///     OpenRouter and falls back to the legacy plain-text request on any
    ///     incompatibility or malformed provider output.
    /// </summary>
    /// <param name="text">The visible source text.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="cacheKey">The normalized request cache key.</param>
    /// <param name="dialogueContext">The runtime-only dialogue context.</param>
    /// <returns>
    ///     The structured translated text when successful; otherwise
    ///     <see langword="null" /> so the legacy path can run.
    /// </returns>
    private async Task<string?> TryTranslateStructuredDialogueAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string cacheKey,
        DialogueTranslationContext dialogueContext)
    {
        if (StructuredDialogueCapabilityHelper.GetPreferredCapability(
                Echoglossian.TransEngines.OpenRouter) != StructuredDialogueProviderCapability.JsonSchema)
        {
            return null;
        }

        try
        {
            var normalizedText = FixText(text);
            var glossaryEntries = StructuredDialogueGlossaryStore.GetEntries(
                sourceLanguage,
                targetLanguage);
            var structuredRequest =
                StructuredDialogueTranslationRequestBuilder.Build(
                    normalizedText,
                    sourceLanguage,
                    targetLanguage,
                    TranslationSurfaceGroup.Dialogue,
                    dialogueContext,
                    glossaryEntries);
            var structuredPrompt =
                StructuredDialogueOpenAiToolHelper.BuildUserPrompt(
                    PromptTemplateManager.RenderPrompt(
                        this.promptTemplate,
                        normalizedText,
                        sourceLanguage,
                        targetLanguage),
                    structuredRequest);
            var request = new
            {
                this.model,
                messages = new[]
                {
                    new { role = "user", content = structuredPrompt },
                },
                this.temperature,
                tools = new[]
                {
                    new
                    {
                        type = "function",
                        function = new
                        {
                            name = StructuredDialogueOpenAiToolHelper.ToolFunctionName,
                            description = StructuredDialogueOpenAiToolHelper.ToolFunctionDescription,
                            parameters = StructuredDialogueOpenAiCompatiblePayloadHelper.BuildFunctionParametersJsonElement(),
                            strict = true,
                        },
                    },
                },
                tool_choice = new
                {
                    type = "function",
                    function = new
                    {
                        name = StructuredDialogueOpenAiToolHelper.ToolFunctionName,
                    },
                },
            };

            var response = await this.httpClient.PostAsJsonAsync(
                "chat/completions",
                request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseJson =
                await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var rawStructuredPayload =
                StructuredDialogueOpenAiCompatiblePayloadHelper.ExtractRawStructuredPayload(
                    responseJson,
                    StructuredDialogueOpenAiToolHelper.ToolFunctionName);
            var structuredValidation =
                StructuredDialogueTranslationResponseValidator.ParseAndValidate(
                    rawStructuredPayload);

            if (!structuredValidation.IsValid ||
                !structuredValidation.Response.HasValue)
            {
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    $"OpenRouter structured dialogue path rejected provider output and will fall back to plain-text: {structuredValidation.FailureReason ?? "unknown-structured-dialogue-failure"}");
                return null;
            }

            var translatedText =
                structuredValidation.Response.Value.TextTranslated.Trim();
            if (TranslationResultGuard.IsPersistableTranslation(translatedText))
            {
                this.translationCache.Remember(
                    cacheKey,
                    translatedText);
                return translatedText;
            }

            return null;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Debug(
                this.pluginLog,
                $"OpenRouter structured dialogue path failed and will fall back to plain-text: {ex.Message}");
            return null;
        }
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
