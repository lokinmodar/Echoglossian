// <copyright file="StructuredDialogueTranslationResponseValidatorTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared structured dialogue response validator introduced for
///     issue 148.
/// </summary>
public class StructuredDialogueTranslationResponseValidatorTests
{
  /// <summary>
  ///     Ensures a strict JSON object matching the shared response contract is
  ///     accepted.
  /// </summary>
  [Fact]
  public void ParseAndValidate_StrictStructuredJson_ShouldSucceed()
  {
    const string rawContent =
        "{\"speaker_translated\":\"Sphene\",\"text_translated\":\"I will protect the smiles of everyone in Alexandria.\"}";

    var result =
        StructuredDialogueTranslationResponseValidator.ParseAndValidate(
            rawContent,
            requireSpeakerTranslated: true);

    result.IsValid.Should().BeTrue();
    result.Response.Should().NotBeNull();
    result.Response!.Value.SpeakerTranslated.Should().Be("Sphene");
    result.Response!.Value.TextTranslated.Should().Be(
        "I will protect the smiles of everyone in Alexandria.");
  }

  /// <summary>
  ///     Ensures annotation leakage or surrounding prose is rejected instead
  ///     of being silently extracted.
  /// </summary>
  [Fact]
  public void ParseAndValidate_AnnotatedWrapperText_ShouldFail()
  {
    const string rawContent =
        "Here is your translation: {\"speaker_translated\":\"Sphene\",\"text_translated\":\"Hello.\"}";

    var result =
        StructuredDialogueTranslationResponseValidator.ParseAndValidate(
            rawContent,
            requireSpeakerTranslated: true);

    result.IsValid.Should().BeFalse();
    result.FailureReason.Should().Be("invalid-structured-dialogue-json");
  }

  /// <summary>
  ///     Ensures empty or synthetic translated text is rejected by the shared
  ///     validator.
  /// </summary>
  [Fact]
  public void Validate_SyntheticOrMissingText_ShouldFail()
  {
    StructuredDialogueTranslationResponse response = new(
        "Krile",
        "[Translation Error: quota exceeded]");

    var result =
        StructuredDialogueTranslationResponseValidator.Validate(
            response,
            requireSpeakerTranslated: true);

    result.IsValid.Should().BeFalse();
    result.FailureReason.Should().Be("missing-structured-dialogue-text");
  }

  /// <summary>
  ///     Ensures a missing translated speaker can be enforced when the caller
  ///     requires it.
  /// </summary>
  [Fact]
  public void Validate_RequiredSpeakerMissing_ShouldFail()
  {
    StructuredDialogueTranslationResponse response = new(
        string.Empty,
        "Stay close.");

    var result =
        StructuredDialogueTranslationResponseValidator.Validate(
            response,
            requireSpeakerTranslated: true);

    result.IsValid.Should().BeFalse();
    result.FailureReason.Should().Be("missing-structured-dialogue-speaker");
  }
}
