// <copyright file="OpenAiProviderVariantHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers resolution of the active OpenAI-family provider variant.
/// </summary>
public class OpenAiProviderVariantHelperTests
{
  /// <summary>
  ///     Ensures the official OpenAI profile remains the default active
  ///     provider path.
  /// </summary>
  [Fact]
  public void ResolveActiveSettings_DefaultVariant_UsesOfficialOpenAiProfile()
  {
    var config = new Config
    {
      OpenAiProviderVariant = OpenAiProviderVariant.OfficialOpenAI,
      ChatGptApiKey = "official-key",
      ChatGPTBaseUrl = "https://api.openai.com/v1",
      OpenAILlmModel = "gpt-4o-mini",
      UseLiveOpenAIModelList = true,
    };

    var settings = OpenAiProviderVariantHelper.ResolveActiveSettings(config);

    Assert.Equal(OpenAiProviderVariant.OfficialOpenAI, settings.Variant);
    Assert.Equal("OpenAI", settings.ProviderName);
    Assert.Equal("official-key", settings.ApiKey);
    Assert.Equal("https://api.openai.com/v1", settings.BaseUrl);
    Assert.Equal("gpt-4o-mini", settings.Model);
    Assert.True(settings.UseLiveModelList);
  }

  /// <summary>
  ///     Ensures the custom provider profile becomes the active OpenAI-family
  ///     path when the variant is switched explicitly.
  /// </summary>
  [Fact]
  public void ResolveActiveSettings_CustomVariant_UsesCustomCompatibleProfile()
  {
    var config = new Config
    {
      OpenAiProviderVariant = OpenAiProviderVariant.CustomOpenAICompatible,
      CustomOpenAiCompatibleApiKey = "custom-key",
      CustomOpenAiCompatibleBaseUrl = "https://nano-gpt.com/api/v1",
      CustomOpenAiCompatibleModel = "llama-3.3-70b",
      UseLiveCustomOpenAiCompatibleModelList = true,
    };

    var settings = OpenAiProviderVariantHelper.ResolveActiveSettings(config);

    Assert.Equal(OpenAiProviderVariant.CustomOpenAICompatible, settings.Variant);
    Assert.Equal("OpenAI-Compatible", settings.ProviderName);
    Assert.Equal("custom-key", settings.ApiKey);
    Assert.Equal("https://nano-gpt.com/api/v1", settings.BaseUrl);
    Assert.Equal("llama-3.3-70b", settings.Model);
    Assert.True(settings.UseLiveModelList);
  }
}
