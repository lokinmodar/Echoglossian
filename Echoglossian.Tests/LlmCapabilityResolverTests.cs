// <copyright file="LlmCapabilityResolverTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Capabilities;

using FluentAssertions;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers conservative resolution of static and overlay LLM capabilities.
/// </summary>
public class LlmCapabilityResolverTests
{
  /// <summary>
  ///     Ensures an exact-model overlay takes precedence over a matching
  ///     family-prefix static rule.
  /// </summary>
  [Fact]
  public void Resolve_WithExactAndFamilyRules_PrefersExactRule()
  {
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");
    var exactRule = LlmCapabilityRuleDefinition.ExactModel(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Supported,
        source: "LiveRefresh",
        reason: "provider metadata confirmed support");

    var snapshot = LlmCapabilityResolver.Resolve(scope, [exactRule]);

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Supported);
  }

  /// <summary>
  ///     Ensures the static family-prefix rule disables temperature for
  ///     matching OpenAI reasoning models.
  /// </summary>
  [Fact]
  public void Resolve_WithMatchingFamilyRule_ReturnsUnsupported()
  {
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");

    var snapshot = LlmCapabilityResolver.Resolve(
        scope,
        Array.Empty<LlmCapabilityRuleDefinition>());

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Unsupported);
  }

  /// <summary>
  ///     Ensures unsupported support wins when equally specific overlay rules
  ///     disagree.
  /// </summary>
  [Fact]
  public void Resolve_WithAmbiguousOverlayRules_PrefersUnsupported()
  {
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra");
    var supportedRule = LlmCapabilityRuleDefinition.ExactModel(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Supported,
        source: "LiveRefresh",
        reason: "provider metadata confirmed support");
    var unsupportedRule = LlmCapabilityRuleDefinition.ExactModel(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-5.6-terra",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Unsupported,
        omitWhenDefaultOnly: true,
        source: "Observed400",
        reason: "provider rejected non-default temperature");

    var snapshot = LlmCapabilityResolver.Resolve(
        scope,
        [supportedRule, unsupportedRule]);

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Unsupported);
  }

  /// <summary>
  ///     Ensures unmatched provider capabilities remain unknown.
  /// </summary>
  [Fact]
  public void Resolve_WithUnknownSupport_RemainsConservative()
  {
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.Gemini,
        "Gemini",
        "https://generativelanguage.googleapis.com",
        "gemini-3-pro");

    var snapshot = LlmCapabilityResolver.Resolve(
        scope,
        Array.Empty<LlmCapabilityRuleDefinition>());

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Unknown);
  }
}
