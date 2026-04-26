// <copyright file="TraitDetailPrefetchRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using TraitSheet = Lumina.Excel.Sheets.Trait;
using TraitTransientSheet = Lumina.Excel.Sheets.TraitTransient;

namespace Echoglossian;

/// <summary>
///     Provides DB-first background prefetch for canonical trait payloads.
/// </summary>
public unsafe partial class Echoglossian
{
    private const int TraitDetailPrefetchTraitsPerTick = 6;

    private static readonly TimeSpan TraitDetailPrefetchTickInterval =
        TimeSpan.FromSeconds(2);

    private readonly List<uint> traitDetailPrefetchQueue = [];

    private string traitDetailPrefetchSignature = string.Empty;

    private DateTime traitDetailPrefetchLastTickUtc = DateTime.MinValue;

    private int traitDetailPrefetchQueueIndex;

    /// <summary>
    ///     Ticks the trait prefetch runtime so current class/job traits are translated
    ///     into canonical storage ahead of tooltip and ActionMenu use.
    /// </summary>
    private void TickTraitDetailPrefetch()
    {
        if (!this.ShouldPrefetchStructuredTooltips() ||
            DateTime.UtcNow - this.traitDetailPrefetchLastTickUtc <
            TraitDetailPrefetchTickInterval)
        {
            return;
        }

        this.traitDetailPrefetchLastTickUtc = DateTime.UtcNow;

        if (!TryGetCurrentClassJobInfo(
                out var currentClassJobId,
                out var currentClassJobAbbreviation) ||
            !TryCollectCurrentClassJobTraitIds(
                currentClassJobId,
                currentClassJobAbbreviation,
                out var traitIds))
        {
            this.ClearTraitDetailPrefetchState();
            return;
        }

        var signature =
            $"{currentClassJobId}|{string.Join(',', traitIds)}";
        if (!string.Equals(
                this.traitDetailPrefetchSignature,
                signature,
                StringComparison.Ordinal))
        {
            this.traitDetailPrefetchSignature = signature;
            this.traitDetailPrefetchQueue.Clear();
            this.traitDetailPrefetchQueue.AddRange(traitIds);
            this.traitDetailPrefetchQueueIndex = 0;
        }

        if (this.traitDetailPrefetchQueueIndex >=
            this.traitDetailPrefetchQueue.Count)
        {
            return;
        }

        var processedCount = 0;
        while (processedCount < TraitDetailPrefetchTraitsPerTick &&
               this.traitDetailPrefetchQueueIndex <
               this.traitDetailPrefetchQueue.Count)
        {
            var traitId =
                this.traitDetailPrefetchQueue[this.traitDetailPrefetchQueueIndex++];
            this.PrefetchTraitDetail(traitId, currentClassJobId);
            processedCount++;
        }
    }

    /// <summary>
    ///     Clears the trait prefetch runtime state.
    /// </summary>
    private void ClearTraitDetailPrefetchState()
    {
        this.traitDetailPrefetchQueue.Clear();
        this.traitDetailPrefetchQueueIndex = 0;
        this.traitDetailPrefetchSignature = string.Empty;
        this.traitDetailPrefetchLastTickUtc = DateTime.MinValue;
    }

    /// <summary>
    ///     Prefetches one canonical trait payload and any missing translations.
    /// </summary>
    /// <param name="traitId">The trait row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    private void PrefetchTraitDetail(uint traitId, byte currentClassJobId)
    {
        if (!TryBuildTraitCanonicalPayload(
                traitId,
                currentClassJobId,
                out var originalPayload))
        {
            return;
        }

        var originalRow = TraitPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var existingRow = this.FindTrait(originalRow) ?? originalRow;
        this.InsertTrait(originalRow);

        this.PrefetchTraitDetailName(originalPayload, existingRow);
        this.PrefetchTraitDetailDescription(originalPayload, existingRow);
    }

