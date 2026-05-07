// <copyright file="TranslationPersistenceGuard.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
/// Provides shared guards for deciding whether translated results and
/// translation failures are safe to persist or reuse.
/// </summary>
internal static class TranslationPersistenceGuard
{
  /// <summary>
  /// Determines whether a translation failure reason is stable enough to
  /// persist across sessions.
  /// </summary>
  /// <param name="failureReason">The recorded failure reason.</param>
  /// <returns>
  /// <c>true</c> when the failure reason should be persisted; otherwise,
  /// <c>false</c>.
  /// </returns>
  public static bool IsPersistentFailureReason(string? failureReason)
  {
    return !string.IsNullOrWhiteSpace(failureReason) &&
           !string.Equals(
               failureReason,
               "empty-result",
               StringComparison.Ordinal) &&
           !string.Equals(
               failureReason,
               TranslationResultGuard.SyntheticErrorFailureReason,
               StringComparison.Ordinal);
  }

  /// <summary>
  /// Determines whether a translated dialogue text is safe to reuse or save.
  /// </summary>
  /// <param name="originalText">The original source text.</param>
  /// <param name="translatedText">The translated text candidate.</param>
  /// <param name="originalLanguage">The original source language.</param>
  /// <param name="translationLanguage">The target translation language.</param>
  /// <returns>
  /// <c>true</c> when the translated text is non-empty, non-synthetic, and not
  /// an unchanged echo of the original across different languages; otherwise,
  /// <c>false</c>.
  /// </returns>
  public static bool IsUsableDialogueTranslation(
      string? originalText,
      string? translatedText,
      string? originalLanguage,
      string? translationLanguage)
  {
    if (!TranslationResultGuard.IsPersistableTranslation(translatedText))
    {
      return false;
    }

    if (string.IsNullOrWhiteSpace(originalText))
    {
      return true;
    }

    if (RuntimeLanguageHelper.LanguagesMatch(
            originalLanguage,
            translationLanguage))
    {
      return true;
    }

    return !string.Equals(
        originalText.Trim(),
        translatedText!.Trim(),
        StringComparison.Ordinal);
  }
}
