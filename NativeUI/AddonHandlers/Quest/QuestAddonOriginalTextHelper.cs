// <copyright file="QuestAddonOriginalTextHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Resolves whether a quest-addon row should keep using the previously
///     captured original text or switch to the currently visible text.
/// </summary>
internal static class QuestAddonOriginalTextHelper
{
  /// <summary>
  ///     Resolves the original text for a visible quest-addon row while
  ///     allowing stable recovery from previously applied translated text.
  /// </summary>
  /// <param name="visibleText">The text currently visible in the addon.</param>
  /// <param name="previousOriginalText">The last captured original text.</param>
  /// <param name="previousTranslatedDisplayText">
  ///     The translated text exactly as it would have been written into the
  ///     native UI for the previous row state.
  /// </param>
  /// <returns>
  ///     The original source text that should back the current visible row.
  /// </returns>
  public static string ResolveOriginalVisibleText(
      string visibleText,
      string? previousOriginalText,
      string? previousTranslatedDisplayText)
  {
    if (string.IsNullOrWhiteSpace(previousOriginalText))
    {
      return visibleText;
    }

    if (string.Equals(
            visibleText,
            previousOriginalText,
            StringComparison.Ordinal))
    {
      return previousOriginalText;
    }

    if (!string.IsNullOrWhiteSpace(previousTranslatedDisplayText) &&
        string.Equals(
            visibleText,
            previousTranslatedDisplayText,
            StringComparison.Ordinal))
    {
      return previousOriginalText;
    }

    return visibleText;
  }
}
