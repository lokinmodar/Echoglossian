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

public class ChatGPTTranslator : ITranslator
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
    /// <param name="baseUrl"></param>
    /// <param name="apiKey"></param>
    /// <param name="model"></param>
    /// <param name="temperature"></param>
    /// <param name="promptTemplate"></param>
    public ChatGPTTranslator(
        IPluginLog pluginLog,
        string baseUrl = "https://api.openai.com/v1",
        string apiKey = "",
        string model = "gpt-4o-mini",
        float temperature = 0.1f,
        string? promptTemplate = null)
    {
        this.pluginLog = pluginLog;
        this.model = model;
        this.temperature = temperature;
        this.promptTemplate = string.IsNullOrWhiteSpace(promptTemplate)
            ? PromptTemplateManager.DefaultPrompt
            : promptTemplate;

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
                    pluginLog,
                    $"ChatGPTTranslator: {baseUrl}, {apiKey[..20]}***{apiKey[^5..]}, {temperature}");

                var clientOptions = new OpenAIClientOptions
                {
                    Endpoint = new Uri(baseUrl),
                };

                PluginRuntimeLog.Debug(
                    pluginLog,
                    $"ChatGPTTranslator: Endpoint={clientOptions.Endpoint}");

                this.chatClient = new ChatClient(
                    model,
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
        string cacheKey)
    {
        var client = this.chatClient;

        if (client is null)
        {
            return Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
        }

        var prompt = BuildPrompt(
            this.promptTemplate,
            text,
            sourceLanguage,
            targetLanguage);

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
                await client.CompleteChatAsync(
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
            PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} {ex.Message}");
            return $"[{Resources.TranslationError} {ex.Message}]";
        }

        return string.Empty;
    }

    internal static string BuildPrompt(
        string? promptTemplate,
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        var normalizedTemplate = string.IsNullOrWhiteSpace(promptTemplate)
            ? PromptTemplateManager.DefaultPrompt
            : promptTemplate;

        return normalizedTemplate
            .Replace(
                "{sourceLanguage}",
                sourceLanguage,
                StringComparison.Ordinal)
            .Replace(
                "{targetLanguage}",
                targetLanguage,
                StringComparison.Ordinal)
            .Replace("{text}", FixText(text), StringComparison.Ordinal);
    }
}
