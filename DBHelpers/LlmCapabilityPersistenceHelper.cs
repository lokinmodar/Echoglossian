// <copyright file="LlmCapabilityPersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.DBHelpers;

/// <summary>
///     Persists LLM capability overlays and provider-feedback observations.
/// </summary>
public static class LlmCapabilityPersistenceHelper
{
    /// <summary>
    ///     Inserts or updates capability rules by their scoped lookup identity.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    /// <param name="rules">The rules to persist.</param>
    public static void UpsertRules(
        string configDir,
        IReadOnlyList<LlmModelCapabilityRule> rules)
    {
        using var context = new EchoglossianDbContext(configDir);
        context.Database.Migrate();

        foreach (var rule in rules)
        {
            var existing = context.LlmModelCapabilityRules.FirstOrDefault(row =>
                row.Engine == rule.Engine &&
                row.ProviderScope == rule.ProviderScope &&
                row.EndpointScope == rule.EndpointScope &&
                row.MatchType == rule.MatchType &&
                row.MatchValue == rule.MatchValue &&
                row.ParameterName == rule.ParameterName);
            if (existing is null)
            {
                context.LlmModelCapabilityRules.Add(rule);
                continue;
            }

            existing.SupportState = rule.SupportState;
            existing.MinValue = rule.MinValue;
            existing.MaxValue = rule.MaxValue;
            existing.AllowedEnumValuesJson = rule.AllowedEnumValuesJson;
            existing.OmitWhenDefaultOnly = rule.OmitWhenDefaultOnly;
            existing.Source = rule.Source;
            existing.Reason = rule.Reason;
            existing.Confidence = rule.Confidence;
            existing.ObservedAtUtc = rule.ObservedAtUtc;
            existing.ExpiresAtUtc = rule.ExpiresAtUtc;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        context.SaveChanges();
    }

}
