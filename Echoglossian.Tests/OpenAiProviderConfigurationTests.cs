// <copyright file="OpenAiProviderConfigurationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Helpers;
using Echoglossian.Properties;
using Echoglossian.Tests.TestDoubles;
using Echoglossian.Translators;
using Echoglossian.Translators.OpenAI;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers OpenAI-family provider configuration and unavailability behavior
///     for the official and custom provider variants.
/// </summary>
public class OpenAiProviderConfigurationTests
{
  /// <summary>
  ///     Ensures the active OpenAI-family configuration gate accepts the
  ///     custom provider when its own endpoint, key, and model are present.
  /// </summary>
  [Fact]
  public void IsConfigured_CustomVariantWithRequiredFields_ReturnsTrue()
  {
    var config = new Config
    {
      ChosenTransEngine = (int)Echoglossian.TransEngines.ChatGPT,
      OpenAiProviderVariant = OpenAiProviderVariant.CustomOpenAICompatible,
      CustomOpenAiCompatibleApiKey = "custom-key",
      CustomOpenAiCompatibleBaseUrl = "https://nano-gpt.com/api/v1",
      CustomOpenAiCompatibleModel = "llama-3.3-70b",
      ChatGptApiKey = string.Empty,
      ChatGPTBaseUrl = string.Empty,
      OpenAILlmModel = string.Empty,
    };

    var configured = TranslationEngineConfigurationHelper.IsConfigured(config);

    Assert.True(configured);
  }

  /// <summary>
  ///     Ensures the active OpenAI-family configuration gate rejects the
  ///     custom provider when its active model is still missing.
  /// </summary>
  [Fact]
  public void IsConfigured_CustomVariantWithoutModel_ReturnsFalse()
  {
    var config = new Config
    {
      ChosenTransEngine = (int)Echoglossian.TransEngines.ChatGPT,
      OpenAiProviderVariant = OpenAiProviderVariant.CustomOpenAICompatible,
      CustomOpenAiCompatibleApiKey = "custom-key",
      CustomOpenAiCompatibleBaseUrl = "https://nano-gpt.com/api/v1",
      CustomOpenAiCompatibleModel = string.Empty,
    };

    var configured = TranslationEngineConfigurationHelper.IsConfigured(config);

    Assert.False(configured);
  }

  /// <summary>
  ///     Ensures the custom OpenAI-compatible provider returns its own
  ///     unavailable message instead of the legacy official OpenAI message
  ///     when the provider profile is incomplete.
  /// </summary>
  /// <returns>A task representing the test.</returns>
  [Fact]
  public async Task TranslateAsync_CustomVariantWithoutClient_ReturnsCustomUnavailableMessage()
  {
    var translator = new ChatGPTTranslator(
        new NoOpPluginLog(),
        new Config
        {
          OpenAiProviderVariant = OpenAiProviderVariant.CustomOpenAICompatible,
          CustomOpenAiCompatibleApiKey = string.Empty,
          CustomOpenAiCompatibleBaseUrl = "https://nano-gpt.com/api/v1",
          CustomOpenAiCompatibleModel = "llama-3.3-70b",
        });

    var result = await translator.TranslateAsync("Hello", "en", "pt-BR");

    Assert.Equal(
        Resources.OpenAiCompatibleTranslationUnavailablePleaseCheckProviderConfiguration,
        result);
  }
}
