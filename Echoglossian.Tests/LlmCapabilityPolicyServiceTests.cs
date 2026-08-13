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
    ///     Ensures structured ChatGPT requests omit reasoning effort when the
    ///     effective capability policy marks it unsupported.
    /// </summary>
    [Fact]
    public void ChatGptTranslator_WhenReasoningEffortIsUnsupported_OmitsStructuredOption()
    {
        var translator = new ChatGPTTranslator(
            new NoOpPluginLog(),
            new Config
            {
                ChatGptApiKey = "test-key",
                ChatGPTBaseUrl = "https://api.openai.com/v1",
                OpenAILlmModel = "gpt-5.6-terra",
            });
        var options = new ChatCompletionOptions();

#pragma warning disable OPENAI001
        options.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
#pragma warning restore OPENAI001

        var applied = translator.ApplyStructuredReasoningEffortPolicy(options);

        applied.Should().BeFalse();
#pragma warning disable OPENAI001
        options.ReasoningEffortLevel.Should().BeNull();
#pragma warning restore OPENAI001
    }

    /// <summary>
    ///     Ensures structured ChatGPT requests retain a configured reasoning
    ///     effort only when the effective capability policy supports it.
    /// </summary>
    [Fact]
    public void ChatGptTranslator_WhenReasoningEffortIsSupported_RetainsStructuredOption()
    {
        LlmCapabilityCacheManager.Clear();
        try
        {
            LlmCapabilityCacheManager.PublishRule(
                LlmCapabilityRuleDefinition.ExactModel(
                    Echoglossian.TransEngines.ChatGPT.ToString(),
                    "OpenAI",
                    "https://api.openai.com/v1",
                    "test-model",
                    LlmCapabilityParameterName.ReasoningEffort,
                    LlmCapabilitySupportState.Supported));
            var translator = new ChatGPTTranslator(
                new NoOpPluginLog(),
                new Config
                {
                    ChatGptApiKey = "test-key",
                    ChatGPTBaseUrl = "https://api.openai.com/v1",
                    OpenAILlmModel = "test-model",
                });
            var options = new ChatCompletionOptions();

#pragma warning disable OPENAI001
            options.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
#pragma warning restore OPENAI001

            var applied = translator.ApplyStructuredReasoningEffortPolicy(options);

            applied.Should().BeTrue();
#pragma warning disable OPENAI001
            options.ReasoningEffortLevel.Should().Be(ChatReasoningEffortLevel.Low);
#pragma warning restore OPENAI001
        }
        finally
        {
            LlmCapabilityCacheManager.Clear();
        }
    }

    /// <summary>
    ///     Ensures a reasoning-effort rejection is promoted only for the exact
    ///     model that returned the structured tool-calling error.
    /// </summary>
    [Fact]
    public void LearnFromProviderFailure_WithReasoningEffort400_RecordsExactModelFeedback()
    {
        this.WithTemporaryConfigurationDirectory(_ =>
        {
            var scope = new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra");

            var result = LlmCapabilityPolicyService.LearnFromProviderFailure(
                scope,
                LlmCapabilityParameterName.ReasoningEffort,
                400,
                "Function tools with reasoning_effort are not supported for gpt-5.6-terra in v1/chat/completions");

            result.ObservationRecorded.Should().BeTrue();
            result.RulePromoted.Should().BeFalse();

            using var context = new EchoglossianDbContext(_);
            context.LlmModelCapabilityObservations.Should().ContainSingle(
                observation => observation.ModelId == "gpt-5.6-terra" &&
                    observation.ParameterName == LlmCapabilityParameterName.ReasoningEffort.ToString());
        });
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
    ///     Ensures Gemini 3 discoveries inherit the committed family policy
    ///     into an exact-model overlay.
    /// </summary>
    [Fact]
    public void PromoteDiscoveredModels_WithGemini3Model_CreatesExactRuleFromFamilyDefault()
    {
        this.WithTemporaryConfigurationDirectory(_ =>
        {
            LlmCapabilityRefreshPromoter.PromoteDiscoveredModels(
                Echoglossian.TransEngines.Gemini,
                "Gemini",
                "https://generativelanguage.googleapis.com",
                ["gemini-3-pro"],
                new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc));

            LlmCapabilityCacheManager.GetRuleDefinitions()
                .Should()
                .Contain(rule => rule.MatchValue == "gemini-3-pro");
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

    /// <summary>
    ///     Locates the repository root for production wiring contract checks.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }
}
