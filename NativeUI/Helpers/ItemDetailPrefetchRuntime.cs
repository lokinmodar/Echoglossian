// <copyright file="ItemDetailPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using Dalamud.Game.Gui;
using Dalamud.Utility;
using DetailKind = Dalamud.Game.Gui.DetailKind;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using DeepDungeonItemSheet = Lumina.Excel.Sheets.DeepDungeonItem;
using EventItemSheet = Lumina.Excel.Sheets.EventItem;
using ItemSheet = Lumina.Excel.Sheets.Item;

namespace Echoglossian;

/// <summary>
///     Provides DB-first background prefetch for canonical ItemDetail
///     payloads.
/// </summary>
public unsafe partial class Echoglossian
{
    private const int ItemDetailPrefetchItemsPerTick = 10;

    private static readonly TimeSpan ItemDetailPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

    private static readonly InventoryType[] PrefetchInventoryTypes =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.EquippedItems,
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryWaist,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.ArmorySoulCrystal,
    ];

    private readonly List<uint> itemDetailPrefetchQueue = [];

    private string itemDetailPrefetchSignature = string.Empty;

    private DateTime itemDetailPrefetchLastTickUtc = DateTime.MinValue;

    private int itemDetailPrefetchQueueIndex;

    /// <summary>
    ///     Describes which canonical sheet family produced one ItemDetail
    ///     payload.
    /// </summary>
    private enum StructuredTooltipItemSourceKind
    {
        /// <summary>
        ///     No source family resolved.
        /// </summary>
        None = 0,

        /// <summary>
        ///     The standard <c>Item</c> sheet resolved the payload.
        /// </summary>
        Item = 1,

        /// <summary>
        ///     The <c>EventItem</c> sheet resolved the payload.
        /// </summary>
        EventItem = 2,

        /// <summary>
        ///     The <c>DeepDungeonItem</c> sheet resolved the payload.
        /// </summary>
        DeepDungeonItem = 3,
    }

    /// <summary>
    ///     Ticks the item-tooltip prefetch runtime so current inventory surfaces
    ///     are translated into canonical storage ahead of tooltip use.
    /// </summary>
    private void TickItemDetailPrefetch()
    {
        if (!this.ShouldPrefetchStructuredTooltips() ||
            DateTime.UtcNow - this.itemDetailPrefetchLastTickUtc <
            ItemDetailPrefetchTickInterval)
        {
            return;
        }

        this.itemDetailPrefetchLastTickUtc = DateTime.UtcNow;

        if (!TryCollectTrackedItemIds(out var itemIds))
        {
            this.ClearItemDetailPrefetchState();
            return;
        }

        var signature = string.Join(',', itemIds);
        if (!string.Equals(
                this.itemDetailPrefetchSignature,
                signature,
                StringComparison.Ordinal))
        {
            this.itemDetailPrefetchSignature = signature;
            this.itemDetailPrefetchQueue.Clear();
            this.itemDetailPrefetchQueue.AddRange(itemIds);
            this.itemDetailPrefetchQueueIndex = 0;
        }

        if (this.itemDetailPrefetchQueueIndex >=
            this.itemDetailPrefetchQueue.Count)
        {
            return;
        }

        var processedCount = 0;
        while (processedCount < ItemDetailPrefetchItemsPerTick &&
               this.itemDetailPrefetchQueueIndex <
               this.itemDetailPrefetchQueue.Count)
        {
            var itemId =
                this.itemDetailPrefetchQueue[this.itemDetailPrefetchQueueIndex++];
            this.PrefetchItemDetail(itemId);
            processedCount++;
        }
    }

    /// <summary>
    ///     Clears the item-tooltip prefetch runtime state.
    /// </summary>
    private void ClearItemDetailPrefetchState()
    {
        this.itemDetailPrefetchQueue.Clear();
        this.itemDetailPrefetchQueueIndex = 0;
        this.itemDetailPrefetchSignature = string.Empty;
        this.itemDetailPrefetchLastTickUtc = DateTime.MinValue;
    }

    /// <summary>
    ///     Gets whether structured action/item tooltips should be prefetched.
    /// </summary>
    /// <returns>True when the background prefetch should run.</returns>
    private bool ShouldPrefetchStructuredTooltips()
    {
        return this.configuration.Translate &&
               this.configuration.TranslateTooltips &&
               ClientStateInterface.IsLoggedIn;
    }

    /// <summary>
    ///     Gets whether action-adjacent canonical tooltip rows should be
    ///     prefetched for consumers such as <c>ActionMenu</c>.
    /// </summary>
    /// <returns>True when action-adjacent canonical prefetch should run.</returns>
    private bool ShouldPrefetchActionAdjacentCanonicalTooltips()
    {
        return this.configuration.Translate &&
               ClientStateInterface.IsLoggedIn &&
               (this.configuration.TranslateTooltips ||
                this.configuration.TranslateActionMenuWindow);
    }

    /// <summary>
    ///     Prefetches one canonical item-tooltip payload and any missing translations.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    private void PrefetchItemDetail(uint itemId)
    {
        if (!TryBuildItemTooltipCanonicalPayload(itemId, out var originalPayload))
        {
            return;
        }

        var originalRow = ItemTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var existingRow = this.FindItemTooltip(originalRow) ?? originalRow;
        this.InsertItemTooltip(originalRow);

        this.PrefetchItemDetailName(originalPayload, existingRow);
        this.PrefetchItemDetailDescription(originalPayload, existingRow);
    }

    /// <summary>
    ///     Prefetches the translated item name when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchItemDetailName(
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Name) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedItemName))
        {
            return;
        }

        var translationKey =
            BuildItemDetailNameTranslationKey(originalPayload);
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedName))
        {
            this.ApplyItemDetailTranslation(
                originalPayload.ItemId,
                translatedName: cachedTranslatedName);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Name,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedName => this.ApplyItemDetailTranslation(
                originalPayload.ItemId,
                translatedName: translatedName));
    }

    /// <summary>
    ///     Prefetches the translated item description when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchItemDetailDescription(
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Description) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedItemDescription))
        {
            return;
        }

        var translationKey =
            BuildItemDetailDescriptionTranslationKey(originalPayload);
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedDescription))
        {
            this.ApplyItemDetailTranslation(
                originalPayload.ItemId,
                translatedDescription: cachedTranslatedDescription);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Description,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedDescription => this.ApplyItemDetailTranslation(
                originalPayload.ItemId,
                translatedDescription: translatedDescription));
    }

    /// <summary>
    ///     Applies one resolved item-tooltip translation into canonical storage.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    private void ApplyItemDetailTranslation(
        uint itemId,
        string? translatedName = null,
        string? translatedDescription = null)
    {
        if (!TryBuildItemTooltipCanonicalPayload(itemId, out var originalPayload))
        {
            return;
        }

        var existingProbe = ItemTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var existingRow = this.FindItemTooltip(existingProbe);
        var translatedPayload = existingRow == null
            ? originalPayload
            : ItemTooltipCanonicalPayload.Deserialize(
                    existingRow.CanonicalPayloadAsText) ??
                originalPayload;

        translatedPayload.ItemId = originalPayload.ItemId;
        translatedPayload.IconId = originalPayload.IconId;
        translatedPayload.ItemActionId = originalPayload.ItemActionId;
        translatedPayload.ItemUiCategoryId = originalPayload.ItemUiCategoryId;
        translatedPayload.ClassJobCategoryId =
            originalPayload.ClassJobCategoryId;
        translatedPayload.Name = originalPayload.Name;
        translatedPayload.Description = originalPayload.Description;
        translatedPayload.TranslatedName =
            !string.IsNullOrWhiteSpace(translatedName)
                ? translatedName
                : translatedPayload.TranslatedName;
        translatedPayload.TranslatedDescription =
            !string.IsNullOrWhiteSpace(translatedDescription)
                ? translatedDescription
                : translatedPayload.TranslatedDescription;
        this.TryPopulatePendingItemDetailTranslations(
            originalPayload,
            translatedPayload);
        if (!translatedPayload.HasCompleteTranslation)
        {
            return;
        }

        var translatedRow = ItemTooltipPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload,
            translatedPayload);
        this.InsertItemTooltip(translatedRow);
    }

    /// <summary>
    ///     Tries to enrich one item-detail payload with any queued counterpart
    ///     translation so canonical persistence only happens when the payload is complete.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The partially translated payload.</param>
    private void TryPopulatePendingItemDetailTranslations(
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltipCanonicalPayload translatedPayload)
    {
        if (string.IsNullOrWhiteSpace(translatedPayload.TranslatedName) &&
            !string.IsNullOrWhiteSpace(originalPayload.Name) &&
            this.TryGetQueuedTranslation(
                BuildItemDetailNameTranslationKey(originalPayload),
                out var cachedTranslatedName))
        {
            translatedPayload.TranslatedName = cachedTranslatedName;
        }

        if (string.IsNullOrWhiteSpace(translatedPayload.TranslatedDescription) &&
            !string.IsNullOrWhiteSpace(originalPayload.Description) &&
            this.TryGetQueuedTranslation(
                BuildItemDetailDescriptionTranslationKey(originalPayload),
                out var cachedTranslatedDescription))
        {
            translatedPayload.TranslatedDescription =
                cachedTranslatedDescription;
        }
    }

    /// <summary>
    ///     Builds the stable queued-translation key for one item-detail name.
    /// </summary>
    /// <param name="payload">The canonical payload.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildItemDetailNameTranslationKey(
        ItemTooltipCanonicalPayload payload)
    {
        return $"ItemDetailPrefetch|{payload.ItemId}|Name|{payload.Name}";
    }

    /// <summary>
    ///     Builds the stable queued-translation key for one item-detail description.
    /// </summary>
    /// <param name="payload">The canonical payload.</param>
    /// <returns>The stable queue key.</returns>
    private static string BuildItemDetailDescriptionTranslationKey(
        ItemTooltipCanonicalPayload payload)
    {
        return
            $"ItemDetailPrefetch|{payload.ItemId}|Description|{payload.Description}";
    }

    /// <summary>
    ///     Tries to collect tracked item ids from inventory, armory, equipment, and hotbars.
    /// </summary>
    /// <param name="itemIds">The collected item ids.</param>
    /// <returns>True when item ids were collected successfully.</returns>
    private static bool TryCollectTrackedItemIds(out List<uint> itemIds)
    {
        itemIds = [];

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            return false;
        }

        HashSet<uint> uniqueItemIds = [];
        foreach (var inventoryType in PrefetchInventoryTypes)
        {
            var container = inventoryManager->GetInventoryContainer(inventoryType);
            if (container == null || !container->IsLoaded || container->Items == null)
            {
                continue;
            }

            for (var index = 0; index < container->Size; index++)
            {
                var item = container->GetInventorySlot(index);
                if (item == null || item->IsEmpty())
                {
                    continue;
                }

                var itemId = item->GetBaseItemId();
                if (itemId > 0)
                {
                    uniqueItemIds.Add(itemId);
                }
            }
        }

        var hotbarModule = RaptureHotbarModule.Instance();
        if (hotbarModule != null && hotbarModule->ModuleReady)
        {
            for (uint hotbarId = 0; hotbarId < 18; hotbarId++)
            {
                for (uint slotId = 0; slotId < 16; slotId++)
                {
                    var slot = hotbarModule->GetSlotById(hotbarId, slotId);
                    if (slot == null || slot->IsEmpty)
                    {
                        continue;
                    }

                    if (slot->ApparentSlotType is not
                        (RaptureHotbarModule.HotbarSlotType.Item or
                         RaptureHotbarModule.HotbarSlotType.InventoryItem))
                    {
                        continue;
                    }

                    var itemId = slot->ApparentActionId != 0
                        ? slot->ApparentActionId
                        : slot->CommandId;
                    if (itemId > 0)
                    {
                        uniqueItemIds.Add(itemId);
                    }
                }
            }
        }

        itemIds = uniqueItemIds.OrderBy(id => id).ToList();
        return itemIds.Count > 0;
    }

    /// <summary>
    ///     Tries to build one canonical item-tooltip payload from the active
    ///     item-adjacent sheets.
    /// </summary>
    /// <param name="rawItemId">The raw hovered or agent item identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildItemTooltipCanonicalPayload(
        uint rawItemId,
        out ItemTooltipCanonicalPayload payload)
    {
        return TryBuildItemTooltipCanonicalPayload(
            rawItemId,
            DetailKind.None,
            out payload,
            out _);
    }

    /// <summary>
    ///     Tries to build one canonical item-tooltip payload from the active
    ///     item-adjacent sheets while also reporting the source family.
    /// </summary>
    /// <param name="rawItemId">The raw hovered or agent item identifier.</param>
    /// <param name="hoverActionKind">The current hover action kind, if any.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <param name="sourceKind">The sheet family that produced the payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildItemTooltipCanonicalPayload(
        uint rawItemId,
        DetailKind hoverActionKind,
        out ItemTooltipCanonicalPayload payload,
        out StructuredTooltipItemSourceKind sourceKind)
    {
        payload = new ItemTooltipCanonicalPayload();
        sourceKind = StructuredTooltipItemSourceKind.None;

        if (rawItemId == 0)
        {
            return false;
        }

        if (hoverActionKind == DetailKind.DeepDungeonItem &&
            TryBuildDeepDungeonItemCanonicalPayload(
                rawItemId,
                out payload))
        {
            sourceKind = StructuredTooltipItemSourceKind.DeepDungeonItem;
            return true;
        }

        if (ItemUtil.IsEventItem(rawItemId) &&
            TryBuildEventItemCanonicalPayload(
                rawItemId,
                out payload))
        {
            sourceKind = StructuredTooltipItemSourceKind.EventItem;
            return true;
        }

        var normalizedItemId = NormalizeStandardItemReferenceId(rawItemId);
        if (normalizedItemId != 0 &&
            TryBuildStandardItemTooltipCanonicalPayload(
                normalizedItemId,
                out payload))
        {
            sourceKind = StructuredTooltipItemSourceKind.Item;
            return true;
        }

        if (TryBuildDeepDungeonItemCanonicalPayload(
                rawItemId,
                out payload))
        {
            sourceKind = StructuredTooltipItemSourceKind.DeepDungeonItem;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Normalizes one raw item id to the base <c>Item</c> sheet row
    ///     identifier.
    /// </summary>
    /// <param name="rawItemId">The raw hovered or agent item identifier.</param>
    /// <returns>The normalized base <c>Item</c> row identifier.</returns>
    private static uint NormalizeStandardItemReferenceId(uint rawItemId)
    {
        if (rawItemId == 0)
        {
            return 0;
        }

        var (itemId, _) = ItemUtil.GetBaseId(rawItemId);
        return itemId;
    }

    /// <summary>
    ///     Tries to build one canonical payload from the standard
    ///     <c>Item</c> sheet.
    /// </summary>
    /// <param name="itemId">The normalized <c>Item</c> row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildStandardItemTooltipCanonicalPayload(
        uint itemId,
        out ItemTooltipCanonicalPayload payload)
    {
        payload = new ItemTooltipCanonicalPayload();

        var itemSheet =
            DManager.GetExcelSheet<ItemSheet>(ClientStateInterface.ClientLanguage);
        if (itemSheet == null || !itemSheet.TryGetRow(itemId, out var itemRow))
        {
            return false;
        }

        payload = new ItemTooltipCanonicalPayload
        {
            ItemId = itemRow.RowId,
            IconId = (uint)itemRow.Icon,
            ItemActionId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                itemRow.ItemAction.RowId),
            ItemUiCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                itemRow.ItemUICategory.RowId),
            ClassJobCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                itemRow.ClassJobCategory.RowId),
            Name = itemRow.Name.ExtractText(),
            Description = itemRow.Description.ExtractText(),
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }

    /// <summary>
    ///     Tries to build one canonical payload from the <c>EventItem</c>
    ///     sheet.
    /// </summary>
    /// <param name="eventItemId">The <c>EventItem</c> row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildEventItemCanonicalPayload(
        uint eventItemId,
        out ItemTooltipCanonicalPayload payload)
    {
        payload = new ItemTooltipCanonicalPayload();

        var eventItemSheet =
            DManager.GetExcelSheet<EventItemSheet>(
                ClientStateInterface.ClientLanguage);
        if (eventItemSheet == null ||
            !eventItemSheet.TryGetRow(eventItemId, out var eventItemRow))
        {
            return false;
        }

        payload = new ItemTooltipCanonicalPayload
        {
            ItemId = eventItemRow.RowId,
            IconId = eventItemRow.Icon,
            ItemActionId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                eventItemRow.Action.RowId),
            ItemUiCategoryId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                eventItemRow.Category.RowId),
            ClassJobCategoryId = 0,
            Name = eventItemRow.Name.ExtractText(),
            Description = string.Empty,
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }

    /// <summary>
    ///     Tries to build one canonical payload from the
    ///     <c>DeepDungeonItem</c> sheet.
    /// </summary>
    /// <param name="deepDungeonItemId">
    ///     The <c>DeepDungeonItem</c> row identifier.
    /// </param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildDeepDungeonItemCanonicalPayload(
        uint deepDungeonItemId,
        out ItemTooltipCanonicalPayload payload)
    {
        payload = new ItemTooltipCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<DeepDungeonItemSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null ||
            !sheet.TryGetRow(deepDungeonItemId, out var row))
        {
            return false;
        }

        payload = new ItemTooltipCanonicalPayload
        {
            ItemId = row.RowId,
            IconId = row.Icon,
            ItemActionId = SheetRowIdNormalizationHelper.NormalizeOrZero(
                row.Action.RowId),
            ItemUiCategoryId = 0,
            ClassJobCategoryId = 0,
            Name = row.Name.ExtractText(),
            Description = row.Tooltip.ExtractText(),
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }
}
