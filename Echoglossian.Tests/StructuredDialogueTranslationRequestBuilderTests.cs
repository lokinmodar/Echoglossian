// <copyright file="StructuredDialogueTranslationRequestBuilderTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared structured dialogue request builder introduced for
///     issue 148.
/// </summary>
public class StructuredDialogueTranslationRequestBuilderTests
{
  private static readonly DateTime FixedObservedAtUtc =
      new(2026, 5, 15, 18, 0, 0, DateTimeKind.Utc);

  /// <summary>
  ///     Ensures the builder projects runtime-only dialogue context into the
  ///     stable structured request contract.
  /// </summary>
  [Fact]
  public void Build_WithDialogueContext_ShouldProjectContextAndMetadata()
  {
    DialogueTranslationContext context = new(
        "Talk",
        "krile-session",
        "Krile",
        [
          new DialogueTranslationTurn(
              "Krile",
              "Stay close.",
              FixedObservedAtUtc),
          new DialogueTranslationTurn(
              "Thancred",
              "We're moving.",
              FixedObservedAtUtc.AddSeconds(1)),
        ]);
    StructuredDialogueGlossaryEntry[] glossary =
    [
      new StructuredDialogueGlossaryEntry(
          "スフェーン",
          "Sphene",
          "Female name",
          "ja-JP",
          "en-US"),
    ];

    StructuredDialogueTranslationRequest request =
        StructuredDialogueTranslationRequestBuilder.Build(
            "We must press on.",
            "ja-JP",
            "en-US",
            TranslationSurfaceGroup.Dialogue,
            context,
            glossary,
            "A Greater Purpose",
            "npc",
            "she/her",
            "we");

    request.SurfaceFamily.Should().Be("Dialogue");
    request.SpeakerOriginal.Should().Be("Krile");
    request.TextOriginal.Should().Be("We must press on.");
    request.Metadata.QuestNameOriginal.Should().Be("A Greater Purpose");
    request.Metadata.SpeakerOriginal.Should().Be("Krile");
    request.Metadata.SpeakerRoleHint.Should().Be("npc");
    request.Metadata.PronounHint.Should().Be("she/her");
    request.Metadata.SubjectHint.Should().Be("we");

    request.DialogueContext.Should().HaveCount(2);
    request.DialogueContext[0].SpeakerOriginal.Should().Be("Krile");
    request.DialogueContext[0].TextOriginal.Should().Be("Stay close.");
    request.DialogueContext[1].SpeakerOriginal.Should().Be("Thancred");
    request.DialogueContext[1].TextOriginal.Should().Be("We're moving.");

    request.Glossary.Should().ContainSingle();
    request.Glossary[0].SourceText.Should().Be("スフェーン");
    request.Glossary[0].TargetText.Should().Be("Sphene");
  }

  /// <summary>
  ///     Ensures the builder remains null-safe when no dialogue context or
  ///     glossary is available.
  /// </summary>
  [Fact]
  public void Build_WithoutDialogueContext_ShouldReturnEmptyStructuredCollections()
  {
    StructuredDialogueTranslationRequest request =
        StructuredDialogueTranslationRequestBuilder.Build(
            "Plain line.",
            "English",
            "Portuguese",
            TranslationSurfaceGroup.Default);

    request.SurfaceFamily.Should().Be("Default");
    request.SpeakerOriginal.Should().BeEmpty();
    request.TextOriginal.Should().Be("Plain line.");
    request.DialogueContext.Should().BeEmpty();
    request.Glossary.Should().BeEmpty();
    request.Metadata.SpeakerOriginal.Should().BeEmpty();
  }
}
