// <copyright file="LlmCapabilityPolicyService.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Provides the shared runtime policy path for LLM capability decisions.
/// </summary>
public static class LlmCapabilityPolicyService
{
    /// <summary>
    ///     Creates a conservatively normalized capability lookup scope.
    /// </summary>
    /// <param name="engine">The active translation engine.</param>
    /// <param name="providerScope">The provider identity within the engine.</param>
    /// <param name="endpointScope">The endpoint identity within the provider.</param>
    /// <param name="modelId">The selected model identifier.</param>
    /// <returns>A normalized capability lookup scope.</returns>
    public static LlmCapabilityScope CreateScope(
        Echoglossian.TransEngines engine,
        string? providerScope,
        string? endpointScope,
        string? modelId)
    {
        return new LlmCapabilityScope(
            engine,
            providerScope?.Trim() ?? string.Empty,
            endpointScope?.Trim().TrimEnd('/') ?? string.Empty,
            modelId?.Trim() ?? string.Empty);
    }

    /// <summary>
    ///     Gets the effective capability snapshot without querying SQLite.
    /// </summary>
    /// <param name="scope">The active capability lookup scope.</param>
    /// <returns>The resolved capability snapshot.</returns>
    public static LlmCapabilitySnapshot GetSnapshot(LlmCapabilityScope scope)
    {
        return LlmCapabilityResolver.Resolve(
            NormalizeScope(scope),
            LlmCapabilityCacheManager.GetRuleDefinitions());
    }

    /// <summary>
    ///     Resolves a temperature value for provider payload use.
    /// </summary>
    /// <param name="scope">The active capability lookup scope.</param>
    /// <param name="configuredValue">The configured temperature value.</param>
    /// <param name="sanitizedValue">When this method returns, contains the safe value when it may be sent. This parameter is treated as uninitialized.</param>
    /// <param name="decision">When this method returns, contains the resolved temperature decision. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true" /> if the value may be sent; otherwise, <see langword="false" />.</returns>
    public static bool TryResolveTemperature(
        LlmCapabilityScope scope,
        float configuredValue,
        out float sanitizedValue,
        out LlmCapabilityParameterDecision decision)
    {
        decision = GetSnapshot(scope).GetDecision(LlmCapabilityParameterName.Temperature);
        sanitizedValue = configuredValue;

        if (decision.SupportState != LlmCapabilitySupportState.Supported ||
            decision.OmitWhenDefaultOnly)
        {
            sanitizedValue = default;
            return false;
        }

        if (decision.MinValue.HasValue && configuredValue < decision.MinValue.Value)
        {
            sanitizedValue = decision.MinValue.Value;
        }

        if (decision.MaxValue.HasValue && configuredValue > decision.MaxValue.Value)
        {
            sanitizedValue = decision.MaxValue.Value;
        }

        return true;
    }

    /// <summary>
    ///     Learns from a bounded, classified provider failure.
    /// </summary>
    /// <param name="scope">The capability scope associated with the request.</param>
    /// <param name="parameterName">The parameter sent in the failed request.</param>
    /// <param name="statusCode">The provider response status code.</param>
    /// <param name="responseText">The provider response body.</param>
    /// <returns>The persisted observation and promotion outcome.</returns>
    public static LlmCapabilityLearningResult LearnFromProviderFailure(
        LlmCapabilityScope scope,
        LlmCapabilityParameterName parameterName,
        int? statusCode,
        string? responseText)
    {
        var normalizedScope = NormalizeScope(scope);
        if (string.IsNullOrWhiteSpace(normalizedScope.ModelId))
        {
            return new LlmCapabilityLearningResult(
                false,
                false,
                "unclassified");
        }

        var classification = LlmCapabilityErrorClassifier.TryClassify(
            normalizedScope,
            parameterName,
            statusCode,
            responseText);
        if (!classification.ObservationRecorded ||
            string.IsNullOrWhiteSpace(Echoglossian.ConfigDirectory))
        {
            return new LlmCapabilityLearningResult(
                false,
                false,
                classification.FailureKind);
        }

        var observation = new LlmModelCapabilityObservation
        {
            Engine = normalizedScope.Engine.ToString(),
            ProviderScope = normalizedScope.ProviderScope,
            EndpointScope = normalizedScope.EndpointScope,
            ModelId = normalizedScope.ModelId,
            ParameterName = parameterName.ToString(),
            StatusCode = statusCode ?? 0,
            ProviderErrorCode = classification.ProviderErrorCode,
            MessageExcerpt = classification.MessageExcerpt,
        };

        try
        {
            LlmCapabilityPersistenceHelper.RecordObservation(
                Echoglossian.ConfigDirectory,
                observation);
            LlmCapabilityCacheManager.PublishObservation(observation);

            if (!classification.RulePromoted)
            {
                return new LlmCapabilityLearningResult(
                    true,
                    false,
                    classification.FailureKind);
            }

            var rule = LlmModelCapabilityRule.CreateExactModel(
                normalizedScope.Engine.ToString(),
                normalizedScope.ProviderScope,
                normalizedScope.EndpointScope,
                normalizedScope.ModelId,
                parameterName,
                LlmCapabilitySupportState.Unsupported,
                source: "Observed400",
                reason: "Provider returned a classified unsupported parameter response.");
            LlmCapabilityPersistenceHelper.UpsertRules(
                Echoglossian.ConfigDirectory,
                [rule]);
            LlmCapabilityCacheManager.PublishRule(rule.ToDefinition());
            return new LlmCapabilityLearningResult(
                true,
                true,
                classification.FailureKind);
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Warning(
                $"[LlmCapabilityPolicyService] Failed to persist {classification.FailureKind} capability feedback: {ex.GetType().Name}");
            return new LlmCapabilityLearningResult(
                false,
                false,
                "persistence-failed");
        }
    }

    private static LlmCapabilityScope NormalizeScope(LlmCapabilityScope scope)
    {
        return CreateScope(
            scope.Engine,
            scope.ProviderScope,
            scope.EndpointScope,
            scope.ModelId);
    }
}

/// <summary>
///     Describes the persisted outcome of provider failure learning.
/// </summary>
/// <param name="ObservationRecorded">Whether a sanitized observation was persisted.</param>
/// <param name="RulePromoted">Whether an exact-model rule was persisted.</param>
/// <param name="FailureKind">The bounded failure classification.</param>
public readonly record struct LlmCapabilityLearningResult(
    bool ObservationRecorded,
    bool RulePromoted,
    string FailureKind);
