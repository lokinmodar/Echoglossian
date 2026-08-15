// <copyright file="LlmCapabilityRequestPayloadSanitizer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Applies shared LLM capability decisions to mutable provider payloads.
/// </summary>
public static class LlmCapabilityRequestPayloadSanitizer
{
    /// <summary>
    ///     Adds the configured temperature to a provider payload only when the
    ///     shared policy permits the parameter for the active scope.
    /// </summary>
    /// <param name="payload">The mutable provider request payload.</param>
    /// <param name="scope">The active capability scope.</param>
    /// <param name="configuredTemperature">The configured temperature value.</param>
    /// <returns>
    ///     <see langword="true" /> when temperature was added; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public static bool TryAddTemperature(
        IDictionary<string, object> payload,
        LlmCapabilityScope scope,
        float configuredTemperature)
    {
        if (!LlmCapabilityPolicyService.TryResolveTemperature(
                scope,
                configuredTemperature,
                out var sanitizedTemperature,
                out _))
        {
            return false;
        }

        payload.Add("temperature", sanitizedTemperature);
        return true;
    }
}
