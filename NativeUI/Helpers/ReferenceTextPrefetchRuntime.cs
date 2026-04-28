// <copyright file="ReferenceTextPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.Helpers;
using System.Security.Cryptography;
using System.Text;
using ActionSheet = Lumina.Excel.Sheets.Action;
using ActionTransientSheet = Lumina.Excel.Sheets.ActionTransient;
using AozActionSheet = Lumina.Excel.Sheets.AozAction;
using AozActionTransientSheet = Lumina.Excel.Sheets.AozActionTransient;
using BgcArmyActionSheet = Lumina.Excel.Sheets.BgcArmyAction;
using BgcArmyActionTransientSheet = Lumina.Excel.Sheets.BgcArmyActionTransient;
using BuddyActionSheet = Lumina.Excel.Sheets.BuddyAction;
using CompanyActionSheet = Lumina.Excel.Sheets.CompanyAction;
using CraftActionSheet = Lumina.Excel.Sheets.CraftAction;
using DeepDungeonItemSheet = Lumina.Excel.Sheets.DeepDungeonItem;
using EurekaMagiaActionSheet = Lumina.Excel.Sheets.EurekaMagiaAction;
using EventActionSheet = Lumina.Excel.Sheets.EventAction;
using EventItemSheet = Lumina.Excel.Sheets.EventItem;
using GeneralActionSheet = Lumina.Excel.Sheets.GeneralAction;
using MainCommandSheet = Lumina.Excel.Sheets.MainCommand;
using MountActionSheet = Lumina.Excel.Sheets.MountAction;
using PetActionSheet = Lumina.Excel.Sheets.PetAction;
using PvPActionSheet = Lumina.Excel.Sheets.PvPAction;

namespace Echoglossian;

/// <summary>
///     Provides reusable DB-first background prefetch for action-adjacent
///     reference-text sheets used by <c>ActionMenu</c>.
/// </summary>
public unsafe partial class Echoglossian
{
    private const int ReferenceTextPrefetchRowsPerTick = 8;

    private static readonly TimeSpan ReferenceTextPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, ReferenceTextPrefetchState>
        referenceTextPrefetchStates = new(StringComparer.Ordinal);
    private IReadOnlyList<ReferenceTextPrefetchRegistration>?
        referenceTextPrefetchRegistrations;
    private DateTime referenceTextPrefetchLastTickUtc = DateTime.MinValue;
    private int referenceTextPrefetchRoundRobinIndex;

    /// <summary>
    ///     Ticks the shared reference-text prefetch runtime so action-adjacent
    ///     sheets are translated into canonical storage ahead of ActionMenu
    ///     lookups.
    /// </summary>
    private void TickReferenceTextPrefetch()
    {
        if (!this.ShouldPrefetchReferenceTexts() ||
            DateTime.UtcNow - this.referenceTextPrefetchLastTickUtc <
            ReferenceTextPrefetchTickInterval)
        {
            return;
        }

        this.referenceTextPrefetchLastTickUtc = DateTime.UtcNow;

        var registrations = this.GetReferenceTextPrefetchRegistrations();
        if (registrations.Count == 0)
        {
            return;
        }

        var startIndex =
            this.referenceTextPrefetchRoundRobinIndex % registrations.Count;
        var remaining = ReferenceTextPrefetchRowsPerTick;
        for (var offset = 0;
             offset < registrations.Count && remaining > 0;
             offset++)
        {
            var registration =
                registrations[(startIndex + offset) % registrations.Count];
            remaining -= this.TickReferenceTextPrefetchRegistration(
                registration,
                remaining);
        }

        this.referenceTextPrefetchRoundRobinIndex =
            (startIndex + 1) % registrations.Count;
    }

    /// <summary>
    ///     Clears the shared reference-text prefetch runtime state.
    /// </summary>
    private void ClearReferenceTextPrefetchState()
    {
        this.referenceTextPrefetchStates.Clear();
        this.referenceTextPrefetchLastTickUtc = DateTime.MinValue;
        this.referenceTextPrefetchRoundRobinIndex = 0;
    }

