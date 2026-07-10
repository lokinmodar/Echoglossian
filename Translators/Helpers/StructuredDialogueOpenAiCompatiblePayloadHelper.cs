// <copyright file="StructuredDialogueOpenAiCompatiblePayloadHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Provides the small shared JSON helpers for OpenAI-compatible chat
///     responses used by OpenRouter, DeepSeek, LM Studio, and similar
///     providers.
/// </summary>
public static class StructuredDialogueOpenAiCompatiblePayloadHelper
{
    /// <summary>
    ///     Builds one cloned JSON element representing the structured dialogue
    ///     function parameter schema.
    /// </summary>
    /// <returns>The cloned JSON element.</returns>
    public static System.Text.Json.JsonElement BuildFunctionParametersJsonElement()
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            StructuredDialogueOpenAiToolHelper.BuildFunctionParametersSchemaJson());
        return document.RootElement.Clone();
    }

    /// <summary>
    ///     Extracts the raw structured payload from one OpenAI-compatible chat
    ///     response JSON document, preferring matching tool-call arguments and
    ///     falling back to direct text content when the endpoint ignored tool
    ///     calling.
    /// </summary>
    /// <param name="responseJson">The raw response JSON.</param>
    /// <param name="expectedFunctionName">The expected function-tool name.</param>
    /// <returns>The extracted raw payload, or an empty string.</returns>
    public static string ExtractRawStructuredPayload(
        string responseJson,
        string expectedFunctionName)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return string.Empty;
        }

        using var document = System.Text.Json.JsonDocument.Parse(
            responseJson);
        if (!document.RootElement.TryGetProperty(
                "choices",
                out var choicesElement) ||
            choicesElement.ValueKind != System.Text.Json.JsonValueKind.Array ||
            choicesElement.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstChoice = choicesElement[0];
        if (!firstChoice.TryGetProperty(
                "message",
                out var messageElement))
        {
            return string.Empty;
        }

        if (messageElement.TryGetProperty(
                "tool_calls",
                out var toolCallsElement) &&
            toolCallsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var toolCallElement in toolCallsElement.EnumerateArray())
            {
                if (!toolCallElement.TryGetProperty(
                        "function",
                        out var functionElement))
                {
                    continue;
                }

                if (!functionElement.TryGetProperty(
                        "name",
                        out var functionNameElement) ||
                    !string.Equals(
                        functionNameElement.GetString(),
                        expectedFunctionName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (functionElement.TryGetProperty(
                        "arguments",
                        out var argumentsElement))
                {
                    return argumentsElement.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => argumentsElement.GetString() ?? string.Empty,
                        _ => argumentsElement.GetRawText(),
                    };
                }
            }
        }

        if (messageElement.TryGetProperty(
                "content",
                out var contentElement))
        {
            return contentElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => contentElement.GetString() ?? string.Empty,
                _ => contentElement.GetRawText(),
            };
        }

        return string.Empty;
    }
}
