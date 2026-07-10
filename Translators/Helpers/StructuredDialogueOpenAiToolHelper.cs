// <copyright file="StructuredDialogueOpenAiToolHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Builds the narrow OpenAI-family tool schema and user prompt used by the
///     first live structured dialogue path.
/// </summary>
public static class StructuredDialogueOpenAiToolHelper
{
    private const string FunctionDescription =
        "Submit the translated dialogue speaker and dialogue line as JSON arguments.";
    private const string FunctionName = "submit_dialogue_translation";
    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    ///     Gets the stable function name used for the structured dialogue tool
    ///     call.
    /// </summary>
    public static string ToolFunctionName => FunctionName;

    /// <summary>
    ///     Gets the stable function description used for the structured
    ///     dialogue tool call.
    /// </summary>
    public static string ToolFunctionDescription => FunctionDescription;

    /// <summary>
    ///     Builds the JSON schema for the OpenAI-family function tool
    ///     parameters.
    /// </summary>
    /// <returns>The JSON schema string.</returns>
    public static string BuildFunctionParametersSchemaJson()
    {
        return
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "speaker_translated": {
                  "type": "string",
                  "description": "The translated speaker name. Use an empty string when no speaker is visible."
                },
                "text_translated": {
                  "type": "string",
                  "description": "The translated dialogue text."
                }
              },
              "required": [
                "speaker_translated",
                "text_translated"
              ]
            }
            """;
    }

    /// <summary>
    ///     Serializes the internal structured dialogue request payload into the
    ///     stable JSON representation consumed by the first OpenAI-family
    ///     structured path.
    /// </summary>
    /// <param name="request">The internal structured request payload.</param>
    /// <returns>The serialized request JSON.</returns>
    public static string SerializeRequestPayload(
        StructuredDialogueTranslationRequest request)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            request,
            SerializerOptions);
    }

    /// <summary>
    ///     Builds the user prompt sent to the OpenAI-family structured tool
    ///     path.
    /// </summary>
    /// <param name="basePrompt">
    ///     The existing rendered translator prompt for the
    ///     same line.
    /// </param>
    /// <param name="request">The structured request payload.</param>
    /// <returns>The full user prompt.</returns>
    public static string BuildUserPrompt(
        string basePrompt,
        StructuredDialogueTranslationRequest request)
    {
        var serializedRequest = SerializeRequestPayload(request);
        return
            $"{basePrompt}\n\n" +
            "Return the result by calling the translation tool. " +
            "Do not answer with prose, markdown, or code fences.\n\n" +
            "Structured dialogue request JSON:\n" +
            serializedRequest;
    }
}
