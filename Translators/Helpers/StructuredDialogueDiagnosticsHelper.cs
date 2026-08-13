// <copyright file="StructuredDialogueDiagnosticsHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Text.RegularExpressions;

using Echoglossian.Translators.Capabilities;

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Formats standardized structured-dialogue downgrade diagnostics without
///     leaking secrets into runtime logs.
/// </summary>
public static partial class StructuredDialogueDiagnosticsHelper
{
    private const int MaxExcerptLength = 72;

    /// <summary>
    ///     Builds one standardized structured-dialogue fallback diagnostic.
    /// </summary>
    /// <param name="providerName">The provider family name.</param>
    /// <param name="modelName">The selected model name when known.</param>
    /// <param name="capability">The structured capability attempted.</param>
    /// <param name="stage">The downgrade stage such as validation or exception.</param>
    /// <param name="failureReason">The failure reason or message.</param>
    /// <param name="statusCode">The HTTP status code when available.</param>
    /// <param name="responseExcerpt">The short provider error excerpt when available.</param>
    /// <param name="endpointScope">The effective provider endpoint identity.</param>
    /// <param name="route">The provider request route when known.</param>
    /// <param name="capabilityDecisionTokens">The effective capability decision tokens.</param>
    /// <param name="glossaryApplied">Whether glossary entries were included.</param>
    /// <returns>The formatted diagnostic message.</returns>
    public static string FormatStructuredFallbackMessage(
        string providerName,
        string? modelName,
        StructuredDialogueProviderCapability capability,
        string stage,
        string failureReason,
        int? statusCode = null,
        string? responseExcerpt = null,
        string? endpointScope = null,
        string? route = null,
        IReadOnlyList<string>? capabilityDecisionTokens = null,
        bool? glossaryApplied = null)
    {
        var parts = new List<string>
        {
            "structured dialogue fallback",
            $"provider={providerName}",
            $"capability={FormatCapability(capability)}",
            $"stage={NormalizeToken(stage)}",
            $"reason={NormalizeToken(failureReason)}",
        };

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            parts.Add($"model={modelName}");
        }

        if (!string.IsNullOrWhiteSpace(endpointScope))
        {
            parts.Add($"endpointScope={SanitizeEndpointScope(endpointScope)}");
        }

        if (!string.IsNullOrWhiteSpace(route))
        {
            parts.Add($"route={NormalizeToken(route)}");
        }

        if (capabilityDecisionTokens is not null)
        {
            parts.Add($"capabilityDecisions={string.Join("|", capabilityDecisionTokens)}");
        }

        if (glossaryApplied.HasValue)
        {
            parts.Add($"glossaryApplied={glossaryApplied.Value.ToString().ToLowerInvariant()}");
        }

        if (statusCode.HasValue)
        {
            parts.Add($"status={statusCode.Value}");
        }

        var sanitizedExcerpt = SanitizeExcerpt(responseExcerpt);
        if (!string.IsNullOrWhiteSpace(sanitizedExcerpt))
        {
            parts.Add($"excerpt={sanitizedExcerpt}");
        }

