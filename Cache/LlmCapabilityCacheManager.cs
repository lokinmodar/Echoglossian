// <copyright file="LlmCapabilityCacheManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Translators.Capabilities;

namespace Echoglossian.Cache;

/// <summary>
///     Maintains DB-free runtime snapshots of persisted LLM capability data.
/// </summary>
public static class LlmCapabilityCacheManager
{
    private static readonly object SyncLock = new();
    private static IReadOnlyList<LlmCapabilityRuleDefinition> cachedRules =
        Array.AsReadOnly(Array.Empty<LlmCapabilityRuleDefinition>());
    private static IReadOnlyList<LlmModelCapabilityObservation> cachedObservations =
        Array.AsReadOnly(Array.Empty<LlmModelCapabilityObservation>());

    /// <summary>
    ///     Hydrates the runtime cache from persisted capability tables.
    /// </summary>
    /// <param name="configDir">The plugin configuration directory.</param>
    public static void Initialize(string configDir)
    {
        try
        {
            using var context = new EchoglossianDbContext(configDir);
            var rules = context.LlmModelCapabilityRules
                .AsNoTracking()
                .ToList()
                .Select(static row => row.ToDefinition())
                .ToList();
            var observations = context.LlmModelCapabilityObservations
                .AsNoTracking()
                .OrderByDescending(static row => row.ObservedAtUtc)
                .Take(128)
                .ToList();

            lock (SyncLock)
            {
                cachedRules = Array.AsReadOnly(rules.ToArray());
                cachedObservations = Array.AsReadOnly(observations.ToArray());
            }
        }
        catch (Exception ex)
        {
            lock (SyncLock)
            {
                cachedRules = Array.AsReadOnly(
                    Array.Empty<LlmCapabilityRuleDefinition>());
                cachedObservations = Array.AsReadOnly(
                    Array.Empty<LlmModelCapabilityObservation>());
            }

            PluginRuntimeLog.Error(
                $"[LlmCapabilityCacheManager] Failed to initialize cache: {ex}");
        }
    }

    /// <summary>
    ///     Gets the current persisted capability rule definitions without
    ///     querying SQLite.
    /// </summary>
    /// <returns>The cached rule definitions.</returns>
    public static IReadOnlyList<LlmCapabilityRuleDefinition> GetRuleDefinitions()
    {
        lock (SyncLock)
        {
            return cachedRules;
        }
    }

    /// <summary>
    ///     Publishes one persisted rule to the DB-free runtime cache.
    /// </summary>
    /// <param name="rule">The persisted rule definition to publish.</param>
    internal static void PublishRule(LlmCapabilityRuleDefinition rule)
    {
        lock (SyncLock)
        {
            var updatedRules = cachedRules
                .Where(existing => !HasSameLookupIdentity(existing, rule))
                .Append(rule)
                .ToArray();
            cachedRules = Array.AsReadOnly(updatedRules);
        }
    }

    /// <summary>
    ///     Publishes one persisted provider-feedback observation to the bounded
    ///     runtime audit snapshot.
    /// </summary>
    /// <param name="observation">The observation to publish.</param>
    internal static void PublishObservation(
        LlmModelCapabilityObservation observation)
    {
        lock (SyncLock)
        {
            var updatedObservations = cachedObservations
                .Where(existing => !HasSameObservationIdentity(existing, observation))
                .Prepend(observation)
                .OrderByDescending(static row => row.ObservedAtUtc)
                .Take(128)
                .ToArray();
            cachedObservations = Array.AsReadOnly(updatedObservations);
        }
    }

    /// <summary>
    ///     Clears the runtime capability snapshots.
    /// </summary>
    public static void Clear()
    {
        lock (SyncLock)
        {
            cachedRules = Array.AsReadOnly(
                Array.Empty<LlmCapabilityRuleDefinition>());
            cachedObservations = Array.AsReadOnly(
                Array.Empty<LlmModelCapabilityObservation>());
        }
    }

    /// <summary>
    ///     Determines whether two rules address the same persisted lookup
    ///     identity.
    /// </summary>
    /// <param name="left">The first rule to compare.</param>
    /// <param name="right">The second rule to compare.</param>
    /// <returns><see langword="true" /> if both rules share one lookup identity; otherwise, <see langword="false" />.</returns>
    private static bool HasSameLookupIdentity(
        LlmCapabilityRuleDefinition left,
        LlmCapabilityRuleDefinition right)
    {
        return string.Equals(left.Engine, right.Engine, StringComparison.Ordinal) &&
            string.Equals(left.ProviderScope, right.ProviderScope, StringComparison.Ordinal) &&
            string.Equals(left.EndpointScope, right.EndpointScope, StringComparison.Ordinal) &&
            left.MatchType == right.MatchType &&
            string.Equals(left.MatchValue, right.MatchValue, StringComparison.Ordinal) &&
            left.ParameterName == right.ParameterName;
    }

    /// <summary>
    ///     Determines whether two observations represent the same bounded
    ///     provider-feedback event identity.
    /// </summary>
    /// <param name="left">The first observation to compare.</param>
    /// <param name="right">The second observation to compare.</param>
    /// <returns><see langword="true" /> if both observations share one identity; otherwise, <see langword="false" />.</returns>
    private static bool HasSameObservationIdentity(
        LlmModelCapabilityObservation left,
        LlmModelCapabilityObservation right)
    {
        return string.Equals(left.Engine, right.Engine, StringComparison.Ordinal) &&
            string.Equals(left.ProviderScope, right.ProviderScope, StringComparison.Ordinal) &&
            string.Equals(left.EndpointScope, right.EndpointScope, StringComparison.Ordinal) &&
            string.Equals(left.ModelId, right.ModelId, StringComparison.Ordinal) &&
            string.Equals(left.ParameterName, right.ParameterName, StringComparison.Ordinal) &&
            left.StatusCode == right.StatusCode &&
            string.Equals(left.MessageExcerpt, right.MessageExcerpt, StringComparison.Ordinal);
    }
}
