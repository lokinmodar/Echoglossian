// <copyright file="LlmCapabilityPolicyServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.Translators;
using Echoglossian.Translators.Capabilities;

using Echoglossian.Tests.TestDoubles;

using FluentAssertions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using OpenAI.Chat;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers shared runtime policy resolution and persisted capability
///     learning.
/// </summary>
public sealed class LlmCapabilityPolicyServiceTests
{
    /// <summary>
    ///     Ensures unknown capability scopes omit temperature from both plain
    ///     and structured OpenAI-compatible request payloads.
    /// </summary>
    [Fact]
    public void TryAddTemperature_WhenCapabilityIsUnknown_OmitsPlainAndStructuredPayloadFields()
    {
        var scope = LlmCapabilityPolicyService.CreateScope(
            Echoglossian.TransEngines.OpenRouter,
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "unknown-model");
        var plainPayload = new Dictionary<string, object>
        {
            ["model"] = "unknown-model",
            ["messages"] = Array.Empty<object>(),
        };
        var structuredPayload = new Dictionary<string, object>
        {
            ["model"] = "unknown-model",
            ["messages"] = Array.Empty<object>(),
            ["tools"] = Array.Empty<object>(),
        };

        LlmCapabilityRequestPayloadSanitizer.TryAddTemperature(
            plainPayload,
            scope,
            0.7f).Should().BeFalse();
        LlmCapabilityRequestPayloadSanitizer.TryAddTemperature(
            structuredPayload,
            scope,
            0.7f).Should().BeFalse();

        JsonConvert.SerializeObject(plainPayload).Should().NotContain("temperature");
        JsonConvert.SerializeObject(structuredPayload).Should().NotContain("temperature");
    }

    /// <summary>
    ///     Ensures the official OpenAI default-only model leaves the SDK
    ///     completion option unset.
    /// </summary>
    [Fact]
    public void ChatGptTranslator_WhenTemperatureIsDefaultOnly_LeavesCompletionOptionUnset()
    {
        var translator = new ChatGPTTranslator(
            new NoOpPluginLog(),
            new Config
            {
                ChatGptApiKey = "test-key",
                ChatGPTBaseUrl = "https://api.openai.com/v1",
                OpenAILlmModel = "gpt-5.6-terra",
                ChatGptTemperature = 0.7f,
            });
        var options = new ChatCompletionOptions();

        translator.ApplyTemperaturePolicy(options);

        options.Temperature.Should().BeNull();
    }

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
    ///     Ensures learning does not persist an observation or rule when the
    ///     provider did not identify the active model.
    /// </summary>
    [Fact]
    public void LearnFromProviderFailure_WithBlankModelId_DoesNotPersistCapabilityFeedback()
    {
        this.WithTemporaryConfigurationDirectory(configDir =>
        {
            var result = LlmCapabilityPolicyService.LearnFromProviderFailure(
                new LlmCapabilityScope(
                    Echoglossian.TransEngines.ChatGPT,
                    "OpenAI",
                    "https://api.openai.com/v1",
                    " "),
                LlmCapabilityParameterName.Temperature,
                400,
                "{ \"error\": { \"message\": \"Unsupported parameter: temperature.\" } }");

            result.ObservationRecorded.Should().BeFalse();
            result.RulePromoted.Should().BeFalse();
            LlmCapabilityCacheManager.GetRuleDefinitions().Should().BeEmpty();

            using var context = new EchoglossianDbContext(configDir);
            context.Database.Migrate();
            context.LlmModelCapabilityObservations.Should().BeEmpty();
            context.LlmModelCapabilityRules.Should().BeEmpty();
        });
    }

    /// <summary>
    ///     Ensures recurring ambiguous provider failures do not grow the
    ///     persisted observation table.
    /// </summary>
    [Fact]
    public void LearnFromProviderFailure_WithRepeatedAmbiguous400_DeduplicatesObservation()
    {
        this.WithTemporaryConfigurationDirectory(configDir =>
        {
            var scope = new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra");

            LlmCapabilityPolicyService.LearnFromProviderFailure(
                scope,
                LlmCapabilityParameterName.Temperature,
                400,
                "{ \"error\": { \"message\": \"Request could not be processed.\" } }");
            LlmCapabilityPolicyService.LearnFromProviderFailure(
                scope,
                LlmCapabilityParameterName.Temperature,
                400,
                "{ \"error\": { \"message\": \"Request could not be processed.\" } }");

            using var context = new EchoglossianDbContext(configDir);
            context.LlmModelCapabilityObservations.Should().ContainSingle();
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
