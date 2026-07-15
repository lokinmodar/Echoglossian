// <copyright file="CanonicalTextLookupSnapshot.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Cache;

/// <summary>
///     Holds one immutable exact-text lookup snapshot for canonical
///     action-adjacent content.
/// </summary>
internal sealed class CanonicalTextLookupSnapshot
{
    /// <summary>
    ///     Gets the empty canonical lookup snapshot.
    /// </summary>
    public static CanonicalTextLookupSnapshot Empty { get; } =
        new(
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly Dictionary<string, string> forwardLookup;
    private readonly HashSet<string> originalTexts;
    private readonly Dictionary<string, string> reverseLookup;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CanonicalTextLookupSnapshot" /> class.
    /// </summary>
    /// <param name="originalTexts">
    ///     The original canonical texts available in this snapshot.
    /// </param>
    /// <param name="forwardLookup">
    ///     The original-to-translated exact-text lookup.
    /// </param>
    /// <param name="reverseLookup">
    ///     The translated-to-original exact-text lookup.
    /// </param>
    public CanonicalTextLookupSnapshot(
        HashSet<string> originalTexts,
        Dictionary<string, string> forwardLookup,
        Dictionary<string, string> reverseLookup)
    {
        this.originalTexts = originalTexts;
        this.forwardLookup = forwardLookup;
        this.reverseLookup = reverseLookup;
    }

    /// <summary>
    ///     Gets the exact original-to-translated lookup.
    /// </summary>
    public IReadOnlyDictionary<string, string> ForwardLookup => this.forwardLookup;

    /// <summary>
    ///     Gets the exact canonical original-text set.
    /// </summary>
    public IReadOnlySet<string> OriginalTexts => this.originalTexts;

    /// <summary>
    ///     Gets the exact translated-to-original lookup.
    /// </summary>
    public IReadOnlyDictionary<string, string> ReverseLookup => this.reverseLookup;

    /// <summary>
    ///     Combines multiple canonical lookup snapshots while preserving
    ///     lookup precedence and reverse-lookup ambiguity safety.
    /// </summary>
    /// <param name="snapshots">The snapshots to merge in precedence order.</param>
    /// <returns>The combined snapshot.</returns>
    public static CanonicalTextLookupSnapshot Combine(
        params CanonicalTextLookupSnapshot[] snapshots)
    {
        if (snapshots == null || snapshots.Length == 0)
        {
            return Empty;
        }

        if (snapshots.Length == 1)
        {
            return snapshots[0] ?? Empty;
        }

        var originalTexts = new HashSet<string>(StringComparer.Ordinal);
        var forwardLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var reverseLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguousReverseKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var snapshot in snapshots)
        {
            if (snapshot == null)
            {
                continue;
            }

            originalTexts.UnionWith(snapshot.originalTexts);

            foreach (var (originalText, translatedText) in snapshot.forwardLookup)
            {
                forwardLookup.TryAdd(originalText, translatedText);
            }

            foreach (var (translatedText, originalText) in snapshot.reverseLookup)
            {
                if (ambiguousReverseKeys.Contains(translatedText))
                {
                    continue;
                }

                if (reverseLookup.TryGetValue(
                        translatedText,
                        out var existingOriginal) &&
                    !string.Equals(
                        existingOriginal,
                        originalText,
                        StringComparison.Ordinal))
                {
                    reverseLookup.Remove(translatedText);
                    ambiguousReverseKeys.Add(translatedText);
                    continue;
                }

                reverseLookup[translatedText] = originalText;
            }
        }

        return new CanonicalTextLookupSnapshot(
            originalTexts,
            forwardLookup,
            reverseLookup);
    }

    /// <summary>
    ///     Determines whether one exact canonical original text exists in this
    ///     snapshot.
    /// </summary>
    /// <param name="originalText">The original text to test.</param>
    /// <returns>
    ///     <see langword="true" /> when the original text exists; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public bool ContainsOriginalText(string originalText)
    {
        return !string.IsNullOrWhiteSpace(originalText) &&
               this.originalTexts.Contains(originalText);
    }

    /// <summary>
    ///     Tries to resolve one exact canonical original text from translated
    ///     text in this snapshot.
    /// </summary>
    /// <param name="translatedText">The translated text to reverse.</param>
    /// <param name="originalText">The resolved original text.</param>
    /// <returns>
    ///     <see langword="true" /> when one exact canonical original text was
    ///     found; otherwise <see langword="false" />.
    /// </returns>
    public bool TryFindOriginalText(
        string translatedText,
        out string originalText)
    {
        originalText = string.Empty;
        if (!string.IsNullOrWhiteSpace(translatedText) &&
            this.reverseLookup.TryGetValue(
                translatedText,
                out var resolvedOriginalText))
        {
            originalText = resolvedOriginalText;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Tries to resolve one exact translated text from original text in
    ///     this snapshot.
    /// </summary>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when one exact translated text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    public bool TryFindTranslatedText(
        string originalText,
        out string translatedText)
    {
        translatedText = string.Empty;
        if (!string.IsNullOrWhiteSpace(originalText) &&
            this.forwardLookup.TryGetValue(
                originalText,
                out var resolvedTranslatedText))
        {
            translatedText = resolvedTranslatedText;
            return true;
        }

        return false;
    }
}
