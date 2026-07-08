// <copyright file="StructuredDialogueOpenAiCompatiblePayloadHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared OpenAI-compatible JSON helper used by the first
///     structured dialogue HTTP translators.
/// </summary>
public class StructuredDialogueOpenAiCompatiblePayloadHelperTests
{
    /// <summary>
    ///     Ensures the shared schema element remains usable after the helper
    ///     disposes its temporary backing document.
    /// </summary>
    [Fact]
    public void BuildFunctionParametersJsonElement_ShouldReturnUsableClonedElement()
    {
        var element =
            StructuredDialogueOpenAiCompatiblePayloadHelper
                .BuildFunctionParametersJsonElement();

        element.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        element.TryGetProperty("type", out var typeElement).Should().BeTrue();
        typeElement.GetString().Should().Be("object");
    }

    /// <summary>
    ///     Ensures the helper can extract matching tool-call arguments from a
    ///     typical OpenAI-compatible response.
    /// </summary>
    [Fact]
    public void ExtractRawStructuredPayload_WithMatchingToolCall_ShouldReturnArguments()
    {
        const string responseJson =
            """
            {
              "choices": [
                {
                  "message": {
                    "tool_calls": [
                      {
                        "function": {
                          "name": "submit_dialogue_translation",
                          "arguments": "{\"speaker_translated\":\"Krile\",\"text_translated\":\"Stay close.\"}"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var payload =
            StructuredDialogueOpenAiCompatiblePayloadHelper.ExtractRawStructuredPayload(
                responseJson,
                StructuredDialogueOpenAiToolHelper.ToolFunctionName);

        payload.Should().Be(
            "{\"speaker_translated\":\"Krile\",\"text_translated\":\"Stay close.\"}");
    }

    /// <summary>
    ///     Ensures the helper falls back to direct content when tool calling is
    ///     not honored by the upstream endpoint.
    /// </summary>
    [Fact]
    public void ExtractRawStructuredPayload_WithoutToolCall_ShouldFallBackToContent()
    {
        const string responseJson =
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"speaker_translated\":\"Krile\",\"text_translated\":\"Stay close.\"}"
                  }
                }
              ]
            }
            """;

        var payload =
            StructuredDialogueOpenAiCompatiblePayloadHelper.ExtractRawStructuredPayload(
                responseJson,
                StructuredDialogueOpenAiToolHelper.ToolFunctionName);

        payload.Should().Be(
            "{\"speaker_translated\":\"Krile\",\"text_translated\":\"Stay close.\"}");
    }

    /// <summary>
    ///     Ensures unrelated tool calls are ignored instead of being treated as
    ///     the structured dialogue payload.
    /// </summary>
    [Fact]
    public void ExtractRawStructuredPayload_WithOtherToolCall_ShouldReturnEmpty()
    {
        const string responseJson =
            """
            {
              "choices": [
                {
                  "message": {
                    "tool_calls": [
                      {
                        "function": {
                          "name": "other_function",
                          "arguments": "{\"text_translated\":\"Ignored\"}"
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var payload =
            StructuredDialogueOpenAiCompatiblePayloadHelper.ExtractRawStructuredPayload(
                responseJson,
                StructuredDialogueOpenAiToolHelper.ToolFunctionName);

        payload.Should().BeEmpty();
    }
}
