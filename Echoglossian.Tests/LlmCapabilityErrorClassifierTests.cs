// <copyright file="LlmCapabilityErrorClassifierTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Capabilities;

using FluentAssertions;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers bounded classification of provider capability failures.
/// </summary>
public sealed class LlmCapabilityErrorClassifierTests
{
    /// <summary>
    ///     Ensures an explicit unsupported parameter response is safe to
    ///     promote.
    /// </summary>
    [Fact]
    public void TryClassify_WithExplicitUnsupportedParameter400_ReturnsPromotableClassification()
    {
        var classification = LlmCapabilityErrorClassifier.TryClassify(
            new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra"),
            LlmCapabilityParameterName.Temperature,
            400,
            "{ \"error\": { \"code\": \"unsupported_parameter\", \"message\": \"Unsupported parameter: temperature. api_key=secret\" } }");

        classification.ObservationRecorded.Should().BeTrue();
        classification.RulePromoted.Should().BeTrue();
        classification.FailureKind.Should().Be("unsupported-parameter");
        classification.MessageExcerpt.Should().Be("Provider rejected parameter 'temperature'.");
        classification.MessageExcerpt.Should().NotContain("secret");
    }

    /// <summary>
    ///     Ensures ambiguous client errors are retained only as sanitized
    ///     observations.
    /// </summary>
    [Fact]
    public void TryClassify_WithAmbiguous400_ReturnsObservationOnlyClassification()
    {
        var classification = LlmCapabilityErrorClassifier.TryClassify(
            new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra"),
            LlmCapabilityParameterName.Temperature,
            400,
            "{ \"error\": { \"message\": \"Request could not be processed.\" } }");

        classification.ObservationRecorded.Should().BeTrue();
        classification.RulePromoted.Should().BeFalse();
        classification.FailureKind.Should().Be("ambiguous-client-error");
    }

    /// <summary>
    ///     Ensures non-client failures are not retained as capability feedback.
    /// </summary>
    [Fact]
    public void TryClassify_WithNon400Failure_ReturnsNoObservation()
    {
        var classification = LlmCapabilityErrorClassifier.TryClassify(
            new LlmCapabilityScope(
                Echoglossian.TransEngines.ChatGPT,
                "OpenAI",
                "https://api.openai.com/v1",
                "gpt-5.6-terra"),
            LlmCapabilityParameterName.Temperature,
            429,
            "rate limited");

        classification.ObservationRecorded.Should().BeFalse();
        classification.RulePromoted.Should().BeFalse();
        classification.FailureKind.Should().Be("unclassified");
    }
}
