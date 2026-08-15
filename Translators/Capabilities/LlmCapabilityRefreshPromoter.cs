// <copyright file="LlmCapabilityRefreshPromoter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Promotes discovered model identifiers into exact-model capability
///     overlays derived from known family policy.
/// </summary>
public static class LlmCapabilityRefreshPromoter
{
    /// <summary>
    ///     Persists exact-model overlays for discovered models that match known
    ///     static family policy.
    /// </summary>
    /// <param name="engine">The active translation engine.</param>
    /// <param name="providerScope">The provider identity within the engine.</param>
    /// <param name="endpointScope">The endpoint identity within the provider.</param>
    /// <param name="modelIds">The discovered model identifiers.</param>
    /// <param name="observedAtUtc">The UTC refresh observation time.</param>
    public static void PromoteDiscoveredModels(
        Echoglossian.TransEngines engine,
        string providerScope,
        string endpointScope,
        IReadOnlyList<string> modelIds,
        DateTime observedAtUtc)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Echoglossian.ConfigDirectory))
            {
                return;
            }

            var promotedRules = new List<LlmModelCapabilityRule>();
            foreach (var modelId in modelIds.Where(static model => !string.IsNullOrWhiteSpace(model)).Distinct(StringComparer.Ordinal))
            {
                var scope = LlmCapabilityPolicyService.CreateScope(
                    engine,
                    providerScope,
                    endpointScope,
                    modelId);
                var staticSnapshot = LlmCapabilityResolver.Resolve(
                    scope,
                    Array.Empty<LlmCapabilityRuleDefinition>());

                foreach (var parameterName in Enum.GetValues<LlmCapabilityParameterName>())
                {
                    var decision = staticSnapshot.GetDecision(parameterName);
                    if (decision.SupportState == LlmCapabilitySupportState.Unknown)
                    {
                        continue;
                    }

                    var rule = LlmModelCapabilityRule.CreateExactModel(
                        scope.Engine.ToString(),
                        scope.ProviderScope,
                        scope.EndpointScope,
                        scope.ModelId,
                        parameterName,
                        decision.SupportState,
                        decision.MinValue,
                        decision.MaxValue,
                        decision.OmitWhenDefaultOnly,
                        "LiveRefresh",
                        decision.Reason);
                    rule.ObservedAtUtc = observedAtUtc;
                    promotedRules.Add(rule);
                }
            }

            if (promotedRules.Count == 0)
            {
                return;
            }

            LlmCapabilityPersistenceHelper.UpsertRules(
                Echoglossian.ConfigDirectory,
                promotedRules);
            foreach (var rule in promotedRules)
            {
                LlmCapabilityCacheManager.PublishRule(rule.ToDefinition());
            }
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Warning(
                $"[LlmCapabilityRefreshPromoter] Failed to promote refreshed capability overlays: {ex.GetType().Name}");
        }
    }
}
