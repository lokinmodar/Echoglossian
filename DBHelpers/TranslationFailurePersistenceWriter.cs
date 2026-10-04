// <copyright file="TranslationFailurePersistenceWriter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;

namespace Echoglossian.DBHelpers;

/// <summary>Schedules exact persistent translation failures through the shared coordinator.</summary>
internal sealed class TranslationFailurePersistenceWriter
{
    private const string WriteDomain = "translation-failure-write";
    private readonly IPersistenceCoordinator coordinator;

    /// <summary>Initializes a new instance of the writer.</summary>
    internal TranslationFailurePersistenceWriter(IPersistenceCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>Schedules a coalesced background upsert without blocking a caller.</summary>
    internal PersistenceAdmissionStatus TryPersist(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        int translationEngine,
        string failureReason,
        string? originContext,
        out Task<PersistenceWriteResult> completion)
    {
        var sourceHash = TranslationFailureKey.ComputeSourceTextHash(sourceText);
        var normalizedSource = RuntimeLanguageHelper.NormalizeLanguage(sourceLanguage);
        var normalizedTarget = RuntimeLanguageHelper.NormalizeLanguage(targetLanguage);
        TranslationFailure? committed = null;
        var identity = string.Concat(sourceHash, "|", normalizedSource, "|", normalizedTarget, "|", translationEngine.ToString(CultureInfo.InvariantCulture));
        return this.coordinator.TryScheduleWrite(
            new PersistenceWriteRequest(
                new PersistenceWorkKey(WriteDomain, identity),
                PersistencePriority.Background,
                async (context, cancellationToken) =>
                {
                    var existing = await context.Set<TranslationFailure>().FirstOrDefaultAsync(row =>
                        row.SourceTextHash == sourceHash && row.SourceText == sourceText &&
                        row.SourceLanguage == normalizedSource && row.TargetLanguage == normalizedTarget &&
                        row.TranslationEngine == translationEngine, cancellationToken).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        existing.FailureReason = failureReason;
                        existing.LastSeenOrigin = originContext;
                        existing.FirstSeenOrigin ??= originContext;
                        existing.FailureCount++;
                        existing.UpdatedDate = DateTime.UtcNow;
                        committed = existing;
                    }
                    else
                    {
                        committed = new TranslationFailure
                        {
                            SourceText = sourceText, SourceTextHash = sourceHash,
                            SourceLanguage = normalizedSource, TargetLanguage = normalizedTarget,
                            TranslationEngine = translationEngine, FailureReason = failureReason,
                            FirstSeenOrigin = originContext, LastSeenOrigin = originContext,
                            FailureCount = 1, CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow,
                        };
                        context.Set<TranslationFailure>().Add(committed);
                    }

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return PersistenceWriteMutation.ChangedResult;
                },
                () =>
                {
                    if (committed is not null)
                    {
                        TranslationFailureCacheManager.Update(committed);
                    }
                }),
            out completion);
    }
}
