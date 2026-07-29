// <copyright file="SelectStringHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

/// <summary>
///     Handles the generic select-string dialog runtime.
/// </summary>
public sealed class SelectStringHandler : SelectionDialogHandlerBase
{
    private readonly Func<SelectionDialogText, SelectionDialogText?>
        findSelectionDialogText;
    private readonly Func<SelectString, SelectString?> findSelectString;
    private readonly Func<SelectionDialogText, Task<string>>
        insertSelectionDialogTextAsync;
    private readonly Func<SelectString, Task<string>> insertSelectStringAsync;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectStringHandler" />
    ///     class.
    /// </summary>
    public SelectStringHandler(
        Config config,
        TranslationService translationService,
        Func<SelectString, SelectString?> findSelectString,
        Func<SelectString, Task<string>> insertSelectStringAsync,
        Func<SelectionDialogText, SelectionDialogText?> findSelectionDialogText,
        Func<SelectionDialogText, Task<string>> insertSelectionDialogTextAsync,
        Action<string, string, string> updateOverlay,
        Action clearOverlay,
        SyncSelectionDialogOverlayBoundsDelegate syncOverlayBounds,
        Func<string, string> normalizeReplacementText)
        : base(
            "SelectString",
            config,
            translationService,
            () => config.TranslateSelectString,
            () => config.SelectStringTranslationDisplayMode,
            updateOverlay,
            clearOverlay,
            syncOverlayBounds,
            normalizeReplacementText)
    {
        this.findSelectString = findSelectString;
        this.insertSelectStringAsync = insertSelectStringAsync;
        this.findSelectionDialogText = findSelectionDialogText;
        this.insertSelectionDialogTextAsync = insertSelectionDialogTextAsync;
    }

    /// <inheritdoc />
    protected override bool TryFindStoredTranslation(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        out List<string> translatedTexts)
    {
        if (CanPersistAsSelectString(originalTexts))
        {
            var lookup = this.BuildSelectStringLookup(sourceLanguage, originalTexts);
            var foundSelectString = lookup != null ? this.findSelectString(lookup) : null;
            if (foundSelectString != null &&
                !string.IsNullOrWhiteSpace(foundSelectString.TranslatedSelectString))
            {
                var storedTexts = new List<string>
                {
                    foundSelectString.TranslatedSelectString!,
                };
                storedTexts.AddRange(DeserializeOptions(foundSelectString.TranslatedOptionsAsText));
                if (storedTexts.Count == originalTexts.Count)
                {
                    translatedTexts = storedTexts;
                    return true;
                }
            }
        }

        var genericLookup = this.BuildSelectionDialogLookup(sourceLanguage, originalTexts);
        var foundSelectionDialog = genericLookup != null
            ? this.findSelectionDialogText(genericLookup)
            : null;
        if (foundSelectionDialog == null ||
            foundSelectionDialog.TranslatedTexts.Count != originalTexts.Count)
        {
            translatedTexts = [];
            return false;
        }

        translatedTexts = [.. foundSelectionDialog.TranslatedTexts];
        return true;
    }

    /// <inheritdoc />
    protected override Task<string> PersistTranslationAsync(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> translatedTexts)
    {
        if (CanPersistAsSelectString(originalTexts))
        {
            var selectStringRow = this.BuildSelectStringRow(
                sourceLanguage,
                originalTexts,
                translatedTexts);
            if (selectStringRow != null)
            {
                return this.insertSelectStringAsync(selectStringRow);
            }
        }

        var genericRow = this.BuildSelectionDialogRow(
            sourceLanguage,
            originalTexts,
            translatedTexts);
        return genericRow == null
            ? Task.FromResult("No data to save.")
            : this.insertSelectionDialogTextAsync(genericRow);
    }
}