    /// <summary>
    ///     Prefetches the translated trait name when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchTraitDetailName(
        TraitCanonicalPayload originalPayload,
        Trait existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Name) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedTraitName))
        {
            return;
        }

        var translationKey =
            $"TraitDetailPrefetch|{originalPayload.TraitId}|Name|{originalPayload.Name}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedName))
        {
            this.ApplyTraitDetailTranslation(
                originalPayload.TraitId,
                originalPayload.ClassJobId,
                translatedName: cachedTranslatedName);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Name,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedName => this.ApplyTraitDetailTranslation(
                originalPayload.TraitId,
                originalPayload.ClassJobId,
                translatedName: translatedName));
    }

    /// <summary>
    ///     Prefetches the translated trait description when it is not yet persisted.
    /// </summary>
    /// <param name="originalPayload">The canonical original payload.</param>
    /// <param name="existingRow">The currently persisted row, if any.</param>
    private void PrefetchTraitDetailDescription(
        TraitCanonicalPayload originalPayload,
        Trait existingRow)
    {
        if (string.IsNullOrWhiteSpace(originalPayload.Description) ||
            !string.IsNullOrWhiteSpace(existingRow.TranslatedTraitDescription))
        {
            return;
        }

        var translationKey =
            $"TraitDetailPrefetch|{originalPayload.TraitId}|Description|{originalPayload.Description}";
        if (this.TryGetQueuedTranslation(
                translationKey,
                out var cachedTranslatedDescription))
        {
            this.ApplyTraitDetailTranslation(
                originalPayload.TraitId,
                originalPayload.ClassJobId,
                translatedDescription: cachedTranslatedDescription);
            return;
        }

        this.QueueTranslation(
            translationKey,
            () => TranslationService.Translate(
                originalPayload.Description,
                ClientStateInterface.ClientLanguage.Humanize(),
                LangDict[LanguageInt].Code),
            translatedDescription => this.ApplyTraitDetailTranslation(
                originalPayload.TraitId,
                originalPayload.ClassJobId,
                translatedDescription: translatedDescription));
    }

    /// <summary>
    ///     Applies one resolved trait translation into canonical storage.
    /// </summary>
    /// <param name="traitId">The trait row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="translatedName">The translated name, if any.</param>
    /// <param name="translatedDescription">The translated description, if any.</param>
    private void ApplyTraitDetailTranslation(
        uint traitId,
        uint currentClassJobId,
        string? translatedName = null,
        string? translatedDescription = null)
    {
        if (!TryBuildTraitCanonicalPayload(
                traitId,
                (byte)currentClassJobId,
                out var originalPayload))
        {
            return;
        }

        var existingProbe = TraitPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload);
        var existingRow = this.FindTrait(existingProbe);
        var translatedPayload = existingRow == null
            ? originalPayload
            : TraitCanonicalPayload.Deserialize(
                    existingRow.CanonicalPayloadAsText) ??
                originalPayload;

        translatedPayload.TraitId = originalPayload.TraitId;
        translatedPayload.IconId = originalPayload.IconId;
        translatedPayload.ClassJobId = originalPayload.ClassJobId;
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

        var translatedRow = TraitPersistenceHelper.CreateCanonicalRow(
            ClientStateInterface.ClientLanguage.Humanize(),
            LangDict[LanguageInt].Code,
            this.configuration.ChosenTransEngine,
            GetGameVersion(),
            originalPayload,
            translatedPayload);
        this.InsertTrait(translatedRow);
    }

    /// <summary>
    ///     Tries to collect the current class/job trait ids from canonical sheets.
    /// </summary>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="currentClassJobAbbreviation">The current class/job abbreviation.</param>
    /// <param name="traitIds">The collected trait ids.</param>
    /// <returns>True when trait ids were collected successfully.</returns>
    private static bool TryCollectCurrentClassJobTraitIds(
        byte currentClassJobId,
        string currentClassJobAbbreviation,
        out List<uint> traitIds)
    {
        traitIds = [];

        var traitSheet =
            DManager.GetExcelSheet<TraitSheet>(ClientStateInterface.ClientLanguage);
        if (traitSheet == null)
        {
            return false;
        }

        HashSet<uint> uniqueTraitIds = [];
        foreach (var traitRow in traitSheet)
        {
            if (traitRow.RowId == 0 ||
                string.IsNullOrWhiteSpace(traitRow.Name.ExtractText()))
            {
                continue;
            }

            var matchesClassJob = traitRow.ClassJob.RowId == currentClassJobId;
            var matchesCategory =
                DoesActionCategoryMatchCurrentJob(
                    traitRow.ClassJobCategory.ValueNullable,
                    currentClassJobAbbreviation);
            if (!matchesClassJob && !matchesCategory)
            {
                continue;
            }

            uniqueTraitIds.Add(traitRow.RowId);
        }

        traitIds = uniqueTraitIds.OrderBy(id => id).ToList();
        return traitIds.Count > 0;
    }

    /// <summary>
    ///     Tries to build one canonical trait payload from sheets.
    /// </summary>
    /// <param name="traitId">The trait row identifier.</param>
    /// <param name="currentClassJobId">The current class-job identifier.</param>
    /// <param name="payload">The resolved payload.</param>
    /// <returns>True when the payload resolved successfully.</returns>
    private static bool TryBuildTraitCanonicalPayload(
        uint traitId,
        byte currentClassJobId,
        out TraitCanonicalPayload payload)
    {
        payload = new TraitCanonicalPayload();

        var traitSheet =
            DManager.GetExcelSheet<TraitSheet>(ClientStateInterface.ClientLanguage);
        var traitTransientSheet =
            DManager.GetExcelSheet<TraitTransientSheet>(
                ClientStateInterface.ClientLanguage);
        if (traitSheet == null ||
            traitTransientSheet == null ||
            !traitSheet.TryGetRow(traitId, out var traitRow))
        {
            return false;
        }

        var description = traitTransientSheet.TryGetRow(traitId, out var transientRow)
            ? transientRow.Description.ExtractText()
            : string.Empty;
        payload = new TraitCanonicalPayload
        {
            TraitId = traitRow.RowId,
            IconId = (uint)traitRow.Icon,
            ClassJobId = traitRow.ClassJob.RowId != 0
                ? traitRow.ClassJob.RowId
                : currentClassJobId,
            ClassJobCategoryId = traitRow.ClassJobCategory.RowId,
            Name = traitRow.Name.ExtractText(),
            Description = description,
        };

        return !string.IsNullOrWhiteSpace(payload.Name);
    }
}
