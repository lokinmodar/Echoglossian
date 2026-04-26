// <copyright file="CharacterReputeSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles translation for the "CharacterRepute" addon using visible text
///     nodes only.
///     Lifecycle-safe: extracts and applies values within valid memory scope per
///     frame.
/// </summary>
public unsafe class CharacterReputeSubWindowHandler
    : CharacterTextNodeWindowHandlerBase
{
    private readonly PayloadStabilityTracker newPayloadStabilityTracker = new(
        minimumObservations: 2,
        minimumStableDuration: TimeSpan.FromMilliseconds(150));

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterReputeSubWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterReputeSubWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "CharacterRepute",
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
    protected override SortedDictionary<string, string> NormalizeCapturedTextNodes(
        SortedDictionary<string, string> capturedTextNodes)
    {
        return CharacterCanonicalPayloadHelper.CollapseDuplicateTextValues(
            capturedTextNodes);
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

    /// <inheritdoc />
    protected override void OnCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        base.OnCleanupEvent(evt, args);
        this.newPayloadStabilityTracker.Reset();
    }

    /// <summary>
    ///     Determines whether the current Repute payload is stable enough to
    ///     justify creating one new persisted shape or queueing fresh remote
    ///     translation.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload is stable enough to create
    ///     one new persisted shape; otherwise <see langword="false" />.
    /// </returns>
    private bool ShouldAllowNewPayloadPersistence(
        DbFirstGameWindowPayload originalPayload)
    {
        if (this.HasPersistedReputeRows())
        {
            return false;
        }

        return this.newPayloadStabilityTracker.Observe(
            originalPayload.Serialize(),
            DateTime.UtcNow);
    }

    /// <summary>
    ///     Determines whether one persisted Repute row already exists for the
    ///     current language and engine scope.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> when at least one persisted row exists;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool HasPersistedReputeRows()
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
}
