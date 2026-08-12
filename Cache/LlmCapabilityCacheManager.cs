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
    private static IReadOnlyList<LlmCapabilityRuleDefinition> cachedRules = [];
    private static IReadOnlyList<LlmModelCapabilityObservation> cachedObservations = [];

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
                cachedRules = rules;
                cachedObservations = observations;
            }
        }
        catch (Exception ex)
        {
            lock (SyncLock)
            {
                cachedRules = [];
                cachedObservations = [];
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
    ///     Clears the runtime capability snapshots.
    /// </summary>
    public static void Clear()
    {
        lock (SyncLock)
        {
            cachedRules = [];
            cachedObservations = [];
        }
    }
}
