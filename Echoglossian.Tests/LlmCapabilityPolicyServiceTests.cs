// <copyright file="LlmCapabilityPolicyServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.Translators.Capabilities;

using FluentAssertions;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers shared runtime policy resolution and persisted capability
///     learning.
/// </summary>
public sealed class LlmCapabilityPolicyServiceTests
{
    /// <summary>
    ///     Ensures scope creation normalizes provider, endpoint, and model
    ///     identity before resolution.
    /// </summary>
    [Fact]
    public void CreateScope_NormalizesLookupIdentity()
    {
        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.ChatGPT,
            " OpenAI ",
            " https://api.openai.com/v1/ ",
            " gpt-5.6-terra ");

        scope.ProviderScope.Should().Be("OpenAI");
        scope.EndpointScope.Should().Be("https://api.openai.com/v1");
        scope.ModelId.Should().Be("gpt-5.6-terra");
    }

    /// <summary>
    ///     Ensures unsupported temperature is omitted from the outbound
    ///     provider payload.
    /// </summary>
    [Fact]
    public void TryResolveTemperature_WithUnsupportedDecision_OmitsPayloadValue()
    {
        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.ChatGPT,
            "OpenAI",
            "https://api.openai.com/v1",
            "gpt-5.6-terra");

        var resolved = LlmCapabilityPolicyService.TryResolveTemperature(
            scope,
            0.7f,
            out var sanitizedValue,
            out var decision);

        resolved.Should().BeFalse();
        sanitizedValue.Should().Be(default);
        decision.SupportState.Should().Be(LlmCapabilitySupportState.Unsupported);
    }

    /// <summary>
    ///     Ensures a clearly classified provider rejection is persisted and
    ///     promoted for the exact model only.
    /// </summary>
    [Fact]
    public void LearnFromProviderFailure_WithClassifiedTemperature400_PromotesExactModelOnly()
    {
        this.WithTemporaryConfigurationDirectory(configDir =>
        {
            var scope = new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra");

            var result = LlmCapabilityPolicyService.LearnFromProviderFailure(
                scope,
                LlmCapabilityParameterName.Temperature,
                400,
                "{ \"error\": { \"message\": \"Unsupported value: 'temperature' does not support 0.7 with this model.\" } }");

            result.ObservationRecorded.Should().BeTrue();
            result.RulePromoted.Should().BeTrue();
            result.FailureKind.Should().Be("unsupported-parameter");
            LlmCapabilityCacheManager.GetRuleDefinitions().Should().ContainSingle(
                rule => rule.MatchType == LlmCapabilityRuleMatchType.ExactModel &&
                    rule.MatchValue == "gpt-5.6-terra" &&
                    rule.ParameterName == LlmCapabilityParameterName.Temperature);

            using var context = new EchoglossianDbContext(configDir);
            context.LlmModelCapabilityRules.Should().ContainSingle(
                rule => rule.MatchType == LlmCapabilityRuleMatchType.ExactModel.ToString() &&
                    rule.MatchValue == "gpt-5.6-terra");
            context.LlmModelCapabilityRules.Should().NotContain(
                rule => rule.MatchType == LlmCapabilityRuleMatchType.FamilyPrefix.ToString());
        });
    }

    /// <summary>
    ///     Ensures discovered models inherit known family policy into
    ///     exact-model persisted overlays.
    /// </summary>
    [Fact]
    public void PromoteDiscoveredModels_WithStaticFamilyRule_PersistsExactModelOverlay()
    {
        this.WithTemporaryConfigurationDirectory(configDir =>
        {
            LlmCapabilityRefreshPromoter.PromoteDiscoveredModels(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                ["gpt-5.6-terra"],
                DateTime.UtcNow);

            using var context = new EchoglossianDbContext(configDir);
            context.LlmModelCapabilityRules.Should().ContainSingle(rule =>
                rule.MatchType == LlmCapabilityRuleMatchType.ExactModel.ToString() &&
                rule.MatchValue == "gpt-5.6-terra" &&
                rule.ParameterName == LlmCapabilityParameterName.Temperature.ToString() &&
                rule.SupportState == LlmCapabilitySupportState.Unsupported.ToString());
        });
    }

    /// <summary>
    ///     Runs an action with an isolated persisted capability database.
    /// </summary>
    /// <param name="action">The action that exercises the policy service.</param>
    private void WithTemporaryConfigurationDirectory(Action<string> action)
    {
        var configDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var originalConfigDirectory = Echoglossian.ConfigDirectory;
        Directory.CreateDirectory(configDir);
        Echoglossian.ConfigDirectory = configDir;
        LlmCapabilityCacheManager.Clear();

        try
        {
            action(configDir);
        }
        finally
        {
            LlmCapabilityCacheManager.Clear();
            Echoglossian.ConfigDirectory = originalConfigDirectory;
            SqliteConnection.ClearAllPools();
            Directory.Delete(configDir, recursive: true);
        }
    }
}
