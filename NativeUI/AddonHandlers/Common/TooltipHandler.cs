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

namespace Echoglossian.NativeUI.AddonHandlers.Common;

/// <summary>
///     Handles DB-first translation for the Tooltip addon.
/// </summary>
internal sealed unsafe class TooltipHandler : DbFirstGameWindowAddonHandler
{
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
