// <copyright file="TooltipHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Helpers;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using Lumina.Text.ReadOnly;

namespace Echoglossian.NativeUI.AddonHandlers.Common;

/// <summary>
///     Handles DB-first translation for the Tooltip addon.
/// </summary>
internal sealed unsafe class TooltipHandler : DbFirstGameWindowAddonHandler
{
    private readonly Dictionary<string, NativeTextNodeLayoutSnapshot>
        appliedLayoutSnapshots = new(StringComparer.Ordinal);
    private readonly List<string> pendingCapturedTexts = [];
    private readonly Func<TooltipText, TooltipText?> findTooltipText;
    private readonly Func<TooltipText, Task<string>> insertTooltipTextAsync;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TooltipHandler" />
    ///     class.
    /// </summary>
    /// <param name="configuration">The plugin configuration.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The translation service.</param>
    /// <param name="findTooltipText">Resolves a persisted Tooltip payload.</param>
    /// <param name="insertTooltipTextAsync">
    ///     Persists a Tooltip payload asynchronously.
    /// </param>
    public TooltipHandler(
        Config configuration,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService,
        Func<TooltipText, TooltipText?> findTooltipText,
        Func<TooltipText, Task<string>> insertTooltipTextAsync)
        : base(
            addonName: "Tooltip",
            config: configuration,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration => configuration.TranslateTooltipAddon,
            useAtkValues: false,
            useTextNodes: true,
            displayModeSelector: static configuration =>
                configuration.TooltipAddonTranslationDisplayMode)
    {
        this.findTooltipText = findTooltipText;
        this.insertTooltipTextAsync = insertTooltipTextAsync;
    }

    /// <inheritdoc />
    protected override List<nint> ResolveTextNodeAddresses(AtkUnitBase* addon)
    {
        return AddonTextNodeResolvers.ResolveReadableTextNodes(addon);
    }

    /// <inheritdoc />
    protected override void OnCleanupEvent(AddonEvent evt, AddonArgs args)
    {
        base.OnCleanupEvent(evt, args);
        this.appliedLayoutSnapshots.Clear();
        this.pendingCapturedTexts.Clear();
    }

    /// <inheritdoc />
    public override void OnPluginUnload()
    {
        base.OnPluginUnload();
        this.appliedLayoutSnapshots.Clear();
        this.pendingCapturedTexts.Clear();
    }

    /// <inheritdoc />
    protected override bool ShouldCaptureTextNode(
        AtkTextNode* textNode,
        string visibleText)
    {
        var normalizedVisibleText =
            TooltipTextNormalizationHelper.NormalizeForCapture(visibleText);
        var preferredCaptureText =
            ResolvePreferredCaptureText(textNode, normalizedVisibleText);
        this.pendingCapturedTexts.Add(preferredCaptureText);
        return !string.IsNullOrWhiteSpace(preferredCaptureText);
    }

    /// <inheritdoc />
    protected override SortedDictionary<string, string> NormalizeCapturedTextNodes(
        SortedDictionary<string, string> capturedTextNodes)
    {
        var normalizedTextNodes = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        try
        {
            var captureIndex = 0;
            foreach (var (key, value) in capturedTextNodes)
            {
                var normalizedText = captureIndex < this.pendingCapturedTexts.Count
                    ? this.pendingCapturedTexts[captureIndex]
                    : TooltipTextNormalizationHelper.NormalizeForCapture(value);
                captureIndex++;
                if (string.IsNullOrWhiteSpace(normalizedText))
                {
                    continue;
                }

                normalizedTextNodes[key] = normalizedText;
            }

            return normalizedTextNodes;
        }
        finally
        {
            this.pendingCapturedTexts.Clear();
        }
    }

    /// <inheritdoc />
    private protected override bool TryResolveSupplementalTranslatedPayload(
        SourceClientLanguage sourceLanguage,
        DbFirstGameWindowPayload originalPayload,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;
        var scope = new TranslationReuseScope(
            sourceLanguage.PersistenceCode,
            RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(
                this.HandlerConfig.Lang),
            this.GetOperationTranslationEngineId(),
            this.HandlerConfig.TranslateAlreadyTranslatedTexts);
        var lookup = this.CreateTooltipText(
            scope,
            originalPayload,
            translatedPayload: null);
        var storedPayload = this.findTooltipText(lookup);
        if (storedPayload == null ||
            !TryProjectStoredTranslatedPayload(
                originalPayload,
                storedPayload.TranslatedTextsAsText,
                out translatedPayload))
        {
            translatedPayload = DbFirstGameWindowPayload.Empty;
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    private protected override bool TryPersistDedicatedPayload(
        TranslationReuseScope scope,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        var row = this.CreateTooltipText(
            scope,
            originalPayload,
            translatedPayload);
        _ = this.insertTooltipTextAsync(row);
        return true;
    }

    /// <inheritdoc />
    private protected override bool TryApplyCustomTextNodePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload sourcePayload,
        DbFirstGameWindowPayload targetPayload)
    {
        var ordinalsByNodeId = new Dictionary<uint, int>();

        foreach (var nodeAddress in this.ResolveTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null ||
                !this.IsEffectivelyVisible((AtkResNode*)textNode))
            {
                continue;
            }

            var textNodeKey = DbFirstTextNodeKeyAllocator.ConsumeVisibleNode(
                ordinalsByNodeId,
                textNode->AtkResNode.NodeId);
            if (!sourcePayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var sourceText) ||
                !targetPayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var targetText))
            {
                continue;
            }

