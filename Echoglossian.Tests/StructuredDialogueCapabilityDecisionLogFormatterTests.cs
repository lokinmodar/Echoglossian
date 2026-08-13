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
}
