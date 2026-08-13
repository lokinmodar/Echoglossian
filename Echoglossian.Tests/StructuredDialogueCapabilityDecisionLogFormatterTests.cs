// <copyright file="StructuredDialogueCapabilityDecisionLogFormatterTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Capabilities;
using Echoglossian.Translators.Helpers;

using FluentAssertions;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers tokens that expose resolved capability decisions in structured
///     dialogue diagnostics.
/// </summary>
public class StructuredDialogueCapabilityDecisionLogFormatterTests
{
    /// <summary>
    ///     Ensures an unsupported reasoning-effort parameter explicitly
    ///     records its required null disable value.
    /// </summary>
    [Fact]
    public void Format_WhenUnsupportedReasoningEffortUsesExplicitNone_ShouldEmitExplicitDisableToken()
    {
        string token = StructuredDialogueCapabilityDecisionLogFormatter.Format(
            LlmCapabilityParameterName.ReasoningEffort,
            new LlmCapabilityParameterDecision(
                LlmCapabilitySupportState.Unsupported,
                null,
                null,
                false,
                "StaticDefault",
                "Unsupported"),
            StructuredDialogueCapabilityEmissionMode.ExplicitDisable);

        token.Should().Be("reasoning_effort=explicit-none(unsupported)");
    }

    /// <summary>
    ///     Ensures a supported reasoning-effort option without a configured
    ///     value is logged as an accurate omission instead of an unknown rule.
    /// </summary>
    [Fact]
    public void Format_WhenSupportedReasoningEffortIsUnset_ShouldEmitSupportedOmissionToken()
    {
        string token = StructuredDialogueCapabilityDecisionLogFormatter.Format(
            LlmCapabilityParameterName.ReasoningEffort,
            new LlmCapabilityParameterDecision(
                LlmCapabilitySupportState.Supported,
                null,
                null,
                false,
                "StaticDefault",
                "Supported"),
            StructuredDialogueCapabilityEmissionMode.OmittedSupported);

        token.Should().Be("reasoning_effort=omitted(unconfigured)");
    }

    /// <summary>
    ///     Ensures incompatible support and emission combinations do not
    ///     silently produce an incorrect capability decision token.
    /// </summary>
    [Fact]
    public void Format_WhenEmissionModeIsIncompatibleWithSupportState_ShouldThrow()
    {
        var decision = new LlmCapabilityParameterDecision(
            LlmCapabilitySupportState.Supported,
            null,
            null,
            false,
            "StaticDefault",
            "Supported");

        var action = () => StructuredDialogueCapabilityDecisionLogFormatter.Format(
            LlmCapabilityParameterName.Temperature,
            decision,
            StructuredDialogueCapabilityEmissionMode.ExplicitDisable);

        action.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    ///     Ensures unrecognized emission modes are rejected instead of being
    ///     rendered as a misleading omission token.
    /// </summary>
    [Fact]
    public void Format_WhenEmissionModeIsUnknown_ShouldThrow()
    {
        var decision = new LlmCapabilityParameterDecision(
            LlmCapabilitySupportState.Unknown,
            null,
            null,
            false,
            "StaticDefault",
            "Unknown");

        var action = () => StructuredDialogueCapabilityDecisionLogFormatter.Format(
            LlmCapabilityParameterName.Temperature,
            decision,
            (StructuredDialogueCapabilityEmissionMode)99);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
