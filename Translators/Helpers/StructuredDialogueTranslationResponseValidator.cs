// <copyright file="StructuredDialogueTranslationResponseValidator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Validates and parses structured dialogue response payloads before any
///     provider-specific wiring starts accepting them as live translated text.
/// </summary>
public static class StructuredDialogueTranslationResponseValidator
{
  private const string InvalidStructuredJsonFailureReason = "invalid-structured-dialogue-json";
  private const string MissingStructuredTextFailureReason = "missing-structured-dialogue-text";
  private const string MissingStructuredSpeakerFailureReason = "missing-structured-dialogue-speaker";
  private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  /// <summary>
  ///     Parses and validates one structured dialogue response payload.
  /// </summary>
  /// <param name="rawContent">The raw provider response content.</param>
  /// <param name="requireSpeakerTranslated">
  ///     Whether a translated speaker field is
  ///     required for the payload to be considered valid.
  /// </param>
  /// <returns>The structured validation result.</returns>
  public static StructuredDialogueResponseValidationResult ParseAndValidate(
      string? rawContent,
      bool requireSpeakerTranslated = false)
  {
    if (string.IsNullOrWhiteSpace(rawContent))
    {
      return new StructuredDialogueResponseValidationResult(
          false,
          null,
          InvalidStructuredJsonFailureReason);
    }

    try
    {
      var parsed = System.Text.Json.JsonSerializer.Deserialize<StructuredDialogueTranslationResponse>(
          rawContent,
          SerializerOptions);
      return Validate(
          parsed,
          requireSpeakerTranslated);
    }
    catch (System.Text.Json.JsonException)
    {
      return new StructuredDialogueResponseValidationResult(
          false,
          null,
          InvalidStructuredJsonFailureReason);
    }
  }

  /// <summary>
  ///     Validates one already parsed structured dialogue response payload.
  /// </summary>
  /// <param name="response">The parsed structured response.</param>
  /// <param name="requireSpeakerTranslated">
  ///     Whether a translated speaker field is
  ///     required for the payload to be considered valid.
  /// </param>
  /// <returns>The structured validation result.</returns>
  public static StructuredDialogueResponseValidationResult Validate(
      StructuredDialogueTranslationResponse response,
      bool requireSpeakerTranslated = false)
  {
    if (!TranslationResultGuard.IsPersistableTranslation(response.TextTranslated))
    {
      return new StructuredDialogueResponseValidationResult(
          false,
          null,
          MissingStructuredTextFailureReason);
    }

    if (requireSpeakerTranslated &&
        string.IsNullOrWhiteSpace(response.SpeakerTranslated))
    {
      return new StructuredDialogueResponseValidationResult(
          false,
          null,
          MissingStructuredSpeakerFailureReason);
    }

    return new StructuredDialogueResponseValidationResult(
        true,
        response,
        null);
  }
}
