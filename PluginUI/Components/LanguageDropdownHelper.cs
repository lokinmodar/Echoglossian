// <copyright file="LanguageDropdownHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Components;

/// <summary>
///     Provides helper methods for rendering a language dropdown in the UI.
/// </summary>
public static class LanguageDropdownHelper
{
    private static List<LanguageEntry>? _cachedSortedLanguages;
    private static string[]? _cachedDisplayNames;

    /// <summary>
    ///     Builds or reuses the sorted language list based on the provided dictionary.
    /// </summary>
    /// <param name="languagesDictionary">
    ///     A dictionary where the key is the original index of the language, and the
    ///     value is a <see cref="LanguageInfo" /> object containing details about the
    ///     language.
    /// </param>
    public static void Initialize(
        Dictionary<int, LanguageInfo> languagesDictionary)
    {
        _cachedSortedLanguages = languagesDictionary
            .Select(kv => new LanguageEntry
                { OriginalIndex = kv.Key, Name = kv.Value.LanguageName })
            .OrderBy(entry => entry.Name).ToList();

        _cachedDisplayNames =
            _cachedSortedLanguages.Select(l => l.Name).ToArray();
    }

    /// <summary>
    ///     Returns the language display names for UI rendering.
    /// </summary>
    /// <returns>An array of strings representing the display names of the languages.</returns>
    public static string[] GetDisplayNames()
    {
        return _cachedDisplayNames ?? Array.Empty<string>();
    }

    /// <summary>
    ///     Returns the sorted index for a given original language index.
    /// </summary>
    /// <param name="originalIndex">
    ///     The original index of the language in the unsorted
    ///     list.
    /// </param>
    /// <returns>The index of the language in the sorted list, or 0 if not found.</returns>
    public static int MapOriginalToSorted(int originalIndex)
    {
        if (_cachedSortedLanguages == null)
        {
            return originalIndex;
        }

        var found =
            _cachedSortedLanguages.FindIndex(e =>
                e.OriginalIndex == originalIndex);
        return found >= 0 ? found : 0;
    }

    /// <summary>
    ///     Returns the original index for a selected sorted dropdown index.
    /// </summary>
    /// <param name="sortedIndex">
    ///     The index of the language in the sorted dropdown
    ///     list.
    /// </param>
    /// <returns>The original index corresponding to the sorted dropdown index.</returns>
    public static int MapSortedToOriginal(int sortedIndex)
    {
        if (_cachedSortedLanguages == null || sortedIndex < 0 ||
            sortedIndex >= _cachedSortedLanguages.Count)
        {
            return 0;
        }

        return _cachedSortedLanguages[sortedIndex].OriginalIndex;
    }

    /// <summary>
    ///     Draws the language dropdown using ImGui, returning true if the value
    ///     changed.
    /// </summary>
    /// <param name="selectedOriginalIndex">
    ///     The original index of the selected
    ///     language. This value will be updated if the selection changes.
    /// </param>
    /// <param name="label">The label to display next to the dropdown in the UI.</param>
    /// <returns>True if the selected language index changed; otherwise, false.</returns>
    public static bool DrawLanguageDropdown(
        ref int selectedOriginalIndex,
        string? label = null)
    {
        if (_cachedSortedLanguages == null || _cachedDisplayNames == null)
        {
            return false;
        }

        label ??= Resources.LanguageSelectLabelText;

        var currentSortedIndex = MapOriginalToSorted(selectedOriginalIndex);
        var changed = ImGui.Combo(
            label,
            ref currentSortedIndex,
            _cachedDisplayNames,
            _cachedDisplayNames.Length);

        if (changed)
        {
            selectedOriginalIndex = MapSortedToOriginal(currentSortedIndex);
        }

        return changed;
    }

    public sealed class LanguageEntry
    {
        public int OriginalIndex { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
