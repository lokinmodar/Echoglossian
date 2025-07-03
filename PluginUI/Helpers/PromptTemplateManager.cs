// <copyright file="PromptTemplateManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
/// Manages prompt validation, retrieval, and dynamic substitution for translator prompts.
/// </summary>
public class PromptTemplateManager
{
  private readonly Config config;

  public PromptTemplateManager(Config config)
  {
    this.config = config;
  }

  /// <summary>
  /// Default prompt used when no custom prompt is defined.
  /// </summary>
  public const string DefaultPrompt = @"As an expert translator and cultural localization specialist with deep knowledge of video game localization, your task is to translate dialogues from the game Final Fantasy XIV from {sourceLanguage} to {targetLanguage}. This is not just a translation, but a full localization effort tailored for the Final Fantasy XIV universe. Please adhere to the following guidelines:

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

Please provide only the translated text in your response, without any explanations, additional comments, or quotation marks. Your goal is to create a localized version that captures the essence of the original Final Fantasy XIV dialogue while feeling authentic to {targetLanguage} speakers and seamlessly fitting into the game world.;";

  private static readonly string[] RequiredPlaceholders =
  {
    "{text}",
    "{sourceLanguage}",
    "{targetLanguage}",
  };

  public bool IsPromptValid(string prompt)
  {
    return RequiredPlaceholders.All(p => prompt.Contains(p, StringComparison.OrdinalIgnoreCase));
  }

  public string ApplyPromptVariables(string template, string text, string sourceLang, string targetLang)
  {
    return template
      .Replace("{text}", text)
      .Replace("{sourceLanguage}", sourceLang)
      .Replace("{targetLanguage}", targetLang);
  }

  public string? GetPrompt(PromptType type)
  {
    return type switch
    {
      PromptType.DeepSeek => this.config.DeepSeekPrompt,
      PromptType.Gemini => this.config.GeminiPrompt,
      PromptType.OpenRouter => this.config.OpenRouterPrompt,
      PromptType.Microsoft => this.config.MicrosoftTranslatorPrompt,
      PromptType.Amazon => this.config.AmazonPrompt,
      PromptType.ChatGPT => this.config.ChatGptPrompt,
      PromptType.YandexCloud => this.config.YandexCloudPrompt,
      PromptType.Ollama => this.config.OllamaPrompt,
      _ => null,
    };
  }

  public string GetPromptOrDefault(PromptType type)
  {
    var prompt = this.GetPrompt(type);
    return string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt;
  }

  public void SetPrompt(PromptType type, string prompt)
  {
    switch (type)
    {
      case PromptType.DeepSeek: this.config.DeepSeekPrompt = prompt; break;
      case PromptType.Gemini: this.config.GeminiPrompt = prompt; break;
      case PromptType.OpenRouter: this.config.OpenRouterPrompt = prompt; break;
      case PromptType.Microsoft: this.config.MicrosoftTranslatorPrompt = prompt; break;
      case PromptType.Amazon: this.config.AmazonPrompt = prompt; break;
      case PromptType.ChatGPT: this.config.ChatGptPrompt = prompt; break;
      case PromptType.YandexCloud: this.config.YandexCloudPrompt = prompt; break;
      case PromptType.Ollama: this.config.OllamaPrompt = prompt; break;
    }
  }

  public PromptType? GetPromptTypeForEngine(int engineIndex)
  {
    return engineIndex switch
    {
      2 => PromptType.ChatGPT,
      3 => PromptType.DeepSeek,
      4 => PromptType.Gemini,
      5 => PromptType.OpenRouter,
      6 => PromptType.Microsoft,
      7 => PromptType.Amazon,
      8 => PromptType.YandexCloud,
      9 => PromptType.Ollama,
      _ => null,
    };
  }
}
