// <copyright file="CharacterStatusSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Plugin.Services;
using Echoglossian.Cache;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.NativeUI.AddonHandlers.Common;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Handles DB-first translation for the CharacterStatus subwindow.
/// </summary>
public unsafe class CharacterStatusSubWindowHandler
    : CharacterTextNodeWindowHandlerBase
{
    private static readonly HashSet<string> ExpectedTranslatedSectionTitles =
    [
        "Atributos",
        "Propriedades Ofensivas",
        "Propriedades Defensivas",
        "Propriedades Físicas",
        "Propriedades Mentais",
        "Equipamento",
        "Função",
    ];

    private bool frameworkUpdateRegistered;
    private DateTime nextRequestedUpdateUtc = DateTime.MinValue;
    private bool requestedUpdatePending;
    private bool requestedUpdateInFlight;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterStatusSubWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    public CharacterStatusSubWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "CharacterStatus",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            stringArrayType: StringArrayType.Character,
            useAtkValues: true)
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
    protected override bool ShouldRequestStringArrayUpdates()
    {
        return true;
    }

    /// <inheritdoc />
    private protected override bool ShouldDeferCleanupWhileVisible(
        AddonEvent evt)
    {
        return true;
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
    private protected override bool TryResolveSupplementalOriginalPayload(
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        if (base.TryResolveSupplementalOriginalPayload(
                livePayload,
                out originalPayload))
        {
            return true;
        }

        if (this.TryResolveCanonicalCharacterStatusPayloadPair(
                livePayload,
                out originalPayload,
                out _))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        if (base.TryResolveSupplementalTranslatedPayload(
                originalPayload,
                out translatedPayload))
        {
            return true;
        }

        if (this.TryResolveCanonicalCharacterStatusPayloadPair(
                originalPayload,
                out _,
                out translatedPayload))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    private protected override bool TryResolveProjectedModeSwitchPayloads(
        DbFirstGameWindowPayload livePayload,
        DbFirstGameWindowRuntimeState runtimeState,
        out DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        originalPayload = runtimeState.OriginalPayload.ProjectToShape(
            livePayload);
        translatedPayload = runtimeState.TranslatedPayload.ProjectToShape(
            livePayload);
        if (!this.HasExpectedCharacterStatusCoverage(
                originalPayload,
                translatedPayload))
        {
            originalPayload = DbFirstGameWindowPayload.Empty;
            translatedPayload = DbFirstGameWindowPayload.Empty;
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    private protected override void AfterRestorePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload translatedPayload,
        DbFirstGameWindowPayload originalPayload)
    {
        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.HandlerConfig.CharacterWindowTranslationDisplayMode,
            this.HandlerConfig.OverlayOnlyLanguage);
        if (displayMode != JournalTranslationDisplayMode.TooltipTranslation ||
            addon == null ||
            !addon->IsVisible ||
            addon->Id == 0 ||
            this.requestedUpdatePending ||
            this.requestedUpdateInFlight ||
            DateTime.UtcNow < this.nextRequestedUpdateUtc ||
            (originalPayload.AtkValues.Count == 0 &&
             originalPayload.StringArrayValues.Count == 0))
        {
            return;
        }

        this.requestedUpdatePending = true;
        this.EnsureFrameworkUpdateRegistered();
    }

    /// <inheritdoc />
    public override void OnPluginUnload()
    {
        this.RemoveFrameworkUpdateRegistration();
        this.requestedUpdatePending = false;
        this.requestedUpdateInFlight = false;
        base.OnPluginUnload();
    }

    /// <summary>
    ///     Issues one deferred native request update on the next framework tick
    ///     after tooltip-only restore, avoiding reentrant lifecycle recursion
    ///     from the <c>PreDraw</c> stack.
    /// </summary>
    /// <param name="framework">The active framework service.</param>
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!this.requestedUpdatePending || this.requestedUpdateInFlight)
        {
            return;
        }

        var displayMode = TranslationDisplayModeHelper.GetEffectiveDisplayMode(
            this.HandlerConfig.CharacterWindowTranslationDisplayMode,
            this.HandlerConfig.OverlayOnlyLanguage);
        if (displayMode != JournalTranslationDisplayMode.TooltipTranslation)
        {
            this.requestedUpdatePending = false;
            this.RemoveFrameworkUpdateRegistration();
            return;
        }

        var addon = AtkStage.Instance()->RaptureAtkUnitManager->GetAddonByName(
            this.AddonName);
        if (addon == null || !addon->IsVisible || addon->Id == 0 ||
            !FrameworkAccessGuard.TryGetRaptureAtkUnitManager(
                out var atkUnitManager))
        {
            this.requestedUpdatePending = false;
            this.RemoveFrameworkUpdateRegistration();
            return;
        }

        var atkStage = AtkStage.Instance();
        if (atkStage == null)
        {
            this.requestedUpdatePending = false;
            this.RemoveFrameworkUpdateRegistration();
            return;
        }

        this.requestedUpdatePending = false;
        this.requestedUpdateInFlight = true;
        try
        {
            atkUnitManager->AddonRequestUpdateById(
                addon->Id,
                atkStage->GetNumberArrayData(),
                atkStage->GetStringArrayData(),
                false);
            this.nextRequestedUpdateUtc = DateTime.UtcNow.AddMilliseconds(250);
        }
        finally
        {
            this.requestedUpdateInFlight = false;
            if (!this.requestedUpdatePending)
            {
                this.RemoveFrameworkUpdateRegistration();
            }
        }
    }

    /// <summary>
    ///     Ensures the deferred framework callback is registered only while a
    ///     native update request is pending.
    /// </summary>
    private void EnsureFrameworkUpdateRegistered()
    {
        if (this.frameworkUpdateRegistered)
        {
            return;
        }

        Echoglossian.FrameworkInterface.Update += this.OnFrameworkUpdate;
        this.frameworkUpdateRegistered = true;
    }

    /// <summary>
    ///     Removes the deferred framework callback when no further native
    ///     update pass is required.
    /// </summary>
    private void RemoveFrameworkUpdateRegistration()
    {
        if (!this.frameworkUpdateRegistered)
        {
            return;
        }

        Echoglossian.FrameworkInterface.Update -= this.OnFrameworkUpdate;
        this.frameworkUpdateRegistered = false;
    }

    /// <summary>
    ///     Tries to resolve one canonical original and translated payload pair
    ///     from the latest rich <c>addon:CharacterStatus</c> structured row.
    /// </summary>
    /// <param name="referencePayload">
    ///     The current live payload shape that the canonical row should be
    ///     projected onto.
    /// </param>
    /// <param name="originalPayload">
    ///     Receives the projected original payload.
    /// </param>
    /// <param name="translatedPayload">
    ///     Receives the projected translated payload.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when a rich canonical row was found and
    ///     projected successfully; otherwise <see langword="false" />.
    /// </returns>
    private bool TryResolveCanonicalCharacterStatusPayloadPair(
        DbFirstGameWindowPayload referencePayload,
        out DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;
        translatedPayload = DbFirstGameWindowPayload.Empty;

        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.HandlerConfig.Lang);
        foreach (var row in StringArrayDataCacheManager.GetCandidates(
                     StringArrayType.Character.ToString(),
                     "addon:CharacterStatus",
                     targetLanguage,
                     this.HandlerConfig.ChosenTransEngine,
                     GetGameVersion()).OrderByDescending(candidate => candidate.Id))
        {
            if (!StringArrayStructuredPayloadResolver.TryResolvePayloads(
                    row,
                    out var resolvedOriginalPayload,
                    out var resolvedTranslatedPayload) ||
                resolvedOriginalPayload == null ||
                resolvedTranslatedPayload == null ||
                !DbFirstStructuredStringArrayHelper.TryProjectTranslatedPayload(
                    resolvedOriginalPayload,
                    resolvedTranslatedPayload,
                    out var translatedProjection))
            {
                continue;
            }

            var originalProjection =
                DbFirstStructuredStringArrayHelper.ProjectOriginalPayload(
                    resolvedOriginalPayload);
            var projectedOriginalPayload = new DbFirstGameWindowPayload(
                    originalProjection.AtkValues,
                    originalProjection.StringArrayValues,
                    originalProjection.TextNodes)
                .ProjectToShape(referencePayload);
            var projectedTranslatedPayload = new DbFirstGameWindowPayload(
                    translatedProjection.AtkValues,
                    translatedProjection.StringArrayValues,
                    translatedProjection.TextNodes)
                .ProjectToShape(referencePayload);
            if (!this.HasExpectedCharacterStatusCoverage(
                    projectedOriginalPayload,
                    projectedTranslatedPayload))
            {
                continue;
            }

            originalPayload = projectedOriginalPayload;
            translatedPayload = projectedTranslatedPayload;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Determines whether one projected CharacterStatus payload pair is
    ///     rich enough to be trusted as the latest canonical fallback.
    /// </summary>
    /// <param name="originalPayload">The projected original payload.</param>
    /// <param name="translatedPayload">The projected translated payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the translated payload contains enough
    ///     expected section titles and differs meaningfully from the original
    ///     payload; otherwise <see langword="false" />.
    /// </returns>
    private bool HasExpectedCharacterStatusCoverage(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        var translatedTexts = translatedPayload.AtkValues.Values
            .Concat(translatedPayload.StringArrayValues.Values)
            .Concat(translatedPayload.TextNodes.Values)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToHashSet(StringComparer.Ordinal);
        var matchedSectionTitles = ExpectedTranslatedSectionTitles.Count(
            translatedTexts.Contains);
        if (matchedSectionTitles < 3)
        {
            return false;
        }

        return !originalPayload.StructurallyEquals(translatedPayload);
    }
}
