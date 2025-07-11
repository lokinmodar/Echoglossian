// <copyright file="ChatGPTTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.ClientModel;

using Echoglossian.Properties;

using OpenAI;
using OpenAI.Chat;

namespace Echoglossian.Translators
{
  public class ChatGPTTranslator : ITranslator
  {
    private readonly ChatClient? chatClient;
    private readonly IPluginLog pluginLog;
    private readonly string model;
    private float temperature;
    private Dictionary<string, string> translationCache = new Dictionary<string, string>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatGPTTranslator"/> class.
    /// </summary>
    /// <param name="pluginLog"></param>
    /// <param name="baseUrl"></param>
    /// <param name="apiKey"></param>
    /// <param name="model"></param>
    /// <param name="temperature"></param>
    public ChatGPTTranslator(IPluginLog pluginLog, string baseUrl = "https://api.openai.com/v1", string apiKey = "", string model = "gpt-4o-mini", float temperature = 0.1f)
    {
      this.pluginLog = pluginLog;
      this.model = model;
      this.temperature = temperature;

      if (string.IsNullOrWhiteSpace(apiKey))
      {
        this.pluginLog.Warning(Resources.APIKeyIsEmptyOrInvalidChatGPTTranslationWillNotBeAvailable);
        this.chatClient = null;
      }
      else
      {
        try
        {
          pluginLog.Debug($"ChatGPTTranslator: {baseUrl}, {apiKey[..20]}***{apiKey[^5..]}, {temperature}");

          var clientOptions = new OpenAIClientOptions
          {
            Endpoint = new Uri(baseUrl),
          };

          pluginLog.Debug($"ChatGPTTranslator: {string.Join(", ", clientOptions.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(clientOptions)}"))}");

          this.chatClient = new ChatClient(model, new ApiKeyCredential(apiKey), clientOptions);
        }
        catch (Exception ex)
        {
          this.pluginLog.Error($"Failed to initialize GPT ChatClient: {ex.Message}");
          this.chatClient = null;
        }
      }
    }

    /// <summary>
    /// Translates the specified text from the source language to the target language.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <returns>The translated text.</returns>
    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).GetAwaiter().GetResult() ?? string.Empty;
    }

    /// <summary>
    /// Translates the specified text from the source language to the target language asynchronously.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language of the text.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <returns>A task that represents the asynchronous translation operation. The task result contains the translated text.</returns>
    public async Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      if (this.chatClient == null)
      {
        return Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
      }

      string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
      if (this.translationCache.TryGetValue(cacheKey, out string? cachedTranslation))
      {
        return cachedTranslation;
      }

      string prompt = @$"As an expert translator and cultural localization specialist with deep knowledge of video game localization, your task is to translate dialogues from the game Final Fantasy XIV from {sourceLanguage} to {targetLanguage}. This is not just a translation, but a full localization effort tailored for the Final Fantasy XIV universe. Please adhere to the following guidelines:

                                1. Preserve the original tone, humor, personality, and emotional nuances of the dialogue, considering the unique style and atmosphere of Final Fantasy XIV.
                                2. Adapt idioms, cultural references, and wordplay to resonate naturally with native {targetLanguage} speakers while maintaining the fantasy RPG context.
                                3. Maintain consistency in character voices, terminology, and naming conventions specific to Final Fantasy XIV throughout the translation.
                                4. Avoid literal translations that may lose the original intent or impact, especially for game-specific terms or lore elements.
                                5. Ensure the translation flows naturally and reads as if it were originally written in {targetLanguage}, while staying true to the game's narrative style.
                                6. Consider the context and subtext of the dialogue, including any references to the game's lore, world, or ongoing storylines.
                                7. If a word, phrase, or name has been translated in a specific way, maintain that translation consistently unless the context demands otherwise, respecting established localization choices for Final Fantasy XIV.
                                8. Pay attention to formal/informal speech patterns and adjust accordingly for the target language and cultural norms, considering the speaker's role and status within the game world.
                                9. Be mindful of character limits or text box constraints that may be present in the game, adapting the translation to fit if necessary.
                                10. Preserve any game-specific jargon, spell names, or technical terms according to the official localization guidelines for Final Fantasy XIV in the target language.

                                Text to translate: ""{text}""

                                Please provide only the translated text in your response, without any explanations, additional comments, or quotation marks. Your goal is to create a localized version that captures the essence of the original Final Fantasy XIV dialogue while feeling authentic to {targetLanguage} speakers and seamlessly fitting into the game world.";

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

        ChatCompletion completion = await this.chatClient.CompleteChatAsync(messages, chatCompletionOptions);
        string translatedText = completion.Content[0].Text.Trim();

        translatedText = translatedText.Trim('"');

        if (!string.IsNullOrEmpty(translatedText))
        {
          this.translationCache[cacheKey] = translatedText;
          return translatedText;
        }
      }
      catch (Exception ex)
      {
        this.pluginLog.Error($"{Resources.TranslationError} {ex.Message}");
        return $"[{Resources.TranslationError} {ex.Message}]";
      }

      return string.Empty;
    }
  }
}