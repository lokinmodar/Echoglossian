// <copyright file="CharacterTextNodeWindowHandlerBase.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;
using Echoglossian.Cache;

namespace Echoglossian.NativeUI.AddonHandlers.Character;

/// <summary>
///     Provides the shared DB-first runtime for Character-family windows that
///     should only capture stable sheet-backed text nodes.
/// </summary>
public abstract unsafe class CharacterTextNodeWindowHandlerBase
    : DbFirstGameWindowAddonHandler
{
    private static readonly TimeSpan CharacterAppliedStateRefreshWindow =
        TimeSpan.FromMilliseconds(500);

    private readonly Config config;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="CharacterTextNodeWindowHandlerBase" /> class.
    /// </summary>
    /// <param name="addonName">The target addon name.</param>
    /// <param name="config">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    /// <param name="useAtkValues">
    ///     When set, the handler also captures and applies live ATK-value text
    ///     alongside text nodes.
    /// </param>
    protected CharacterTextNodeWindowHandlerBase(
        string addonName,
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService,
        StringArrayType? stringArrayType = null,
        bool useAtkValues = false)
        : base(
            addonName: addonName,
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateCharacterWindow,
            useAtkValues: useAtkValues,
            useTextNodes: true,
            stringArrayDataType: stringArrayType,
            displayModeSelector: static configuration =>
                configuration.CharacterWindowTranslationDisplayMode)
    {
        this.config = config;
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return textNode != null &&
               textNode->TextId != 0 &&
               !string.IsNullOrWhiteSpace(visibleText);
    }

    /// <summary>
    ///     Determines whether the visible text can be resolved through the
    ///     canonical shared Character string-array payload.
    /// </summary>
    /// <param name="visibleText">The currently visible text.</param>
    /// <returns>
    ///     <see langword="true" /> when the text belongs to the shared
    ///     Character lookup surface; otherwise <see langword="false" />.
    /// </returns>
    protected bool CanCaptureSupplementalCharacterText(string visibleText)
    {
        if (string.IsNullOrWhiteSpace(visibleText) ||
            !this.TryBuildCharacterLookups(
                out _,
                out _,
                out var knownTexts))
        {
            return false;
        }

        return knownTexts.Contains(visibleText);
    }

    /// <inheritdoc />
    protected override bool ShouldRefreshAppliedStateOnPreDraw()
    {
        return false;
    }

    /// <inheritdoc />
    protected override TimeSpan GetAppliedStatePreDrawRefreshWindow()
    {
        return CharacterAppliedStateRefreshWindow;
    }

    /// <inheritdoc />
    private protected override bool ShouldPersistNewGameWindowPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return false;
    }

    /// <inheritdoc />
    private protected override bool ShouldQueueNewGameWindowTranslation(
        DbFirstGameWindowPayload originalPayload)
    {
        return false;
    }

    /// <summary>
    ///     Applies one text-node payload by matching current visible text
    ///     values rather than unstable node ordinals.
    /// </summary>
    /// <param name="addon">The live addon.</param>
    /// <param name="sourcePayload">
    ///     The payload describing the currently visible source-facing text.
    /// </param>
    /// <param name="targetPayload">
    ///     The payload describing the desired target-facing text.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when at least one text-node mapping was
    ///     available for value-based apply; otherwise <see langword="false" />.
    /// </returns>
    private protected bool ApplyVisibleTextNodesByValue(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        var valueMap = CharacterCanonicalPayloadHelper.BuildValueMap(
            sourcePayload,
            targetPayload);
        if (valueMap.Count == 0)
        {
            return false;
        }

        var hasCanonicalLookups = this.TryBuildCharacterLookups(
            out var originalLookup,
            out var translatedLookup,
            out _);
        var nodeAddresses = AddonTextNodeResolvers.ResolveMiniTalkBubbleTextNodes(
            addon);
        foreach (var nodeAddress in nodeAddresses)
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null)
            {
                continue;
            }

            var currentNode = (AtkResNode*)textNode;
            if (!this.IsEffectivelyVisible(currentNode))
            {
                continue;
            }

            var currentText = this.ReadTextNode(textNode);
            if (!valueMap.TryGetValue(currentText, out var targetText) &&
                (!hasCanonicalLookups ||
                 !CharacterCanonicalPayloadHelper.TryResolveCanonicalFallbackTarget(
                     currentText,
                     valueMap,
                     originalLookup,
                     translatedLookup,
                     out targetText)))
            {
                continue;
            }

            if (string.Equals(
                    currentText,
                    targetText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            textNode->SetText(targetText);
        }

        return true;
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalOriginalPayload(
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;

        if (!this.TryBuildCharacterLookups(
                out var originalLookup,
                out _,
                out _))
        {
            return false;
        }

        var resolved = CharacterCanonicalPayloadHelper.TryCanonicalizePayload(
            livePayload,
            originalLookup,
            out originalPayload);
        return resolved;
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;

        if (!this.TryBuildCharacterLookups(
                out _,
                out var translatedLookup,
                out _))
        {
            return false;
        }

        var resolved = CharacterCanonicalPayloadHelper.TryTranslatePayload(
            originalPayload,
            translatedLookup,
            out translatedPayload);
        return resolved;
    }

    /// <summary>
    ///     Builds one exact original-text to translated-text lookup from the
    ///     canonical shared Character string-array payload already cached in
    ///     memory.
    /// </summary>
    /// <param name="originalLookup">
    ///     Receives the lookup that maps any known visible text back to the
    ///     canonical original text.
    /// </param>
    /// <param name="translatedLookup">
    ///     Receives the lookup that maps any known visible text to the
    ///     canonical translated text.
    /// </param>
    /// <param name="knownTexts">
    ///     Receives the set of known original and translated texts so capture
    ///     shape remains stable in both states.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one canonical Character payload could
    ///     be resolved from cache; otherwise <see langword="false" />.
    /// </returns>
    private bool TryBuildCharacterLookups(
        out Dictionary<string, string> originalLookup,
        out Dictionary<string, string> translatedLookup,
        out HashSet<string> knownTexts)
    {
        originalLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        translatedLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        knownTexts = new HashSet<string>(StringComparer.Ordinal);
        var structuredRowsScanned = 0;
        var structuredRowsResolved = 0;
        var gameWindowRowsScanned = 0;
        var gameWindowRowsResolved = 0;

        var targetLanguage =
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.config.Lang);
        foreach (var contextKey in this.GetCharacterStructuredContextKeys())
        {
            var candidates = StringArrayDataCacheManager.GetCandidates(
                    type: StringArrayType.Character.ToString(),
                    contextKey: contextKey,
                    lang: targetLanguage,
                    engine: this.config.ChosenTransEngine,
                    gameVersion: GetGameVersion())
                .OrderBy(row => row.Id)
                .ToList();

            foreach (var row in candidates)
            {
                structuredRowsScanned++;
                if (!StringArrayStructuredPayloadResolver.TryResolvePayloads(
                        row,
                        out var originalStructuredPayload,
                        out var translatedStructuredPayload) ||
                    originalStructuredPayload == null ||
                    translatedStructuredPayload == null)
                {
                    continue;
                }

                structuredRowsResolved++;
                CharacterCanonicalPayloadHelper.AppendLookupEntries(
                    originalStructuredPayload.Slots.Values,
                    originalLookup,
                    translatedLookup,
                    knownTexts);
                CharacterCanonicalPayloadHelper.AppendLookupEntries(
                    originalStructuredPayload.TextNodes.Values,
                    originalLookup,
                    translatedLookup,
                    knownTexts);
            }
        }

        foreach (var row in GameWindowCacheManager.GetCandidates(
                     this.AddonName,
                     targetLanguage,
                     this.config.ChosenTransEngine,
                     GetGameVersion()).OrderBy(candidate => candidate.Id))
        {
            gameWindowRowsScanned++;
            if (!TryParseSerializedPayload(
                    row.OriginalWindowStrings,
                    out var rowOriginalPayload) ||
                !TryParseSerializedPayload(
                    row.TranslatedWindowStrings,
                    out var rowTranslatedPayload))
            {
                continue;
            }

            gameWindowRowsResolved++;
            CharacterCanonicalPayloadHelper.AppendLookupEntries(
                rowOriginalPayload.AtkValues,
                rowTranslatedPayload.AtkValues,
                originalLookup,
                translatedLookup,
                knownTexts,
                requireDifference: true);
            CharacterCanonicalPayloadHelper.AppendLookupEntries(
                rowOriginalPayload.StringArrayValues,
                rowTranslatedPayload.StringArrayValues,
                originalLookup,
                translatedLookup,
                knownTexts,
                requireDifference: true);
            CharacterCanonicalPayloadHelper.AppendLookupEntries(
                rowOriginalPayload.TextNodes,
                rowTranslatedPayload.TextNodes,
                originalLookup,
                translatedLookup,
                knownTexts,
                requireDifference: true);
        }

        if (string.Equals(
                this.AddonName,
                "Character",
                StringComparison.Ordinal))
        {
            CharacterWindowHandler.AppendStableHeaderFallbackTranslations(
                originalLookup,
                translatedLookup,
                knownTexts);
        }

        return translatedLookup.Count > 0;
    }

    /// <summary>
    ///     Gets the Character-family structured payload context keys that may
    ///     contribute canonical original and translated text pairs for the
    ///     current addon.
    /// </summary>
    /// <returns>
    ///     One sequence of context keys to consult in the shared
    ///     <see cref="StringArrayDataCacheManager" /> cache.
    /// </returns>
    private IEnumerable<string> GetCharacterStructuredContextKeys()
    {
        yield return "addon:Character";

        var specificContextKey = $"addon:{this.AddonName}";
        if (!string.Equals(
                specificContextKey,
                "addon:Character",
                StringComparison.Ordinal))
        {
            yield return specificContextKey;
        }
    }
}
