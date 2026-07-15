// <copyright file="OpenAiProviderVariantHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.OpenAI;

/// <summary>
///     Resolves the active OpenAI-family provider profile while keeping the
///     official OpenAI path and the custom OpenAI-compatible path inside the
///     same engine family.
/// </summary>
internal static class OpenAiProviderVariantHelper
{
  /// <summary>
  ///     Describes one resolved OpenAI-family provider profile.
  /// </summary>
  /// <param name="Variant">The selected provider variant.</param>
  /// <param name="ProviderName">The displayable provider label.</param>
  /// <param name="ApiKey">The API key used by the active profile.</param>
  /// <param name="BaseUrl">The base URL used by the active profile.</param>
  /// <param name="Model">The model used by the active profile.</param>
  /// <param name="UseLiveModelList">
  ///     Whether live model listing is enabled
  ///     for the active profile.
  /// </param>
  internal readonly record struct OpenAiProviderSettings(
      OpenAiProviderVariant Variant,
      string ProviderName,
      string ApiKey,
      string BaseUrl,
      string Model,
      bool UseLiveModelList);

  /// <summary>
  ///     Resolves the active provider profile for the current configuration.
  /// </summary>
  /// <param name="config">The active plugin configuration.</param>
  /// <returns>The resolved provider profile.</returns>
  internal static OpenAiProviderSettings ResolveActiveSettings(Config config)
  {
    return config.OpenAiProviderVariant == OpenAiProviderVariant.CustomOpenAICompatible
        ? new OpenAiProviderSettings(
            OpenAiProviderVariant.CustomOpenAICompatible,
            "OpenAI-Compatible",
            config.CustomOpenAiCompatibleApiKey,
            config.CustomOpenAiCompatibleBaseUrl,
            config.CustomOpenAiCompatibleModel,
            config.UseLiveCustomOpenAiCompatibleModelList)
        : new OpenAiProviderSettings(
            OpenAiProviderVariant.OfficialOpenAI,
            "OpenAI",
            config.ChatGptApiKey,
            config.ChatGPTBaseUrl,
            config.OpenAILlmModel,
            config.UseLiveOpenAIModelList);
  }

  /// <summary>
  ///     Resolves the user-facing unavailable message for the selected
  ///     OpenAI-family provider variant.
  /// </summary>
  /// <param name="variant">The selected provider variant.</param>
  /// <returns>The localized unavailable message for that variant.</returns>
  internal static string ResolveUnavailableMessage(OpenAiProviderVariant variant)
  {
    return variant == OpenAiProviderVariant.CustomOpenAICompatible
        ? Resources.OpenAiCompatibleTranslationUnavailablePleaseCheckProviderConfiguration
        : Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
  }

  /// <summary>
  ///     Resolves the startup or initialization warning used when the selected
  ///     OpenAI-family provider variant is not configured enough to create a
  ///     translator client safely.
  /// </summary>
  /// <param name="variant">The selected provider variant.</param>
  /// <returns>The localized configuration warning message.</returns>
  internal static string ResolveConfigurationWarning(OpenAiProviderVariant variant)
  {
    return variant == OpenAiProviderVariant.CustomOpenAICompatible
        ? Resources.OpenAiCompatibleProviderConfigurationIncompleteTranslationWillNotBeAvailable
        : Resources.APIKeyIsEmptyOrInvalidChatGPTTranslationWillNotBeAvailable;
  }
}
