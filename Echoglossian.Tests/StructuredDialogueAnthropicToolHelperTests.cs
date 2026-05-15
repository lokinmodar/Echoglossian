// <copyright file="StructuredDialogueAnthropicToolHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers Anthropic-specific tool-use extraction for structured dialogue.
/// </summary>
public class StructuredDialogueAnthropicToolHelperTests
{
    /// <summary>
    ///     Ensures the helper extracts the compact tool input payload from one
    ///     Claude tool-use response.
    /// </summary>
    [Fact]
    public void ExtractRawStructuredPayload_ShouldReadToolInputJson()
    {
        var responseJson =
            """
            {
              "content": [
                {
                  "type": "tool_use",
                  "name": "submit_dialogue_translation",
                  "input": {
                    "speaker_translated": "Krile",
                    "text_translated": "Stay close."
                  }
                }
              ]
            }
            """;

        var payload =
            StructuredDialogueAnthropicToolHelper.ExtractRawStructuredPayload(
                responseJson,
                StructuredDialogueAnthropicToolHelper.ToolName);

        payload.Should().Be("{\"speaker_translated\":\"Krile\",\"text_translated\":\"Stay close.\"}");
    }

    /// <summary>
    ///     Ensures the helper ignores other content blocks when no matching
    ///     tool-use block is present.
    /// </summary>
    [Fact]
    public void ExtractRawStructuredPayload_ShouldReturnNullWhenNoMatchingToolUse()
    {
        var responseJson =
            """
            {
              "content": [
                {
                  "type": "text",
                  "text": "Here is your translation."
                }
              ]
            }
            """;

        var payload =
            StructuredDialogueAnthropicToolHelper.ExtractRawStructuredPayload(
                responseJson,
                StructuredDialogueAnthropicToolHelper.ToolName);

        payload.Should().BeNull();
    }
}
