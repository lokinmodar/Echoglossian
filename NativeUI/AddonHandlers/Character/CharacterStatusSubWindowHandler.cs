// <copyright file="CharacterStatusSubWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

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
            this.EmitCharacterStatusModeDetail(
                "CharacterStatus fallback resolved original payload from latest canonical structured row.");
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
            this.EmitCharacterStatusModeDetail(
                "CharacterStatus fallback resolved translated payload from latest canonical structured row.");
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
