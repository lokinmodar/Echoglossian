// <copyright file="ChatGPTTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.ClientModel;
using System.Text;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Helpers;
using Echoglossian.Translators.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace Echoglossian.Translators;

public class ChatGPTTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly LlmCapabilityScope capabilityScope;
    private readonly ChatClient? chatClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string promptTemplate;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();
    private readonly string unavailableMessage;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatGPTTranslator" /> class.
    /// </summary>
    /// <param name="pluginLog"></param>
    /// <param name="config">The active plugin configuration.</param>
    public ChatGPTTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.temperature = config.ChatGptTemperature;
        this.promptTemplate = string.IsNullOrWhiteSpace(config.ChatGptPrompt)
            ? PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.ChatGPT)
            : config.ChatGptPrompt;

        var providerSettings =
            OpenAiProviderVariantHelper.ResolveActiveSettings(config);
        var baseUrl = providerSettings.BaseUrl;
        var apiKey = providerSettings.ApiKey;
        this.model = providerSettings.Model;
        this.capabilityScope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.ChatGPT,
            providerSettings.ProviderName,
            baseUrl,
            this.model);
        this.unavailableMessage =
            OpenAiProviderVariantHelper.ResolveUnavailableMessage(
                providerSettings.Variant);

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(this.model))
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                OpenAiProviderVariantHelper.ResolveConfigurationWarning(
                    providerSettings.Variant));
            this.chatClient = null;
        }
        else
        {
            try
            {
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    $"ChatGPTTranslator: provider={providerSettings.ProviderName}, {baseUrl}, {MaskApiKeyForDebugLog(apiKey)}, {this.temperature}");

                var clientOptions = new OpenAIClientOptions
                {
                    Endpoint = new Uri(baseUrl),
                };

                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    $"ChatGPTTranslator: Endpoint={clientOptions.Endpoint}");

                this.chatClient = new ChatClient(
                    this.model,
                    new ApiKeyCredential(apiKey),
                    clientOptions);
            }
            catch (Exception ex)
            {
                PluginRuntimeLog.Error(
                    this.pluginLog,
                    $"Failed to initialize GPT ChatClient: {ex.Message}");
                this.chatClient = null;
            }
        }
    }

    /// <summary>
    ///     Masks an API key for debug logging without assuming a minimum key
    ///     length.
    /// </summary>
    /// <param name="apiKey">The API key to mask.</param>
    /// <returns>A masked representation safe for debug logs.</returns>
    private static string MaskApiKeyForDebugLog(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return "<empty>";
        }

        if (apiKey.Length <= 8)
        {
            return $"{apiKey[0]}***{apiKey[^1]}";
        }

        var prefixLength = Math.Min(20, Math.Max(1, apiKey.Length - 5));
        var suffixLength = Math.Min(5, Math.Max(1, apiKey.Length - prefixLength));
        return $"{apiKey[..prefixLength]}***{apiKey[^suffixLength..]}";
    }

    /// <summary>
    ///     Translates the specified text from the source language to the target
    ///     language.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <returns>The translated text.</returns>
    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return this.TranslateAsync(text, sourceLanguage, targetLanguage)
            .GetAwaiter().GetResult() ?? string.Empty;
    }

    /// <summary>
    ///     Translates the specified text from the source language to the target
    ///     language asynchronously.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <returns>
    ///     A task that represents the asynchronous translation operation. The
    ///     task result contains the translated text.
    /// </returns>
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        if (this.chatClient == null)
        {
            return this.unavailableMessage;
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

    /// <inheritdoc/>
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

        if (this.chatClient == null)
        {
            return this.unavailableMessage;
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
    ///     Performs the actual OpenAI chat completion call for a single cache
    ///     key. Concurrent callers for the same key share the same in-flight
    ///     task.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <param name="cacheKey">The normalized cache key for this request.</param>
    /// <returns>The translated text, or a synthetic error placeholder.</returns>
    private async Task<string?> TranslateCoreAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string cacheKey,
        DialogueTranslationContext? dialogueContext = null)
    {
        if (dialogueContext.HasValue)
        {
            var structuredTranslation = await this.TryTranslateStructuredDialogueAsync(
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

        var temperatureWasSent = false;
        try
        {
            var chatCompletionOptions = new ChatCompletionOptions();
            temperatureWasSent = this.ApplyTemperaturePolicy(chatCompletionOptions);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateUserMessage(prompt),
            };

            ChatCompletion completion =
                await this.chatClient.CompleteChatAsync(
                    messages,
                    chatCompletionOptions).ConfigureAwait(false);
            var translatedText = completion.Content.Count > 0
                ? completion.Content[0].Text?.Trim() ?? string.Empty
                : string.Empty;

            translatedText = translatedText.Trim('"');

            if (!string.IsNullOrEmpty(translatedText))
            {
                if (TranslationResultGuard.IsPersistableTranslation(translatedText))
                {
                    this.translationCache.Remember(cacheKey, translatedText);
                }

                return translatedText;
            }
        }
        catch (Exception ex)
        {
            if (temperatureWasSent)
            {
                this.LearnTemperatureFailure(ex);
            }

            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} {ex.Message}");
            return $"[{Resources.TranslationError} {ex.Message}]";
        }

        return string.Empty;
    }

    /// <summary>
    ///     Attempts the first live structured dialogue path for OpenAI-family
    ///     providers by forcing one tool call that returns JSON arguments.
    ///     Any failure falls back to the existing plain-text path.
    /// </summary>
    /// <param name="text">The visible source text.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="cacheKey">The cache key for this request.</param>
    /// <param name="dialogueContext">The runtime-only dialogue context.</param>
    /// <returns>
    ///     The structured translated text when successful; otherwise
    ///     <see langword="null" /> so the caller can use the plain-text path.
    /// </returns>
    private async Task<string?> TryTranslateStructuredDialogueAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        string cacheKey,
        DialogueTranslationContext dialogueContext)
    {
        if (StructuredDialogueCapabilityHelper.GetPreferredCapability(
                Echoglossian.TransEngines.ChatGPT) != StructuredDialogueProviderCapability.JsonSchema)
        {
            return null;
        }

        var glossaryEntries = StructuredDialogueGlossaryStore.GetEntries(
            sourceLanguage,
            targetLanguage);
        var usedGlossary = glossaryEntries.Count > 0;
        var temperatureWasSent = false;
        var reasoningEffortWasSent = false;
        IReadOnlyList<string> capabilityDecisionTokens = [];
        try
        {
            var normalizedText = FixText(text);
            var structuredRequest = StructuredDialogueTranslationRequestBuilder.Build(
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

            var structuredTool = ChatTool.CreateFunctionTool(
                StructuredDialogueOpenAiToolHelper.ToolFunctionName,
                StructuredDialogueOpenAiToolHelper.ToolFunctionDescription,
                BinaryData.FromString(
                    StructuredDialogueOpenAiToolHelper.BuildFunctionParametersSchemaJson()),
                true);

            var chatCompletionOptions = new ChatCompletionOptions
            {
                ToolChoice = ChatToolChoice.CreateFunctionChoice(
                    StructuredDialogueOpenAiToolHelper.ToolFunctionName),
            };
            temperatureWasSent = this.ApplyTemperaturePolicy(chatCompletionOptions);
            reasoningEffortWasSent = this.ApplyStructuredReasoningEffortPolicy(
                chatCompletionOptions);
            chatCompletionOptions.Tools.Add(structuredTool);

            var capabilitySnapshot = LlmCapabilityPolicyService.GetSnapshot(
                this.capabilityScope);
            var temperatureDecision = capabilitySnapshot.GetDecision(
                LlmCapabilityParameterName.Temperature);
            var reasoningDecision = capabilitySnapshot.GetDecision(
                LlmCapabilityParameterName.ReasoningEffort);
#pragma warning disable OPENAI001
            var reasoningEffortExplicitlyDisabled = reasoningEffortWasSent &&
                chatCompletionOptions.ReasoningEffortLevel == ChatReasoningEffortLevel.None;
#pragma warning restore OPENAI001
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
                StructuredDialogueCapabilityDecisionLogFormatter.Format(
                    LlmCapabilityParameterName.ReasoningEffort,
                    reasoningDecision,
                    reasoningEffortExplicitlyDisabled
                        ? StructuredDialogueCapabilityEmissionMode.ExplicitDisable
                        : reasoningEffortWasSent
                            ? StructuredDialogueCapabilityEmissionMode.SentConfigured
                            : StructuredDialogueCapabilityEmissionMode.OmittedUnknown),
            ];

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateUserMessage(structuredPrompt),
            };

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
                    null,
                    structuredPrompt,
                    normalizedText,
                    capabilityDecisionTokens));

            ChatCompletion completion =
                await this.chatClient!.CompleteChatAsync(
                    messages,
                    chatCompletionOptions).ConfigureAwait(false);

            var rawStructuredPayload = this.ExtractStructuredPayload(
                completion);
            var structuredValidation =
                StructuredDialogueTranslationResponseValidator.ParseAndValidate(
                    rawStructuredPayload);

            if (!structuredValidation.IsValid ||
                !structuredValidation.Response.HasValue)
            {
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.ChatGPT,
                    false,
                    usedGlossary,
                    structuredValidation.FailureReason ??
                    "unknown-structured-dialogue-failure");
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                        "ChatGPT",
                        this.model,
                        StructuredDialogueProviderCapability.JsonSchema,
                        "validation",
                        structuredValidation.FailureReason ??
                        "unknown-structured-dialogue-failure",
                        endpointScope: this.capabilityScope.EndpointScope,
                        route: "chat/completions",
                        capabilityDecisionTokens: capabilityDecisionTokens,
                        glossaryApplied: usedGlossary));
                return null;
            }

            var translatedText =
                structuredValidation.Response.Value.TextTranslated.Trim();
            if (TranslationResultGuard.IsPersistableTranslation(translatedText))
            {
                TranslatorMetricsCollector.RecordStructuredAttempt(
                    (int)Echoglossian.TransEngines.ChatGPT,
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
                (int)Echoglossian.TransEngines.ChatGPT,
                false,
                usedGlossary,
                "non-persistable-structured-result");
            return null;
        }
        catch (Exception ex)
        {
            if (reasoningEffortWasSent)
            {
                this.LearnReasoningEffortFailure(ex);
            }

            if (temperatureWasSent)
            {
                this.LearnTemperatureFailure(ex);
            }

            TranslatorMetricsCollector.RecordStructuredAttempt(
                (int)Echoglossian.TransEngines.ChatGPT,
                false,
                usedGlossary,
                ex.Message);
            PluginRuntimeLog.Debug(
                this.pluginLog,
                StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
                    "ChatGPT",
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

    /// <summary>
    ///     Extracts one raw structured payload from the OpenAI-family response,
    ///     preferring forced tool-call arguments and then falling back to any
    ///     direct text content when a compatible endpoint ignores tool calling.
    /// </summary>
    /// <param name="completion">The provider completion.</param>
    /// <returns>The raw structured payload string, if any.</returns>
    private string ExtractStructuredPayload(ChatCompletion completion)
    {
        foreach (var toolCall in completion.ToolCalls)
        {
            if (toolCall.Kind == ChatToolCallKind.Function &&
                string.Equals(
                    toolCall.FunctionName,
                    StructuredDialogueOpenAiToolHelper.ToolFunctionName,
                    StringComparison.Ordinal))
            {
                return toolCall.FunctionArguments?.ToString() ?? string.Empty;
            }
        }

        if (completion.Content.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (var contentPart in completion.Content)
        {
            builder.Append(contentPart.Text);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    ///     Applies the shared temperature decision to an OpenAI SDK request.
    /// </summary>
    /// <param name="options">The completion options to sanitize.</param>
    /// <returns>
    ///     <see langword="true" /> when temperature was added; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    internal bool ApplyTemperaturePolicy(ChatCompletionOptions options)
    {
        if (!LlmCapabilityPolicyService.TryResolveTemperature(
                this.capabilityScope,
                this.temperature,
                out var sanitizedTemperature,
                out _))
        {
            return false;
        }

        options.Temperature = sanitizedTemperature;
        return true;
    }

    /// <summary>
    ///     Applies the effective structured reasoning-effort capability policy
    ///     to an OpenAI SDK completion request.
    /// </summary>
    /// <param name="options">The completion options to sanitize.</param>
    /// <returns>
    ///     <see langword="true" /> when a configured reasoning-effort value
    ///     or an explicit provider disable remains eligible for transmission;
    ///     otherwise, <see langword="false" />.
    /// </returns>
    internal bool ApplyStructuredReasoningEffortPolicy(
        ChatCompletionOptions options)
    {
        var decision = LlmCapabilityPolicyService.GetSnapshot(
                this.capabilityScope)
            .GetDecision(LlmCapabilityParameterName.ReasoningEffort);

        if (decision.SupportState == LlmCapabilitySupportState.Supported &&
            !decision.OmitWhenDefaultOnly)
        {
#pragma warning disable OPENAI001
            return options.ReasoningEffortLevel is not null;
#pragma warning restore OPENAI001
        }

        if (decision.SupportState == LlmCapabilitySupportState.Unsupported)
        {
#pragma warning disable OPENAI001
            options.ReasoningEffortLevel = ChatReasoningEffortLevel.None;
#pragma warning restore OPENAI001
            return true;
        }

#pragma warning disable OPENAI001
        options.ReasoningEffortLevel = null;
#pragma warning restore OPENAI001
        return false;
    }

    /// <summary>
    ///     Records a sanitized exact-model temperature observation from an
    ///     OpenAI SDK request failure.
    /// </summary>
    /// <param name="exception">The provider exception associated with the request.</param>
    private void LearnTemperatureFailure(Exception exception)
    {
        int? statusCode = exception is ClientResultException clientResultException
            ? clientResultException.Status
            : exception is HttpRequestException httpRequestException &&
            httpRequestException.StatusCode.HasValue
                ? (int)httpRequestException.StatusCode.Value
                : null;
        var responseText = exception is ClientResultException responseException
            ? responseException.GetRawResponse()?.Content?.ToString()
            : null;
        var learning = LlmCapabilityPolicyService.LearnFromProviderFailure(
            this.capabilityScope,
            LlmCapabilityParameterName.Temperature,
            statusCode,
            responseText ?? exception.Message);
        PluginRuntimeLog.Debug(
            this.pluginLog,
            $"Capability learning: promoted={learning.RulePromoted}, kind={learning.FailureKind}");
    }

    /// <summary>
    ///     Records exact-model reasoning-effort feedback from a structured
    ///     OpenAI SDK request failure.
    /// </summary>
    /// <param name="exception">The provider exception associated with the request.</param>
    private void LearnReasoningEffortFailure(Exception exception)
    {
        int? statusCode = exception is ClientResultException clientResultException
            ? clientResultException.Status
            : exception is HttpRequestException httpRequestException &&
            httpRequestException.StatusCode.HasValue
                ? (int)httpRequestException.StatusCode.Value
                : null;
        var responseText = exception is ClientResultException responseException
            ? responseException.GetRawResponse()?.Content?.ToString()
            : null;
        var learning = LlmCapabilityPolicyService.LearnFromProviderFailure(
            this.capabilityScope,
            LlmCapabilityParameterName.ReasoningEffort,
            statusCode,
            responseText ?? exception.Message);
        PluginRuntimeLog.Debug(
            this.pluginLog,
            $"Capability learning: promoted={learning.RulePromoted}, kind={learning.FailureKind}");
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
