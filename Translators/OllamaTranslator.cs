// <copyright file="OllamaTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

public class OllamaTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly string endpoint;
    private readonly HttpClient httpClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string promptTemplate;
    private readonly LlmCapabilityScope capabilityScope;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    public OllamaTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.endpoint =
            config.OllamaUrl?.TrimEnd('/') ?? "http://localhost:11434";
        this.model = config.OllamaModel ?? "llama3";
        this.capabilityScope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.Ollama,
            "Ollama",
            this.endpoint,
            this.model);
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

        var request = new Dictionary<string, object>
        {
            ["model"] = this.model,
            ["prompt"] = prompt,
            ["stream"] = false,
        };
        var temperatureWasSent = LlmCapabilityRequestPayloadSanitizer.TryAddTemperature(
            request,
            this.capabilityScope,
            this.temperature);

        try
        {
            var response =
                await this.httpClient.PostAsJsonAsync("/api/generate", request);
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
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

        var glossaryEntries = StructuredDialogueGlossaryStore.GetEntries(
            sourceLanguage,
            targetLanguage);
        var usedGlossary = glossaryEntries.Count > 0;
        IReadOnlyList<string> capabilityDecisionTokens = [];
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
            var options = new Dictionary<string, object>();
            var temperatureWasSent = LlmCapabilityRequestPayloadSanitizer.TryAddTemperature(
                options,
                this.capabilityScope,
                this.temperature);
            var temperatureDecision = LlmCapabilityPolicyService.GetSnapshot(
                    this.capabilityScope)
                .GetDecision(LlmCapabilityParameterName.Temperature);
            capabilityDecisionTokens =
            [
                StructuredDialogueCapabilityDecisionLogFormatter.Format(
                    LlmCapabilityParameterName.Temperature,
                    temperatureDecision,
                    temperatureWasSent
                        ? StructuredDialogueCapabilityEmissionMode.SentConfigured
                        : temperatureDecision.OmitWhenDefaultOnly
                            ? StructuredDialogueCapabilityEmissionMode.OmittedDefaultOnly
                            : temperatureDecision.SupportState == LlmCapabilitySupportState.Unsupported
                                ? StructuredDialogueCapabilityEmissionMode.OmittedUnsupported
                                : StructuredDialogueCapabilityEmissionMode.OmittedUnknown),
            ];
            var request = new Dictionary<string, object>
            {
                ["model"] = this.model,
                ["prompt"] = structuredPrompt,
                ["stream"] = false,
                ["format"] = JObject.Parse(
                    StructuredDialogueOpenAiToolHelper.BuildFunctionParametersSchemaJson()),
                ["options"] = options,
            };
            var jsonContent = JsonConvert.SerializeObject(request);
            using var httpContent = new StringContent(
                jsonContent,
                Encoding.UTF8,
                "application/json");

            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
                    this.capabilityScope,
                    "/api/generate",
                    StructuredDialogueProviderCapability.JsonSchema,
                    dialogueContext.SessionNamespace,
                    dialogueContext.PriorTurns.Count,
                    glossaryEntries.Count,
                    !string.IsNullOrWhiteSpace(dialogueContext.SpeakerName),
                    !string.IsNullOrWhiteSpace(dialogueContext.AddresseeHint),
                    structuredPrompt.Length,
                    jsonContent.Length,
                    structuredPrompt,
                    normalizedText,
                    capabilityDecisionTokens));

            var response =
                await this.httpClient.PostAsync("/api/generate", httpContent)
                    .ConfigureAwait(false);
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
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
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.Ollama,
                    false,
                    usedGlossary,
                    structuredValidation.FailureReason ??
                    "unknown-structured-dialogue-failure");
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                        "Ollama",
                        this.model,
                        StructuredDialogueProviderCapability.JsonSchema,
                        "validation",
                        structuredValidation.FailureReason ??
                        "unknown-structured-dialogue-failure",
                        endpointScope: this.capabilityScope.EndpointScope,
                        route: "/api/generate",
                        capabilityDecisionTokens: capabilityDecisionTokens,
                        glossaryApplied: usedGlossary,
                        responseExcerpt: rawStructuredPayload));
                return null;
            }

            var translatedText =
                structuredValidation.Response.Value.TextTranslated.Trim();
            if (TranslationResultGuard.IsPersistableTranslation(translatedText))
            {
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.Ollama,
                    true,
                    usedGlossary);
                this.translationCache.Remember(cacheKey, translatedText);
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
                        this.capabilityScope,
                        "/api/generate",
                        StructuredDialogueProviderCapability.JsonSchema,
                        usedGlossary,
                        rawStructuredPayload?.Length ?? 0,
                        translatedText.Length,
                        rawStructuredPayload ?? string.Empty,
                        translatedText));
                return translatedText;
            }

            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.Ollama,
                false,
                usedGlossary,
                "non-persistable-structured-result");
            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                    "Ollama",
                    this.model,
                    StructuredDialogueProviderCapability.JsonSchema,
                    "validation",
                    "non-persistable-structured-result",
                    endpointScope: this.capabilityScope.EndpointScope,
                    route: "/api/generate",
                    capabilityDecisionTokens: capabilityDecisionTokens,
                    glossaryApplied: usedGlossary,
                    responseExcerpt: rawStructuredPayload));
            return null;
        }
        catch (Exception ex)
        {
            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.Ollama,
                false,
                usedGlossary,
                ex.Message);
            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                    "Ollama",
                    this.model,
                    StructuredDialogueProviderCapability.JsonSchema,
                    "exception",
                    ex.Message,
                    ex is HttpRequestException httpRequestException &&
                    httpRequestException.StatusCode.HasValue
                        ? (int)httpRequestException.StatusCode.Value
                        : null,
                    ex.Message,
                    this.capabilityScope.EndpointScope,
                    "/api/generate",
                    capabilityDecisionTokens,
                    usedGlossary));
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

    /// <summary>
    ///     Records a sanitized exact-model temperature observation from a
    ///     failed Ollama request that included temperature.
    /// </summary>
    /// <param name="response">The provider response associated with the request.</param>
    /// <param name="temperatureWasSent">Whether the request included temperature.</param>
    private async Task LearnTemperatureFailureAsync(
        HttpResponseMessage response,
        bool temperatureWasSent)
    {
        if (response.IsSuccessStatusCode || !temperatureWasSent)
        {
            return;
        }

        var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var learning = await LlmCapabilityPolicyService.LearnFromProviderFailureAsync(
            this.capabilityScope,
            LlmCapabilityParameterName.Temperature,
            (int)response.StatusCode,
            responseText);
        PluginRuntimeLog.Debug(
            this.pluginLog,
            $"Capability learning: promoted={learning.RulePromoted}, kind={learning.FailureKind}");
    }
}
