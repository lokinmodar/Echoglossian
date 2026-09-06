// <copyright file="ClaudeTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

/// <summary>
///     Translator implementation for Anthropic Claude using the Messages API.
/// </summary>
public class ClaudeTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly LlmCapabilityScope capabilityScope;
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
        this.capabilityScope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.Claude,
            "Anthropic",
            this.baseUrl,
            this.model);
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

    /// <inheritdoc/>
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        if (this.httpClient is null)
        {
            return Resources.ClaudeTranslationUnavailablePleaseCheckYourAPIKey;
        }

        if (!DialogueContextPromptHelper.HasUsableDialogueContext(dialogueContext))
        {
            return await this.TranslateAsync(
                text,
                sourceLanguage,
                targetLanguage).ConfigureAwait(false);
        }

        string cacheKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);
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
                cacheKey,
                dialogueContext)).ConfigureAwait(false);
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

        string fullPrompt = this.BuildPrompt(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);

        var requestData = new Dictionary<string, object>
        {
            ["model"] = this.model,
            ["max_tokens"] = MaxOutputTokens,
            ["messages"] = new[]
            {
                new
                {
                    role = "user",
                    content = fullPrompt,
                },
            },
        };
        var temperatureWasSent = LlmCapabilityRequestPayloadSanitizer.TryAddTemperature(
            requestData,
            this.capabilityScope,
            this.temperature);

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
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
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

    /// <summary>
    ///     Attempts the Anthropic structured dialogue path and falls back to
    ///     the legacy plain-text request on incompatibility or malformed tool
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
                Echoglossian.TransEngines.Claude) != StructuredDialogueProviderCapability.JsonSchema)
        {
            return null;
        }

        IReadOnlyList<StructuredDialogueGlossaryEntry> glossaryEntries =
            StructuredDialogueGlossaryStore.GetEntries(
                sourceLanguage,
                targetLanguage);
        bool usedGlossary = glossaryEntries.Count > 0;
        IReadOnlyList<string> capabilityDecisionTokens = [];
        try
        {
            string normalizedText = FixText(text);
            StructuredDialogueTranslationRequest structuredRequest =
                StructuredDialogueTranslationRequestBuilder.Build(
                    normalizedText,
                    sourceLanguage,
                    targetLanguage,
                    TranslationSurfaceGroup.Dialogue,
                    dialogueContext,
                    glossaryEntries);
            string basePrompt = this.promptTemplate
                .Replace("{text}", normalizedText, StringComparison.Ordinal)
                .Replace("{sourceLanguage}", sourceLanguage, StringComparison.Ordinal)
                .Replace("{targetLanguage}", targetLanguage, StringComparison.Ordinal);
            string structuredPrompt =
                StructuredDialogueOpenAiToolHelper.BuildUserPrompt(
                    basePrompt,
                    structuredRequest);

            var requestData = new Dictionary<string, object>
            {
                ["model"] = this.model,
                ["max_tokens"] = MaxOutputTokens,
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
                        name = StructuredDialogueAnthropicToolHelper.ToolName,
                        description = StructuredDialogueAnthropicToolHelper.ToolDescription,
                        input_schema = JObject.Parse(
                            StructuredDialogueAnthropicToolHelper.BuildInputSchemaJson()),
                    },
                },
                ["tool_choice"] = new
                {
                    type = "tool",
                    name = StructuredDialogueAnthropicToolHelper.ToolName,
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

            string jsonContent = JsonConvert.SerializeObject(requestData);
            using StringContent httpContent = new(
                jsonContent,
                Encoding.UTF8,
                "application/json");

            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
                    this.capabilityScope,
                    "v1/messages",
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

            using HttpResponseMessage response = await this.httpClient!.PostAsync(
                "v1/messages",
                httpContent).ConfigureAwait(false);
            await this.LearnTemperatureFailureAsync(response, temperatureWasSent).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string? rawStructuredPayload =
                StructuredDialogueAnthropicToolHelper.ExtractRawStructuredPayload(
                    responseString,
                    StructuredDialogueAnthropicToolHelper.ToolName);
            StructuredDialogueResponseValidationResult structuredValidation =
                StructuredDialogueTranslationResponseValidator.ParseAndValidate(
                    rawStructuredPayload);

            if (!structuredValidation.IsValid ||
                !structuredValidation.Response.HasValue)
            {
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.Claude,
                    false,
                    usedGlossary,
                    structuredValidation.FailureReason ??
                    "unknown-structured-dialogue-failure");
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                        this.capabilityScope.ProviderScope,
                        this.model,
                        StructuredDialogueProviderCapability.JsonSchema,
                        "validation",
                        structuredValidation.FailureReason ??
                        "unknown-structured-dialogue-failure",
                        endpointScope: this.capabilityScope.EndpointScope,
                        route: "v1/messages",
                        capabilityDecisionTokens: capabilityDecisionTokens,
                        glossaryApplied: usedGlossary,
                        responseExcerpt: rawStructuredPayload));
                return null;
            }

            string translatedText =
                structuredValidation.Response.Value.TextTranslated.Trim();
            if (TranslationResultGuard.IsPersistableTranslation(translatedText))
            {
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.Claude,
                    true,
                    usedGlossary);
                this.translationCache.Remember(cacheKey, translatedText);
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
                        this.capabilityScope,
                        "v1/messages",
                        StructuredDialogueProviderCapability.JsonSchema,
                        usedGlossary,
                        rawStructuredPayload?.Length ?? 0,
                        translatedText.Length,
                        rawStructuredPayload ?? string.Empty,
                        translatedText));
                return translatedText;
            }

            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.Claude,
                false,
                usedGlossary,
                "non-persistable-structured-result");
            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                    this.capabilityScope.ProviderScope,
                    this.model,
                    StructuredDialogueProviderCapability.JsonSchema,
                    "validation",
                    "non-persistable-structured-result",
                    endpointScope: this.capabilityScope.EndpointScope,
                    route: "v1/messages",
                    capabilityDecisionTokens: capabilityDecisionTokens,
                    glossaryApplied: usedGlossary,
                    responseExcerpt: rawStructuredPayload));
            return null;
        }
        catch (Exception ex)
        {
            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.Claude,
                false,
                usedGlossary,
                ex.Message);
            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                    this.capabilityScope.ProviderScope,
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
                    "v1/messages",
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
        string prompt = this.promptTemplate
            .Replace("{text}", text, StringComparison.Ordinal)
            .Replace("{sourceLanguage}", sourceLanguage, StringComparison.Ordinal)
            .Replace("{targetLanguage}", targetLanguage, StringComparison.Ordinal);

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
    ///     failed Anthropic request that included temperature.
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
