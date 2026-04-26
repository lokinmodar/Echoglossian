// <copyright file="CharacterClassSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles translation for the "CharacterClass" addon using visible text
///     nodes only.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public unsafe class CharacterClassSubWindowHandler
    : CharacterTextNodeWindowHandlerBase
{
    private const int MinimumStableClassJobCount = 6;
    private const string ClassJobTextNodeKeyPrefix = "3:";

    private readonly PayloadStabilityTracker newPayloadStabilityTracker = new(
        minimumObservations: 2,
        minimumStableDuration: TimeSpan.FromMilliseconds(150));

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterClassSubWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterClassSubWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "CharacterClass",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService)
    {
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return base.ShouldCaptureTextNode(textNode, visibleText) ||
               this.CanCaptureSupplementalCharacterText(visibleText);
    }

    /// <inheritdoc />
    protected override bool ShouldReuseCompatiblePayloads()
    {
        return false;
    }

    /// <inheritdoc />
    private protected override bool ShouldPersistNewGameWindowPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return this.ShouldAllowNewPayloadPersistence(originalPayload);
    }

    /// <inheritdoc />
    private protected override bool ShouldQueueNewGameWindowTranslation(
        DbFirstGameWindowPayload originalPayload)
    {
        return this.ShouldAllowNewPayloadPersistence(originalPayload);
    }

    /// <inheritdoc />
    protected override bool ShouldRefreshAppliedStateOnPreDraw()
    {
        return false;
    }

    /// <inheritdoc />
    protected override void OnCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        base.OnCleanupEvent(evt, args);
        this.newPayloadStabilityTracker.Reset();
    }

    /// <inheritdoc />
    private protected override bool TryApplyCustomTextNodePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        return this.ApplyVisibleTextNodesByValue(
            addon,
            sourcePayload,
            targetPayload);
    }

    /// <summary>
    ///     Determines whether the current Class payload is stable enough to
    ///     justify creating one persisted shape or queueing one fresh remote
    ///     translation.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload is stable enough to create
    ///     one canonical row; otherwise <see langword="false" />.
    /// </returns>
    private bool ShouldAllowNewPayloadPersistence(
        DbFirstGameWindowPayload originalPayload)
    {
        if (!this.HasSufficientClassCoverage(originalPayload))
        {
            return false;
        }

        if (!this.HasPersistedClassRows())
        {
            return this.newPayloadStabilityTracker.Observe(
                originalPayload.Serialize(),
                DateTime.UtcNow);
        }

        if (!this.HasMeaningfulUnseenClassTexts(originalPayload))
        {
            return false;
        }

        return this.newPayloadStabilityTracker.Observe(
            originalPayload.Serialize(),
            DateTime.UtcNow);
    }

    /// <summary>
    ///     Determines whether one Class payload already contains enough stable
    ///     job rows to justify persisting it as one canonical shape.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload contains enough distinct
    ///     Class job entries to be useful as one canonical row; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private bool HasSufficientClassCoverage(
        DbFirstGameWindowPayload originalPayload)
    {
        return CharacterCanonicalPayloadHelper
                   .CountDistinctTextValuesWithKeyPrefix(
                       originalPayload.TextNodes,
                       ClassJobTextNodeKeyPrefix) >=
               MinimumStableClassJobCount;
    }

    /// <summary>
    ///     Determines whether one persisted Class row already exists for the
    ///     current language and engine scope.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when at least one persisted row exists;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool HasPersistedClassRows()
    {
        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.HandlerConfig.Lang);
        return GameWindowCacheManager.GetCandidates(
                this.AddonName,
                targetLanguage,
                this.HandlerConfig.ChosenTransEngine,
                GetGameVersion())
            .Any();
    }

    /// <summary>
    ///     Determines whether the current Class payload introduces enough new
    ///     stable text to justify expanding the canonical bank.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload contains a meaningful set
    ///     of previously unseen texts; otherwise <see langword="false" />.
    /// </returns>
    private bool HasMeaningfulUnseenClassTexts(
        DbFirstGameWindowPayload originalPayload)
    {
        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.HandlerConfig.Lang);
        var knownTexts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in GameWindowCacheManager.GetCandidates(
                     this.AddonName,
                     targetLanguage,
                     this.HandlerConfig.ChosenTransEngine,
                     GetGameVersion()))
        {
            if (!TryParseSerializedPayload(
                    row.OriginalWindowStrings,
                    out var persistedPayload))
            {
                continue;
            }

            foreach (var text in persistedPayload.TextNodes.Values)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    knownTexts.Add(text);
                }
            }
        }

        return CharacterCanonicalPayloadHelper.CountUnseenTextValues(
                   originalPayload.TextNodes,
                   knownTexts) >= 3;
    }
}
