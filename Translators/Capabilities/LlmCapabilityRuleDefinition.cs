// <copyright file="LlmCapabilityRuleDefinition.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Defines one static or persisted LLM capability rule.
/// </summary>
/// <param name="Engine">The engine identifier to match.</param>
/// <param name="ProviderScope">The provider identity to match.</param>
/// <param name="EndpointScope">The endpoint identity to match.</param>
/// <param name="MatchType">The model matching strategy.</param>
/// <param name="MatchValue">The model identifier or family prefix to match.</param>
/// <param name="ParameterName">The governed capability parameter.</param>
/// <param name="SupportState">The known support state.</param>
/// <param name="MinValue">The inclusive lower bound, when known.</param>
/// <param name="MaxValue">The inclusive upper bound, when known.</param>
/// <param name="OmitWhenDefaultOnly">
///     Whether the parameter must be omitted when only its implicit default is
///     allowed.
/// </param>
/// <param name="Source">The source that established the rule.</param>
/// <param name="Reason">The explanation for the rule.</param>
public readonly record struct LlmCapabilityRuleDefinition(
    string Engine,
    string ProviderScope,
    string EndpointScope,
    LlmCapabilityRuleMatchType MatchType,
    string MatchValue,
    LlmCapabilityParameterName ParameterName,
    LlmCapabilitySupportState SupportState,
    float? MinValue,
    float? MaxValue,
    bool OmitWhenDefaultOnly,
    string Source,
    string Reason)
{
  /// <summary>
  ///     Creates a rule that applies to one complete model identifier.
  /// </summary>
  /// <param name="engine">The engine identifier to match.</param>
  /// <param name="providerScope">The provider identity to match.</param>
  /// <param name="endpointScope">The endpoint identity to match.</param>
  /// <param name="modelId">The complete model identifier to match.</param>
  /// <param name="parameterName">The governed capability parameter.</param>
  /// <param name="supportState">The known support state.</param>
  /// <param name="minValue">The inclusive lower bound, when known.</param>
  /// <param name="maxValue">The inclusive upper bound, when known.</param>
  /// <param name="omitWhenDefaultOnly">
  ///     <see langword="true" /> to omit the parameter when only its implicit
  ///     default is allowed; otherwise, <see langword="false" />.
  /// </param>
  /// <param name="source">The source that established the rule.</param>
  /// <param name="reason">The explanation for the rule.</param>
  /// <returns>An exact-model capability rule.</returns>
  public static LlmCapabilityRuleDefinition ExactModel(
      string engine,
      string providerScope,
      string endpointScope,
      string modelId,
      LlmCapabilityParameterName parameterName,
      LlmCapabilitySupportState supportState,
      float? minValue = null,
      float? maxValue = null,
      bool omitWhenDefaultOnly = false,
      string source = "",
      string reason = "")
  {
    return new LlmCapabilityRuleDefinition(
        engine,
        providerScope,
        endpointScope,
        LlmCapabilityRuleMatchType.ExactModel,
        modelId,
        parameterName,
        supportState,
        minValue,
        maxValue,
        omitWhenDefaultOnly,
        source,
        reason);
  }

  /// <summary>
  ///     Creates a rule that applies to models in one identifier family.
  /// </summary>
  /// <param name="engine">The engine identifier to match.</param>
  /// <param name="providerScope">The provider identity to match.</param>
  /// <param name="endpointScope">The endpoint identity to match.</param>
  /// <param name="modelPrefix">The model identifier prefix to match.</param>
  /// <param name="parameterName">The governed capability parameter.</param>
  /// <param name="supportState">The known support state.</param>
  /// <param name="minValue">The inclusive lower bound, when known.</param>
  /// <param name="maxValue">The inclusive upper bound, when known.</param>
  /// <param name="omitWhenDefaultOnly">
  ///     <see langword="true" /> to omit the parameter when only its implicit
  ///     default is allowed; otherwise, <see langword="false" />.
  /// </param>
  /// <param name="source">The source that established the rule.</param>
  /// <param name="reason">The explanation for the rule.</param>
  /// <returns>A family-prefix capability rule.</returns>
  public static LlmCapabilityRuleDefinition FamilyPrefix(
      string engine,
      string providerScope,
      string endpointScope,
      string modelPrefix,
      LlmCapabilityParameterName parameterName,
      LlmCapabilitySupportState supportState,
      float? minValue = null,
      float? maxValue = null,
      bool omitWhenDefaultOnly = false,
      string source = "StaticDefault",
      string reason = "")
  {
    return new LlmCapabilityRuleDefinition(
        engine,
        providerScope,
        endpointScope,
        LlmCapabilityRuleMatchType.FamilyPrefix,
        modelPrefix,
        parameterName,
        supportState,
        minValue,
        maxValue,
        omitWhenDefaultOnly,
        source,
        reason);
  }
}
