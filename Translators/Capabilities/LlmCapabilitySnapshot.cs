// <copyright file="LlmCapabilitySnapshot.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Represents the immutable resolved capability policy for one LLM scope.
/// </summary>
public sealed class LlmCapabilitySnapshot
{
  private static readonly LlmCapabilityParameterDecision UnknownDecision = new(
      LlmCapabilitySupportState.Unknown,
      null,
      null,
      false,
      "None",
      "No matching capability rule.");

  private readonly IReadOnlyDictionary<LlmCapabilityParameterName,
      LlmCapabilityParameterDecision> decisions;

  private LlmCapabilitySnapshot(
      IReadOnlyDictionary<LlmCapabilityParameterName,
          LlmCapabilityParameterDecision> decisions)
  {
    this.decisions = decisions;
  }

  /// <summary>
  ///     Gets the resolved decision for a capability parameter.
  /// </summary>
  /// <param name="parameterName">The capability parameter.</param>
  /// <returns>The resolved parameter decision.</returns>
  public LlmCapabilityParameterDecision GetDecision(
      LlmCapabilityParameterName parameterName)
  {
    return this.decisions.TryGetValue(parameterName, out var decision)
        ? decision
        : UnknownDecision;
  }

  /// <summary>
  ///     Creates a snapshot from matching static definitions and overlay rules.
  /// </summary>
  /// <param name="scope">The active capability lookup scope.</param>
  /// <param name="staticDefinitions">The committed static capability rules.</param>
  /// <param name="overlayRules">The persisted capability overlay rules.</param>
  /// <returns>The resolved capability snapshot.</returns>
  internal static LlmCapabilitySnapshot Create(
      LlmCapabilityScope scope,
      IEnumerable<LlmCapabilityRuleDefinition> staticDefinitions,
      IReadOnlyList<LlmCapabilityRuleDefinition> overlayRules)
  {
    var resolvedDecisions = new Dictionary<LlmCapabilityParameterName,
        LlmCapabilityParameterDecision>();
    var matchingStaticRules = staticDefinitions
        .Where(rule => IsMatch(scope, rule))
        .ToArray();
    var matchingOverlayRules = overlayRules
        .Where(rule => IsMatch(scope, rule))
        .ToArray();

    foreach (var parameterName in Enum.GetValues<LlmCapabilityParameterName>())
    {
      var decision = ResolveParameter(
          parameterName,
          matchingStaticRules,
          matchingOverlayRules);
      resolvedDecisions.Add(parameterName, decision);
    }

    return new LlmCapabilitySnapshot(resolvedDecisions);
  }

  private static bool IsMatch(
      LlmCapabilityScope scope,
      LlmCapabilityRuleDefinition rule)
  {
    if (!string.Equals(rule.Engine, scope.Engine.ToString(),
            StringComparison.Ordinal) ||
        !string.Equals(rule.ProviderScope, scope.ProviderScope,
            StringComparison.Ordinal) ||
        !string.Equals(rule.EndpointScope, scope.EndpointScope,
            StringComparison.Ordinal))
    {
      return false;
    }

    return rule.MatchType == LlmCapabilityRuleMatchType.ExactModel
        ? string.Equals(rule.MatchValue, scope.ModelId, StringComparison.Ordinal)
        : scope.ModelId.StartsWith(rule.MatchValue, StringComparison.Ordinal);
  }

  private static LlmCapabilityParameterDecision ResolveParameter(
      LlmCapabilityParameterName parameterName,
      IReadOnlyList<LlmCapabilityRuleDefinition> staticRules,
      IReadOnlyList<LlmCapabilityRuleDefinition> overlayRules)
  {
    var matchingStaticRules = staticRules
        .Where(rule => rule.ParameterName == parameterName)
        .ToArray();
    var matchingOverlayRules = overlayRules
        .Where(rule => rule.ParameterName == parameterName)
        .ToArray();
    var specificity = Math.Max(
        GetHighestSpecificity(matchingStaticRules),
        GetHighestSpecificity(matchingOverlayRules));

    if (specificity == 0)
    {
      return UnknownDecision;
    }

    var matchingOverlayAtSpecificity = matchingOverlayRules
        .Where(rule => GetSpecificity(rule) == specificity)
        .ToArray();
    var rulesToResolve = matchingOverlayAtSpecificity.Length > 0
        ? matchingOverlayAtSpecificity
        : matchingStaticRules
            .Where(rule => GetSpecificity(rule) == specificity)
            .ToArray();
    var conservativeRule = rulesToResolve
        .OrderBy(rule => GetConservativeSupportRank(rule.SupportState))
        .First();

    return new LlmCapabilityParameterDecision(
        conservativeRule.SupportState,
        GetNarrowestMinimum(rulesToResolve),
        GetNarrowestMaximum(rulesToResolve),
        rulesToResolve.Any(rule => rule.OmitWhenDefaultOnly),
        conservativeRule.Source,
        conservativeRule.Reason);
  }

  private static int GetHighestSpecificity(
      IReadOnlyList<LlmCapabilityRuleDefinition> rules)
  {
    return rules.Count == 0 ? 0 : rules.Max(GetSpecificity);
  }

  private static int GetSpecificity(LlmCapabilityRuleDefinition rule)
  {
    return rule.MatchType == LlmCapabilityRuleMatchType.ExactModel ? 2 : 1;
  }

  private static int GetConservativeSupportRank(
      LlmCapabilitySupportState supportState)
  {
    return supportState switch
    {
      LlmCapabilitySupportState.Unsupported => 0,
      LlmCapabilitySupportState.Unknown => 1,
      _ => 2,
    };
  }

  private static float? GetNarrowestMinimum(
      IReadOnlyList<LlmCapabilityRuleDefinition> rules)
  {
    var minimums = rules.Where(rule => rule.MinValue.HasValue)
        .Select(rule => rule.MinValue!.Value)
        .ToArray();
    return minimums.Length > 0 ? minimums.Max() : null;
  }

  private static float? GetNarrowestMaximum(
      IReadOnlyList<LlmCapabilityRuleDefinition> rules)
  {
    var maximums = rules.Where(rule => rule.MaxValue.HasValue)
        .Select(rule => rule.MaxValue!.Value)
        .ToArray();
    return maximums.Length > 0 ? maximums.Min() : null;
  }
}
