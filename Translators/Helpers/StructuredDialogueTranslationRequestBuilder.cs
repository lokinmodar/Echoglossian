// <copyright file="StructuredDialogueTranslationRequestBuilder.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Builds the shared internal structured dialogue request contract from the
///     existing runtime-only dialogue context and operator-facing metadata.
/// </summary>
public static class StructuredDialogueTranslationRequestBuilder
{
  /// <summary>
  ///     Builds one structured dialogue translation request.
  /// </summary>
  /// <param name="textOriginal">The visible source text.</param>
  /// <param name="sourceLanguage">The source-language code or display name.</param>
  /// <param name="targetLanguage">The target-language code or display name.</param>
  /// <param name="surfaceGroup">The coarse translation surface family.</param>
  /// <param name="dialogueContext">The optional runtime-only dialogue context.</param>
  /// <param name="glossary">Optional glossary rows to inject.</param>
  /// <param name="questNameOriginal">Optional quest-name metadata.</param>
  /// <param name="speakerRoleHint">Optional speaker-role hint.</param>
  /// <param name="pronounHint">Optional pronoun hint.</param>
  /// <param name="subjectHint">Optional omitted-subject hint.</param>
  /// <returns>The constructed structured dialogue request.</returns>
  public static StructuredDialogueTranslationRequest Build(
      string textOriginal,
      string sourceLanguage,
      string targetLanguage,
      TranslationSurfaceGroup surfaceGroup,
      DialogueTranslationContext? dialogueContext = null,
      IReadOnlyList<StructuredDialogueGlossaryEntry>? glossary = null,
      string? questNameOriginal = null,
      string? speakerRoleHint = null,
      string? pronounHint = null,
      string? subjectHint = null)
  {
    string normalizedSpeakerOriginal = dialogueContext?.SpeakerName ?? string.Empty;
    IReadOnlyList<StructuredDialogueContextTurn> dialogueTurns = dialogueContext?.PriorTurns
        .Select(static turn => new StructuredDialogueContextTurn(
            turn.SpeakerName ?? string.Empty,
            turn.SourceText ?? string.Empty))
        .ToList() ?? [];
    IReadOnlyList<StructuredDialogueGlossaryEntry> glossaryRows =
        glossary?.ToList() ?? [];
    string? resolvedSpeakerRoleHint = dialogueContext?.SpeakerRoleHint ?? speakerRoleHint;

    return new StructuredDialogueTranslationRequest(
        sourceLanguage ?? string.Empty,
        targetLanguage ?? string.Empty,
        MapSurfaceFamily(surfaceGroup),
        normalizedSpeakerOriginal,
        textOriginal ?? string.Empty,
        dialogueTurns,
        glossaryRows,
        new StructuredDialogueTranslationMetadata(
            questNameOriginal,
            normalizedSpeakerOriginal,
            resolvedSpeakerRoleHint,
            dialogueContext?.SpeakerGenderHint,
            dialogueContext?.AddresseeHint,
            dialogueContext?.AddresseeRoleHint,
            dialogueContext?.AddresseeGenderHint,
            dialogueContext?.MetadataProvenance,
            dialogueContext?.MetadataConfidenceTier,
            pronounHint,
            subjectHint));
  }

  /// <summary>
  ///     Maps one coarse translation surface group to the stable structured
  ///     family label used by the internal request contract.
  /// </summary>
  /// <param name="surfaceGroup">The coarse translation surface group.</param>
  /// <returns>The stable structured surface-family label.</returns>
  private static string MapSurfaceFamily(TranslationSurfaceGroup surfaceGroup)
  {
    return surfaceGroup switch
    {
      TranslationSurfaceGroup.Dialogue => "Dialogue",
      _ => "Default",
    };
  }
}
