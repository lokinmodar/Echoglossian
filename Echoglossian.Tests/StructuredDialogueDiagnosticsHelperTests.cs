// <copyright file="StructuredDialogueDiagnosticsHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Helpers;
using Echoglossian.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared structured dialogue diagnostics formatting helper.
/// </summary>
public class StructuredDialogueDiagnosticsHelperTests
{
    /// <summary>
    ///     Ensures request diagnostics identify the effective route, glossary
    ///     state, and capability decisions.
    /// </summary>
    [Fact]
    public void FormatStructuredStartMessage_ShouldIncludeRouteGlossaryAndCapabilityDecisionTokens()
    {
        var scope = new LlmCapabilityScope(
            Echoglossian.TransEngines.ChatGPT,
            "OpenAI",
            "https://api.openai.com/v1",
            "gpt-5.6-terra");

        string message = StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
            scope,
            "chat/completions",
            StructuredDialogueProviderCapability.JsonSchema,
            "Talk",
            1,
            2,
            true,
            false,
            420,
            640,
            "Return only a JSON object...",
            "All these bright lights...",
            [
                "temperature=omitted(default-only)",
                "reasoning_effort=explicit-none(unsupported)",
            ]);

        message.Should().Contain("structured-start");
        message.Should().Contain("endpointScope=https://api.openai.com/v1");
        message.Should().Contain("route=chat-completions");
        message.Should().Contain("glossaryCount=2");
        message.Should().Contain("capabilityDecisions=temperature=omitted(default-only)|reasoning_effort=explicit-none(unsupported)");
    }

    /// <summary>
    ///     Ensures response diagnostics retain useful output context without
    ///     exposing a credential-like response token.
    /// </summary>
    [Fact]
    public void FormatStructuredSuccessMessage_ShouldIncludeSanitizedResponsePreview()
    {
        var scope = new LlmCapabilityScope(
            Echoglossian.TransEngines.OpenRouter,
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "openai/gpt-5-mini");

        string message = StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
            scope,
            "chat/completions",
            StructuredDialogueProviderCapability.JsonSchema,
            true,
            218,
            84,
            "{\"textTranslated\":\"sk-secret should never leak\"}",
            "Texto traduzido final");

        message.Should().Contain("structured-success");
        message.Should().Contain("glossaryApplied=true");
        message.Should().Contain("rawPayloadLength=218");
        message.Should().Contain("translatedLength=84");
        message.Should().NotContain("sk-secret");
    }

    /// <summary>
    ///     Ensures both header-style and JSON-style bearer credentials are
    ///     redacted before a structured response preview is logged.
    /// </summary>
    [Fact]
    public void FormatStructuredSuccessMessage_ShouldRedactBearerCredentials()
    {
        var scope = new LlmCapabilityScope(
            Echoglossian.TransEngines.ChatGPT,
            "OpenAI",
            "https://api.openai.com/v1",
            "gpt-5.6-terra");

        string message = StructuredDialogueDiagnosticsHelper.FormatStructuredSuccessMessage(
            scope,
            "chat/completions",
            StructuredDialogueProviderCapability.JsonSchema,
            false,
            64,
            8,
            "Authorization: Bearer abc123; {\"authorization\":\"Bearer xyz456\"}",
            "Traducao");

        message.Should().Contain("[redacted]");
        message.Should().NotContain("abc123");
        message.Should().NotContain("xyz456");
    }

    /// <summary>
    ///     Ensures endpoint diagnostics retain a readable endpoint while
    ///     removing user information, query secrets, fragments, and newlines.
    /// </summary>
    [Fact]
    public void FormatStructuredStartMessage_ShouldSanitizeEndpointScope()
    {
        var scope = new LlmCapabilityScope(
            Echoglossian.TransEngines.ChatGPT,
            "OpenAI",
            "https://user:password@api.openai.com/v1?api_key=secret#fragment\r\nforged=true",
            "gpt-5.6-terra");

        string startMessage = StructuredDialogueDiagnosticsHelper.FormatStructuredStartMessage(
            scope,
            "chat/completions",
            StructuredDialogueProviderCapability.JsonSchema,
            "Talk",
            0,
            0,
            false,
            false,
            24,
            null,
            "Prompt",
            "Source",
            []);
        string fallbackMessage = StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
            "OpenAI",
            "gpt-5.6-terra",
            StructuredDialogueProviderCapability.JsonSchema,
            "request",
            "bad-request",
            endpointScope: scope.EndpointScope);

        startMessage.Should().Contain("endpointScope=https://api.openai.com/v1");
        fallbackMessage.Should().Contain("endpointScope=https://api.openai.com/v1");
        startMessage.Should().NotContain("user");
        startMessage.Should().NotContain("password");
        startMessage.Should().NotContain("secret");
        startMessage.Should().NotContain("forged");
        fallbackMessage.Should().NotContain("\r");
        fallbackMessage.Should().NotContain("\n");
    }

    /// <summary>
    ///     Ensures captured debug logs render their structured values for
    ///     complete assertions in later provider tests.
    /// </summary>
    [Fact]
    public void CapturingPluginLog_Debug_ShouldRenderMessageValues()
    {
        var pluginLog = new CapturingPluginLog();

        pluginLog.Debug(
            "route={Route}, status={Status}",
            "chat/completions",
            200);

        pluginLog.DebugMessages.Should().ContainSingle()
            .Which.Should().Be("route=\"chat/completions\", status=200");
    }

    /// <summary>
    ///     Ensures structured fallback diagnostics expose a normalized shape
    ///     with provider, model, capability, stage, and HTTP status.
    /// </summary>
    [Fact]
    public void FormatStructuredFallbackMessage_ShouldIncludeNormalizedShape()
    {
        string message = StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
            "Gemini",
            "gemini-2.5-flash",
            StructuredDialogueProviderCapability.JsonSchema,
            "exception",
            "Bad Request",
            400);

        message.Should().Contain("provider=Gemini");
        message.Should().Contain("model=gemini-2.5-flash");
        message.Should().Contain("capability=json-schema");
        message.Should().Contain("stage=exception");
        message.Should().Contain("reason=bad-request");
        message.Should().Contain("status=400");
    }

    /// <summary>
    ///     Ensures free-form excerpts are redacted and truncated before they
    ///     reach runtime logs.
    /// </summary>
    [Fact]
    public void FormatStructuredFallbackMessage_ShouldRedactAndTruncateExcerpt()
    {
        string message = StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
            "Gemini",
            "gemini-2.5-flash",
            StructuredDialogueProviderCapability.JsonSchema,
            "exception",
            "bad-request",
            400,
            "apiKey=sk-secret-1234567890 token=abc123 this-is-a-very-long-excerpt-that-should-be-truncated-before-it-reaches-the-log-line");

        message.Should().Contain("excerpt=");
        message.Should().NotContain("sk-secret-1234567890");
        message.Should().NotContain("token=abc123");
        message.Should().Contain("[redacted]");
        message.Length.Should().BeLessThan(240);
    }

    /// <summary>
    ///     Ensures fallback diagnostics preserve the effective endpoint, route,
    ///     capability decisions, and glossary state.
    /// </summary>
    [Fact]
    public void FormatStructuredFallbackMessage_ShouldIncludeEffectiveRequestShape()
    {
        string message = StructuredDialogueDiagnosticsHelper.FormatStructuredFallbackMessage(
            "OpenAI",
            "gpt-5.6-terra",
            StructuredDialogueProviderCapability.JsonSchema,
            "request",
            "unsupported-parameter",
            400,
            endpointScope: "https://api.openai.com/v1",
            route: "chat/completions",
            capabilityDecisionTokens: ["reasoning_effort=explicit-none(unsupported)"],
            glossaryApplied: true);

        message.Should().Contain("endpointScope=https://api.openai.com/v1");
        message.Should().Contain("route=chat-completions");
        message.Should().Contain("capabilityDecisions=reasoning_effort=explicit-none(unsupported)");
        message.Should().Contain("glossaryApplied=true");
    }
}
