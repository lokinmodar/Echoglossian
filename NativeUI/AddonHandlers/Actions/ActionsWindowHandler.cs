// <copyright file="ActionsWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian.NativeUI.AddonHandlers.Actions;

/// <summary>
///     Handles DB-first translation for the <c>Actions</c> addon while
///     reusing canonical action-name and description translations that already
///     exist in <c>ActionTooltip</c> storage.
/// </summary>
public unsafe class ActionsWindowHandler : DbFirstGameWindowAddonHandler
{
    private readonly Config config;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="ActionsWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The shared translation service.</param>
    public ActionsWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "Actions",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateActionsWindow,
            useAtkValues: true,
            useTextNodes: true,
            displayModeSelector: static configuration =>
                configuration.ActionsWindowTranslationDisplayMode)
    {
        this.config = config;
    }

    /// <inheritdoc />
    protected override bool ShouldReuseCompatiblePayloads()
    {
        return false;
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return textNode != null &&
               !string.IsNullOrWhiteSpace(visibleText);
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        var targetLanguage = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
            this.config.Lang);
        var gameVersion = GetGameVersion();
        var changed = false;

        var translatedAtkValues = TranslateIntMap(
            originalPayload.AtkValues,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            ref changed);
        var translatedStringArrayValues = TranslateIntMap(
            originalPayload.StringArrayValues,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            ref changed);
        var translatedTextNodes = TranslateStringMap(
            originalPayload.TextNodes,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            ref changed);

        if (!changed)
        {
            translatedPayload = DbFirstGameWindowPayload.Empty;
            return false;
        }

        translatedPayload = new DbFirstGameWindowPayload(
            translatedAtkValues,
            translatedStringArrayValues,
            translatedTextNodes);
        return true;
    }

    /// <summary>
    ///     Translates one integer-keyed payload map by exact lookup against the
    ///     canonical action-tooltip cache.
    /// </summary>
    /// <param name="sourceValues">The original values.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="changed">
    ///     Receives whether any translated value differs from the original.
    /// </param>
    /// <returns>The translated map.</returns>
    private static SortedDictionary<int, string> TranslateIntMap(
        SortedDictionary<int, string> sourceValues,
        string targetLanguage,
        int engine,
        string? gameVersion,
        ref bool changed)
    {
        var translatedValues = new SortedDictionary<int, string>();

        foreach (var (key, originalText) in sourceValues)
        {
            var translatedText = ResolveTranslatedText(
                originalText,
                targetLanguage,
                engine,
                gameVersion);
            if (!string.Equals(
                    translatedText,
                    originalText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            translatedValues[key] = translatedText;
        }

        return translatedValues;
    }

    /// <summary>
    ///     Translates one text-node payload map by exact lookup against the
    ///     canonical action-tooltip cache.
    /// </summary>
    /// <param name="sourceValues">The original values.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="changed">
    ///     Receives whether any translated value differs from the original.
    /// </param>
    /// <returns>The translated map.</returns>
    private static SortedDictionary<string, string> TranslateStringMap(
        SortedDictionary<string, string> sourceValues,
        string targetLanguage,
        int engine,
        string? gameVersion,
        ref bool changed)
    {
        var translatedValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var (key, originalText) in sourceValues)
        {
            var translatedText = ResolveTranslatedText(
                originalText,
                targetLanguage,
                engine,
                gameVersion);
            if (!string.Equals(
                    translatedText,
                    originalText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            translatedValues[key] = translatedText;
        }

        return translatedValues;
    }

    /// <summary>
    ///     Resolves one translated action text from canonical tooltip storage,
    ///     falling back to the original text when no exact translation exists.
    /// </summary>
    /// <param name="originalText">The original visible text.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <returns>The translated text, or the original text.</returns>
    private static string ResolveTranslatedText(
        string originalText,
        string targetLanguage,
        int engine,
        string? gameVersion)
    {
        return ActionTooltipCacheManager.TryFindTranslatedText(
            targetLanguage,
            engine,
            gameVersion,
            originalText,
            out var translatedText)
            ? translatedText
            : originalText;
    }
}
