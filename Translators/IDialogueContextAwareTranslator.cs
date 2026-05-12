// <copyright file="IDialogueContextAwareTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Optional translator extension point for engines that can consume
///     runtime-only short-lived dialogue context.
/// </summary>
public interface IDialogueContextAwareTranslator
{
  /// <summary>
  ///     Translates the given text with access to runtime-only short-lived
  ///     dialogue context.
  /// </summary>
  /// <param name="text">The text to translate.</param>
  /// <param name="sourceLanguage">The source language display name.</param>
  /// <param name="targetLanguage">The target language code or display name.</param>
  /// <param name="dialogueContext">The runtime-only short-lived dialogue context.</param>
  /// <returns>A task that resolves to the translated text.</returns>
  Task<string?> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage,
      DialogueTranslationContext dialogueContext);
}
