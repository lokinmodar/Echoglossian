// <copyright file="StructuredDialogueOpenAiToolHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared OpenAI-family helper introduced for the first live
///     structured dialogue path.
/// </summary>
public class StructuredDialogueOpenAiToolHelperTests
{
    /// <summary>
    ///     Ensures the helper exposes a stable tool schema with the expected
    ///     required JSON fields.
    /// </summary>
    [Fact]
    public void BuildFunctionParametersSchemaJson_ShouldRequireStructuredFields()
    {
        var schemaJson =
            StructuredDialogueOpenAiToolHelper.BuildFunctionParametersSchemaJson();

        schemaJson.Should().Contain("\"additionalProperties\": false");
        schemaJson.Should().Contain("\"speaker_translated\"");
        schemaJson.Should().Contain("\"text_translated\"");
        schemaJson.Should().Contain("\"required\"");
    }

    /// <summary>
    ///     Ensures the helper prompt includes both the legacy prompt text and
    ///     the serialized structured request payload.
    /// </summary>
    [Fact]
    public void BuildUserPrompt_ShouldEmbedLegacyPromptAndRequestJson()
    {
        StructuredDialogueTranslationRequest request =
            StructuredDialogueTranslationRequestBuilder.Build(
                "Stay close.",
                "English",
                "Portuguese",
                TranslationSurfaceGroup.Dialogue,
                new DialogueTranslationContext(
                    "Talk",
                    "krile-session",
                    "Krile",
                    [
                        new DialogueTranslationTurn(
                            "Krile",
                            "Stay close.",
                            new DateTime(2026, 5, 15, 19, 0, 0, DateTimeKind.Utc)),
                    ]));

        var prompt = StructuredDialogueOpenAiToolHelper.BuildUserPrompt(
            "Translate this line naturally.",
            request);

        prompt.Should().Contain("Translate this line naturally.");
        prompt.Should().Contain("Structured dialogue request JSON:");
        prompt.Should().Contain("\"speaker_original\":\"Krile\"");
        prompt.Should().Contain("\"text_original\":\"Stay close.\"");
        prompt.Should().Contain("Return the result by calling the translation tool.");
    }
}
