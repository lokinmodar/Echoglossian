// <copyright file="SelectIconStringHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

/// <summary>
///     Handles the icon-bearing selection dialog runtime.
/// </summary>
public sealed class SelectIconStringHandler : SelectionDialogHandlerBase
{
    private readonly Func<SelectionDialogText, SelectionDialogText?>
        findSelectionDialogText;
    private readonly Func<SelectionDialogText, Task<string>>
        insertSelectionDialogTextAsync;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="SelectIconStringHandler" /> class.
    /// </summary>
    public SelectIconStringHandler(
        Config config,
        TranslationService translationService,
        Func<SelectionDialogText, SelectionDialogText?> findSelectionDialogText,
        Func<SelectionDialogText, Task<string>> insertSelectionDialogTextAsync,
        Action<string, string, string> updateOverlay,
        Action clearOverlay,
        SyncSelectionDialogOverlayBoundsDelegate syncOverlayBounds,
        Func<string, string> normalizeReplacementText)
        : base(
            "SelectIconString",
            config,
            translationService,
            () => config.TranslateSelectString,
            () => config.SelectStringTranslationDisplayMode,
            updateOverlay,
            clearOverlay,
            syncOverlayBounds,
            normalizeReplacementText)
    {
        this.findSelectionDialogText = findSelectionDialogText;
        this.insertSelectionDialogTextAsync = insertSelectionDialogTextAsync;
    }

    /// <inheritdoc />
    protected override bool TryFindStoredTranslation(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        out List<string> translatedTexts)
    {
        var lookup = this.BuildSelectionDialogLookup(sourceLanguage, originalTexts);
        var found = lookup != null ? this.findSelectionDialogText(lookup) : null;
        if (found == null || found.TranslatedTexts.Count != originalTexts.Count)
        {
            translatedTexts = [];
            return false;
        }

        translatedTexts = [.. found.TranslatedTexts];
        return true;
    }

    /// <inheritdoc />
    protected override Task<string> PersistTranslationAsync(
        SourceClientLanguage sourceLanguage,
        IReadOnlyList<string> originalTexts,
        IReadOnlyList<string> translatedTexts)
    {
        var row = this.BuildSelectionDialogRow(
            sourceLanguage,
            originalTexts,
            translatedTexts);
        return row == null
            ? Task.FromResult("No data to save.")
            : this.insertSelectionDialogTextAsync(row);
    }

    /// <inheritdoc />
    protected override bool ShouldPromoteFirstOverlayTextToTitle()
    {
        return false;
    }
}
