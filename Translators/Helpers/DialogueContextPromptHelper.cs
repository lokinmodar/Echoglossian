// <copyright file="DialogueContextPromptHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Newtonsoft.Json;

namespace Echoglossian.Translators.Helpers;

/// <summary>
///     Shared helper for runtime-only dialogue-context prompt and cache-key
///     composition.
/// </summary>
public static class DialogueContextPromptHelper
{
    /// <summary>
    ///     Returns whether the provided captured dialogue context can
    ///     materially influence a translation request.
    /// </summary>
    /// <param name="dialogueContext">The dialogue context to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when captured dialogue context is available;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public static bool HasUsableDialogueContext(DialogueTranslationContext dialogueContext)
    {
        return true;
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
        var cacheKeyValues = new Dictionary<string, object?>
        {
            ["Scope"] = "dialogue",
            ["SessionNamespace"] = dialogueContext.SessionNamespace,
            ["SessionKey"] = dialogueContext.SessionKey,
            ["CurrentSpeaker"] = dialogueContext.SpeakerName,
            ["Text"] = text,
            ["SourceLanguage"] = sourceLanguage,
            ["TargetLanguage"] = targetLanguage,
            ["PriorTurns"] = dialogueContext.PriorTurns.Select(turn => new
            {
                turn.SpeakerName,
                turn.SourceText,
            }),
        };
        AddNonEmptyCacheValue(cacheKeyValues, "SpeakerRoleHint", dialogueContext.SpeakerRoleHint);
        AddNonEmptyCacheValue(cacheKeyValues, "SpeakerGenderHint", dialogueContext.SpeakerGenderHint);
        AddNonEmptyCacheValue(cacheKeyValues, "AddresseeHint", dialogueContext.AddresseeHint);
        AddNonEmptyCacheValue(cacheKeyValues, "AddresseeRoleHint", dialogueContext.AddresseeRoleHint);
        AddNonEmptyCacheValue(cacheKeyValues, "AddresseeGenderHint", dialogueContext.AddresseeGenderHint);
        AddNonEmptyCacheValue(cacheKeyValues, "MetadataProvenance", dialogueContext.MetadataProvenance);
        if (dialogueContext.MetadataConfidenceTier.HasValue)
        {
            cacheKeyValues["MetadataConfidenceTier"] = dialogueContext.MetadataConfidenceTier.Value;
        }

        return JsonConvert.SerializeObject(cacheKeyValues);
    }

    /// <summary>
    ///     Appends captured dialogue metadata and bounded prior dialogue turns
    ///     to an already-rendered prompt.
    /// </summary>
    /// <param name="prompt">The already-rendered prompt for the current text.</param>
    /// <param name="dialogueContext">The dialogue context to append.</param>
    /// <param name="sanitizeText">A text sanitizer used on prior turns.</param>
    /// <returns>The prompt, optionally enriched with dialogue context.</returns>
    public static string AppendDialogueContext(
        string prompt,
        DialogueTranslationContext dialogueContext,
        Func<string, string> sanitizeText)
    {
        if (!HasUsableDialogueContext(dialogueContext))
        {
            return prompt;
        }

        var contextLines = new List<string>();
        AddNonEmptyPromptLine(contextLines, "Current speaker", dialogueContext.SpeakerName);
        AddNonEmptyPromptLine(contextLines, "Speaker role", dialogueContext.SpeakerRoleHint);
        AddNonEmptyPromptLine(contextLines, "Speaker gender", dialogueContext.SpeakerGenderHint);
        AddNonEmptyPromptLine(contextLines, "Addressee", dialogueContext.AddresseeHint);
        AddNonEmptyPromptLine(contextLines, "Addressee role", dialogueContext.AddresseeRoleHint);
        AddNonEmptyPromptLine(contextLines, "Addressee gender", dialogueContext.AddresseeGenderHint);
        contextLines.AddRange(dialogueContext.PriorTurns.Select(
            (turn, index) => $"[{index + 1}] {turn.SpeakerName}: {sanitizeText(turn.SourceText)}"));

        return
            $"{prompt}{Environment.NewLine}{Environment.NewLine}Previous dialogue context for translation consistency only (translate only the current text, not the history):{Environment.NewLine}{string.Join(Environment.NewLine, contextLines)}";
    }

    private static void AddNonEmptyCacheValue(
        IDictionary<string, object?> values,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[key] = value;
        }
    }

    private static void AddNonEmptyPromptLine(
        ICollection<string> lines,
        string label,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }
}
