// <copyright file="TranslationFailurePersistenceHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;

namespace Echoglossian.DBHelpers;

/// <summary>
///     Persists exact translation failures so the same request can be skipped
///     in later sessions.
/// </summary>
public static class TranslationFailurePersistenceHelper
{
    /// <summary>
    ///     Inserts or updates one exact translation-failure row and refreshes
    ///     the in-memory cache.
    /// </summary>
    /// <param name="configDirectory">The plugin configuration directory.</param>
    /// <param name="sourceText">The exact sanitized source text.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <param name="translationEngine">The translation engine identifier.</param>
    /// <param name="failureReason">The failure reason to persist.</param>
    /// <param name="originContext">The origin context that produced the request.</param>
    /// <param name="cacheUpdate">The callback used to refresh the cache.</param>
    public static void RecordFailure(
        string configDirectory,
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        int translationEngine,
        string failureReason,
        string? originContext,
        Action<TranslationFailure> cacheUpdate)
    {
        if (string.IsNullOrWhiteSpace(configDirectory) ||
            string.IsNullOrWhiteSpace(sourceText) ||
            !TranslationPersistenceGuard.IsPersistentFailureReason(
                failureReason))
        {
            return;
        }

        var sourceTextHash = TranslationFailureKey.ComputeSourceTextHash(sourceText);
        var normalizedSourceLanguage =
            RuntimeLanguageHelper.NormalizeLanguage(sourceLanguage);
        var normalizedTargetLanguage =
            RuntimeLanguageHelper.NormalizeLanguage(targetLanguage);

        using var context = new EchoglossianDbContext(configDirectory);
        var existing = context.Set<TranslationFailure>().FirstOrDefault(row =>
            row.SourceTextHash == sourceTextHash &&
            row.SourceText == sourceText &&
            row.SourceLanguage == normalizedSourceLanguage &&
            row.TargetLanguage == normalizedTargetLanguage &&
            row.TranslationEngine == translationEngine);

        if (existing != null)
        {
            existing.FailureReason = failureReason;
            existing.FirstSeenOrigin =
                string.IsNullOrWhiteSpace(existing.FirstSeenOrigin)
                    ? originContext
                    : existing.FirstSeenOrigin;
            existing.LastSeenOrigin = originContext;
            existing.FailureCount++;
            existing.UpdatedDate = DateTime.UtcNow;
            context.SaveChanges();
            cacheUpdate(existing);
            return;
        }

        var newRecord = new TranslationFailure
        {
            SourceText = sourceText,
            SourceTextHash = sourceTextHash,
            SourceLanguage = normalizedSourceLanguage,
            TargetLanguage = normalizedTargetLanguage,
            TranslationEngine = translationEngine,
            FailureReason = failureReason,
            FirstSeenOrigin = originContext,
            LastSeenOrigin = originContext,
            FailureCount = 1,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };

        context.Set<TranslationFailure>().Add(newRecord);
        context.SaveChanges();
        cacheUpdate(newRecord);
    }
}
