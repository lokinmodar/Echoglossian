// <copyright file="ChatGPTTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.ClientModel;
using Echoglossian.PluginUI.Helpers;
using Echoglossian.Translators.Helpers;
using OpenAI;
using OpenAI.Chat;

namespace Echoglossian.Translators;

public class ChatGPTTranslator : ITranslator, IDialogueContextAwareTranslator
{
    private readonly ChatClient? chatClient;
    private readonly string model;
    private readonly IPluginLog pluginLog;
    private readonly string promptTemplate;
    private readonly float temperature;
    private readonly ConcurrentTranslationRequestCache translationCache = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatGPTTranslator" /> class.
    /// </summary>
    /// <param name="pluginLog"></param>
    /// <param name="config">The active plugin configuration.</param>
    public ChatGPTTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.model = config.OpenAILlmModel;
        this.temperature = config.ChatGptTemperature;
        this.promptTemplate = string.IsNullOrWhiteSpace(config.ChatGptPrompt)
            ? PromptTemplateManager.GetDefaultPrompt(Echoglossian.PromptType.ChatGPT)
            : config.ChatGptPrompt;

        var baseUrl = config.ChatGPTBaseUrl;
        var apiKey = config.ChatGptApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                Resources
                    .APIKeyIsEmptyOrInvalidChatGPTTranslationWillNotBeAvailable);
            this.chatClient = null;
        }
        else
        {
            try
            {
                PluginRuntimeLog.Debug(
                    this.pluginLog,
                    $"ChatGPTTranslator: {baseUrl}, {apiKey[..20]}***{apiKey[^5..]}, {this.temperature}");

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

    /// <inheritdoc/>
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        if (!HasUsableDialogueContext(dialogueContext))
        {
            return await this.TranslateAsync(
                text,
                sourceLanguage,
                targetLanguage).ConfigureAwait(false);
        }

        if (this.chatClient == null)
        {
            return Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
        }

        var cacheKey = this.BuildDialogueContextCacheKey(
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
        var prompt = this.BuildPrompt(
            text,
            sourceLanguage,
            targetLanguage,
            dialogueContext);

        try
        {
            var chatCompletionOptions = new ChatCompletionOptions
            {
                Temperature = this.temperature,
            };

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateUserMessage(prompt),
            };

            ChatCompletion completion =
                await this.chatClient.CompleteChatAsync(
                    messages,
                    chatCompletionOptions).ConfigureAwait(false);
            var translatedText = completion.Content[0].Text.Trim();

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
            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} {ex.Message}");
            return $"[{Resources.TranslationError} {ex.Message}]";
        }

        return string.Empty;
    }

    private static bool HasUsableDialogueContext(DialogueTranslationContext dialogueContext)
    {
        return dialogueContext.PriorTurns.Count > 0;
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

        if (!dialogueContext.HasValue ||
            !HasUsableDialogueContext(dialogueContext.Value))
        {
            return prompt;
        }

        var priorTurns = string.Join(
            Environment.NewLine,
            dialogueContext.Value.PriorTurns.Select(
                (turn, index) =>
                    $"[{index + 1}] {turn.SpeakerName}: {FixText(turn.SourceText)}"));

        return
            $"{prompt}{Environment.NewLine}{Environment.NewLine}Previous dialogue context for translation consistency only (translate only the current text, not the history):{Environment.NewLine}Current speaker: {dialogueContext.Value.SpeakerName}{Environment.NewLine}{priorTurns}";
    }

    private string BuildDialogueContextCacheKey(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        var historyKey = string.Join(
            "|",
            dialogueContext.PriorTurns.Select(
                turn => $"{turn.SpeakerName}:{turn.SourceText}"));

        return
            $"dialogue|{dialogueContext.SessionNamespace}|{dialogueContext.SessionKey}|{historyKey}|{text}_{sourceLanguage}_{targetLanguage}";
    }
}
