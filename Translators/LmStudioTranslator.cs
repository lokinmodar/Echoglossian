// <copyright file="LmStudioTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

/// <summary>
///     Translator implementation for LM Studio, using OpenAI-compatible local API.
/// </summary>
public class LmStudioTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly LlmCapabilityScope capabilityScope;
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
        this.prompt = string.IsNullOrWhiteSpace(config.LmStudioPrompt)
            ? PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.LmStudio)
            : config.LmStudioPrompt;

        var baseUrl = config.LmStudioBaseUrl?.TrimEnd('/') ??
                      "http://localhost:1234/v1";
        this.capabilityScope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.LmStudio,
            "LmStudio",
            baseUrl,
            this.model);
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

    /// <inheritdoc />
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
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
                cacheKey,
                dialogueContext)).ConfigureAwait(false);
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

        var fullPrompt = this.BuildPrompt(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);

        var request = new Dictionary<string, object>
        {
            ["model"] = this.model,
            ["messages"] = new[]
            {
                new { role = "user", content = fullPrompt },
            },
        };
        var temperatureWasSent = this.TryAddTemperature(request);

        try
        {
            var response = await this.httpClient.PostAsJsonAsync(
                "chat/completions",
                request);
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
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

    /// <summary>
    ///     Attempts the OpenAI-compatible structured dialogue path for LM
    ///     Studio and falls back to the legacy plain-text request on any
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
                Echoglossian.TransEngines.LmStudio) != StructuredDialogueProviderCapability.JsonSchema)
        {
            return null;
        }

        var glossaryEntries = StructuredDialogueGlossaryStore.GetEntries(
            sourceLanguage,
            targetLanguage);
        var usedGlossary = glossaryEntries.Count > 0;
        try
        {
            var normalizedText = FixText(text);
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
                    this.BuildPrompt(
                        normalizedText,
                        sourceLanguage,
                        targetLanguage),
                    structuredRequest);
            var request = new Dictionary<string, object>
            {
                ["model"] = this.model,
                ["messages"] = new[]
                {
                    new { role = "user", content = structuredPrompt },
                },
                ["tools"] = new[]
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
                ["tool_choice"] = new
                {
                    type = "function",
                    function = new
                    {
                        name = StructuredDialogueOpenAiToolHelper.ToolFunctionName,
                    },
                },
            };
            var temperatureWasSent = this.TryAddTemperature(request);

            var response = await this.httpClient.PostAsJsonAsync(
                "chat/completions",
                request).ConfigureAwait(false);
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
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
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.LmStudio,
                    false,
                    usedGlossary,
                    structuredValidation.FailureReason ??
                    "unknown-structured-dialogue-failure");
                PluginRuntimeLog.Debug(this.pluginLog, $"LmStudio structured dialogue path rejected provider output and will fall back to plain-text: {structuredValidation.FailureReason ?? "unknown-structured-dialogue-failure"}");
                return null;
            }

            var translatedText =
                structuredValidation.Response.Value.TextTranslated.Trim();
            if (TranslationResultGuard.IsPersistableTranslation(translatedText))
            {
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.LmStudio,
                    true,
                    usedGlossary);
                this.translationCache.Remember(
                    cacheKey,
                    translatedText);
                return translatedText;
            }

            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.LmStudio,
                false,
                usedGlossary,
                "non-persistable-structured-result");
            return null;
        }
        catch (Exception ex)
        {
            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.LmStudio,
                false,
                usedGlossary,
                ex.Message);
            PluginRuntimeLog.Debug(this.pluginLog, $"LmStudio structured dialogue path failed and will fall back to plain-text: {ex.Message}");
            return null;
        }
    }

    private string BuildPrompt(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext? dialogueContext = null)
    {
        var fixedText = FixText(text);
        var prompt = this.prompt.Replace("{text}", fixedText)
            .Replace("{sourceLanguage}", sourceLanguage)
            .Replace("{targetLanguage}", targetLanguage);

        if (!dialogueContext.HasValue)
        {
            return prompt;
        }

        return DialogueContextPromptHelper.AppendDialogueContext(
            prompt,
            dialogueContext.Value,
            FixText);
    }

    private bool TryAddTemperature(Dictionary<string, object> request)
    {
        if (!LlmCapabilityPolicyService.TryResolveTemperature(
                this.capabilityScope,
                this.temperature,
                out var sanitizedTemperature,
                out _))
        {
            return false;
        }

        request.Add("temperature", sanitizedTemperature);
        return true;
    }

    private async Task LearnTemperatureFailureAsync(
        HttpResponseMessage response,
        bool temperatureWasSent)
    {
        if (response.IsSuccessStatusCode || !temperatureWasSent)
        {
            return;
        }

        var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var learning = LlmCapabilityPolicyService.LearnFromProviderFailure(
            this.capabilityScope,
            LlmCapabilityParameterName.Temperature,
            (int)response.StatusCode,
            responseText);
        PluginRuntimeLog.Debug(
            this.pluginLog,
            $"Capability learning: promoted={learning.RulePromoted}, kind={learning.FailureKind}");
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