    /// <summary>
    ///     Gets whether shared reference-text prefetch should run.
    /// </summary>
    /// <returns>True when the background prefetch should run.</returns>
    private bool ShouldPrefetchReferenceTexts()
    {
        return this.configuration.Translate &&
               ClientStateInterface.IsLoggedIn &&
               (this.configuration.TranslateActionMenuWindow ||
                this.configuration.TranslateMainCommandWindow ||
                this.configuration.TranslateTooltips);
    }

    /// <summary>
    ///     Gets whether action-adjacent reference-text prefetch should run.
    /// </summary>
    /// <returns>True when action-adjacent prefetch is enabled.</returns>
    private bool ShouldPrefetchActionAdjacentReferenceTexts()
    {
        return this.configuration.TranslateActionMenuWindow ||
               this.configuration.TranslateTooltips;
    }

    /// <summary>
    ///     Gets whether MainCommand sheet prefetch should run.
    /// </summary>
    /// <returns>True when MainCommand sheet prefetch is enabled.</returns>
    private bool ShouldPrefetchMainCommandReferenceTexts()
    {
        return this.configuration.TranslateActionMenuWindow ||
               this.configuration.TranslateMainCommandWindow;
    }

    /// <summary>
    ///     Gets whether item-adjacent reference-text prefetch should run.
    /// </summary>
    /// <returns>True when item-adjacent prefetch is enabled.</returns>
    private bool ShouldPrefetchItemAdjacentReferenceTexts()
    {
        return this.configuration.TranslateTooltips;
    }

    /// <summary>
    ///     Ticks one specific sheet-registration queue and processes up to the
    ///     requested number of rows.
    /// </summary>
    /// <param name="registration">The registration to tick.</param>
    /// <param name="remainingBudget">The remaining per-tick row budget.</param>
    /// <returns>The number of rows processed.</returns>
    private int TickReferenceTextPrefetchRegistration(
        ReferenceTextPrefetchRegistration registration,
        int remainingBudget)
    {
        if (!registration.IsEnabled())
        {
            this.referenceTextPrefetchStates.Remove(registration.Key);
            return 0;
        }

        if (!registration.TryCollectReferenceIds(out var referenceIds))
        {
            this.referenceTextPrefetchStates.Remove(registration.Key);
            return 0;
        }

        var state = this.GetOrCreateReferenceTextPrefetchState(
            registration.Key);
        var signature = string.Join(',', referenceIds);
        if (!string.Equals(
                state.Signature,
                signature,
                StringComparison.Ordinal))
        {
            state.Signature = signature;
            state.Queue.Clear();
            state.Queue.AddRange(referenceIds);
            state.QueueIndex = 0;
        }

        if (state.QueueIndex >= state.Queue.Count)
        {
            return 0;
        }

        var processedCount = 0;
        while (processedCount < remainingBudget &&
               state.QueueIndex < state.Queue.Count)
        {
            var referenceId = state.Queue[state.QueueIndex++];
            this.PrefetchReferenceText(registration, referenceId);
            processedCount++;
        }

        return processedCount;
    }

    /// <summary>
    ///     Prefetches one canonical reference-text payload and any missing
    ///     translations.
    /// </summary>
    /// <param name="registration">The registration describing the sheet family.</param>
    /// <param name="referenceId">The sheet-row identifier.</param>
    private void PrefetchReferenceText(
        ReferenceTextPrefetchRegistration registration,
        uint referenceId)
    {
        if (!registration.TryBuildPayload(referenceId, out var originalPayload))
        {
            return;
        }

        var originalRow = registration.CreateRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload,
            null);
        var existingRow = registration.FindRow(originalRow) ?? originalRow;
        registration.InsertRow(originalRow);

