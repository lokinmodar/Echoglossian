// <copyright file="LlmCapabilityPolicyServiceTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.DBHelpers;
using Echoglossian.Persistence;
using Echoglossian.Translators;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Helpers;

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
    ///     Ensures structured ChatGPT requests explicitly disable reasoning
    ///     effort when the effective capability policy marks it unsupported.
    /// </summary>
    [Fact]
    public void ChatGptTranslator_WhenReasoningEffortIsUnsupported_SendsExplicitStructuredDisable()
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

        applied.Should().BeTrue();
#pragma warning disable OPENAI001
        options.ReasoningEffortLevel.Should().Be(ChatReasoningEffortLevel.None);
#pragma warning restore OPENAI001
    }

    /// <summary>
    ///     Ensures OpenAI-compatible structured dialogue requests describe the
    ///     actual request shape before dispatch and the validated result after
    ///     completion.
    /// </summary>
    [Fact]
    public async Task OpenRouterTranslator_StructuredDialogue_LogsRequestShapeAndSuccess()
    {
        var pluginLog = new CapturingPluginLog();
        var translator = new OpenRouterTranslator(
            pluginLog,
            new Config
            {
                OpenRouterApiKey = "test-key",
                OpenRouterBaseUrl = "https://openrouter.example/v1",
                OpenRouterModel = "test-model",
            });
        var responseHandler = new StructuredDialogueResponseHandler(
            isStructuredStartLogged: () => pluginLog.DebugMessages.Any(
                message => message.Contains("structured-start", StringComparison.Ordinal)));
        this.ReplaceHttpClient(
            translator,
            new HttpClient(responseHandler)
            {
                BaseAddress = new Uri("https://openrouter.example/v1/"),
            });

        var translated = await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext(
                "Talk",
                "quest-1",
                "Krile",
                [],
                SpeakerGenderHint: "female"));

        translated.Should().Be("Fique perto.");
        responseHandler.RequestMethod.Should().Be(HttpMethod.Post);
        responseHandler.RequestUri.Should().Be("https://openrouter.example/v1/chat/completions");
        responseHandler.RequestBody.Should().Contain("\"model\":\"test-model\"");
        responseHandler.RequestBody.Should().Contain("\"tool_choice\"");
        responseHandler.StructuredStartWasLoggedAtDispatch.Should().BeTrue();
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-start", StringComparison.Ordinal) &&
                message.Contains("route=chat-completions", StringComparison.Ordinal) &&
                message.Contains("endpointScope=https://openrouter.example/v1", StringComparison.Ordinal) &&
                message.Contains("glossaryApplied=false", StringComparison.Ordinal) &&
                message.Contains("capabilityDecisions=temperature=omitted(unknown)", StringComparison.Ordinal) &&
                message.Contains($"requestJsonLength={responseHandler.RequestBody.Length}", StringComparison.Ordinal));
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-success", StringComparison.Ordinal) &&
                message.Contains("route=chat-completions", StringComparison.Ordinal) &&
                message.Contains("translatedLength=12", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures a rejected structured OpenAI-compatible response retains
    ///     enough request-shape detail to diagnose the plain-text fallback.
    /// </summary>
    [Fact]
    public async Task OpenRouterTranslator_WhenStructuredResponseIsRejected_LogsFallbackRequestShape()
    {
        var pluginLog = new CapturingPluginLog();
        var translator = new OpenRouterTranslator(
            pluginLog,
            new Config
            {
                OpenRouterApiKey = "test-key",
                OpenRouterBaseUrl = "https://openrouter.example/v1",
                OpenRouterModel = "test-model",
            });
        this.ReplaceHttpClient(
            translator,
            new HttpClient(new StructuredDialogueResponseHandler(
                """
                {"choices":[{"message":{"content":"{}"}}]}
                """))
            {
                BaseAddress = new Uri("https://openrouter.example/v1/"),
            });

        await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext("Talk", "quest-1", "Krile", []));

        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-fallback", StringComparison.Ordinal) &&
                message.Contains("stage=validation", StringComparison.Ordinal) &&
                message.Contains("endpointScope=https://openrouter.example/v1", StringComparison.Ordinal) &&
                message.Contains("route=chat-completions", StringComparison.Ordinal) &&
                message.Contains("glossaryApplied=false", StringComparison.Ordinal) &&
                message.Contains("capabilityDecisions=temperature=omitted(unknown)", StringComparison.Ordinal) &&
                message.Contains("excerpt={}", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures the default OpenAI structured path records its explicit
    ///     gpt-5.6 parameter decisions without changing provider policy.
    /// </summary>
    [Fact]
    public void ChatGptTranslator_StructuredDefaultRules_UseExplicitCapabilityDecisionTokens()
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

        translator.ApplyTemperaturePolicy(options).Should().BeFalse();
        translator.ApplyStructuredReasoningEffortPolicy(options).Should().BeTrue();
        var snapshot = LlmCapabilityPolicyService.GetSnapshot(
            LlmCapabilityPolicyService.CreateScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra"));

        StructuredDialogueCapabilityDecisionLogFormatter.Format(
            LlmCapabilityParameterName.Temperature,
            snapshot.GetDecision(LlmCapabilityParameterName.Temperature),
            StructuredDialogueCapabilityEmissionMode.OmittedDefaultOnly)
            .Should().Be("temperature=omitted(default-only)");
        StructuredDialogueCapabilityDecisionLogFormatter.Format(
            LlmCapabilityParameterName.ReasoningEffort,
            snapshot.GetDecision(LlmCapabilityParameterName.ReasoningEffort),
            StructuredDialogueCapabilityEmissionMode.ExplicitDisable)
            .Should().Be("reasoning_effort=explicit-none(unsupported)");
    }

    /// <summary>
    ///     Ensures the DeepSeek structured HTTP path emits the same start and
    ///     success shape as the other OpenAI-compatible translators.
    /// </summary>
    [Fact]
    public async Task DeepSeekTranslator_StructuredDialogue_LogsRequestShapeAndSuccess()
    {
        var pluginLog = new CapturingPluginLog();
        var responseHandler = new StructuredDialogueResponseHandler(
            isStructuredStartLogged: () => pluginLog.DebugMessages.Any(
                message => message.Contains("structured-start", StringComparison.Ordinal)));
        var translator = new DeepSeekTranslator(
            pluginLog,
            new Config
            {
                DeepSeekTranslatorApiKey = new string('k', 25),
                DeepSeekBaseUrl = "https://deepseek.example/v1",
                DeepSeekModel = "test-model",
            });
        this.ReplaceHttpClient(
            translator,
            new HttpClient(responseHandler)
            {
                BaseAddress = new Uri("https://deepseek.example/v1/"),
            });

        var translated = await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext("Talk", "quest-1", "Krile", []));

        translated.Should().Be("Fique perto.");
        responseHandler.RequestMethod.Should().Be(HttpMethod.Post);
        responseHandler.RequestUri.Should().Be("https://deepseek.example/v1/chat/completions");
        responseHandler.RequestBody.Should().Contain("\"tool_choice\"");
        responseHandler.StructuredStartWasLoggedAtDispatch.Should().BeTrue();
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-start", StringComparison.Ordinal) &&
                message.Contains("provider=DeepSeek", StringComparison.Ordinal) &&
                message.Contains("route=chat-completions", StringComparison.Ordinal) &&
                message.Contains("requestJsonLength=", StringComparison.Ordinal));
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-success", StringComparison.Ordinal) &&
                message.Contains("provider=DeepSeek", StringComparison.Ordinal) &&
                message.Contains("translatedLength=12", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures Gemini records the actual generative-language route around
    ///     structured dialogue dispatch and validation.
    /// </summary>
    [Fact]
    public async Task GeminiTranslator_StructuredDialogue_LogsRequestShapeAndSuccess()
    {
        var pluginLog = new CapturingPluginLog();
        var responseHandler = new StructuredDialogueResponseHandler(
            """
            {"candidates":[{"content":{"parts":[{"text":"{\"text_translated\":\"Fique perto.\"}"}]}}]}
            """,
            () => pluginLog.DebugMessages.Any(
                message => message.Contains("structured-start", StringComparison.Ordinal)));
        var translator = new GeminiTranslator(
            pluginLog,
            new Config
            {
                GeminiTranslatorApiKey = new string('k', 25),
                GeminiModel = "test-model",
            });
        this.ReplaceHttpClient(translator, new HttpClient(responseHandler));

        var translated = await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext("Talk", "quest-1", "Krile", []));

        translated.Should().Be("Fique perto.");
        responseHandler.RequestUri.Should().Contain("v1beta/models/test-model:generateContent");
        responseHandler.StructuredStartWasLoggedAtDispatch.Should().BeTrue();
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-start", StringComparison.Ordinal) &&
                message.Contains("provider=Gemini", StringComparison.Ordinal) &&
                message.Contains("route=v1beta-models-test-model-generatecontent", StringComparison.Ordinal) &&
                message.Contains("capabilityDecisions=temperature=omitted(unknown)", StringComparison.Ordinal) &&
                message.Contains($"requestJsonLength={responseHandler.RequestBody.Length}", StringComparison.Ordinal));
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-success", StringComparison.Ordinal) &&
                message.Contains("provider=Gemini", StringComparison.Ordinal) &&
                message.Contains("route=v1beta-models-test-model-generatecontent", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures Claude records its Messages API request shape around
    ///     structured tool output validation.
    /// </summary>
    [Fact]
    public async Task ClaudeTranslator_StructuredDialogue_LogsRequestShapeAndSuccess()
    {
        var pluginLog = new CapturingPluginLog();
        var responseHandler = new StructuredDialogueResponseHandler(
            """
            {"content":[{"type":"tool_use","name":"submit_dialogue_translation","input":{"text_translated":"Fique perto."}}]}
            """,
            () => pluginLog.DebugMessages.Any(
                message => message.Contains("structured-start", StringComparison.Ordinal)));
        var translator = new ClaudeTranslator(
            pluginLog,
            new Config
            {
                ClaudeApiKey = "test-key",
                ClaudeBaseUrl = "https://claude.example",
                ClaudeModel = "test-model",
            });
        this.ReplaceHttpClient(
            translator,
            new HttpClient(responseHandler)
            {
                BaseAddress = new Uri("https://claude.example/"),
            });

        var translated = await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext("Talk", "quest-1", "Krile", []));

        translated.Should().Be("Fique perto.");
        responseHandler.RequestUri.Should().Be("https://claude.example/v1/messages");
        responseHandler.StructuredStartWasLoggedAtDispatch.Should().BeTrue();
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-start", StringComparison.Ordinal) &&
                message.Contains("provider=Anthropic", StringComparison.Ordinal) &&
                message.Contains("route=v1-messages", StringComparison.Ordinal) &&
                message.Contains("capabilityDecisions=temperature=omitted(unknown)", StringComparison.Ordinal) &&
                message.Contains($"requestJsonLength={responseHandler.RequestBody.Length}", StringComparison.Ordinal));
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-success", StringComparison.Ordinal) &&
                message.Contains("provider=Anthropic", StringComparison.Ordinal) &&
                message.Contains("route=v1-messages", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures Claude fallback diagnostics retain the scope-derived
    ///     Anthropic provider identity and active capability decisions.
    /// </summary>
    [Fact]
    public async Task ClaudeTranslator_WhenStructuredResponseIsRejected_LogsScopeProviderAndDecision()
    {
        var pluginLog = new CapturingPluginLog();
        var translator = new ClaudeTranslator(
            pluginLog,
            new Config
            {
                ClaudeApiKey = "test-key",
                ClaudeBaseUrl = "https://claude.example",
                ClaudeModel = "test-model",
            });
        this.ReplaceHttpClient(
            translator,
            new HttpClient(new StructuredDialogueResponseHandler(
                """
                {"content":[]}
                """))
            {
                BaseAddress = new Uri("https://claude.example/"),
            });

        await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext("Talk", "quest-1", "Krile", []));

        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-fallback", StringComparison.Ordinal) &&
                message.Contains("provider=Anthropic", StringComparison.Ordinal) &&
                message.Contains("route=v1-messages", StringComparison.Ordinal) &&
                message.Contains("capabilityDecisions=temperature=omitted(unknown)", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures Ollama records the generate endpoint used by its structured
    ///     JSON-schema request.
    /// </summary>
    [Fact]
    public async Task OllamaTranslator_StructuredDialogue_LogsRequestShapeAndSuccess()
    {
        var pluginLog = new CapturingPluginLog();
        var responseHandler = new StructuredDialogueResponseHandler(
            """
            {"response":"{\"text_translated\":\"Fique perto.\"}"}
            """,
            () => pluginLog.DebugMessages.Any(
                message => message.Contains("structured-start", StringComparison.Ordinal)));
        var translator = new OllamaTranslator(
            pluginLog,
            new Config
            {
                OllamaUrl = "http://ollama.example",
                OllamaModel = "test-model",
            });
        this.ReplaceHttpClient(
            translator,
            new HttpClient(responseHandler)
            {
                BaseAddress = new Uri("http://ollama.example/"),
            });

        var translated = await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext("Talk", "quest-1", "Krile", []));

        translated.Should().Be("Fique perto.");
        responseHandler.RequestUri.Should().Be("http://ollama.example/api/generate");
        responseHandler.StructuredStartWasLoggedAtDispatch.Should().BeTrue();
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-start", StringComparison.Ordinal) &&
                message.Contains("provider=Ollama", StringComparison.Ordinal) &&
                message.Contains("route=api-generate", StringComparison.Ordinal) &&
                message.Contains("capabilityDecisions=temperature=omitted(unknown)", StringComparison.Ordinal) &&
                message.Contains($"requestJsonLength={responseHandler.RequestBody.Length}", StringComparison.Ordinal));
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-success", StringComparison.Ordinal) &&
                message.Contains("provider=Ollama", StringComparison.Ordinal) &&
                message.Contains("route=api-generate", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Ensures LM Studio records its OpenAI-compatible chat-completions
    ///     route around structured dialogue dispatch and validation.
    /// </summary>
    [Fact]
    public async Task LmStudioTranslator_StructuredDialogue_LogsRequestShapeAndSuccess()
    {
        var pluginLog = new CapturingPluginLog();
        var responseHandler = new StructuredDialogueResponseHandler(
            isStructuredStartLogged: () => pluginLog.DebugMessages.Any(
                message => message.Contains("structured-start", StringComparison.Ordinal)));
        var translator = new LmStudioTranslator(
            pluginLog,
            new Config
            {
                LmStudioBaseUrl = "http://lmstudio.example/v1",
                LmStudioModel = "test-model",
            });
        this.ReplaceHttpClient(
            translator,
            new HttpClient(responseHandler)
            {
                BaseAddress = new Uri("http://lmstudio.example/v1/"),
            });

        var translated = await translator.TranslateAsync(
            "Stay close.",
            "English",
            "Portuguese",
            new DialogueTranslationContext("Talk", "quest-1", "Krile", []));

        translated.Should().Be("Fique perto.");
        responseHandler.RequestUri.Should().Be("http://lmstudio.example/v1/chat/completions");
        responseHandler.StructuredStartWasLoggedAtDispatch.Should().BeTrue();
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-start", StringComparison.Ordinal) &&
                message.Contains("provider=LmStudio", StringComparison.Ordinal) &&
                message.Contains("route=chat-completions", StringComparison.Ordinal) &&
                message.Contains("capabilityDecisions=temperature=omitted(unknown)", StringComparison.Ordinal) &&
                message.Contains($"requestJsonLength={responseHandler.RequestBody.Length}", StringComparison.Ordinal));
        pluginLog.DebugMessages.Should().ContainSingle(
            message => message.Contains("structured-success", StringComparison.Ordinal) &&
                message.Contains("provider=LmStudio", StringComparison.Ordinal) &&
                message.Contains("route=chat-completions", StringComparison.Ordinal));
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
    ///     Ensures production DeepSeek and LM Studio base addresses preserve
    ///     their configured API-version segment for relative request routes.
    /// </summary>
    [Fact]
    public void OpenAiCompatibleTranslators_WhenBaseUrlLacksTrailingSlash_PreserveVersionSegment()
    {
        var deepSeekTranslator = new DeepSeekTranslator(
            new NoOpPluginLog(),
            new Config
            {
                DeepSeekTranslatorApiKey = new string('k', 25),
                DeepSeekBaseUrl = "https://deepseek.example/v1",
            });
        var lmStudioTranslator = new LmStudioTranslator(
            new NoOpPluginLog(),
            new Config
            {
                LmStudioBaseUrl = "http://lmstudio.example/v1",
            });

        this.GetHttpClient(deepSeekTranslator).BaseAddress.Should().Be(
            new Uri("https://deepseek.example/v1/"));
        this.GetHttpClient(lmStudioTranslator).BaseAddress.Should().Be(
            new Uri("http://lmstudio.example/v1/"));
    }

    /// <summary>
    ///     Ensures a reasoning-effort rejection is promoted only for the exact
    ///     model that returned the structured tool-calling error.
    /// </summary>
    [Fact]
    public async Task LearnFromProviderFailure_WithReasoningEffort400_PromotesExactModelFeedback()
    {
        await this.WithTemporaryConfigurationDirectoryAsync(async _ =>
        {
            var scope = new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra");

            var result = await LlmCapabilityPolicyService.LearnFromProviderFailureAsync(
                scope,
                LlmCapabilityParameterName.ReasoningEffort,
                400,
                "Function tools with reasoning_effort are not supported for gpt-5.6-terra in v1/chat/completions");

            result.ObservationRecorded.Should().BeTrue();
            result.RulePromoted.Should().BeTrue();

            using var context = new EchoglossianDbContext(_);
            context.LlmModelCapabilityObservations.Should().ContainSingle(
                observation => observation.ModelId == "gpt-5.6-terra" &&
                    observation.ParameterName == LlmCapabilityParameterName.ReasoningEffort.ToString());
            context.LlmModelCapabilityRules.Should().ContainSingle(
                rule => rule.MatchType == LlmCapabilityRuleMatchType.ExactModel.ToString() &&
                    rule.MatchValue == "gpt-5.6-terra" &&
                    rule.ParameterName == LlmCapabilityParameterName.ReasoningEffort.ToString() &&
                    rule.SupportState == LlmCapabilitySupportState.Unsupported.ToString());
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
    public async Task LearnFromProviderFailure_WithClassifiedTemperature400_PromotesExactModelOnly()
    {
        await this.WithTemporaryConfigurationDirectoryAsync(async configDir =>
        {
            var scope = new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra");

            var result = await LlmCapabilityPolicyService.LearnFromProviderFailureAsync(
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
    public async Task LearnFromProviderFailure_WithBlankModelId_DoesNotPersistCapabilityFeedback()
    {
        await this.WithTemporaryConfigurationDirectoryAsync(async configDir =>
        {
            var result = await LlmCapabilityPolicyService.LearnFromProviderFailureAsync(
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
    public async Task LearnFromProviderFailure_WithRepeatedAmbiguous400_DeduplicatesObservation()
    {
        await this.WithTemporaryConfigurationDirectoryAsync(async configDir =>
        {
            var scope = new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra");

            await LlmCapabilityPolicyService.LearnFromProviderFailureAsync(
                scope,
                LlmCapabilityParameterName.Temperature,
                400,
                "{ \"error\": { \"message\": \"Request could not be processed.\" } }");
            await LlmCapabilityPolicyService.LearnFromProviderFailureAsync(
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
    ///     Runs an asynchronous action with the real observation runtime
    ///     registered.
    /// </summary>
    /// <param name="action">The asynchronous action that exercises learning.</param>
    private async Task WithTemporaryConfigurationDirectoryAsync(Func<string, Task> action)
    {
        var configDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var originalConfigDirectory = Echoglossian.ConfigDirectory;
        Directory.CreateDirectory(configDir);
        Echoglossian.ConfigDirectory = configDir;
        LlmCapabilityCacheManager.Clear();
        var factory = new EchoglossianDbContextRuntimeFactory(configDir);
        var coordinator = new PersistenceCoordinator(factory);
        var writer = new LlmCapabilityObservationWriter(coordinator);
        LlmCapabilityObservationRuntime.Register(writer);
        try
        {
            await using (var context = await factory.CreateDbContextAsync())
            {
                await context.Database.MigrateAsync();
            }
            await action(configDir);
        }
        finally
        {
            LlmCapabilityObservationRuntime.Unregister(writer);
            await coordinator.DisposeAsync();
            LlmCapabilityCacheManager.Clear();
            PluginRuntimeFileLog.GetCurrentFilePathForTests().Should().Be(
                Path.Combine(configDir, "Echoglossian.log"));
            PluginRuntimeFileLog.FlushForTests();
            PluginRuntimeFileLog.ResetForTests();
            Echoglossian.ConfigDirectory = originalConfigDirectory;
            SqliteConnection.ClearAllPools();
            Directory.Delete(configDir, recursive: true);
        }
    }

    private void ReplaceHttpClient(OpenRouterTranslator translator, HttpClient httpClient)
    {
        typeof(OpenRouterTranslator)
            .GetField("httpClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(translator, httpClient);
    }

    private void ReplaceHttpClient(DeepSeekTranslator translator, HttpClient httpClient)
    {
        typeof(DeepSeekTranslator)
            .GetField("httpClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(translator, httpClient);
    }

    private HttpClient GetHttpClient<TTranslator>(TTranslator translator)
        where TTranslator : class
    {
        return (HttpClient)typeof(TTranslator)
            .GetField("httpClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(translator)!;
    }

    private void ReplaceHttpClient<TTranslator>(TTranslator translator, HttpClient httpClient)
        where TTranslator : class
    {
        typeof(TTranslator)
            .GetField("httpClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(translator, httpClient);
    }

    private sealed class StructuredDialogueResponseHandler : HttpMessageHandler
    {
        private const string DefaultResponseBody = """
            {"choices":[{"message":{"content":"{\"text_translated\":\"Fique perto.\"}"}}]}
            """;

        private readonly Func<bool>? isStructuredStartLogged;
        private readonly string responseBody;

        public StructuredDialogueResponseHandler(
            string? responseBody = null,
            Func<bool>? isStructuredStartLogged = null)
        {
            this.responseBody = responseBody ?? DefaultResponseBody;
            this.isStructuredStartLogged = isStructuredStartLogged;
        }

        public string RequestBody { get; private set; } = string.Empty;

        public string? RequestUri { get; private set; }

        public HttpMethod? RequestMethod { get; private set; }

        public bool StructuredStartWasLoggedAtDispatch { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.RequestMethod = request.Method;
            this.RequestUri = request.RequestUri?.ToString();
            this.RequestBody = request.Content?.ReadAsStringAsync(cancellationToken)
                .GetAwaiter()
                .GetResult() ?? string.Empty;
            this.StructuredStartWasLoggedAtDispatch = this.isStructuredStartLogged?.Invoke() ?? false;

            return Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(this.responseBody),
                });
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
