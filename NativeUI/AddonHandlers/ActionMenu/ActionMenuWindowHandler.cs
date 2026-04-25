// <copyright file="ActionMenuWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Text.RegularExpressions;

namespace Echoglossian.NativeUI.AddonHandlers.ActionMenu;

/// <summary>
///     Handles DB-first translation for the <c>ActionMenu</c> addon while
///     reusing canonical action-name and description translations that already
///     exist in <c>ActionTooltip</c> storage.
/// </summary>
public class ActionMenuWindowHandler : DbFirstGameWindowAddonHandler
{
    private const int SwitchViewAtkValueIndex = 0;
    private const int LevelAtkValueIndex = 10;
    private const int ClassJobAtkValueIndex = 12;
    private const int MinimumUntranslatedContentEntriesForRejection = 3;
    private const int MinimumStableSignatureEntryCount = 5;
    private const int MaximumStableSignatureWordCount = 6;
    private const int MaximumStableSignatureCharacterCount = 64;
    private const string MainCommandWindowTitle = "_MainCommand";

    private static readonly Regex TrailingLevelTokenPattern = new(
        @"^(?<prefix>.*?)(?<separator>\s*)(?<level>(?:Lv\.|Nv\.|Level|Nível)\s*\d+)$",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly Config config;
    private readonly PayloadStabilityTracker newPayloadStabilityTracker = new(
        minimumObservations: 2,
        minimumStableDuration: TimeSpan.FromMilliseconds(150));
    private readonly HashSet<string> queuedStablePayloadSignatures = new(
        StringComparer.Ordinal);

    /// <summary>
///     Initializes a new instance of the
    ///     <see cref="ActionMenuWindowHandler" /> class.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The shared translation service.</param>
    public ActionMenuWindowHandler(
        Config config,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService)
        : base(
            addonName: "ActionMenu",
            config: config,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration =>
                configuration.TranslateActionMenuWindow,
            useAtkValues: true,
            useTextNodes: true,
            displayModeSelector: static configuration =>
                configuration.ActionMenuWindowTranslationDisplayMode)
    {
        this.config = config;
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
        return this.ShouldAllowNewPayloadTranslation(originalPayload);
    }

    /// <inheritdoc />
    private protected override uint? GetPersistedGameWindowClassJobId(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return GetCurrentClassJobId();
    }

    /// <inheritdoc />
    private protected override uint? GetLookupGameWindowClassJobId(
        DbFirstGameWindowPayload payload)
    {
        return GetCurrentClassJobId();
    }

    /// <inheritdoc />
    private protected override async Task<bool>
        TranslateAndPersistGameWindowPayloadAsync(
            DbFirstGameWindowPayload originalPayload)
    {
        var classJobId = GetCurrentClassJobId();
        var classJobName = GetPayloadClassJobName(originalPayload);
        var translatedPayloadResult = await GenericAddonHandlerHelper
            .TranslatePayloadAsync(
                originalPayload.AtkValues,
                originalPayload.StringArrayValues,
                originalPayload.TextNodes,
                originalPayload.AtkValues,
                originalPayload.StringArrayValues,
                originalPayload.TextNodes,
                ClientStateInterface.ClientLanguage.Humanize(),
                RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                    this.config.Lang),
                this.HandlerTranslationService);
        if (!translatedPayloadResult.HasValue)
        {
            PluginLog.Debug(
                $"[{this.AddonName}] [Async] Skipping persistence because the translated payload is empty or incomplete.");
            return false;
        }

        var translatedPayload = this.NormalizeResolvedTranslatedPayload(
            originalPayload,
            translatedPayloadResult.Value,
            classJobId,
            classJobName);
        if (!this.ShouldAcceptResolvedTranslatedPayload(
                originalPayload,
                translatedPayload))
        {
            PluginLog.Debug(
                $"[{this.AddonName}] [Async] Skipping persistence because the normalized payload was still not acceptable.");
            return true;
        }

        var stablePayloadSignature = BuildStablePayloadSignature(
            originalPayload);
        if (string.IsNullOrWhiteSpace(stablePayloadSignature) ||
            !this.HasSufficientStableSignatureCoverage(
                stablePayloadSignature) ||
            this.HasPersistedStableSignature(
                stablePayloadSignature,
                classJobId) ||
            !this.HasMeaningfulUnseenTexts(
                originalPayload,
                classJobId,
                classJobName))
        {
            PluginLog.Debug(
                $"[{this.AddonName}] [Async] Skipping persistence because the stable-signature gate rejected the normalized payload.");
            return true;
        }

        this.PersistResolvedGameWindowPayload(
            originalPayload,
            translatedPayload,
            classJobId);
        return true;
    }

    /// <inheritdoc />
    private protected override DbFirstGameWindowPayload
        NormalizeResolvedTranslatedPayload(
            DbFirstGameWindowPayload originalPayload,
            DbFirstGameWindowPayload translatedPayload)
    {
        return this.NormalizeResolvedTranslatedPayload(
            originalPayload,
            translatedPayload,
            GetCurrentClassJobId(),
            GetPayloadClassJobName(originalPayload));
    }

