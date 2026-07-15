// <copyright file="StructuredDialogueAnthropicToolHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Builds the narrow Anthropic tool schema and extracts one structured
///     dialogue payload from Claude tool-use responses.
/// </summary>
public static class StructuredDialogueAnthropicToolHelper
{
    /// <summary>
    ///     Gets the stable Anthropic tool name used for structured dialogue.
    /// </summary>
    public static string ToolName => StructuredDialogueOpenAiToolHelper.ToolFunctionName;

    /// <summary>
    ///     Gets the stable Anthropic tool description used for structured
    ///     dialogue.
    /// </summary>
    public static string ToolDescription => StructuredDialogueOpenAiToolHelper.ToolFunctionDescription;

    /// <summary>
    ///     Builds the JSON schema for the Anthropic tool input.
    /// </summary>
    /// <returns>The JSON schema string.</returns>
    public static string BuildInputSchemaJson()
    {
        return StructuredDialogueOpenAiToolHelper.BuildFunctionParametersSchemaJson();
    }

    /// <summary>
    ///     Extracts the raw structured tool payload from one Anthropic
    ///     Messages API response body.
    /// </summary>
    /// <param name="responseJson">The raw API response JSON.</param>
    /// <param name="expectedToolName">The expected tool name.</param>
    /// <returns>
    ///     The compact JSON payload for the matched tool input, or
    ///     <see langword="null" /> when no compatible tool call was found.
    /// </returns>
    public static string? ExtractRawStructuredPayload(
        string responseJson,
        string expectedToolName)
    {
        var responseObject = JObject.Parse(responseJson);
        var contentBlocks = responseObject["content"]?.OfType<JObject>() ?? [];
        foreach (var block in contentBlocks)
        {
            if (!string.Equals(
                    block["type"]?.ToString(),
                    "tool_use",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(
                    block["name"]?.ToString(),
                    expectedToolName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var inputPayload = block["input"];
            if (inputPayload == null ||
                inputPayload.Type == JTokenType.Null)
            {
                return null;
            }

            return inputPayload.ToString(Formatting.None);
        }

        return null;
    }
}