            var currentText = this.ReadTextNode(textNode);
            if (string.Equals(
                    NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                        currentText),
                    NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                        targetText),
                    StringComparison.Ordinal))
            {
                continue;
            }

            var layoutSnapshot =
                NativeTextNodeLayoutHelper.ApplyTextReplacementWithInferredReflow(
                    addon,
                    textNode,
                    targetText);
            if (layoutSnapshot != null)
            {
                this.appliedLayoutSnapshots[textNodeKey] = layoutSnapshot;
            }
        }

        return true;
    }

    /// <summary>
    ///     Prefers the original source text when the visible Tooltip text only
    ///     differs by wrap markers or inline formatting payloads, but keeps the
    ///     live visible text once the game is already showing translated
    ///     content.
    /// </summary>
    /// <param name="textNode">The live Tooltip text node.</param>
    /// <param name="normalizedVisibleText">The normalized visible text.</param>
    /// <returns>The preferred semantic capture text.</returns>
    private static string ResolvePreferredCaptureText(
        AtkTextNode* textNode,
        string normalizedVisibleText)
    {
        if (string.IsNullOrWhiteSpace(normalizedVisibleText))
        {
            return string.Empty;
        }

        var normalizedSourceText = ReadNormalizedSourceText(textNode);
        if (string.IsNullOrWhiteSpace(normalizedSourceText))
        {
            return normalizedVisibleText;
        }

        var comparisonVisibleText =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                normalizedVisibleText);
        var comparisonSourceText =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                normalizedSourceText);
        if (string.IsNullOrWhiteSpace(comparisonVisibleText) ||
            string.IsNullOrWhiteSpace(comparisonSourceText))
        {
            return normalizedVisibleText;
        }

        return string.Equals(
                   comparisonVisibleText,
                   comparisonSourceText,
                   StringComparison.Ordinal) ||
               comparisonVisibleText.Contains(
                   comparisonSourceText,
                   StringComparison.Ordinal) ||
               comparisonSourceText.Contains(
                   comparisonVisibleText,
                   StringComparison.Ordinal)
            ? normalizedSourceText
            : normalizedVisibleText;
    }

    /// <summary>
    ///     Reads the original Tooltip source text without live wrap markers so
    ///     capture can persist a semantic source string when the live text is
    ///     still game-owned.
    /// </summary>
    /// <param name="textNode">The live Tooltip text node.</param>
    /// <returns>The normalized source text, or an empty string.</returns>
    private static string ReadNormalizedSourceText(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return string.Empty;
        }

        try
        {
            var sourceText = textNode->OriginalTextPointer
                .AsReadOnlySeStringSpan()
                .ExtractText();
            return TooltipTextNormalizationHelper.NormalizeForCapture(sourceText);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    private protected override void AfterRestorePayload(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload translatedPayload,
        DbFirstGameWindowPayload originalPayload)
    {
        if (this.appliedLayoutSnapshots.Count == 0)
        {
            return;
        }

        var restoredKeys = new List<string>();
        var ordinalsByNodeId = new Dictionary<uint, int>();
        foreach (var nodeAddress in this.ResolveTextNodeAddresses(addon))
        {
            var textNode = (AtkTextNode*)nodeAddress;
            if (textNode == null)
            {
                continue;
            }

            var textNodeKey = DbFirstTextNodeKeyAllocator.ConsumeVisibleNode(
                ordinalsByNodeId,
                textNode->AtkResNode.NodeId);
            if (!this.appliedLayoutSnapshots.TryGetValue(
                    textNodeKey,
                    out var layoutSnapshot) ||
                !originalPayload.TextNodes.TryGetValue(
                    textNodeKey,
                    out var originalText))
            {
                continue;
            }

            NativeTextNodeLayoutHelper.RestoreLayoutSnapshot(
                layoutSnapshot,
                originalText,
                restoreText: false);
            restoredKeys.Add(textNodeKey);
        }

        foreach (var restoredKey in restoredKeys)
        {
            this.appliedLayoutSnapshots.Remove(restoredKey);
        }
    }

    /// <summary>
    ///     Builds one dedicated Tooltip persistence row from the ordered
    ///     visible text-node payload.
    /// </summary>
    /// <param name="scope">The immutable source, target, and engine scope.</param>
    /// <param name="originalPayload">The ordered original-facing payload.</param>
    /// <param name="translatedPayload">The optional ordered translated payload.</param>
    /// <returns>The dedicated Tooltip row.</returns>
    private TooltipText CreateTooltipText(
        TranslationReuseScope scope,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload? translatedPayload)
    {
        var originalTextsAsText = JsonConvert.SerializeObject(
            GetOrderedTextNodes(originalPayload.TextNodes)
                .Select(pair => pair.Value));
        return new TooltipText
        {
            AddonName = this.AddonName,
            OriginalTextsAsText = originalTextsAsText,
            OriginalLang = scope.SourceLanguageCode,
            TranslatedTextsAsText = translatedPayload.HasValue
                ? JsonConvert.SerializeObject(
                    GetOrderedTextNodes(translatedPayload.Value.TextNodes)
                        .Select(pair => pair.Value))
                : string.Empty,
            TranslationLang = scope.TargetLanguageCode,
            TranslationEngine = scope.TranslationEngine,
            GameVersion = GetGameVersion() ?? string.Empty,
            SourceContentHash = ComputeSourceContentHash(originalTextsAsText),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };
    }

    /// <summary>
    ///     Projects one ordered dedicated Tooltip payload onto the current
    ///     text-node keys.
    /// </summary>
    /// <param name="originalPayload">The visible original-facing payload.</param>
    /// <param name="translatedTextsAsText">The stored ordered translations.</param>
    /// <param name="translatedPayload">Receives the projected payload.</param>
    /// <returns>
    ///     <see langword="true" /> when the stored ordered payload matches the
    ///     visible text-node shape; otherwise <see langword="false" />.
    /// </returns>
    private static bool TryProjectStoredTranslatedPayload(
        DbFirstGameWindowPayload originalPayload,
        string? translatedTextsAsText,
        out DbFirstGameWindowPayload translatedPayload)
    {
        translatedPayload = DbFirstGameWindowPayload.Empty;

        try
        {
            var translatedTexts = JsonConvert.DeserializeObject<List<string>>(
                translatedTextsAsText ?? string.Empty);
            if (translatedTexts == null ||
                translatedTexts.Count != originalPayload.TextNodes.Count ||
                translatedTexts.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            var translatedTextNodes = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            using var originalKeys = GetOrderedTextNodes(
                    originalPayload.TextNodes)
                .Select(pair => pair.Key)
                .GetEnumerator();
            using var translatedValues = translatedTexts.GetEnumerator();
            while (originalKeys.MoveNext() && translatedValues.MoveNext())
            {
                translatedTextNodes[originalKeys.Current] = translatedValues.Current;
            }

            translatedPayload = new DbFirstGameWindowPayload(
                [],
                [],
                translatedTextNodes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Orders visible text-node payload entries by their numeric node id
    ///     and duplicate visible-node ordinal.
    /// </summary>
    /// <param name="textNodes">The text-node payload to order.</param>
    /// <returns>The entries in their stable numeric node-key order.</returns>
    private static IEnumerable<KeyValuePair<string, string>> GetOrderedTextNodes(
        SortedDictionary<string, string> textNodes)
    {
        return textNodes
            .OrderBy(pair => GetTextNodeKeyOrder(pair.Key).NodeId)
            .ThenBy(pair => GetTextNodeKeyOrder(pair.Key).Ordinal)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Parses one text-node payload key into numeric order parts.
    /// </summary>
    /// <param name="key">The text-node key.</param>
    /// <returns>The parsed node id and duplicate ordinal.</returns>
    private static (uint NodeId, int Ordinal) GetTextNodeKeyOrder(string key)
    {
        var separators = key.Split(':', 2);
        return separators.Length == 2 &&
               uint.TryParse(separators[0], out var nodeId) &&
               int.TryParse(separators[1], out var ordinal)
            ? (nodeId, ordinal)
            : (uint.MaxValue, int.MaxValue);
    }

    /// <summary>
    ///     Computes the stable hash used to key one ordered Tooltip original
    ///     payload.
    /// </summary>
    /// <param name="originalTextsAsText">The normalized ordered source JSON.</param>
    /// <returns>The uppercase SHA-256 hash.</returns>
    private static string ComputeSourceContentHash(string originalTextsAsText)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(originalTextsAsText));
        return Convert.ToHexString(bytes);
    }
}