    /// <summary>
    ///     Normalizes one resolved translated payload by merging canonical
    ///     ActionMenu lookups for the provided class/job scope.
    /// </summary>
    /// <param name="originalPayload">The original-facing payload.</param>
    /// <param name="translatedPayload">The translated payload candidate.</param>
    /// <param name="classJobId">The current class/job identifier.</param>
    /// <param name="classJobName">The current class/job name.</param>
    /// <returns>The normalized translated payload.</returns>
    private DbFirstGameWindowPayload NormalizeResolvedTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        uint? classJobId,
        string? classJobName)
    {
        var targetLanguage = RuntimeLanguageHelper
            .GetConfiguredTargetLanguageCode(this.config.Lang);
        var gameVersion = GetGameVersion();
        this.BuildPersistedActionMenuLookups(
            out _,
            out var persistedTranslatedLookup,
            classJobId,
            classJobName);
        return MergeResolvedTranslatedPayload(
            originalPayload,
            translatedPayload,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            persistedTranslatedLookup);
    }

    /// <inheritdoc />
    private protected override bool ShouldAcceptResolvedTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        return ShouldAcceptActionMenuResolvedPayload(
            originalPayload,
            translatedPayload);
    }

    /// <inheritdoc />
    protected override void OnCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        base.OnCleanupEvent(evt, args);
        this.newPayloadStabilityTracker.Reset();
    }

    /// <inheritdoc />
    protected override unsafe bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        return textNode != null &&
               !string.IsNullOrWhiteSpace(visibleText);
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalOriginalPayload(
        DbFirstGameWindowPayload livePayload,
        out DbFirstGameWindowPayload originalPayload)
    {
        originalPayload = DbFirstGameWindowPayload.Empty;

        var targetLanguage = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
            this.config.Lang);
        var gameVersion = GetGameVersion();
        var classJobId = GetCurrentClassJobId();
        this.BuildPersistedActionMenuLookups(
            out var persistedOriginalLookup,
            out _,
            classJobId,
            GetPayloadClassJobName(livePayload));

        var changed = false;
        var resolvedAtkValues = this.CanonicalizeIntMap(
            livePayload.AtkValues,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            persistedOriginalLookup,
            ref changed);
        var resolvedStringArrayValues = this.CanonicalizeIntMap(
            livePayload.StringArrayValues,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            persistedOriginalLookup,
            ref changed);
        var resolvedTextNodes = this.CanonicalizeStringMap(
            livePayload.TextNodes,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            persistedOriginalLookup,
            ref changed);

        if (!changed)
        {
            return false;
        }

        originalPayload = new DbFirstGameWindowPayload(
            resolvedAtkValues,
            resolvedStringArrayValues,
            resolvedTextNodes);
        return true;
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        var targetLanguage = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
            this.config.Lang);
        var gameVersion = GetGameVersion();
        var classJobId = GetCurrentClassJobId();
        this.BuildPersistedActionMenuLookups(
            out _,
            out var persistedTranslatedLookup,
            classJobId,
            GetPayloadClassJobName(originalPayload));
        var changed = false;

        var translatedAtkValues = TranslateIntMap(
            originalPayload.AtkValues,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            persistedTranslatedLookup,
            ref changed);
        var translatedStringArrayValues = TranslateIntMap(
            originalPayload.StringArrayValues,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            persistedTranslatedLookup,
            ref changed);
        var translatedTextNodes = TranslateStringMap(
            originalPayload.TextNodes,
            targetLanguage,
            this.config.ChosenTransEngine,
            gameVersion,
            persistedTranslatedLookup,
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
        IReadOnlyDictionary<string, string> fallbackLookup,
        ref bool changed)
    {
        var translatedValues = new SortedDictionary<int, string>();

        foreach (var (key, originalText) in sourceValues)
        {
            var translatedText = ResolveTranslatedText(
                originalText,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup);
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
        IReadOnlyDictionary<string, string> fallbackLookup,
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
                gameVersion,
                fallbackLookup);
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
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        if (TryResolveLevelAwareTranslatedText(
                originalText,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup,
                out var levelAwareTranslatedText))
        {
            return levelAwareTranslatedText;
        }

        if (ActionTooltipCacheManager.TryFindTranslatedText(
                targetLanguage,
                engine,
                gameVersion,
                originalText,
                out var translatedText))
        {
            return PreserveSourceLevelSeparator(originalText, translatedText);
        }

        if (fallbackLookup.TryGetValue(originalText, out translatedText))
        {
            return PreserveSourceLevelSeparator(originalText, translatedText);
        }

        return originalText;
    }

    /// <summary>
    ///     Resolves one canonical original action-menu text from either the
    ///     action-tooltip cache or previously persisted ActionMenu rows.
    /// </summary>
    /// <param name="visibleText">The currently visible text.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">
    ///     The reverse lookup built from persisted ActionMenu rows.
    /// </param>
    /// <returns>The canonical original text, or the visible text.</returns>
    private static string ResolveOriginalText(
        string visibleText,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        if (TryResolveLevelAwareOriginalText(
                visibleText,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup,
                out var levelAwareOriginalText))
        {
            return levelAwareOriginalText;
        }

        if (ActionTooltipCacheManager.TryFindOriginalText(
                targetLanguage,
                engine,
                gameVersion,
                visibleText,
                out var originalText))
        {
            return PreserveSourceLevelSeparator(visibleText, originalText);
        }

        if (fallbackLookup.TryGetValue(visibleText, out originalText))
        {
            return PreserveSourceLevelSeparator(visibleText, originalText);
        }

        return visibleText;
    }

    /// <summary>
    ///     Merges one resolved translated payload with canonical
    ///     action-tooltip or persisted ActionMenu translations so stale or
    ///     malformed stored rows can be repaired before apply.
    /// </summary>
    /// <param name="originalPayload">The original-facing payload.</param>
    /// <param name="resolvedTranslatedPayload">
    ///     The resolved translated payload from cache or DB.
    /// </param>
    /// <param name="targetLanguage">The active target language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">
    ///     The original-to-translated fallback lookup built from persisted
    ///     ActionMenu rows.
    /// </param>
    /// <returns>The merged translated payload.</returns>
    internal static DbFirstGameWindowPayload MergeResolvedTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload resolvedTranslatedPayload,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        return new DbFirstGameWindowPayload(
            MergeTranslatedIntMap(
                originalPayload.AtkValues,
                resolvedTranslatedPayload.AtkValues,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup),
            MergeTranslatedIntMap(
                originalPayload.StringArrayValues,
                resolvedTranslatedPayload.StringArrayValues,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup),
            MergeTranslatedStringMap(
                originalPayload.TextNodes,
                resolvedTranslatedPayload.TextNodes,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup));
    }

    /// <summary>
    ///     Determines whether one resolved translated ActionMenu payload is
    ///     complete enough to use immediately or whether the addon should fall
    ///     back to the async translation path instead.
    /// </summary>
    /// <param name="originalPayload">The original-facing payload.</param>
    /// <param name="translatedPayload">The translated payload candidate.</param>
    /// <returns>
    ///     <see langword="true" /> when the payload is acceptable;
    ///     otherwise <see langword="false" />.
    /// </returns>
    internal static bool ShouldAcceptActionMenuResolvedPayload(
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        var contentEntryCount = 0;
        var untranslatedContentCount = 0;

        AccumulateCoverage(
            originalPayload.AtkValues,
            translatedPayload.AtkValues,
            static (key, text) =>
                key is not (SwitchViewAtkValueIndex or
                            LevelAtkValueIndex or
                            ClassJobAtkValueIndex) &&
                ContainsLetters(text),
            ref contentEntryCount,
            ref untranslatedContentCount);
        AccumulateCoverage(
            originalPayload.StringArrayValues,
            translatedPayload.StringArrayValues,
            static (_, text) => ContainsLetters(text),
            ref contentEntryCount,
            ref untranslatedContentCount);
        AccumulateCoverage(
            originalPayload.TextNodes,
            translatedPayload.TextNodes,
            static (_, text) => ContainsLetters(text),
            ref contentEntryCount,
            ref untranslatedContentCount);

        if (contentEntryCount == 0 || untranslatedContentCount == 0)
        {
            return true;
        }

        var translatedContentCount = contentEntryCount - untranslatedContentCount;
        return untranslatedContentCount < MinimumUntranslatedContentEntriesForRejection ||
               translatedContentCount > untranslatedContentCount;
    }

    /// <summary>
    ///     Builds one stable ActionMenu page signature from short, meaningful
    ///     visible labels while excluding volatile metadata and long
    ///     descriptive text.
    /// </summary>
    /// <param name="payload">The payload to summarize.</param>
    /// <returns>The stable page signature.</returns>
    internal static string BuildStablePayloadSignature(
        DbFirstGameWindowPayload payload)
    {
        var texts = EnumerateStableSignatureTexts(payload)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static text => text, StringComparer.Ordinal)
            .ToList();
        if (TryGetPayloadClassJobName(payload, out var classJobName))
        {
            texts.Add($"job:{classJobName}");
        }

        texts = texts
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static text => text, StringComparer.Ordinal)
            .ToList();
        return texts.Count == 0
            ? string.Empty
            : string.Join("\u001F", texts);
    }

    /// <summary>
    ///     Counts one payload's distinct short texts that are still not
    ///     covered by canonical ActionMenu sources.
    /// </summary>
    /// <param name="payload">The original-facing payload.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">
    ///     The persisted ActionMenu and <c>_MainCommand</c> fallback lookup.
    /// </param>
    /// <returns>The count of unresolved short texts.</returns>
    internal static int CountMeaningfulUnseenTexts(
        DbFirstGameWindowPayload payload,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        return EnumerateStableSignatureTexts(payload)
            .Where(text => !IsKnownCanonicalActionMenuText(
                text,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    /// <summary>
    ///     Preserves the separator that precedes one trailing level token in
    ///     the source text when the resolved target text carries the same
    ///     trailing level structure but collapsed spacing.
    /// </summary>
    /// <param name="sourceText">The source text whose separator should win.</param>
    /// <param name="targetText">The resolved target text.</param>
    /// <returns>
    ///     The target text with the source separator restored when applicable;
    ///     otherwise the original target text.
    /// </returns>
    internal static string PreserveSourceLevelSeparator(
        string sourceText,
        string targetText)
    {
        if (string.IsNullOrWhiteSpace(sourceText) ||
            string.IsNullOrWhiteSpace(targetText))
        {
            return targetText;
        }

        var sourceMatch = TrailingLevelTokenPattern.Match(sourceText);
        var targetMatch = TrailingLevelTokenPattern.Match(targetText);
        if (!sourceMatch.Success || !targetMatch.Success)
        {
            return targetText;
        }

        var sourceSeparator = sourceMatch.Groups["separator"].Value;
        var targetSeparator = targetMatch.Groups["separator"].Value;
        if (string.IsNullOrEmpty(sourceSeparator) ||
            string.Equals(
                sourceSeparator,
                targetSeparator,
                StringComparison.Ordinal))
        {
            return targetText;
        }

        return string.Concat(
            targetMatch.Groups["prefix"].Value,
            sourceSeparator,
            targetMatch.Groups["level"].Value);
    }

    /// <summary>
    ///     Tries to resolve one translated text by translating only the
    ///     visible action name when the source text ends with a trailing level
    ///     token.
    /// </summary>
    /// <param name="originalText">The original visible text.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">The persisted fallback lookup.</param>
    /// <param name="translatedText">The resolved translated text.</param>
    /// <returns>
    ///     <see langword="true" /> when a translated text was resolved;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private static bool TryResolveLevelAwareTranslatedText(
        string originalText,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup,
        out string translatedText)
    {
        translatedText = string.Empty;
        if (!TryParseTrailingLevelToken(
                originalText,
                out var originalPrefix,
                out var separator,
                out var levelLabel,
                out var levelNumber))
        {
            return false;
        }

        var translatedPrefix = ResolveTranslatedBaseText(
            originalPrefix,
            targetLanguage,
            engine,
            gameVersion,
            fallbackLookup);
        if (string.Equals(
                translatedPrefix,
                originalPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        translatedText = string.Concat(
            translatedPrefix,
            NormalizeResolvedLevelSeparator(separator),
            NormalizeTranslatedLevelToken(levelLabel, levelNumber, targetLanguage));
        return true;
    }

    /// <summary>
    ///     Tries to resolve one canonical original text by reversing only the
    ///     visible action name when the translated text ends with a trailing
    ///     level token.
    /// </summary>
    /// <param name="visibleText">The visible translated text.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">The persisted reverse lookup.</param>
    /// <param name="originalText">The resolved canonical original text.</param>
    /// <returns>
    ///     <see langword="true" /> when an original text was resolved;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private static bool TryResolveLevelAwareOriginalText(
        string visibleText,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup,
        out string originalText)
    {
        originalText = string.Empty;
        if (!TryParseTrailingLevelToken(
                visibleText,
                out var visiblePrefix,
                out var separator,
                out var levelLabel,
                out var levelNumber))
        {
            return false;
        }

        var resolvedOriginalPrefix = ResolveOriginalBaseText(
            visiblePrefix,
            targetLanguage,
            engine,
            gameVersion,
            fallbackLookup);
        if (string.Equals(
                resolvedOriginalPrefix,
                visiblePrefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        originalText = string.Concat(
            resolvedOriginalPrefix,
            NormalizeResolvedLevelSeparator(separator),
            NormalizeOriginalLevelToken(levelLabel, levelNumber));
        return true;
    }

    /// <summary>
    ///     Resolves one translated text without applying any level-token
    ///     decomposition.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">The persisted fallback lookup.</param>
    /// <returns>The resolved translated text, or the original text.</returns>
    private static string ResolveTranslatedBaseText(
        string originalText,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        if (ActionTooltipCacheManager.TryFindTranslatedText(
                targetLanguage,
                engine,
                gameVersion,
                originalText,
                out var translatedText))
        {
            return translatedText;
        }

        if (fallbackLookup.TryGetValue(originalText, out translatedText))
        {
            return translatedText;
        }

        return originalText;
    }

    /// <summary>
    ///     Resolves one canonical original text without applying any
    ///     level-token decomposition.
    /// </summary>
    /// <param name="visibleText">The translated text.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">The persisted reverse lookup.</param>
    /// <returns>The resolved original text, or the visible text.</returns>
    private static string ResolveOriginalBaseText(
        string visibleText,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        if (ActionTooltipCacheManager.TryFindOriginalText(
                targetLanguage,
                engine,
                gameVersion,
                visibleText,
                out var originalText))
        {
            return originalText;
        }

        if (fallbackLookup.TryGetValue(visibleText, out originalText))
        {
            return originalText;
        }

        return visibleText;
    }

    /// <summary>
    ///     Tries to parse one text into a visible prefix, separator, and
    ///     trailing level token.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="prefix">The visible text before the level token.</param>
    /// <param name="separator">The separator before the level token.</param>
    /// <param name="levelLabel">The level label prefix.</param>
    /// <param name="levelNumber">The trailing level number.</param>
    /// <returns>
    ///     <see langword="true" /> when the text contains one trailing level
    ///     token; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryParseTrailingLevelToken(
        string text,
        out string prefix,
        out string separator,
        out string levelLabel,
        out string levelNumber)
    {
        prefix = string.Empty;
        separator = string.Empty;
        levelLabel = string.Empty;
        levelNumber = string.Empty;

        var match = TrailingLevelTokenPattern.Match(text);
        if (!match.Success)
        {
            return false;
        }

        prefix = match.Groups["prefix"].Value;
        separator = match.Groups["separator"].Value;
        var levelValue = match.Groups["level"].Value;
        var numberIndex = levelValue.Length - 1;
        while (numberIndex >= 0 && char.IsDigit(levelValue[numberIndex]))
        {
            numberIndex--;
        }

        if (numberIndex < 0 || numberIndex >= levelValue.Length - 1)
        {
            return false;
        }

        levelLabel = levelValue[..(numberIndex + 1)].TrimEnd();
        levelNumber = levelValue[(numberIndex + 1)..];
        return !string.IsNullOrWhiteSpace(prefix) &&
               !string.IsNullOrWhiteSpace(levelLabel) &&
               !string.IsNullOrWhiteSpace(levelNumber);
    }

    /// <summary>
    ///     Normalizes one translated level token for the target language.
    /// </summary>
    /// <param name="levelLabel">The source level label.</param>
    /// <param name="levelNumber">The trailing level number.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <returns>The normalized translated level token.</returns>
    private static string NormalizeTranslatedLevelToken(
        string levelLabel,
        string levelNumber,
        string targetLanguage)
    {
        if (RuntimeLanguageHelper.NormalizeLanguage(targetLanguage)
                .StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return $"Nv. {levelNumber}";
        }

        return $"{levelLabel} {levelNumber}";
    }

    /// <summary>
    ///     Normalizes one translated level token back to the canonical English
    ///     source token.
    /// </summary>
    /// <param name="levelLabel">The visible translated level label.</param>
    /// <param name="levelNumber">The trailing level number.</param>
    /// <returns>The canonical English level token.</returns>
    private static string NormalizeOriginalLevelToken(
        string levelLabel,
        string levelNumber)
    {
        if (levelLabel is "Nv." or "Nível")
        {
            return $"Lv. {levelNumber}";
        }

        return $"{levelLabel} {levelNumber}";
    }

    /// <summary>
    ///     Normalizes one resolved level separator so malformed payloads that
    ///     collapsed the separator can still be rewritten into readable text.
    /// </summary>
    /// <param name="separator">The captured separator.</param>
    /// <returns>The separator to use in one rebuilt level-aware label.</returns>
    private static string NormalizeResolvedLevelSeparator(string separator)
    {
        return string.IsNullOrEmpty(separator) ? " " : separator;
    }

    /// <summary>
    ///     Merges one integer-keyed translated payload map with canonical
    ///     translations derived from repo-local sources.
    /// </summary>
    /// <param name="originalValues">The original values.</param>
    /// <param name="resolvedTranslatedValues">The resolved translated values.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">The persisted fallback lookup.</param>
    /// <returns>The merged translated map.</returns>
    private static SortedDictionary<int, string> MergeTranslatedIntMap(
        SortedDictionary<int, string> originalValues,
        SortedDictionary<int, string> resolvedTranslatedValues,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        var mergedValues = new SortedDictionary<int, string>();

        foreach (var (key, originalText) in originalValues)
        {
            resolvedTranslatedValues.TryGetValue(key, out var currentText);
            var canonicalText = ResolveTranslatedText(
                originalText,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup);
            mergedValues[key] = SelectResolvedTranslatedValue(
                originalText,
                currentText,
                canonicalText);
        }

        foreach (var (key, value) in resolvedTranslatedValues)
        {
            if (!mergedValues.ContainsKey(key))
            {
                mergedValues[key] = value;
            }
        }

        return mergedValues;
    }

    /// <summary>
    ///     Merges one text-node translated payload map with canonical
    ///     translations derived from repo-local sources.
    /// </summary>
    /// <param name="originalValues">The original values.</param>
    /// <param name="resolvedTranslatedValues">The resolved translated values.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">The persisted fallback lookup.</param>
    /// <returns>The merged translated map.</returns>
    private static SortedDictionary<string, string> MergeTranslatedStringMap(
        SortedDictionary<string, string> originalValues,
        SortedDictionary<string, string> resolvedTranslatedValues,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        var mergedValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var (key, originalText) in originalValues)
        {
            resolvedTranslatedValues.TryGetValue(key, out var currentText);
            var canonicalText = ResolveTranslatedText(
                originalText,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup);
            mergedValues[key] = SelectResolvedTranslatedValue(
                originalText,
                currentText,
                canonicalText);
        }

        foreach (var (key, value) in resolvedTranslatedValues)
        {
            if (!mergedValues.ContainsKey(key))
            {
                mergedValues[key] = value;
            }
        }

        return mergedValues;
    }

    /// <summary>
    ///     Chooses the best translated value among the currently resolved text
    ///     and one canonical candidate.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    /// <param name="resolvedText">The resolved translated text.</param>
    /// <param name="canonicalText">The canonical translated text.</param>
    /// <returns>The translated value to keep.</returns>
    private static string SelectResolvedTranslatedValue(
        string originalText,
        string? resolvedText,
        string canonicalText)
    {
        if (!string.Equals(
                canonicalText,
                originalText,
                StringComparison.Ordinal))
        {
            return canonicalText;
        }

        return string.IsNullOrWhiteSpace(resolvedText)
            ? originalText
            : resolvedText;
    }

    /// <summary>
    ///     Accumulates translation coverage metrics for one payload map.
    /// </summary>
    /// <typeparam name="TKey">The payload key type.</typeparam>
    /// <param name="originalValues">The original values.</param>
    /// <param name="translatedValues">The translated values.</param>
    /// <param name="shouldCount">The predicate that marks content entries.</param>
    /// <param name="contentEntryCount">The running content-entry count.</param>
    /// <param name="untranslatedContentCount">
    ///     The running untranslated content-entry count.
    /// </param>
    private static void AccumulateCoverage<TKey>(
        IReadOnlyDictionary<TKey, string> originalValues,
        IReadOnlyDictionary<TKey, string> translatedValues,
        Func<TKey, string, bool> shouldCount,
        ref int contentEntryCount,
        ref int untranslatedContentCount)
        where TKey : notnull
    {
        foreach (var (key, originalText) in originalValues)
        {
            if (!shouldCount(key, originalText))
            {
                continue;
            }

            contentEntryCount++;
            if (!translatedValues.TryGetValue(key, out var translatedText) ||
                string.Equals(
                    translatedText,
                    originalText,
                    StringComparison.Ordinal))
            {
                untranslatedContentCount++;
            }
        }
    }

    /// <summary>
    ///     Gets whether one candidate ActionMenu content text contains letters
    ///     and therefore should count towards translation coverage.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when the text contains letters; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private static bool ContainsLetters(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Any(char.IsLetter);
    }

    /// <summary>
    ///     Determines whether the current payload is stable and novel enough to
    ///     justify persisting one new canonical ActionMenu row.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <returns>
    ///     <see langword="true" /> when one new row should be persisted;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool ShouldAllowNewPayloadPersistence(
        DbFirstGameWindowPayload originalPayload)
    {
        var stablePayloadSignature = BuildStablePayloadSignature(originalPayload);
        if (string.IsNullOrWhiteSpace(stablePayloadSignature) ||
            !this.HasSufficientStableSignatureCoverage(stablePayloadSignature) ||
            this.HasPersistedStableSignature(stablePayloadSignature) ||
            !this.HasMeaningfulUnseenTexts(originalPayload))
        {
            return false;
        }

        return this.newPayloadStabilityTracker.Observe(
            stablePayloadSignature,
            DateTime.UtcNow);
    }

    /// <summary>
    ///     Determines whether the current payload is stable and unresolved
    ///     enough to justify one new remote translation request.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <returns>
    ///     <see langword="true" /> when one remote translation may be queued;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool ShouldAllowNewPayloadTranslation(
        DbFirstGameWindowPayload originalPayload)
    {
        var stablePayloadSignature = BuildStablePayloadSignature(originalPayload);
        if (string.IsNullOrWhiteSpace(stablePayloadSignature) ||
            !this.HasSufficientStableSignatureCoverage(stablePayloadSignature) ||
            this.HasPersistedStableSignature(stablePayloadSignature) ||
            !this.HasMeaningfulUnseenTexts(originalPayload) ||
            !this.newPayloadStabilityTracker.Observe(
                stablePayloadSignature,
                DateTime.UtcNow))
        {
            return false;
        }

        return this.queuedStablePayloadSignatures.Add(stablePayloadSignature);
    }

    /// <summary>
    ///     Determines whether one stable ActionMenu page signature contains
    ///     enough short labels to represent a meaningful page shape.
    /// </summary>
    /// <param name="stablePayloadSignature">The page signature to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when the signature covers enough stable
    ///     entries; otherwise <see langword="false" />.
    /// </returns>
    private bool HasSufficientStableSignatureCoverage(
        string stablePayloadSignature)
    {
        return stablePayloadSignature.Split(
                '\u001F',
                StringSplitOptions.RemoveEmptyEntries).Length >=
               MinimumStableSignatureEntryCount;
    }

    /// <summary>
    ///     Determines whether the current payload still contains any short
    ///     texts that are not already covered by canonical action or menu
    ///     sources.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <returns>
    ///     <see langword="true" /> when unresolved short texts remain;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool HasMeaningfulUnseenTexts(
        DbFirstGameWindowPayload originalPayload)
    {
        return this.HasMeaningfulUnseenTexts(
            originalPayload,
            GetCurrentClassJobId(),
            GetPayloadClassJobName(originalPayload));
    }

    /// <summary>
    ///     Determines whether the current payload still contains any short
    ///     texts that are not already covered by canonical action or menu
    ///     sources for the provided class/job scope.
    /// </summary>
    /// <param name="originalPayload">The current original-facing payload.</param>
    /// <param name="classJobId">The current class/job identifier.</param>
    /// <param name="classJobName">The current class/job name.</param>
    /// <returns>
    ///     <see langword="true" /> when unresolved short texts remain;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private bool HasMeaningfulUnseenTexts(
        DbFirstGameWindowPayload originalPayload,
        uint? classJobId,
        string? classJobName)
    {
        var targetLanguage = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
            this.config.Lang);
        var gameVersion = GetGameVersion();
        this.BuildPersistedActionMenuLookups(
            out _,
            out var persistedTranslatedLookup,
            classJobId,
            classJobName);
        return CountMeaningfulUnseenTexts(
                   originalPayload,
                   targetLanguage,
                   this.config.ChosenTransEngine,
                   gameVersion,
                   persistedTranslatedLookup) > 0;
    }

    /// <summary>
    ///     Determines whether one stable ActionMenu page signature already
    ///     exists in persisted ActionMenu rows for the current scope.
    /// </summary>
    /// <param name="stablePayloadSignature">The page signature to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> when a persisted row already covers the
    ///     same stable page shape; otherwise <see langword="false" />.
    /// </returns>
    private bool HasPersistedStableSignature(string stablePayloadSignature)
    {
        return this.HasPersistedStableSignature(
            stablePayloadSignature,
            GetCurrentClassJobId());
    }

    /// <summary>
    ///     Determines whether one stable ActionMenu page signature already
    ///     exists in persisted ActionMenu rows for the provided class/job
    ///     scope.
    /// </summary>
    /// <param name="stablePayloadSignature">The page signature to inspect.</param>
    /// <param name="classJobId">The current class/job identifier.</param>
    /// <returns>
    ///     <see langword="true" /> when a persisted row already covers the
    ///     same stable page shape; otherwise <see langword="false" />.
    /// </returns>
    private bool HasPersistedStableSignature(
        string stablePayloadSignature,
        uint? classJobId)
    {
        var targetLanguage = RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
            this.config.Lang);
        foreach (var row in GameWindowCacheManager.GetCandidates(
                     this.AddonName,
                     targetLanguage,
                     this.config.ChosenTransEngine,
                     GetGameVersion(),
                     classJobId))
        {
            if (!TryParseSerializedPayload(
                    row.OriginalWindowStrings,
                    out var rowOriginalPayload))
            {
                continue;
            }

            if (string.Equals(
                    BuildStablePayloadSignature(rowOriginalPayload),
                    stablePayloadSignature,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Enumerates one payload's short, stable texts that define an
    ///     ActionMenu page shape and should participate in row dedupe.
    /// </summary>
    /// <param name="payload">The payload to inspect.</param>
    /// <returns>The stable short texts.</returns>
    private static IEnumerable<string> EnumerateStableSignatureTexts(
        DbFirstGameWindowPayload payload)
    {
        foreach (var (key, text) in payload.AtkValues)
        {
            if (!ShouldIncludeStableSignatureText(key, text))
            {
                continue;
            }

            yield return NormalizeStableSignatureText(text);
        }

        foreach (var text in payload.StringArrayValues.Values)
        {
            if (!ShouldIncludeStableSignatureText(text))
            {
                continue;
            }

            yield return NormalizeStableSignatureText(text);
        }

        foreach (var text in payload.TextNodes.Values)
        {
            if (!ShouldIncludeStableSignatureText(text))
            {
                continue;
            }

            yield return NormalizeStableSignatureText(text);
        }
    }

    /// <summary>
    ///     Determines whether one numeric-keyed ActionMenu value should
    ///     participate in stable page dedupe.
    /// </summary>
    /// <param name="key">The numeric payload key.</param>
    /// <param name="text">The visible text.</param>
    /// <returns>
    ///     <see langword="true" /> when the text should participate in stable
    ///     dedupe; otherwise <see langword="false" />.
    /// </returns>
    private static bool ShouldIncludeStableSignatureText(
        int key,
        string text)
    {
        return key is not (SwitchViewAtkValueIndex or
                           LevelAtkValueIndex or
                           ClassJobAtkValueIndex) &&
               ShouldIncludeStableSignatureText(text);
    }

    /// <summary>
    ///     Determines whether one text should participate in stable ActionMenu
    ///     page dedupe.
    /// </summary>
    /// <param name="text">The visible text.</param>
    /// <returns>
    ///     <see langword="true" /> when the text is short and stable enough to
    ///     represent page identity; otherwise <see langword="false" />.
    /// </returns>
    private static bool ShouldIncludeStableSignatureText(string text)
    {
        if (!ContainsLetters(text))
        {
            return false;
        }

        var normalizedText = NormalizeStableSignatureText(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        return normalizedText.Length <= MaximumStableSignatureCharacterCount &&
               normalizedText.Split(
                   ' ',
                   StringSplitOptions.RemoveEmptyEntries).Length <=
               MaximumStableSignatureWordCount;
    }

    /// <summary>
    ///     Normalizes one short ActionMenu text for stable page dedupe by
    ///     collapsing whitespace and trimming noise.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text.</returns>
    private static string NormalizeStableSignatureText(string text)
    {
        return string.Join(
            " ",
            text.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    ///     Determines whether one ActionMenu text is already covered by either
    ///     canonical action-tooltip storage or persisted window-chrome
    ///     lookups.
    /// </summary>
    /// <param name="text">The text to inspect.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">
    ///     The persisted ActionMenu and <c>_MainCommand</c> fallback lookup.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the text is already covered by
    ///     canonical sources; otherwise <see langword="false" />.
    /// </returns>
    private static bool IsKnownCanonicalActionMenuText(
        string text,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup)
    {
        return ActionTooltipCacheManager.TryFindTranslatedText(
                   targetLanguage,
                   engine,
                   gameVersion,
                   text,
                   out _) ||
               fallbackLookup.ContainsKey(text);
    }

    /// <summary>
    ///     Canonicalizes one integer-keyed payload map back to original text.
    /// </summary>
    /// <param name="sourceValues">The currently visible values.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">
    ///     The reverse lookup built from persisted ActionMenu rows.
    /// </param>
    /// <param name="changed">
    ///     Receives whether any canonical original value differs from the live
    ///     text.
    /// </param>
    /// <returns>The canonicalized original-facing map.</returns>
    private SortedDictionary<int, string> CanonicalizeIntMap(
        SortedDictionary<int, string> sourceValues,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup,
        ref bool changed)
    {
        var originalValues = new SortedDictionary<int, string>();

        foreach (var (key, visibleText) in sourceValues)
        {
            var originalText = ResolveOriginalText(
                visibleText,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup);
            if (!string.Equals(
                    originalText,
                    visibleText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            originalValues[key] = originalText;
        }

        return originalValues;
    }

    /// <summary>
    ///     Canonicalizes one text-node payload map back to original text.
    /// </summary>
    /// <param name="sourceValues">The currently visible values.</param>
    /// <param name="targetLanguage">The target translation language.</param>
    /// <param name="engine">The translation engine identifier.</param>
    /// <param name="gameVersion">The current game version.</param>
    /// <param name="fallbackLookup">
    ///     The reverse lookup built from persisted ActionMenu rows.
    /// </param>
    /// <param name="changed">
    ///     Receives whether any canonical original value differs from the live
    ///     text.
    /// </param>
    /// <returns>The canonicalized original-facing map.</returns>
    private SortedDictionary<string, string> CanonicalizeStringMap(
        SortedDictionary<string, string> sourceValues,
        string targetLanguage,
        int engine,
        string? gameVersion,
        IReadOnlyDictionary<string, string> fallbackLookup,
        ref bool changed)
    {
        var originalValues = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var (key, visibleText) in sourceValues)
        {
            var originalText = ResolveOriginalText(
                visibleText,
                targetLanguage,
                engine,
                gameVersion,
                fallbackLookup);
            if (!string.Equals(
                    originalText,
                    visibleText,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            originalValues[key] = originalText;
        }

        return originalValues;
    }

    /// <summary>
    ///     Builds forward and reverse lookups from persisted ActionMenu rows so
    ///     previously translated window chrome can be reused without calling
    ///     the remote translator again.
    /// </summary>
    /// <param name="originalLookup">
    ///     Receives the translated-to-original reverse lookup.
    /// </param>
    /// <param name="translatedLookup">
    ///     Receives the original-to-translated forward lookup.
    /// </param>
    private void BuildPersistedActionMenuLookups(
        out Dictionary<string, string> originalLookup,
        out Dictionary<string, string> translatedLookup,
        uint? classJobId = null,
        string? classJobName = null)
    {
        originalLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        translatedLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguousOriginalKeys = new HashSet<string>(StringComparer.Ordinal);

        this.AppendPersistedWindowLookups(
            MainCommandWindowTitle,
            translatedLookup,
            originalLookup,
            ambiguousOriginalKeys,
            expectedClassJobId: null,
            expectedClassJobName: null);
        this.AppendPersistedWindowLookups(
            this.AddonName,
            translatedLookup,
            originalLookup,
            ambiguousOriginalKeys,
            classJobId,
            classJobName);
    }

    /// <summary>
    ///     Appends one persisted window's forward and reverse text pairs into
    ///     the ActionMenu fallback lookups.
    /// </summary>
    /// <param name="windowTitle">The persisted window title to read.</param>
    /// <param name="translatedLookup">The original-to-translated lookup.</param>
    /// <param name="originalLookup">The translated-to-original lookup.</param>
    /// <param name="ambiguousOriginalKeys">
    ///     Tracks translated texts that map to multiple originals.
    /// </param>
    private void AppendPersistedWindowLookups(
        string windowTitle,
        IDictionary<string, string> translatedLookup,
        IDictionary<string, string> originalLookup,
        ISet<string> ambiguousOriginalKeys,
        uint? expectedClassJobId,
        string? expectedClassJobName)
    {
        foreach (var row in GameWindowCacheManager.GetCandidates(
                     windowTitle,
                     RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                         this.config.Lang),
                     this.config.ChosenTransEngine,
                     GetGameVersion(),
                     expectedClassJobId).OrderBy(candidate => candidate.Id))
        {
            if (!TryParseSerializedPayload(
                    row.OriginalWindowStrings,
                    out var rowOriginalPayload) ||
                !TryParseSerializedPayload(
                    row.TranslatedWindowStrings,
                    out var rowTranslatedPayload))
            {
                continue;
            }

            if (expectedClassJobId.HasValue &&
                row.ClassJobId.HasValue &&
                row.ClassJobId != expectedClassJobId)
            {
                continue;
            }

            if (row.ClassJobId == null &&
                !string.IsNullOrWhiteSpace(expectedClassJobName) &&
                !PayloadMatchesClassJob(
                    rowOriginalPayload,
                    rowTranslatedPayload,
                    expectedClassJobName))
            {
                continue;
            }

            AppendLookupEntries(
                rowOriginalPayload.AtkValues,
                rowTranslatedPayload.AtkValues,
                translatedLookup,
                originalLookup,
                ambiguousOriginalKeys);
            AppendLookupEntries(
                rowOriginalPayload.StringArrayValues,
                rowTranslatedPayload.StringArrayValues,
                translatedLookup,
                originalLookup,
                ambiguousOriginalKeys);
            AppendLookupEntries(
                rowOriginalPayload.TextNodes,
                rowTranslatedPayload.TextNodes,
                translatedLookup,
                originalLookup,
                ambiguousOriginalKeys);
        }
    }

    /// <summary>
    ///     Gets one normalized class or job identifier from the payload when
    ///     the ActionMenu currently exposes that label.
    /// </summary>
    /// <param name="payload">The payload to inspect.</param>
    /// <returns>The normalized class or job text, if any.</returns>
    private static string? GetPayloadClassJobName(
        DbFirstGameWindowPayload payload)
    {
        return TryGetPayloadClassJobName(payload, out var classJobName)
            ? classJobName
            : null;
    }

    /// <summary>
    ///     Tries to resolve one normalized class or job identifier from the
    ///     payload.
    /// </summary>
    /// <param name="payload">The payload to inspect.</param>
    /// <param name="classJobName">
    ///     Receives the normalized class or job text.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when one class or job text was found;
    ///     otherwise <see langword="false" />.
    /// </returns>
    private static bool TryGetPayloadClassJobName(
        DbFirstGameWindowPayload payload,
        out string classJobName)
    {
        classJobName = string.Empty;
        if (!payload.AtkValues.TryGetValue(
                ClassJobAtkValueIndex,
                out var rawClassJobText) ||
            !ContainsLetters(rawClassJobText))
        {
            return false;
        }

        classJobName = NormalizeStableSignatureText(rawClassJobText);
        return !string.IsNullOrWhiteSpace(classJobName);
    }

    /// <summary>
    ///     Gets the current class/job identifier from player state when
    ///     ActionMenu content should be scoped by the active job.
    /// </summary>
    /// <returns>
    ///     The current class/job identifier, or <see langword="null" /> when
    ///     no active job could be resolved.
    /// </returns>
    private static unsafe uint? GetCurrentClassJobId()
    {
        var playerState = PlayerState.Instance();
        if (playerState == null || playerState->CurrentClassJobId == 0)
        {
            return null;
        }

        return playerState->CurrentClassJobId;
    }

    /// <summary>
    ///     Determines whether one persisted ActionMenu row belongs to the same
    ///     class or job as the current payload by comparing both original and
    ///     translated class-job labels.
    /// </summary>
    /// <param name="rowOriginalPayload">The persisted original payload.</param>
    /// <param name="rowTranslatedPayload">The persisted translated payload.</param>
    /// <param name="expectedClassJobName">
    ///     The current normalized class or job identifier.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the row belongs to the same class or
    ///     job; otherwise <see langword="false" />.
    /// </returns>
    private static bool PayloadMatchesClassJob(
        DbFirstGameWindowPayload rowOriginalPayload,
        DbFirstGameWindowPayload rowTranslatedPayload,
        string expectedClassJobName)
    {
        return
            (TryGetPayloadClassJobName(
                 rowOriginalPayload,
                 out var originalClassJobName) &&
             string.Equals(
                 originalClassJobName,
                 expectedClassJobName,
                 StringComparison.Ordinal)) ||
            (TryGetPayloadClassJobName(
                 rowTranslatedPayload,
                 out var translatedClassJobName) &&
             string.Equals(
                 translatedClassJobName,
                 expectedClassJobName,
                 StringComparison.Ordinal));
    }

    /// <summary>
    ///     Appends one numeric payload map pair into the forward and reverse
    ///     ActionMenu lookup maps.
    /// </summary>
    /// <param name="originalValues">The original values.</param>
    /// <param name="translatedValues">The translated values.</param>
    /// <param name="translatedLookup">The original-to-translated lookup.</param>
    /// <param name="originalLookup">The translated-to-original lookup.</param>
    /// <param name="ambiguousOriginalKeys">
    ///     Tracks translated texts that map to multiple originals.
    /// </param>
    private static void AppendLookupEntries(
        IReadOnlyDictionary<int, string> originalValues,
        IReadOnlyDictionary<int, string> translatedValues,
        IDictionary<string, string> translatedLookup,
        IDictionary<string, string> originalLookup,
        ISet<string> ambiguousOriginalKeys)
    {
        foreach (var (key, originalText) in originalValues)
        {
            if (!translatedValues.TryGetValue(key, out var translatedText))
            {
                continue;
            }

            TryAddLookupEntry(
                originalText,
                translatedText,
                translatedLookup,
                originalLookup,
                ambiguousOriginalKeys);
        }
    }

    /// <summary>
    ///     Appends one text-node payload map pair into the forward and reverse
    ///     ActionMenu lookup maps.
    /// </summary>
    /// <param name="originalValues">The original values.</param>
    /// <param name="translatedValues">The translated values.</param>
    /// <param name="translatedLookup">The original-to-translated lookup.</param>
    /// <param name="originalLookup">The translated-to-original lookup.</param>
    /// <param name="ambiguousOriginalKeys">
    ///     Tracks translated texts that map to multiple originals.
    /// </param>
    private static void AppendLookupEntries(
        IReadOnlyDictionary<string, string> originalValues,
        IReadOnlyDictionary<string, string> translatedValues,
        IDictionary<string, string> translatedLookup,
        IDictionary<string, string> originalLookup,
        ISet<string> ambiguousOriginalKeys)
    {
        foreach (var (key, originalText) in originalValues)
        {
            if (!translatedValues.TryGetValue(key, out var translatedText))
            {
                continue;
            }

            TryAddLookupEntry(
                originalText,
                translatedText,
                translatedLookup,
                originalLookup,
                ambiguousOriginalKeys);
        }
    }

    /// <summary>
    ///     Adds one original/translated text pair to the forward and reverse
    ///     ActionMenu lookup maps.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <param name="translatedLookup">The original-to-translated lookup.</param>
    /// <param name="originalLookup">The translated-to-original lookup.</param>
    /// <param name="ambiguousOriginalKeys">
    ///     Tracks translated texts that map to multiple originals.
    /// </param>
    private static void TryAddLookupEntry(
        string? originalText,
        string? translatedText,
        IDictionary<string, string> translatedLookup,
        IDictionary<string, string> originalLookup,
        ISet<string> ambiguousOriginalKeys)
    {
        TryAddLookupPair(
            originalText,
            translatedText,
            translatedLookup,
            originalLookup,
            ambiguousOriginalKeys);

        if (!TryParseTrailingLevelToken(
                originalText ?? string.Empty,
                out var originalPrefix,
                out _,
                out _,
                out var originalLevelNumber) ||
            !TryParseTrailingLevelToken(
                translatedText ?? string.Empty,
                out var translatedPrefix,
                out _,
                out _,
                out var translatedLevelNumber) ||
            !string.Equals(
                originalLevelNumber,
                translatedLevelNumber,
                StringComparison.Ordinal))
        {
            return;
        }

        TryAddLookupPair(
            originalPrefix,
            translatedPrefix,
            translatedLookup,
            originalLookup,
            ambiguousOriginalKeys);
    }

    /// <summary>
    ///     Adds one original/translated text pair to the forward and reverse
    ///     ActionMenu lookup maps without additional normalization.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <param name="translatedLookup">The original-to-translated lookup.</param>
    /// <param name="originalLookup">The translated-to-original lookup.</param>
    /// <param name="ambiguousOriginalKeys">
    ///     Tracks translated texts that map to multiple originals.
    /// </param>
    private static void TryAddLookupPair(
        string? originalText,
        string? translatedText,
        IDictionary<string, string> translatedLookup,
        IDictionary<string, string> originalLookup,
        ISet<string> ambiguousOriginalKeys)
    {
        if (string.IsNullOrWhiteSpace(originalText) ||
            string.IsNullOrWhiteSpace(translatedText) ||
            string.Equals(
                originalText,
                translatedText,
                StringComparison.Ordinal))
        {
            return;
        }

        translatedLookup[originalText] = translatedText;

        if (ambiguousOriginalKeys.Contains(translatedText))
        {
            return;
        }

        if (originalLookup.TryGetValue(translatedText, out var existingOriginal) &&
            !string.Equals(
                existingOriginal,
                originalText,
                StringComparison.Ordinal))
        {
            originalLookup.Remove(translatedText);
            ambiguousOriginalKeys.Add(translatedText);
            return;
        }

        originalLookup[translatedText] = originalText;
    }
}
