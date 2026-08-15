// <copyright file="DeepSeekTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Helpers;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;

namespace Echoglossian.Translators;

public class DeepSeekTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly LlmCapabilityScope capabilityScope;
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
        this.capabilityScope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.DeepSeek,
            "DeepSeek",
            this.baseUrl,
            this.model);
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
                    BaseAddress = new Uri(this.baseUrl.TrimEnd('/') + "/"),
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

        try
        {
            var requestData = new Dictionary<string, object>
            {
                ["model"] = this.model,
                ["messages"] = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt,
                    },
                },
            };
            var temperatureWasSent = LlmCapabilityRequestPayloadSanitizer.TryAddTemperature(
                requestData,
                this.capabilityScope,
                this.temperature);

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var httpContent = new StringContent(
                jsonContent,
                Encoding.UTF8,
                "application/json");

            var response = await this.httpClient.PostAsync(
                "chat/completions",
                httpContent);
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
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

    /// <summary>
    ///     Attempts the OpenAI-compatible structured dialogue path for
    ///     DeepSeek and falls back to the legacy plain-text request on any
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
                Echoglossian.TransEngines.DeepSeek) != StructuredDialogueProviderCapability.JsonSchema)
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
            var structuredPrompt =
                StructuredDialogueOpenAiToolHelper.BuildUserPrompt(
                    PromptTemplateManager.RenderPrompt(
                        this.promptTemplate,
                        normalizedText,
                        sourceLanguage,
                        targetLanguage),
                    structuredRequest);
            var requestData = new Dictionary<string, object>
            {
                ["model"] = this.model,
                ["messages"] = new[]
                {
                    new
                    {
                        role = "user",
                        content = structuredPrompt,
                    },
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
                            parameters = JObject.Parse(
                                StructuredDialogueOpenAiToolHelper.BuildFunctionParametersSchemaJson()),
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
            var temperatureWasSent = LlmCapabilityRequestPayloadSanitizer.TryAddTemperature(
                requestData,
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

            var jsonContent = JsonConvert.SerializeObject(requestData);
            var httpContent = new StringContent(
                jsonContent,
                Encoding.UTF8,
                "application/json");

            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
                    this.capabilityScope,
                    "chat/completions",
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

            var response = await this.httpClient!.PostAsync(
                "chat/completions",
                httpContent).ConfigureAwait(false);
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseString =
                await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var rawStructuredPayload =
                StructuredDialogueOpenAiCompatiblePayloadHelper.ExtractRawStructuredPayload(
                    responseString,
                    StructuredDialogueOpenAiToolHelper.ToolFunctionName);
            var structuredValidation =
                StructuredDialogueTranslationResponseValidator.ParseAndValidate(
                    rawStructuredPayload);

            if (!structuredValidation.IsValid ||
                !structuredValidation.Response.HasValue)
            {
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.DeepSeek,
                    false,
                    usedGlossary,
                    structuredValidation.FailureReason ??
                    "unknown-structured-dialogue-failure");
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                        "DeepSeek",
                        this.model,
                        StructuredDialogueProviderCapability.JsonSchema,
                        "validation",
                        structuredValidation.FailureReason ??
                        "unknown-structured-dialogue-failure",
                        endpointScope: this.capabilityScope.EndpointScope,
                        route: "chat/completions",
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
                    (int)Echoglossian.TransEngines.DeepSeek,
                    true,
                    usedGlossary);
                this.translationCache.Remember(
                    cacheKey,
                    translatedText);
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
                        this.capabilityScope,
                        "chat/completions",
                        StructuredDialogueProviderCapability.JsonSchema,
                        usedGlossary,
                        rawStructuredPayload.Length,
                        translatedText.Length,
                        rawStructuredPayload,
                        translatedText));
                return translatedText;
            }

            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.DeepSeek,
                false,
                usedGlossary,
                "non-persistable-structured-result");
            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                    "DeepSeek",
                    this.model,
                    StructuredDialogueProviderCapability.JsonSchema,
                    "validation",
                    "non-persistable-structured-result",
                    endpointScope: this.capabilityScope.EndpointScope,
                    route: "chat/completions",
                    capabilityDecisionTokens: capabilityDecisionTokens,
                    glossaryApplied: usedGlossary,
                    responseExcerpt: rawStructuredPayload));
            return null;
        }
        catch (Exception ex)
        {
            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.DeepSeek,
                false,
                usedGlossary,
                ex.Message);
            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                    "DeepSeek",
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
                    "chat/completions",
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

    /// <summary>
    ///     Records a sanitized exact-model temperature observation from a
    ///     failed DeepSeek request that included temperature.
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
        var learning = LlmCapabilityPolicyService.LearnFromProviderFailure(
            this.capabilityScope,
            LlmCapabilityParameterName.Temperature,
            (int)response.StatusCode,
            responseText);
        PluginRuntimeLog.Debug(
            this.pluginLog,
            $"Capability learning: promoted={learning.RulePromoted}, kind={learning.FailureKind}");
    }
}
