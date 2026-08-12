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
  ///     Ensures a narrower matching family-prefix rule takes precedence over
  ///     a broader family rule.
  /// </summary>
  [Fact]
  public void Resolve_WithNestedFamilyPrefixes_PrefersLongerPrefix()
  {
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-4.1-mini");
    var broaderRule = LlmCapabilityRuleDefinition.FamilyPrefix(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Unsupported,
        source: "LiveRefresh",
        reason: "broader family does not support temperature");
    var narrowerRule = LlmCapabilityRuleDefinition.FamilyPrefix(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-4.1-",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Supported,
        source: "LiveRefresh",
        reason: "narrower family supports temperature");

    var snapshot = LlmCapabilityResolver.Resolve(
        scope,
        [broaderRule, narrowerRule]);

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Supported);
  }

  /// <summary>
  ///     Ensures conflicting supported ranges with no common value resolve to
  ///     an unknown decision instead of an invalid supported range.
  /// </summary>
  [Fact]
  public void Resolve_WithDisjointSupportedRanges_ReturnsUnknownDecision()
  {
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-4.1-mini");
    var lowerRangeRule = LlmCapabilityRuleDefinition.ExactModel(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-4.1-mini",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Supported,
        minValue: 0f,
        maxValue: 0.2f,
        source: "LiveRefresh",
        reason: "first observed range");
    var upperRangeRule = LlmCapabilityRuleDefinition.ExactModel(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-4.1-mini",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Supported,
        minValue: 0.8f,
        maxValue: 1f,
        source: "Observed400",
        reason: "second observed range");

    var decision = LlmCapabilityResolver.Resolve(
            scope,
            [lowerRangeRule, upperRangeRule])
        .GetDecision(LlmCapabilityParameterName.Temperature);

    decision.SupportState.Should().Be(LlmCapabilitySupportState.Unknown);
    decision.MinValue.Should().BeNull();
    decision.MaxValue.Should().BeNull();
  }

  /// <summary>
  ///     Ensures an unsupported model-match type cannot be interpreted as a
  ///     family-prefix rule.
  /// </summary>
  [Fact]
  public void Resolve_WithUnsupportedMatchType_ReturnsUnknown()
  {
    var scope = new LlmCapabilityScope(
        Echoglossian.TransEngines.ChatGPT,
        "OpenAI",
        "https://api.openai.com/v1",
        "gpt-4.1-mini");
    var invalidRule = new LlmCapabilityRuleDefinition(
        "ChatGPT",
        "OpenAI",
        "https://api.openai.com/v1",
        (LlmCapabilityRuleMatchType)99,
        "gpt-",
        LlmCapabilityParameterName.Temperature,
        LlmCapabilitySupportState.Supported,
        null,
        null,
        false,
        "LiveRefresh",
        "invalid match type");

    var snapshot = LlmCapabilityResolver.Resolve(scope, [invalidRule]);

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Unknown);
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
        "gemini-4-pro");

    var snapshot = LlmCapabilityResolver.Resolve(
        scope,
        Array.Empty<LlmCapabilityRuleDefinition>());

    snapshot.GetDecision(LlmCapabilityParameterName.Temperature)
        .SupportState
        .Should()
        .Be(LlmCapabilitySupportState.Unknown);
  }
}
