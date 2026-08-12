// <copyright file="LlmCapabilityStaticCatalog.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Provides committed conservative capability defaults by LLM engine.
/// </summary>
public static class LlmCapabilityStaticCatalog
{
  /// <summary>
  ///     Gets the static capability definitions for an engine.
  /// </summary>
  /// <param name="engine">The translation engine.</param>
  /// <returns>The static capability definitions for <paramref name="engine" />.</returns>
  internal static IEnumerable<LlmCapabilityRuleDefinition> GetDefinitions(
      Echoglossian.TransEngines engine)
  {
    if (engine == Echoglossian.TransEngines.ChatGPT)
    {
      yield return LlmCapabilityRuleDefinition.FamilyPrefix(
          "ChatGPT",
          "OpenAI",
          "https://api.openai.com/v1",
          "gpt-5.6-",
          LlmCapabilityParameterName.Temperature,
          LlmCapabilitySupportState.Unsupported,
          omitWhenDefaultOnly: true,
          reason: "OpenAI chat-completions reasoning models accept only the implicit default temperature.");
    }
  }
}