        this.PrefetchReferenceTextName(
            registration,
            originalPayload,
            existingRow);
        this.PrefetchReferenceTextDescription(
            registration,
            originalPayload,
            existingRow);
    }

    /// <summary>
    ///     Prefetches the translated reference-text name when it is not yet
    ///     persisted.
    /// </summary>
    /// <param name="registration">The registration describing the sheet family.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchReferenceTextName(
        ReferenceTextPrefetchRegistration registration,
        ReferenceTextCanonicalPayload originalPayload,
        ReferenceTextRowBase existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Name) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedName))
        {
            return;
        }

        var translationKey =
            $"{registration.Key}|{originalPayload.ReferenceId}|Name|{originalPayload.Name}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedName))
        {
            this.ApplyReferenceTextTranslation(
                registration,
                originalPayload.ReferenceId,
                translatedName: cachedTranslatedName);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Name,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedName => this.ApplyReferenceTextTranslation(
                registration,
                originalPayload.ReferenceId,
                translatedName: translatedName));
    }

    /// <summary>
    ///     Prefetches the translated reference-text description when it is not
    ///     yet persisted.
    /// </summary>
    /// <param name="registration">The registration describing the sheet family.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchReferenceTextDescription(
        ReferenceTextPrefetchRegistration registration,
        ReferenceTextCanonicalPayload originalPayload,
        ReferenceTextRowBase existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Description) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedDescription))
        {
            return;
        }

        var translationKey =
            $"{registration.Key}|{originalPayload.ReferenceId}|Description|{originalPayload.Description}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedDescription))
        {
            this.ApplyReferenceTextTranslation(
                registration,
                originalPayload.ReferenceId,
                translatedDescription: cachedTranslatedDescription);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Description,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedDescription => this.ApplyReferenceTextTranslation(
                registration,
                originalPayload.ReferenceId,
                translatedDescription: translatedDescription));
    }

    /// <summary>
    ///     Applies one resolved reference-text translation into canonical
    ///     storage.
    /// </summary>
    /// <param name="registration">The registration describing the sheet family.</param>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    private void ApplyReferenceTextTranslation(
        ReferenceTextPrefetchRegistration registration,
        uint referenceId,
        string? translatedName = null,
        string? translatedDescription = null)
    {
        if (!registration.TryBuildPayload(referenceId, out var originalPayload))
        {
            return;
        }

        var existingProbe = registration.CreateRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload,
            null);
        var existingRow = registration.FindRow(existingProbe);
        var translatedPayload = existingRow == null
            ? originalPayload
            : ReferenceTextCanonicalPayload.Deserialize(
                    existingRow.CanonicalPayloadAsText) ??
                originalPayload;

        translatedPayload.ReferenceId = originalPayload.ReferenceId;
        translatedPayload.ActionId = originalPayload.ActionId;
        translatedPayload.IconId = originalPayload.IconId;
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

        var translatedRow = registration.CreateRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload,
            translatedPayload);
        registration.InsertRow(translatedRow);
    }

    /// <summary>
    ///     Gets the shared sheet-family registrations, building them lazily on
    ///     first use.
    /// </summary>
    /// <returns>The registration list.</returns>
    private IReadOnlyList<ReferenceTextPrefetchRegistration>
        GetReferenceTextPrefetchRegistrations()
    {
        return this.referenceTextPrefetchRegistrations ??=
        [
            this.CreateReferenceTextPrefetchRegistration<GeneralActionText>(
                "GeneralActionPrefetch",
                TryCollectGeneralActionIds,
                TryBuildGeneralActionPayload,
                ReferenceTextCacheRegistry.GeneralActionTexts,
                static context => context.GeneralActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<BuddyActionText>(
                "BuddyActionPrefetch",
                TryCollectBuddyActionIds,
                TryBuildBuddyActionPayload,
                ReferenceTextCacheRegistry.BuddyActionTexts,
                static context => context.BuddyActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<CompanyActionText>(
                "CompanyActionPrefetch",
                TryCollectCompanyActionIds,
                TryBuildCompanyActionPayload,
                ReferenceTextCacheRegistry.CompanyActionTexts,
                static context => context.CompanyActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<CraftActionText>(
                "CraftActionPrefetch",
                TryCollectCraftActionIds,
                TryBuildCraftActionPayload,
                ReferenceTextCacheRegistry.CraftActionTexts,
                static context => context.CraftActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<PetActionText>(
                "PetActionPrefetch",
                TryCollectPetActionIds,
                TryBuildPetActionPayload,
                ReferenceTextCacheRegistry.PetActionTexts,
                static context => context.PetActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<EventActionText>(
                "EventActionPrefetch",
                TryCollectEventActionIds,
                TryBuildEventActionPayload,
                ReferenceTextCacheRegistry.EventActionTexts,
                static context => context.EventActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<EventItemText>(
                "EventItemPrefetch",
                TryCollectEventItemIds,
                TryBuildEventItemPayload,
                ReferenceTextCacheRegistry.EventItemTexts,
                static context => context.EventItemTexts,
                this.ShouldPrefetchItemAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<BgcArmyActionText>(
                "BgcArmyActionPrefetch",
                TryCollectBgcArmyActionIds,
                TryBuildBgcArmyActionPayload,
                ReferenceTextCacheRegistry.BgcArmyActionTexts,
                static context => context.BgcArmyActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<AozActionText>(
                "AozActionPrefetch",
                TryCollectAozActionIds,
                TryBuildAozActionPayload,
                ReferenceTextCacheRegistry.AozActionTexts,
                static context => context.AozActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<PvPActionText>(
                "PvPActionPrefetch",
                TryCollectPvPActionIds,
                TryBuildPvPActionPayload,
                ReferenceTextCacheRegistry.PvPActionTexts,
                static context => context.PvPActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<MountActionText>(
                "MountActionPrefetch",
                TryCollectMountActionIds,
                TryBuildMountActionPayload,
                ReferenceTextCacheRegistry.MountActionTexts,
                static context => context.MountActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<MainCommandText>(
                "MainCommandPrefetch",
                TryCollectMainCommandIds,
                TryBuildMainCommandPayload,
                ReferenceTextCacheRegistry.MainCommandTexts,
                static context => context.MainCommandTexts,
                this.ShouldPrefetchMainCommandReferenceTexts,
                CreateMainCommandTextRow),
            this.CreateReferenceTextPrefetchRegistration<EurekaMagiaActionText>(
                "EurekaMagiaActionPrefetch",
                TryCollectEurekaMagiaActionIds,
                TryBuildEurekaMagiaActionPayload,
                ReferenceTextCacheRegistry.EurekaMagiaActionTexts,
                static context => context.EurekaMagiaActionTexts,
                this.ShouldPrefetchActionAdjacentReferenceTexts),
            this.CreateReferenceTextPrefetchRegistration<DeepDungeonItemText>(
                "DeepDungeonItemPrefetch",
                TryCollectDeepDungeonItemIds,
                TryBuildDeepDungeonItemPayload,
                ReferenceTextCacheRegistry.DeepDungeonItemTexts,
                static context => context.DeepDungeonItemTexts,
                this.ShouldPrefetchItemAdjacentReferenceTexts),
        ];
    }

    /// <summary>
    ///     Creates one shared sheet-family registration.
    /// </summary>
    /// <typeparam name="TRow">The concrete persisted row type.</typeparam>
    /// <param name="key">The unique registration key.</param>
    /// <param name="tryCollectReferenceIds">Collects current row identifiers.</param>
    /// <param name="tryBuildPayload">Builds one canonical payload from a row identifier.</param>
    /// <param name="cacheStore">The specific cache store for this family.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <param name="isEnabled">Gets whether this registration should run.</param>
    /// <param name="createRow">Builds the persisted row for this registration.</param>
    /// <returns>The registration.</returns>
    private ReferenceTextPrefetchRegistration
        CreateReferenceTextPrefetchRegistration<TRow>(
            string key,
            TryCollectReferenceIdsDelegate tryCollectReferenceIds,
            TryBuildReferencePayloadDelegate tryBuildPayload,
            ReferenceTextCacheStore<TRow> cacheStore,
            Func<EchoglossianDbContext, DbSet<TRow>> setSelector,
            Func<bool>? isEnabled = null,
            Func<string, string, int?, string?, ReferenceTextCanonicalPayload, ReferenceTextCanonicalPayload?, TRow>? createRow = null)
            where TRow : ReferenceTextRowBase, new()
    {
        return new ReferenceTextPrefetchRegistration
        {
            Key = key,
            IsEnabled = isEnabled ?? (() => true),
            TryCollectReferenceIds = tryCollectReferenceIds,
            TryBuildPayload = tryBuildPayload,
            CreateRow = (
                originalLang,
                translationLang,
                translationEngine,
                gameVersion,
                originalPayload,
                translatedPayload) =>
                (createRow ??
                 ReferenceTextPersistenceHelper.CreateCanonicalRow<TRow>)(
                    originalLang,
                    translationLang,
                    translationEngine,
                    gameVersion,
                    originalPayload,
                    translatedPayload),
            FindRow = row =>
                this.FindReferenceText((TRow)row, cacheStore, setSelector),
            InsertRow = row =>
            {
                _ = this.InsertReferenceText(
                    (TRow)row,
                    cacheStore,
                    setSelector);
            },
        };
    }

    /// <summary>
    ///     Returns the cached queue state for one registration, creating it when
    ///     missing.
    /// </summary>
    /// <param name="key">The registration key.</param>
    /// <returns>The mutable queue state.</returns>
    private ReferenceTextPrefetchState GetOrCreateReferenceTextPrefetchState(
        string key)
    {
        if (!this.referenceTextPrefetchStates.TryGetValue(
                key,
                out var state))
        {
            state = new ReferenceTextPrefetchState();
            this.referenceTextPrefetchStates[key] = state;
        }

        return state;
    }

    /// <summary>
    ///     Tries to collect unlocked GeneralAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectGeneralActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<GeneralActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()) &&
                UnlockStateInterface.IsGeneralActionUnlocked(row))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect unlocked BuddyAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectBuddyActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<BuddyActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()) &&
                UnlockStateInterface.IsBuddyActionUnlocked(row))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect CompanyAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectCompanyActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<CompanyActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect unlocked CraftAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectCraftActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<CraftActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()) &&
                UnlockStateInterface.IsCraftActionUnlocked(row))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect PetAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectPetActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<PetActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect EventAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectEventActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<EventActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect EventItem row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectEventItemIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<EventItemSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect BgcArmyAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectBgcArmyActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<BgcArmyActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect unlocked AozAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectAozActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<AozActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                row.Action.RowId != 0 &&
                UnlockStateInterface.IsAozActionUnlocked(row))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect PvPAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectPvPActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<PvPActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                row.Action.RowId != 0)
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect MountAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectMountActionIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<MountActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
            {
                foreach (var actionRef in row.Action)
                {
                    if (actionRef.RowId != 0)
                    {
                        return true;
                    }
                }

                return false;
            })
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect MainCommand row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectMainCommandIds(out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<MainCommandSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect EurekaMagiaAction row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectEurekaMagiaActionIds(
        out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<EurekaMagiaActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                row.Action.RowId != 0)
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to collect DeepDungeonItem row identifiers.
    /// </summary>
    /// <param name="referenceIds">The collected row identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private static bool TryCollectDeepDungeonItemIds(
        out List<uint> referenceIds)
    {
        referenceIds = [];

        var sheet =
            DManager.GetExcelSheet<DeepDungeonItemSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null)
        {
            return false;
        }

        referenceIds = sheet
            .Where(row =>
                row.RowId != 0 &&
                !string.IsNullOrWhiteSpace(row.Name.ExtractText()))
            .Select(row => row.RowId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        return referenceIds.Count > 0;
    }

    /// <summary>
    ///     Tries to build one canonical GeneralAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The GeneralAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildGeneralActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<GeneralActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            row.Description.ExtractText(),
            (uint)row.Icon,
            row.Action.RowId != 0 ? row.Action.RowId : null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical BuddyAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The BuddyAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildBuddyActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<BuddyActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            row.Description.ExtractText(),
            (uint)row.Icon,
            actionId: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical CompanyAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The CompanyAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildCompanyActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<CompanyActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            row.Description.ExtractText(),
            (uint)row.Icon,
            actionId: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical CraftAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The CraftAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildCraftActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<CraftActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            row.Description.ExtractText(),
            row.Icon,
            actionId: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical PetAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The PetAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildPetActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<PetActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            row.Description.ExtractText(),
            (uint)row.Icon,
            row.Action.RowId != 0 ? row.Action.RowId : null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical EventAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The EventAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildEventActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<EventActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            description: null,
            row.Icon,
            actionId: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical EventItem payload from sheets.
    /// </summary>
    /// <param name="referenceId">The EventItem row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildEventItemPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<EventItemSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            description: null,
            row.Icon,
            row.Action.RowId != 0 ? row.Action.RowId : null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical BgcArmyAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The BgcArmyAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildBgcArmyActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<BgcArmyActionSheet>(
                ClientStateInterface.ClientLanguage);
        var transientSheet =
            DManager.GetExcelSheet<BgcArmyActionTransientSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null ||
            transientSheet == null ||
            !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        var description = transientSheet.TryGetRow(referenceId, out var transientRow)
            ? EvaluateSheetText(transientRow.Text)
            : null;
        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            description,
            (uint)row.Icon,
            actionId: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical AozAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The AozAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildAozActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<AozActionSheet>(
                ClientStateInterface.ClientLanguage);
        var transientSheet =
            DManager.GetExcelSheet<AozActionTransientSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null ||
            transientSheet == null ||
            !sheet.TryGetRow(referenceId, out var row) ||
            row.Action.RowId == 0)
        {
            return false;
        }

        string? description = null;
        uint? iconId = null;
        if (transientSheet.TryGetRow(referenceId, out var transientRow))
        {
            description = EvaluateSheetText(transientRow.Description);
            iconId = transientRow.Icon;
        }

        return TryBuildActionReferenceTextPayload(
            row.RowId,
            row.Action.RowId,
            iconId,
            description,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical PvPAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The PvPAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildPvPActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<PvPActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null ||
            !sheet.TryGetRow(referenceId, out var row) ||
            row.Action.RowId == 0)
        {
            return false;
        }

        return TryBuildActionReferenceTextPayload(
            row.RowId,
            row.Action.RowId,
            iconOverride: null,
            descriptionOverride: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical MountAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The MountAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildMountActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<MountActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        var actionId = 0u;
        foreach (var actionRef in row.Action)
        {
            if (actionRef.RowId == 0)
            {
                continue;
            }

            actionId = actionRef.RowId;
            break;
        }

        if (actionId == 0)
        {
            return false;
        }

        return TryBuildActionReferenceTextPayload(
            row.RowId,
            actionId,
            iconOverride: null,
            descriptionOverride: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical MainCommand payload from sheets.
    /// </summary>
    /// <param name="referenceId">The MainCommand row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildMainCommandPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<MainCommandSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        if (!TryBuildDirectReferenceTextPayload(
                row.RowId,
                row.Name.ExtractText(),
                row.Description.ExtractText(),
                Convert.ToUInt32(row.Icon),
                actionId: null,
                out payload))
        {
            return false;
        }

        payload.CategoryId = row.Category;
        payload.MainCommandCategoryId =
            row.MainCommandCategory.RowId != 0
                ? row.MainCommandCategory.RowId
                : null;
        payload.Unknown0 = NormalizeMainCommandUnknown0(row.Unknown0);
        payload.SortId = NormalizeMainCommandSortId(row.SortID);
        return true;
    }

    /// <summary>
    ///     Normalizes the <c>MainCommand.Unknown0</c> metadata value so the
    ///     runtime only persists meaningful positive values.
    /// </summary>
    /// <param name="unknown0">The raw sheet value.</param>
    /// <returns>
    ///     The persisted unsigned value, or <see langword="null" /> when the
    ///     sheet uses its zero sentinel.
    /// </returns>
    internal static uint? NormalizeMainCommandUnknown0(byte unknown0)
    {
        return unknown0 > 0 ? unknown0 : null;
    }

    /// <summary>
    ///     Normalizes the <c>MainCommand.SortID</c> metadata value so negative
    ///     sheet sentinels do not overflow canonical persistence.
    /// </summary>
    /// <param name="sortId">The raw signed sheet value.</param>
    /// <returns>
    ///     The persisted unsigned value, or <see langword="null" /> when the
    ///     sheet exposes a negative sentinel.
    /// </returns>
    internal static uint? NormalizeMainCommandSortId(sbyte sortId)
    {
        return sortId >= 0 ? (uint)sortId : null;
    }

    /// <summary>
    ///     Tries to build one canonical EurekaMagiaAction payload from sheets.
    /// </summary>
    /// <param name="referenceId">The EurekaMagiaAction row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildEurekaMagiaActionPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<EurekaMagiaActionSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null ||
            !sheet.TryGetRow(referenceId, out var row) ||
            row.Action.RowId == 0)
        {
            return false;
        }

        return TryBuildActionReferenceTextPayload(
            row.RowId,
            row.Action.RowId,
            iconOverride: null,
            descriptionOverride: null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical DeepDungeonItem payload from sheets.
    /// </summary>
    /// <param name="referenceId">The DeepDungeonItem row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildDeepDungeonItemPayload(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var sheet =
            DManager.GetExcelSheet<DeepDungeonItemSheet>(
                ClientStateInterface.ClientLanguage);
        if (sheet == null || !sheet.TryGetRow(referenceId, out var row))
        {
            return false;
        }

        return TryBuildDirectReferenceTextPayload(
            row.RowId,
            row.Name.ExtractText(),
            row.Tooltip.ExtractText(),
            row.Icon,
            row.Action.RowId != 0 ? row.Action.RowId : null,
            out payload);
    }

    /// <summary>
    ///     Tries to build one canonical payload directly from visible sheet
    ///     name and description fields.
    /// </summary>
    /// <param name="referenceId">The stable row identifier.</param>
    /// <param name="name">The visible name.</param>
    /// <param name="description">The visible description, when available.</param>
    /// <param name="iconId">The icon identifier, when available.</param>
    /// <param name="actionId">The linked Action row identifier, when available.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildDirectReferenceTextPayload(
        uint referenceId,
        string? name,
        string? description,
        uint? iconId,
        uint? actionId,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload
        {
            ReferenceId = referenceId,
            ActionId = actionId,
            IconId = iconId,
            Name = NormalizeReferenceText(name) ?? string.Empty,
            Description = NormalizeReferenceText(description),
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }

    /// <summary>
    ///     Creates one canonical MainCommand row while preserving sheet-
    ///     specific metadata and a metadata-sensitive source hash.
    /// </summary>
    /// <param name="originalLang">The language of the original payload.</param>
    /// <param name="translationLang">The target translation language.</param>
    /// <param name="translationEngine">The translation-engine identifier.</param>
    /// <param name="gameVersion">The game version associated with the payload.</param>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="translatedPayload">The translated canonical payload, if available.</param>
    /// <returns>The canonical MainCommand DB row.</returns>
    private static MainCommandText CreateMainCommandTextRow(
        string originalLang,
        string translationLang,
        int? translationEngine,
        string? gameVersion,
        ReferenceTextCanonicalPayload originalPayload,
        ReferenceTextCanonicalPayload? translatedPayload = null)
    {
        var row = ReferenceTextPersistenceHelper.CreateCanonicalRow<MainCommandText>(
            originalLang,
            translationLang,
            translationEngine,
            gameVersion,
            originalPayload,
            translatedPayload);
        row.IconId = originalPayload.IconId;
        row.CategoryId = originalPayload.CategoryId;
        row.MainCommandCategoryId = originalPayload.MainCommandCategoryId;
        row.Unknown0 = originalPayload.Unknown0;
        row.SortId = originalPayload.SortId;
        row.SourceContentHash = ComputeMainCommandSourceContentHash(
            originalPayload);
        return row;
    }

    /// <summary>
    ///     Computes a metadata-sensitive source hash for MainCommand rows.
    /// </summary>
    /// <param name="payload">The canonical source payload.</param>
    /// <returns>The stable source hash.</returns>
    private static string ComputeMainCommandSourceContentHash(
        ReferenceTextCanonicalPayload payload)
    {
        var builder = new StringBuilder();
        builder.Append(payload.SchemaVersion)
            .Append('|')
            .Append(payload.ReferenceId)
            .Append('|')
            .Append(payload.IconId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.CategoryId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.MainCommandCategoryId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.Unknown0?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.SortId?.ToString() ?? string.Empty)
            .Append('|')
            .Append(payload.Name)
            .Append('|')
            .Append(payload.Description ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    ///     Tries to build one canonical payload from a linked Action row and
    ///     optional overrides.
    /// </summary>
    /// <param name="referenceId">The stable row identifier.</param>
    /// <param name="actionId">The linked Action row identifier.</param>
    /// <param name="iconOverride">The icon override, when the source sheet provides one.</param>
    /// <param name="descriptionOverride">The description override, when the source sheet provides one.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildActionReferenceTextPayload(
        uint referenceId,
        uint actionId,
        uint? iconOverride,
        string? descriptionOverride,
        out ReferenceTextCanonicalPayload payload)
    {
        payload = new ReferenceTextCanonicalPayload();

        var actionSheet =
            DManager.GetExcelSheet<ActionSheet>(ClientStateInterface.ClientLanguage);
        var transientSheet =
            DManager.GetExcelSheet<ActionTransientSheet>(
                ClientStateInterface.ClientLanguage);
        if (actionSheet == null ||
            transientSheet == null ||
            actionId == 0 ||
            !actionSheet.TryGetRow(actionId, out var actionRow))
        {
            return false;
        }

        var description = NormalizeReferenceText(descriptionOverride);
        if (string.IsNullOrWhiteSpace(description) &&
            transientSheet.TryGetRow(actionId, out var transientRow))
        {
            description = NormalizeReferenceText(
                transientRow.Description.ExtractText());
        }

        payload = new ReferenceTextCanonicalPayload
        {
            ReferenceId = referenceId,
            ActionId = actionRow.RowId,
            IconId = iconOverride ?? actionRow.Icon,
            Name = NormalizeReferenceText(actionRow.Name.ExtractText()) ??
                   string.Empty,
            Description = description,
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }

    /// <summary>
    ///     Normalizes one optional reference-text field so empty strings become
    ///     <see langword="null" />.
    /// </summary>
    /// <param name="text">The candidate text.</param>
    /// <returns>The normalized text.</returns>
    private static string? NormalizeReferenceText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    ///     Represents one reusable sheet-family registration.
    /// </summary>
    private sealed class ReferenceTextPrefetchRegistration
    {
        /// <summary>
        ///     Gets or sets the unique registration key.
        /// </summary>
        public required string Key { get; set; }

        /// <summary>
        ///     Gets or sets whether this registration should currently run.
        /// </summary>
        public required Func<bool> IsEnabled { get; set; }

        /// <summary>
        ///     Gets or sets the row-identifier collector.
        /// </summary>
        public required TryCollectReferenceIdsDelegate TryCollectReferenceIds
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the payload builder.
        /// </summary>
        public required TryBuildReferencePayloadDelegate TryBuildPayload
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the canonical row creator.
        /// </summary>
        public required Func<string, string, int?, string?, ReferenceTextCanonicalPayload, ReferenceTextCanonicalPayload?, ReferenceTextRowBase> CreateRow
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the cache-first finder.
        /// </summary>
        public required Func<ReferenceTextRowBase, ReferenceTextRowBase?> FindRow
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the insert/update operation.
        /// </summary>
        public required Action<ReferenceTextRowBase> InsertRow { get; set; }
    }

    /// <summary>
    ///     Holds mutable queue state for one shared sheet-family registration.
    /// </summary>
    private sealed class ReferenceTextPrefetchState
    {
        /// <summary>
        ///     Gets the row-identifier queue.
        /// </summary>
        public List<uint> Queue { get; } = [];

        /// <summary>
        ///     Gets or sets the last queue signature.
        /// </summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the next queue index to process.
        /// </summary>
        public int QueueIndex { get; set; }
    }

    /// <summary>
    ///     Tries to collect the current row identifiers for one sheet family.
    /// </summary>
    /// <param name="referenceIds">The collected identifiers.</param>
    /// <returns>True when identifiers were collected successfully.</returns>
    private delegate bool TryCollectReferenceIdsDelegate(
        out List<uint> referenceIds);

    /// <summary>
    ///     Tries to build one canonical payload from one sheet-row identifier.
    /// </summary>
    /// <param name="referenceId">The sheet-row identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private delegate bool TryBuildReferencePayloadDelegate(
        uint referenceId,
        out ReferenceTextCanonicalPayload payload);
}
