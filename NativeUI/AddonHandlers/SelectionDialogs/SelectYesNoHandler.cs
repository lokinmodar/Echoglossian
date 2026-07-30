// <copyright file="SelectYesNoHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

/// <summary>
///     Handles the generic yes/no dialog runtime.
/// </summary>
public sealed class SelectYesNoHandler : SelectionDialogHandlerBase
{
    private readonly Func<SelectionDialogText, SelectionDialogText?>
        findSelectionDialogText;
    private readonly Func<SelectionDialogText, Task<string>>
        insertSelectionDialogTextAsync;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SelectYesNoHandler" />
    ///     class.
    /// </summary>
    public SelectYesNoHandler(
        Config config,
        TranslationService translationService,
        HoverTooltipManager hoverTooltipManager,
        Func<SelectionDialogText, SelectionDialogText?> findSelectionDialogText,
        Func<SelectionDialogText, Task<string>> insertSelectionDialogTextAsync,
        Func<string, string> normalizeReplacementText)
        : base(
            "SelectYesno",
            config,
            translationService,
            hoverTooltipManager,
            () => config.TranslateYesNoScreen,
            () => config.SelectYesNoTranslationDisplayMode,
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
}
