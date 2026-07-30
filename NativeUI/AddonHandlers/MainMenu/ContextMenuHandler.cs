// <copyright file="ContextMenuHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.AddonHandlers.Toasts;
using Echoglossian.NativeUI.Helpers;
using Newtonsoft.Json;

namespace Echoglossian.NativeUI.AddonHandlers.MainMenu;

/// <summary>
///     Handles DB-first translation for the ContextMenu addon.
/// </summary>
internal sealed unsafe class ContextMenuHandler : DbFirstGameWindowAddonHandler
{
    private const int ContextMenuRowsNodeListIndex = 2;
    private const int FirstContextMenuRowNodeIndex = 1;
    private const int ContextMenuRowCollisionNodeIndex = 3;

    private readonly Func<ContextMenuText, ContextMenuText?> findContextMenuText;
    private readonly Func<ContextMenuText, Task<string>> insertContextMenuTextAsync;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextMenuHandler" />
    ///     class.
    /// </summary>
    /// <param name="configuration">The configuration settings for the plugin.</param>
    /// <param name="hoverTooltipManager">The shared hover-tooltip manager.</param>
    /// <param name="translationService">The service used for translating text.</param>
    /// <param name="findContextMenuText">
    ///     Resolves a persisted dedicated ContextMenu payload.
    /// </param>
    /// <param name="insertContextMenuTextAsync">
    ///     Persists a dedicated ContextMenu payload asynchronously.
    /// </param>
    public ContextMenuHandler(
        Config configuration,
        HoverTooltipManager hoverTooltipManager,
        TranslationService translationService,
        Func<ContextMenuText, ContextMenuText?> findContextMenuText,
        Func<ContextMenuText, Task<string>> insertContextMenuTextAsync)
        : base(
            addonName: "ContextMenu",
            config: configuration,
            hoverTooltipManager: hoverTooltipManager,
            translationService: translationService,
            enabledSelector: static configuration => configuration.TranslateContextMenu,
            useAtkValues: false,
            useTextNodes: true,
            displayModeSelector: static configuration =>
                configuration.ContextMenuTranslationDisplayMode)
    {
        this.findContextMenuText = findContextMenuText;
        this.insertContextMenuTextAsync = insertContextMenuTextAsync;
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
        var lookup = this.CreateContextMenuText(
            scope,
            originalPayload,
            translatedPayload: null);
        var storedPayload = this.findContextMenuText(lookup);
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
    protected override List<nint> ResolveTextNodeAddresses(AtkUnitBase* addon)
    {
        return ResolveContextMenuRowTextNodes(addon).ToList();
    }

    /// <inheritdoc />
    private protected override bool TryPersistDedicatedPayload(
        TranslationReuseScope scope,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload)
    {
        var row = this.CreateContextMenuText(
            scope,
            originalPayload,
            translatedPayload);
        _ = this.insertContextMenuTextAsync(row);
        return true;
    }

    /// <inheritdoc />
    private protected override bool TryRegisterCustomHoverTooltips(
        AtkUnitBase* addon,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload translatedPayload,
        JournalTranslationDisplayMode displayMode)
    {
        var originalTexts = GetOrderedTextNodes(originalPayload.TextNodes)
            .Select(pair => pair.Value)
            .ToList();
        var translatedTexts = GetOrderedTextNodes(translatedPayload.TextNodes)
            .Select(pair => pair.Value)
            .ToList();
        var registeredAny = false;

        foreach (var (index, collisionNodeAddress) in ResolveContextMenuRows(
                     addon).Select((row, index) => (index, row.CollisionNode)))
        {
            if (index >= originalTexts.Count || index >= translatedTexts.Count)
            {
                break;
            }

            var collisionNode = (AtkResNode*)collisionNodeAddress;
            if (collisionNode == null)
            {
                continue;
            }

            this.RegisterTranslatedHoverTooltip(
                $"row-{index}",
                new Vector2(collisionNode->ScreenX, collisionNode->ScreenY),
                new Vector2(
                    collisionNode->ScreenX + Math.Max(1f, collisionNode->Width),
                    collisionNode->ScreenY + Math.Max(1f, collisionNode->Height)),
                originalTexts[index],
                translatedTexts[index],
                displayMode);
            registeredAny = true;
        }

        return registeredAny;
    }

    /// <summary>
    ///     Resolves the visible ContextMenu row label text nodes in row-chain
    ///     order.
    /// </summary>
    /// <param name="addon">The live ContextMenu addon.</param>
    /// <returns>The visible row label text-node addresses.</returns>
    private static unsafe IEnumerable<nint> ResolveContextMenuRowTextNodes(
        AtkUnitBase* addon)
    {
        return ResolveContextMenuRows(addon).Select(row => row.TextNode);
    }

    /// <summary>
    ///     Resolves the visible row labels and collision nodes from the
    ///     ContextMenu row chain.
    /// </summary>
    /// <param name="addon">The live ContextMenu addon.</param>
    /// <returns>The visible row label and collision-node pairs.</returns>
    private static unsafe IEnumerable<(nint TextNode, nint CollisionNode)>
        ResolveContextMenuRows(AtkUnitBase* addon)
    {
        List<(nint TextNode, nint CollisionNode)> rows = [];
        if (addon == null ||
            addon->UldManager.NodeList == null ||
            addon->UldManager.NodeListCount <= ContextMenuRowsNodeListIndex)
        {
            return rows;
        }

        var rowsComponentNode = (AtkComponentNode*)addon->UldManager.NodeList[
            ContextMenuRowsNodeListIndex];
        if (rowsComponentNode == null ||
            rowsComponentNode->Component == null ||
            rowsComponentNode->Component->UldManager.NodeList == null ||
            rowsComponentNode->Component->UldManager.NodeListCount <=
            FirstContextMenuRowNodeIndex)
        {
            return rows;
        }

        for (var rowNode = rowsComponentNode->Component->UldManager.NodeList[
                 FirstContextMenuRowNodeIndex];
             rowNode != null;
             rowNode = rowNode->NextSiblingNode)
        {
            if (!rowNode->IsVisible() || (ushort)rowNode->Type < 1000)
            {
                continue;
            }

            var rowComponentNode = (AtkComponentNode*)rowNode;
            var rowComponent = rowComponentNode->Component;
            if (rowComponent == null ||
                rowComponent->UldManager.NodeList == null ||
                rowComponent->UldManager.NodeListCount <=
                ContextMenuRowCollisionNodeIndex)
            {
                continue;
            }

            var componentNodes = rowComponent->UldManager.NodeList;
            // ComponentNodes[3] is the visible row collision node; its Next
            // sibling is the row label text node. ComponentRoot shares its bounds.
            var collisionNode = componentNodes[ContextMenuRowCollisionNodeIndex];
            if (collisionNode == null)
            {
                continue;
            }

            var textNode = collisionNode->NextSiblingNode;
            if (textNode == null ||
                !textNode->IsVisible() ||
                textNode->Type != NodeType.Text)
            {
                continue;
            }

            rows.Add(((nint)textNode, (nint)collisionNode));
        }

        return rows;
    }

    /// <summary>
    ///     Builds one dedicated ContextMenu persistence row from the ordered
    ///     visible text-node payload.
    /// </summary>
    /// <param name="scope">The immutable source, target, and engine scope.</param>
    /// <param name="originalPayload">The ordered original-facing payload.</param>
    /// <param name="translatedPayload">The optional ordered translated payload.</param>
    /// <returns>The dedicated ContextMenu row.</returns>
    private ContextMenuText CreateContextMenuText(
        TranslationReuseScope scope,
        DbFirstGameWindowPayload originalPayload,
        DbFirstGameWindowPayload? translatedPayload)
    {
        var originalTextsAsText = JsonConvert.SerializeObject(
            GetOrderedTextNodes(originalPayload.TextNodes)
                .Select(pair => pair.Value));
        return new ContextMenuText
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
    ///     Projects one ordered dedicated ContextMenu payload onto the current
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
    ///     Parses the numeric components of one DB-first text-node key.
    /// </summary>
    /// <param name="textNodeKey">The stable node-id and ordinal key.</param>
    /// <returns>The numeric key parts, or maximum values for an invalid key.</returns>
    private static (uint NodeId, int Ordinal) GetTextNodeKeyOrder(
        string textNodeKey)
    {
        var separatorIndex = textNodeKey.LastIndexOf(':');
        if (separatorIndex > 0 &&
            uint.TryParse(textNodeKey.AsSpan(0, separatorIndex), out var nodeId) &&
            int.TryParse(textNodeKey.AsSpan(separatorIndex + 1), out var ordinal))
        {
            return (nodeId, ordinal);
        }

        return (uint.MaxValue, int.MaxValue);
    }

    /// <summary>
    ///     Computes the stable hash used to key one ordered ContextMenu
    ///     original payload.
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
