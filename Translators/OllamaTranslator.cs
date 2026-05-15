// <copyright file="OllamaTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

public class OllamaTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly string endpoint;
    private readonly HttpClient httpClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string promptTemplate;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    public OllamaTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.endpoint =
            config.OllamaUrl?.TrimEnd('/') ?? "http://localhost:11434";
        this.model = config.OllamaModel ?? "llama3";
        this.promptTemplate = string.IsNullOrWhiteSpace(config.OllamaPrompt)
            ? PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.Ollama)
            : config.OllamaPrompt;
        this.temperature = config.OllamaTemperature;

        this.httpClient = new HttpClient
        {
            BaseAddress = new Uri(this.endpoint),
        };
        this.httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
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
    ///     Performs the actual Ollama translation request for one cache key.
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
            prompt,
            this.temperature,
            stream = false,
        };

        try
        {
            var response =
                await this.httpClient.PostAsJsonAsync("/api/generate", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JObject.Parse(json);
            var output = parsed["response"]?.ToString()?.Trim();

            if (!string.IsNullOrWhiteSpace(output))
            {
                var cleaned = FixText(output.Trim('"'));
                if (TranslationResultGuard.IsPersistableTranslation(cleaned))
                {
                    this.translationCache.Remember(cacheKey, cleaned);
                }

                return cleaned;
            }

            PluginRuntimeLog.Warning(this.pluginLog, "OllamaTranslator: No output returned.");
            return
                $"[{Resources.TranslationError} No translation received from Ollama]";
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"OllamaTranslator failed: {ex.Message}");
            return $"[{Resources.TranslationError} Ollama error: {ex.Message}]";
        }
    }

    /// <summary>
    ///     Attempts the Ollama structured dialogue path and falls back to the
    ///     legacy plain-text request on incompatibility or malformed provider
    ///     output.
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
                Echoglossian.TransEngines.Ollama) != StructuredDialogueProviderCapability.JsonSchema)
        {
            return null;
        }

        try
        {
            var normalizedText = FixText(text);
            var structuredRequest =
                StructuredDialogueTranslationRequestBuilder.Build(
                    normalizedText,
                    sourceLanguage,
                    targetLanguage,
                    TranslationSurfaceGroup.Dialogue,
                    dialogueContext);
            var basePrompt = this.promptTemplate
                .Replace("{text}", normalizedText)
                .Replace("{sourceLanguage}", sourceLanguage)
                .Replace("{targetLanguage}", targetLanguage);
            var structuredPrompt =
                $"{basePrompt}\n\n" +
                "Return only a JSON object that matches the provided response schema. " +
                "Do not answer with prose, markdown, or code fences.\n\n" +
                "Structured dialogue request JSON:\n" +
                StructuredDialogueOpenAiToolHelper.SerializeRequestPayload(
                    structuredRequest);
            var request = new
            {
                this.model,
                prompt = structuredPrompt,
                stream = false,
                format = JObject.Parse(
                    StructuredDialogueOpenAiToolHelper.BuildFunctionParametersSchemaJson()),
                options = new
                {
                    temperature = this.temperature,
                },
            };

            var response =
                await this.httpClient.PostAsJsonAsync("/api/generate", request)
                    .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var parsed = JObject.Parse(json);
            var rawStructuredPayload = parsed["response"]?.ToString();
            var structuredValidation =
                StructuredDialogueTranslationResponseValidator.ParseAndValidate(
                    rawStructuredPayload);

            if (!structuredValidation.IsValid ||
                !structuredValidation.Response.HasValue)
            {
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    $"Ollama structured dialogue path rejected provider output and will fall back to plain-text: {structuredValidation.FailureReason ?? "unknown-structured-dialogue-failure"}");
                return null;
            }

            var translatedText =
                structuredValidation.Response.Value.TextTranslated.Trim();
            if (TranslationResultGuard.IsPersistableTranslation(translatedText))
            {
                this.translationCache.Remember(cacheKey, translatedText);
                return translatedText;
            }

            return null;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Debug(
                this.pluginLog,
                $"Ollama structured dialogue path failed and will fall back to plain-text: {ex.Message}");
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
        var prompt = this.promptTemplate
            .Replace("{text}", fixedText)
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
}
