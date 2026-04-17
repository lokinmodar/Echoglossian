// <copyright file="ItemTooltipPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ItemSheet = Lumina.Excel.Sheets.Item;

namespace Echoglossian;

/// <summary>
///     Provides DB-first background prefetch for canonical item-tooltip payloads.
/// </summary>
public unsafe partial class Echoglossian
{
    private const int ItemTooltipPrefetchItemsPerTick = 10;

    private static readonly TimeSpan ItemTooltipPrefetchTickInterval =
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

    private readonly List<uint> itemTooltipPrefetchQueue = [];

    private string itemTooltipPrefetchSignature = string.Empty;

    private DateTime itemTooltipPrefetchLastTickUtc = DateTime.MinValue;

    private int itemTooltipPrefetchQueueIndex;

    /// <summary>
    ///     Ticks the item-tooltip prefetch runtime so current inventory surfaces
    ///     are translated into canonical storage ahead of tooltip use.
    /// </summary>
    private void TickItemTooltipPrefetch()
    {
        if (!this.ShouldPrefetchStructuredTooltips() ||
            DateTime.UtcNow - this.itemTooltipPrefetchLastTickUtc <
            ItemTooltipPrefetchTickInterval)
        {
            return;
        }

        this.itemTooltipPrefetchLastTickUtc = DateTime.UtcNow;

        if (!TryCollectTrackedItemIds(out var itemIds))
        {
            this.ClearItemTooltipPrefetchState();
            return;
        }

        var signature = string.Join(',', itemIds);
        if (!string.Equals(
                this.itemTooltipPrefetchSignature,
                signature,
                StringComparison.Ordinal))
        {
            this.itemTooltipPrefetchSignature = signature;
            this.itemTooltipPrefetchQueue.Clear();
            this.itemTooltipPrefetchQueue.AddRange(itemIds);
            this.itemTooltipPrefetchQueueIndex = 0;
        }

        if (this.itemTooltipPrefetchQueueIndex >=
            this.itemTooltipPrefetchQueue.Count)
        {
            return;
        }

        var processedCount = 0;
        while (processedCount < ItemTooltipPrefetchItemsPerTick &&
               this.itemTooltipPrefetchQueueIndex <
               this.itemTooltipPrefetchQueue.Count)
        {
            var itemId =
                this.itemTooltipPrefetchQueue[this.itemTooltipPrefetchQueueIndex++];
            this.PrefetchItemTooltip(itemId);
            processedCount++;
        }
    }

    /// <summary>
    ///     Clears the item-tooltip prefetch runtime state.
    /// </summary>
    private void ClearItemTooltipPrefetchState()
    {
        this.itemTooltipPrefetchQueue.Clear();
        this.itemTooltipPrefetchQueueIndex = 0;
        this.itemTooltipPrefetchSignature = string.Empty;
        this.itemTooltipPrefetchLastTickUtc = DateTime.MinValue;
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
    ///     Prefetches one canonical item-tooltip payload and any missing translations.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    private void PrefetchItemTooltip(uint itemId)
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

        this.PrefetchItemTooltipName(originalPayload, existingRow);
        this.PrefetchItemTooltipDescription(originalPayload, existingRow);
    }

    /// <summary>
    ///     Prefetches the translated item name when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchItemTooltipName(
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Name) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedItemName))
        {
            return;
        }

        var translationKey =
            $"ItemTooltipPrefetch|{originalPayload.ItemId}|Name|{originalPayload.Name}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedName))
        {
            this.ApplyItemTooltipTranslation(
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
            translatedName => this.ApplyItemTooltipTranslation(
                originalPayload.ItemId,
                translatedName: translatedName));
    }

    /// <summary>
    ///     Prefetches the translated item description when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchItemTooltipDescription(
        ItemTooltipCanonicalPayload originalPayload,
        ItemTooltip existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Description) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedItemDescription))
        {
            return;
        }

        var translationKey =
            $"ItemTooltipPrefetch|{originalPayload.ItemId}|Description|{originalPayload.Description}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedDescription))
        {
            this.ApplyItemTooltipTranslation(
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
            translatedDescription => this.ApplyItemTooltipTranslation(
                originalPayload.ItemId,
                translatedDescription: translatedDescription));
    }

    /// <summary>
    ///     Applies one resolved item-tooltip translation into canonical storage.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    private void ApplyItemTooltipTranslation(
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
    ///     Tries to build one canonical item-tooltip payload from the item sheet.
    /// </summary>
    /// <param name="itemId">The item row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildItemTooltipCanonicalPayload(
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
            ItemActionId = itemRow.ItemAction.RowId,
            ItemUiCategoryId = itemRow.ItemUICategory.RowId,
            ClassJobCategoryId = itemRow.ClassJobCategory.RowId,
            Name = itemRow.Name.ExtractText(),
            Description = itemRow.Description.ExtractText(),
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }
}
