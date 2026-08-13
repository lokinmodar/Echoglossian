// <copyright file="StructuredDialogueCapabilityDecisionLogFormatter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Capabilities;

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Identifies how a resolved capability parameter was represented in a
///     provider request.
/// </summary>
internal enum StructuredDialogueCapabilityEmissionMode
{
    /// <summary>
    ///     The configured parameter value was sent.
    /// </summary>
    SentConfigured,

    /// <summary>
    ///     The parameter was omitted because the provider does not support it.
    /// </summary>
    OmittedUnsupported,

    /// <summary>
    ///     The parameter was omitted because only its implicit default is allowed.
    /// </summary>
    OmittedDefaultOnly,

    /// <summary>
    ///     The parameter was omitted because its support state is unknown.
    /// </summary>
    OmittedUnknown,

    /// <summary>
    ///     The parameter was sent with its explicit disable value.
    /// </summary>
    ExplicitDisable,
}

/// <summary>
///     Formats compact, provider-independent capability decision tokens for
///     structured dialogue diagnostics.
/// </summary>
internal static class StructuredDialogueCapabilityDecisionLogFormatter
{
    /// <summary>
    ///     Formats one capability decision using its effective request emission.
    /// </summary>
    /// <param name="parameterName">The governed provider request parameter.</param>
    /// <param name="decision">The resolved capability decision.</param>
    /// <param name="emissionMode">How the request represented the decision.</param>
    /// <returns>The compact diagnostic token.</returns>
    public static string Format(
        LlmCapabilityParameterName parameterName,
        LlmCapabilityParameterDecision decision,
        StructuredDialogueCapabilityEmissionMode emissionMode)
    {
        var parameterToken = parameterName switch
        {
            LlmCapabilityParameterName.ReasoningEffort => "reasoning_effort",
            LlmCapabilityParameterName.Temperature => "temperature",
            _ => parameterName.ToString().ToLowerInvariant(),
        };

        if (!Enum.IsDefined(emissionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(emissionMode),
                emissionMode,
                "Unsupported capability emission mode.");
        }

        var emissionToken = emissionMode switch
        {
            StructuredDialogueCapabilityEmissionMode.SentConfigured when decision.SupportState == LlmCapabilitySupportState.Supported
                => "sent(configured)",
            StructuredDialogueCapabilityEmissionMode.OmittedDefaultOnly when
                decision.SupportState == LlmCapabilitySupportState.Supported && decision.OmitWhenDefaultOnly
                => "omitted(default-only)",
            StructuredDialogueCapabilityEmissionMode.OmittedUnsupported when decision.SupportState == LlmCapabilitySupportState.Unsupported
                => "omitted(unsupported)",
            StructuredDialogueCapabilityEmissionMode.OmittedUnknown when decision.SupportState == LlmCapabilitySupportState.Unknown
                => "omitted(unknown)",
            StructuredDialogueCapabilityEmissionMode.ExplicitDisable when
                parameterName == LlmCapabilityParameterName.ReasoningEffort &&
                decision.SupportState == LlmCapabilitySupportState.Unsupported
                => "explicit-none(unsupported)",
            _ => throw new InvalidOperationException(
                $"Emission mode '{emissionMode}' is incompatible with " +
                $"parameter '{parameterName}' and support state '{decision.SupportState}'."),
        };

        return $"{parameterToken}={emissionToken}";
    }
}
