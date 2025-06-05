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

  public const string DefaultPrompt = @"As an expert translator and cultural localization specialist with deep knowledge of video game localization, your task is to translate dialogues from the game Final Fantasy XIV from {sourceLanguage} to {targetLanguage}. ...";

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
      _ => null,
    };
  }

  public void SetPrompt(PromptType type, string? prompt)
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
      _ => null,
    };
  }
}
