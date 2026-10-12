// <copyright file="ItemDetailPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using Dalamud.Game.Gui;
using Dalamud.Utility;
using DetailKind = Dalamud.Game.Gui.DetailKind;
using Echoglossian.DBHelpers;
using Echoglossian.Persistence;
using Echoglossian.Translators;
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
public partial class Echoglossian
{
    private const int ItemDetailPrefetchItemsPerTick = 10;

    private static readonly TimeSpan ItemDetailPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

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
               FrameworkAccessGuard.IsClientReadyForPlayerScopedFrameworkAccess();
    }

    /// <summary>
    ///     Gets whether action-adjacent canonical tooltip rows should be
    ///     prefetched for consumers such as <c>ActionMenu</c>.
    /// </summary>
    /// <returns>True when action-adjacent canonical prefetch should run.</returns>
    private bool ShouldPrefetchActionAdjacentCanonicalTooltips()
    {
        return this.configuration.Translate &&
               FrameworkAccessGuard.IsClientReadyForPlayerScopedFrameworkAccess() &&
               (this.configuration.TranslateTooltips ||
                this.configuration.TranslateActionMenuWindow);
    }


    /// <summary>Captures a canonical sheet payload on the Framework before asynchronous work.</summary>
    /// <param name="itemId">The source row id.</param>
    private void PrefetchItemDetail(uint itemId)
    {
        if (!TryCapturePrefetchOperationScope(ResolveCurrentPrefetchSourceLanguage, this.configuration, out var source, out var scope) ||
            !TryBuildItemTooltipCanonicalPayload(itemId, out var payload))
        {
            return;
        }

        this.StartItemDetailOperation(payload, source, scope, GetGameVersion(), PersistencePriority.Background);
    }

    /// <summary>Admits one managed snapshot while preserving terminal canonical ownership.</summary>
    /// <param name="payload">The Framework-captured source payload.</param>
    /// <param name="source">The captured provider source.</param>
    /// <param name="scope">The immutable translation policy.</param>
    /// <param name="gameVersion">The captured source version.</param>
    /// <param name="priority">The originating persistence lane.</param>
    private void StartItemDetailOperation(ItemTooltipCanonicalPayload payload, SourceClientLanguage source, TranslationReuseScope scope, string? gameVersion, PersistencePriority priority)
    {
        var writer = this.actionItemTooltipPersistenceWriter;
        if (writer == null || writer.ShutdownToken.IsCancellationRequested)
        {
            return;
        }

        var snapshot = payload.Serialize();
        var probe = ItemTooltipPersistenceHelper.CreateCanonicalRow(scope.SourceLanguageCode, scope.TargetLanguageCode, scope.TranslationEngine, gameVersion, payload);
        var key = ActionItemTooltipPersistenceWriter.BuildIdentity("ItemTooltip", payload.ItemId, scope, gameVersion, probe.SourceContentHash);
        var translationService = TranslationService;
        var resolution = translationService.CaptureTranslatorResolution(scope.TranslationEngine!.Value, TranslationSurfaceGroup.Default);
        var broker = this.queuedTranslationBroker;
        this.structuredTooltipOperationOwner.TryStart(key, () => CompleteItemDetailOperationAsync(
            writer, broker, snapshot, probe, scope, priority,
            (text, origin, token) => translationService.TranslateAsync(text, source, scope.TargetLanguageCode,
                TranslationSurfaceGroup.Default, resolution, token, origin)), writer.ShutdownToken);
    }

    /// <summary>Completes captured managed work without consulting game state or mutating native UI.</summary>
    /// <param name="writer">The process-lifetime persistence adapter.</param>
    /// <param name="broker">The existing shared translation broker.</param>
    /// <param name="snapshot">The immutable serialized source payload.</param>
    /// <param name="probe">The operation-owned canonical identity.</param>
    /// <param name="scope">The captured translation policy.</param>
    /// <param name="priority">The originating persistence lane.</param>
    /// <param name="translate">The captured translator operation.</param>
    internal static async Task CompleteItemDetailOperationAsync(
        ActionItemTooltipPersistenceWriter writer, QueuedTranslationBroker broker, string snapshot,
        ItemTooltip probe, TranslationReuseScope scope, PersistencePriority priority,
        Func<string, string, CancellationToken, Task<string>> translate)
    {
        var token = writer.ShutdownToken;
        writer.TryFind(probe, scope, out var read, priority);
        var result = await read.WaitAsync(token).ConfigureAwait(false);
        if (result.Status is not (PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged))
        {
            return;
        }

        var original = ItemTooltipCanonicalPayload.Deserialize(snapshot)!;
        var translated = ItemTooltipCanonicalPayload.Deserialize(snapshot)!;
        translated.TranslatedName = StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original.Name, result.Value?.TranslatedItemName);
        translated.TranslatedDescription = StructuredTooltipTranslationValidation.GetMeaningfulTranslationOrNull(original.Description, result.Value?.TranslatedItemDescription);
        if (translated.HasCompleteTranslation)
        {
            return;
        }

        // Preserve original-row registration, but let the coordinator suppress unchanged writes.
        writer.TryPersist(probe, out var registration, priority);
        var registered = await registration.WaitAsync(token).ConfigureAwait(false);
        if (registered.Status is not (PersistenceCompletionStatus.Succeeded or PersistenceCompletionStatus.Unchanged))
        {
            return;
        }

        var name = ResolveStructuredTooltipFieldAsync(broker,
            BuildTranslationReuseScopedKey($"ItemDetailPrefetch|{original.ItemId}|Name|{original.Name}", scope),
            original.Name, translated.TranslatedName, $"ItemTooltip/{original.ItemId}/Name", translate, token);
        var description = ResolveStructuredTooltipFieldAsync(broker,
            BuildTranslationReuseScopedKey($"ItemDetailPrefetch|{original.ItemId}|Description|{original.Description}", scope),
            original.Description, translated.TranslatedDescription, $"ItemTooltip/{original.ItemId}/Description", translate, token);
        await Task.WhenAll(name, description).ConfigureAwait(false);
        translated.TranslatedName = await name.ConfigureAwait(false);
        translated.TranslatedDescription = await description.ConfigureAwait(false);
        if (!translated.HasCompleteTranslation)
        {
            return;
        }

        var row = ItemTooltipPersistenceHelper.CreateCanonicalRow(scope.SourceLanguageCode, scope.TargetLanguageCode,
            scope.TranslationEngine, probe.GameVersion, original, translated);
        writer.TryPersist(row, out var write, priority);
        await write.WaitAsync(token).ConfigureAwait(false);
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
    ///     Builds the diagnostic surface identity for one item-tooltip field.
    /// </summary>
    /// <param name="payload">The canonical item-tooltip payload.</param>
    /// <param name="fieldName">The translated field name.</param>
    /// <returns>The diagnostic surface identity.</returns>
    private static string BuildItemDetailOriginContext(
        ItemTooltipCanonicalPayload payload,
        string fieldName)
    {
        return $"ItemTooltip/{payload.ItemId}/{fieldName}";
    }

    /// <summary>
    ///     Tries to collect tracked item ids from inventory, armory, equipment, and hotbars.
    /// </summary>
    /// <param name="itemIds">The collected item ids.</param>
    /// <returns>True when item ids were collected successfully.</returns>
    private static unsafe bool TryCollectTrackedItemIds(out List<uint> itemIds)
    {
        itemIds = [];

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            return false;
        }

        HashSet<uint> uniqueItemIds = [];
        foreach (var inventoryType in Enum.GetValues<InventoryType>())
        {
            var container = inventoryManager->GetInventoryContainer(inventoryType);
            if (container == null || !container->IsLoaded || container->Size == 0 || container->Items == null)
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
