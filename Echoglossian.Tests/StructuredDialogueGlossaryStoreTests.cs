// <copyright file="StructuredDialogueGlossaryStoreTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared runtime glossary store introduced for structured
///     dialogue glossary loading.
/// </summary>
public class StructuredDialogueGlossaryStoreTests
{
    /// <summary>
    ///     Ensures the shared store filters loaded entries by language scope.
    /// </summary>
    [Fact]
    public void GetEntries_ShouldFilterBySourceAndTargetLanguage()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                filePath,
                """
                [
                  {
                    "source_text": "スフェーン",
                    "target_text": "Sphene",
                    "source_language": "ja-JP",
                    "target_language": "en-US"
                  },
                  {
                    "source_text": "Krile",
                    "target_text": "Krile",
                    "source_language": "en-US",
                    "target_language": "pt-BR"
                  }
                ]
                """);

            StructuredDialogueGlossaryStore.Clear();
            StructuredDialogueGlossaryStore.Refresh(filePath).Should().BeTrue();

            var japaneseToEnglishEntries =
                StructuredDialogueGlossaryStore.GetEntries("ja-JP", "en-US");
            var englishToPortugueseEntries =
                StructuredDialogueGlossaryStore.GetEntries("en-US", "pt-BR");

            japaneseToEnglishEntries.Should().ContainSingle();
            japaneseToEnglishEntries[0].TargetText.Should().Be("Sphene");
            englishToPortugueseEntries.Should().ContainSingle();
            englishToPortugueseEntries[0].TargetText.Should().Be("Krile");
        }
        finally
        {
            StructuredDialogueGlossaryStore.Clear();
            File.Delete(filePath);
        }
    }
}
