// <copyright file="LlmCapabilityErrorClassifier.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace Echoglossian.Translators.Capabilities;

/// <summary>
///     Classifies provider failures without retaining raw response bodies.
/// </summary>
public static partial class LlmCapabilityErrorClassifier
{
    /// <summary>
    ///     Classifies a provider response as an exact-model promotable failure,
    ///     an observation-only client error, or an unclassified failure.
    /// </summary>
    /// <param name="scope">The capability scope associated with the request.</param>
    /// <param name="parameterName">The parameter sent in the failed request.</param>
    /// <param name="statusCode">The provider response status code.</param>
    /// <param name="responseText">The provider response body.</param>
    /// <returns>A bounded, sanitized failure classification.</returns>
    public static LlmCapabilityFailureClassification TryClassify(
        LlmCapabilityScope scope,
        LlmCapabilityParameterName parameterName,
        int? statusCode,
        string? responseText)
    {
        _ = scope;

        if (statusCode != 400)
        {
            return new LlmCapabilityFailureClassification(
                false,
                false,
                "unclassified",
                string.Empty,
                string.Empty);
        }

        var (providerErrorCode, message) = ExtractResponseDetails(responseText);
        var parameterToken = GetParameterToken(parameterName);
        var explicitlyUnsupported = !string.IsNullOrEmpty(parameterToken) &&
            ContainsParameter(message, parameterToken) &&
            (message.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("does not support", StringComparison.OrdinalIgnoreCase) ||
             providerErrorCode.Contains("unsupported", StringComparison.OrdinalIgnoreCase));

        return explicitlyUnsupported
            ? new LlmCapabilityFailureClassification(
                true,
                true,
                "unsupported-parameter",
                providerErrorCode,
                $"Provider rejected parameter '{parameterToken}'.")
            : new LlmCapabilityFailureClassification(
                true,
                false,
                "ambiguous-client-error",
                providerErrorCode,
                "Provider returned an unclassified 400 response.");
    }

    private static (string ProviderErrorCode, string Message) ExtractResponseDetails(
        string? responseText)
    {
        var providerErrorCode = string.Empty;
        var message = responseText ?? string.Empty;

        try
        {
            var root = JObject.Parse(responseText ?? string.Empty);
            var error = root["error"] ?? root;
            providerErrorCode = error.Value<string>("code") ?? string.Empty;
            message = error.Value<string>("message") ?? message;
        }
        catch (Newtonsoft.Json.JsonReaderException)
        {
            // Non-JSON providers retain only a sanitized bounded excerpt.
        }

        return (SanitizeProviderErrorCode(providerErrorCode), Sanitize(message));
    }

    private static bool ContainsParameter(string message, string parameterToken)
    {
        return message.Contains(parameterToken, StringComparison.OrdinalIgnoreCase) ||
            message.Contains(parameterToken.Replace("_", "-", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase) ||
            message.Contains(parameterToken.Replace("_", " ", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetParameterToken(LlmCapabilityParameterName parameterName)
    {
        return parameterName switch
        {
            LlmCapabilityParameterName.Temperature => "temperature",
            LlmCapabilityParameterName.TopP => "top_p",
            LlmCapabilityParameterName.TopK => "top_k",
            LlmCapabilityParameterName.PresencePenalty => "presence_penalty",
            LlmCapabilityParameterName.FrequencyPenalty => "frequency_penalty",
            LlmCapabilityParameterName.ReasoningEffort => "reasoning_effort",
            LlmCapabilityParameterName.StructuredToolCalling => "tool",
            _ => string.Empty,
        };
    }

    private static string Sanitize(string value)
    {
        var redacted = SensitiveValueRegex().Replace(value, "$1=[redacted]");
        var normalized = WhitespaceRegex().Replace(redacted, " ").Trim();
        return normalized.Length <= 256 ? normalized : normalized[..256];
    }

    private static string SanitizeProviderErrorCode(string value)
    {
        var sanitized = Sanitize(value);
        return ProviderErrorCodeRegex().IsMatch(sanitized) ? sanitized : string.Empty;
    }

    [GeneratedRegex("(?i)\\b(api[_-]?key|authorization|bearer|token|password|secret)\\s*[:=]\\s*[^\\s,;]+")]
    private static partial Regex SensitiveValueRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex ProviderErrorCodeRegex();
}
