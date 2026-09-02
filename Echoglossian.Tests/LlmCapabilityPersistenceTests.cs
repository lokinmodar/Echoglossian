// <copyright file="LlmCapabilityPersistenceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Translators.Capabilities;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers persistence and in-memory hydration of LLM capability overlays.
/// </summary>
public class LlmCapabilityPersistenceTests
{
    /// <summary>
    ///     Ensures an exact-model capability rule survives persistence and is
    ///     available through the DB-free runtime cache.
    /// </summary>
    [Fact]
    public void UpsertRule_ThenReload_PreservesExactModelLookup()
    {
        var configDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configDir);

        try
        {
            LlmCapabilityPersistenceHelper.UpsertRules(
                configDir,
                [
                    LlmModelCapabilityRule.CreateExactModel(
                        "ChatGPT",
                        "OpenAI",
                        "https://api.openai.com/v1",
                        "gpt-5.6-terra",
                        LlmCapabilityParameterName.Temperature,
                        LlmCapabilitySupportState.Unsupported,
                        omitWhenDefaultOnly: true,
                        source: "Observed400",
                        reason: "provider rejected non-default temperature"),
                ]);
            using (var context = new EchoglossianDbContext(configDir))
            {
                context.Database.Migrate();
                context.LlmModelCapabilityObservations.Add(new LlmModelCapabilityObservation
                {
                    Engine = "ChatGPT",
                    ProviderScope = "OpenAI",
                    EndpointScope = "https://api.openai.com/v1",
                    ModelId = "gpt-5.6-terra",
                    ParameterName = "Temperature",
                    StatusCode = 400,
                    ProviderErrorCode = "unsupported_parameter",
                    MessageExcerpt = "temperature is unsupported",
                });
                context.SaveChanges();
            }

            LlmCapabilityCacheManager.Initialize(configDir);

            LlmCapabilityCacheManager.GetRuleDefinitions()
                .Should()
                .ContainSingle(rule => rule.MatchValue == "gpt-5.6-terra");
            LlmCapabilityCacheManager.GetRuleDefinitions()
                .Should()
                .NotBeOfType<List<LlmCapabilityRuleDefinition>>();

            using (var context = new EchoglossianDbContext(configDir))
            {
                context.LlmModelCapabilityObservations.Should().ContainSingle();
            }
        }
        finally
        {
            LlmCapabilityCacheManager.Clear();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(configDir, recursive: true);
        }
    }
}