        return string.Join(", ", parts);
    }

    private static string FormatCapability(StructuredDialogueProviderCapability capability)
    {
        return capability switch
        {
            StructuredDialogueProviderCapability.JsonSchema => "json-schema",
            StructuredDialogueProviderCapability.JsonObject => "json-object",
            StructuredDialogueProviderCapability.PlainTextGlossary => "plain-text-glossary",
            _ => "disabled",
        };
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var normalized = NonTokenCharacterPattern().Replace(
            value.Trim().ToLowerInvariant(),
            "-");
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private static string? SanitizeExcerpt(string? responseExcerpt)
    {
        if (string.IsNullOrWhiteSpace(responseExcerpt))
        {
            return null;
        }

        var sanitized = responseExcerpt
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
        sanitized = BearerCredentialPattern().Replace(
            sanitized,
            "[redacted]");
        sanitized = SecretAssignmentPattern().Replace(
            sanitized,
            "$1=[redacted]");
        sanitized = BearerTokenPattern().Replace(
            sanitized,
            "[redacted]");
        sanitized = MultiWhitespacePattern().Replace(
            sanitized,
            " ").Trim();

        if (sanitized.Length > MaxExcerptLength)
        {
            sanitized = $"{sanitized[..(MaxExcerptLength - 3)]}...";
        }

        return sanitized;
    }

    private static string SanitizeEndpointScope(string? endpointScope)
    {
        if (string.IsNullOrWhiteSpace(endpointScope))
        {
            return "unknown";
        }

        var singleLineEndpoint = endpointScope.Split(['\r', '\n', '\t'])[0].Trim();
        if (!Uri.TryCreate(singleLineEndpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            return "unknown";
        }

        var sanitizedUri = new UriBuilder(endpointUri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return sanitizedUri.Uri.GetLeftPart(UriPartial.Path);
    }

    [GeneratedRegex(@"\b(api[_-]?key|token|authorization)\s*[=:]\s*[^,\s;]+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerCredentialPattern();

    [GeneratedRegex(@"\b(?:sk|rk)-[A-Za-z0-9_-]+\b", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespacePattern();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonTokenCharacterPattern();
    /// <summary>
    ///     Builds a structured-dialogue request-start diagnostic.
    /// </summary>
    /// <param name="scope">The effective capability lookup scope.</param>
    /// <param name="route">The provider request route.</param>
    /// <param name="capability">The structured capability used for the request.</param>
    /// <param name="sessionNamespace">The dialogue session namespace.</param>
    /// <param name="priorTurns">The number of preceding dialogue turns.</param>
    /// <param name="glossaryCount">The number of glossary entries included.</param>
    /// <param name="speakerMetadataPresent">Whether speaker metadata was present.</param>
    /// <param name="addresseeMetadataPresent">Whether addressee metadata was present.</param>
    /// <param name="requestPromptLength">The generated prompt length.</param>
    /// <param name="requestJsonLength">The serialized request length when applicable.</param>
    /// <param name="promptPreview">The generated prompt preview.</param>
    /// <param name="sourcePreview">The source text preview.</param>
    /// <param name="capabilityDecisionTokens">The effective capability decision tokens.</param>
    /// <returns>The formatted diagnostic message.</returns>
    public static string FormatStructuredStartMessage(
        LlmCapabilityScope scope,
        string route,
        StructuredDialogueProviderCapability capability,
        string sessionNamespace,
        int priorTurns,
        int glossaryCount,
        bool speakerMetadataPresent,
        bool addresseeMetadataPresent,
        int requestPromptLength,
        int? requestJsonLength,
        string promptPreview,
        string sourcePreview,
        IReadOnlyList<string> capabilityDecisionTokens)
    {
        var parts = new List<string>
        {
            "structured-start",
            $"provider={scope.ProviderScope}",
            $"endpointScope={SanitizeEndpointScope(scope.EndpointScope)}",
            $"route={NormalizeToken(route)}",
            $"model={scope.ModelId}",
            $"capability={FormatCapability(capability)}",
            $"sessionNamespace={NormalizeToken(sessionNamespace)}",
            $"priorTurns={priorTurns}",
            $"glossaryCount={glossaryCount}",
            $"glossaryApplied={(glossaryCount > 0).ToString().ToLowerInvariant()}",
            $"speakerMetadataPresent={speakerMetadataPresent.ToString().ToLowerInvariant()}",
            $"addresseeMetadataPresent={addresseeMetadataPresent.ToString().ToLowerInvariant()}",
            $"requestPromptLength={requestPromptLength}",
            $"promptPreview={SanitizeExcerpt(promptPreview)}",
            $"sourcePreview={SanitizeExcerpt(sourcePreview)}",
            $"capabilityDecisions={string.Join("|", capabilityDecisionTokens)}",
        };

        if (requestJsonLength.HasValue)
        {
            parts.Add($"requestJsonLength={requestJsonLength.Value}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    ///     Builds a structured-dialogue request-success diagnostic.
    /// </summary>
    /// <param name="scope">The effective capability lookup scope.</param>
    /// <param name="route">The provider request route.</param>
    /// <param name="capability">The structured capability used for the request.</param>
    /// <param name="glossaryApplied">Whether glossary entries were included.</param>
    /// <param name="rawPayloadLength">The raw provider payload length.</param>
    /// <param name="translatedLength">The extracted translation length.</param>
    /// <param name="rawPayloadPreview">The raw provider payload preview.</param>
    /// <param name="translatedPreview">The translated text preview.</param>
    /// <returns>The formatted diagnostic message.</returns>
    public static string FormatStructuredSuccessMessage(
        LlmCapabilityScope scope,
        string route,
        StructuredDialogueProviderCapability capability,
        bool glossaryApplied,
        int rawPayloadLength,
        int translatedLength,
        string rawPayloadPreview,
        string translatedPreview)
    {
        return string.Join(", ", new[]
        {
            "structured-success",
            $"provider={scope.ProviderScope}",
            $"endpointScope={SanitizeEndpointScope(scope.EndpointScope)}",
            $"route={NormalizeToken(route)}",
            $"model={scope.ModelId}",
            $"capability={FormatCapability(capability)}",
            $"glossaryApplied={glossaryApplied.ToString().ToLowerInvariant()}",
            $"rawPayloadLength={rawPayloadLength}",
            $"translatedLength={translatedLength}",
            $"rawPayloadPreview={SanitizeExcerpt(rawPayloadPreview)}",
            $"translatedPreview={SanitizeExcerpt(translatedPreview)}",
        });
    }
}
