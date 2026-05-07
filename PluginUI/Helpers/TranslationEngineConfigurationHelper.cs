// <copyright file="TranslationEngineConfigurationHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.LibreTranslate;

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
/// Provides readiness checks for translator-engine configurations used by the
/// config UI activation flow.
/// </summary>
public static class TranslationEngineConfigurationHelper
{
  /// <summary>
  /// Determines whether the currently selected translation engine has the
  /// minimum required configuration to enable translations.
  /// </summary>
  /// <param name="config">The current plugin configuration.</param>
  /// <returns>
  /// <c>true</c> when the selected engine is ready for activation; otherwise,
  /// <c>false</c>.
  /// </returns>
  public static bool IsConfigured(Config config)
  {
    return IsConfigured(config, (Echoglossian.TransEngines)config.ChosenTransEngine);
  }

  /// <summary>
  /// Determines whether a specific translation engine has the minimum required
  /// configuration to enable translations.
  /// </summary>
  /// <param name="config">The current plugin configuration.</param>
  /// <param name="engine">The engine to validate.</param>
  /// <returns>
  /// <c>true</c> when the engine is ready for activation; otherwise,
  /// <c>false</c>.
  /// </returns>
  public static bool IsConfigured(Config config, Echoglossian.TransEngines engine)
  {
    return engine switch
    {
      Echoglossian.TransEngines.Google => true,
      Echoglossian.TransEngines.Deepl => !config.DeeplTranslatorUsingApiKey ||
          HasValue(config.DeeplTranslatorApiKey),
      Echoglossian.TransEngines.ChatGPT => HasValue(config.ChatGptApiKey) &&
          HasValue(config.ChatGPTBaseUrl),
      Echoglossian.TransEngines.YandexCloud => HasValue(config.YandexFolderId) &&
          HasValue(config.YandexPaidApiKey),
      Echoglossian.TransEngines.GTranslate => true,
      Echoglossian.TransEngines.DeepSeek => HasValue(config.DeepSeekTranslatorApiKey) &&
          HasValue(config.DeepSeekBaseUrl),
      Echoglossian.TransEngines.Ollama => HasValue(config.OllamaUrl) &&
          HasValue(config.OllamaModel),
      Echoglossian.TransEngines.LibreTranslate =>
          config.LibreTranslateInstanceType != LibreTranslateInstanceType.Custom ||
          HasValue(config.LibreTranslateUrl),
      Echoglossian.TransEngines.Microsoft => HasValue(config.MicrosoftTranslatorApiKey) &&
          HasValue(config.MicrosoftTranslatorRegion) &&
          HasValue(config.MicrosoftTranslatorEndpoint),
      Echoglossian.TransEngines.Amazon => HasValue(config.AwsAccessKey) &&
          HasValue(config.AwsSecretKey) &&
          HasValue(config.AwsRegion),
      Echoglossian.TransEngines.Gemini => HasValue(config.GeminiTranslatorApiKey),
      Echoglossian.TransEngines.YandexPublic => true,
      Echoglossian.TransEngines.OpenRouter => HasValue(config.OpenRouterApiKey) &&
          HasValue(config.OpenRouterBaseUrl),
      Echoglossian.TransEngines.LmStudio => HasValue(config.LmStudioBaseUrl) &&
          HasValue(config.LmStudioModel) &&
          (!config.UseLmStudioAuth || HasValue(config.LmStudioApiKey)),
      Echoglossian.TransEngines.Claude => HasValue(config.ClaudeApiKey) &&
          HasValue(config.ClaudeBaseUrl),
      _ => false,
    };
  }

  /// <summary>
  /// Returns whether a configuration value is meaningfully populated.
  /// </summary>
  /// <param name="value">The value to test.</param>
  /// <returns><c>true</c> when the value is non-empty; otherwise, <c>false</c>.</returns>
  private static bool HasValue(string? value)
  {
    return !string.IsNullOrWhiteSpace(value);
  }
}
