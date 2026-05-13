// <copyright file="DialogueContextPromptHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Shared helper for runtime-only dialogue-context prompt and cache-key
///     composition.
/// </summary>
public static class DialogueContextPromptHelper
{
    /// <summary>
    ///     Returns whether the provided dialogue context has prior turns that
    ///     can materially influence a translation request.
    /// </summary>
    /// <param name="dialogueContext">The dialogue context to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when prior turns are available;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool HasUsableDialogueContext(DialogueTranslationContext dialogueContext)
    {
        return dialogueContext.PriorTurns.Count > 0;
    }

    /// <summary>
    ///     Builds a distinct cache key for a dialogue translation that depends
    ///     on prior runtime-only context.
    /// </summary>
    /// <param name="text">The current source text.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="dialogueContext">The dialogue context influencing the request.</param>
    /// <returns>A context-aware cache key.</returns>
    public static string BuildDialogueContextCacheKey(
        string text,
        string sourceLanguage,
        string targetLanguage,
        DialogueTranslationContext dialogueContext)
    {
        string historyKey = string.Join(
            "|",
            dialogueContext.PriorTurns.Select(
                turn => $"{turn.SpeakerName}:{turn.SourceText}"));

        return
            $"dialogue|{dialogueContext.SessionNamespace}|{dialogueContext.SessionKey}|{historyKey}|{text}_{sourceLanguage}_{targetLanguage}";
    }

    /// <summary>
    ///     Appends bounded prior dialogue turns to an already-rendered prompt.
    /// </summary>
    /// <param name="prompt">The already-rendered prompt for the current text.</param>
    /// <param name="dialogueContext">The dialogue context to append.</param>
    /// <param name="sanitizeText">A text sanitizer used on prior turns.</param>
    /// <returns>The prompt, optionally enriched with prior-turn context.</returns>
    public static string AppendDialogueContext(
        string prompt,
        DialogueTranslationContext dialogueContext,
        Func<string, string> sanitizeText)
    {
        if (!HasUsableDialogueContext(dialogueContext))
        {
            return prompt;
        }

        string priorTurns = string.Join(
            Environment.NewLine,
            dialogueContext.PriorTurns.Select(
                (turn, index) =>
                    $"[{index + 1}] {turn.SpeakerName}: {sanitizeText(turn.SourceText)}"));

        return
            $"{prompt}{Environment.NewLine}{Environment.NewLine}Previous dialogue context for translation consistency only (translate only the current text, not the history):{Environment.NewLine}Current speaker: {dialogueContext.SpeakerName}{Environment.NewLine}{priorTurns}";
    }
}
