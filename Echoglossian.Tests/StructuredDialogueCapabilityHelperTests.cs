// <copyright file="StructuredDialogueCapabilityHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the structured dialogue provider-capability mapping used by the
///     first phase of issue 148.
/// </summary>
public class StructuredDialogueCapabilityHelperTests
{
  /// <summary>
  ///     Ensures OpenAI-family and local OpenAI-style engines map to the
  ///     current structured dialogue capability baseline.
  /// </summary>
  [Theory]
  [InlineData(Echoglossian.TransEngines.ChatGPT)]
  [InlineData(Echoglossian.TransEngines.OpenRouter)]
  [InlineData(Echoglossian.TransEngines.DeepSeek)]
  [InlineData(Echoglossian.TransEngines.LmStudio)]
  [InlineData(Echoglossian.TransEngines.Ollama)]
  [InlineData(Echoglossian.TransEngines.Gemini)]
  public void GetPreferredCapability_StructuredFamilies_ReturnJsonSchema(
      Echoglossian.TransEngines engine)
  {
    StructuredDialogueCapabilityHelper.GetPreferredCapability(engine)
        .Should().Be(StructuredDialogueProviderCapability.JsonSchema);
  }

  /// <summary>
  ///     Ensures providers not yet in the first structured rollout remain
  ///     disabled by default.
  /// </summary>
  [Theory]
  [InlineData(Echoglossian.TransEngines.Claude)]
  [InlineData(Echoglossian.TransEngines.Google)]
  [InlineData(Echoglossian.TransEngines.Microsoft)]
  public void GetPreferredCapability_UnsupportedFamilies_ReturnDisabled(
      Echoglossian.TransEngines engine)
  {
    StructuredDialogueCapabilityHelper.GetPreferredCapability(engine)
        .Should().Be(StructuredDialogueProviderCapability.Disabled);
  }
}
