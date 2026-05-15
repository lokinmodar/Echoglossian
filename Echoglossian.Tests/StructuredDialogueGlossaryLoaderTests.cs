// <copyright file="StructuredDialogueGlossaryLoaderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the first-pass structured dialogue glossary loader.
/// </summary>
public class StructuredDialogueGlossaryLoaderTests
{
    /// <summary>
    ///     Ensures the loader accepts a root JSON array of glossary rows.
    /// </summary>
    [Fact]
    public void LoadFromJson_ShouldAcceptArrayRoot()
    {
        var json =
            """
            [
              {
                "source_text": "スフェーン",
                "target_text": "Sphene",
                "comment": "Character name",
                "source_language": "ja-JP",
                "target_language": "en-US"
              }
            ]
            """;

        var result = StructuredDialogueGlossaryLoader.LoadFromJson(json);

        result.Succeeded.Should().BeTrue();
        result.Entries.Should().ContainSingle();
        result.Entries[0].SourceText.Should().Be("スフェーン");
        result.Entries[0].TargetText.Should().Be("Sphene");
        result.SkippedEntryCount.Should().Be(0);
    }

    /// <summary>
    ///     Ensures the loader accepts an object document with an entries array
    ///     and skips malformed rows instead of failing the whole document.
    /// </summary>
    [Fact]
    public void LoadFromJson_ShouldAcceptDocumentObjectAndSkipMalformedRows()
    {
        var json =
            """
            {
              "schema_version": "1.0",
              "entries": [
                {
                  "source": "アレクサンドリア",
                  "target": "Alexandria"
                },
                {
                  "source_text": "",
                  "target_text": "Invalid"
                }
              ]
            }
            """;

        var result = StructuredDialogueGlossaryLoader.LoadFromJson(json);

        result.Succeeded.Should().BeTrue();
        result.Entries.Should().ContainSingle();
        result.Entries[0].SourceText.Should().Be("アレクサンドリア");
        result.Entries[0].TargetText.Should().Be("Alexandria");
        result.SkippedEntryCount.Should().Be(1);
    }

    /// <summary>
    ///     Ensures invalid root JSON fails with a clear load result.
    /// </summary>
    [Fact]
    public void LoadFromJson_ShouldFailForInvalidRootShape()
    {
        var json =
            """
            {
              "schema_version": "1.0"
            }
            """;

        var result = StructuredDialogueGlossaryLoader.LoadFromJson(json);

        result.Succeeded.Should().BeFalse();
        result.Entries.Should().BeEmpty();
        result.FailureDetail.Should().NotBeNullOrWhiteSpace();
    }
}
