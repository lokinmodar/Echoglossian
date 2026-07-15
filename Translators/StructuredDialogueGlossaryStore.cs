// <copyright file="StructuredDialogueGlossaryStore.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

/// <summary>
///     Retains the currently loaded dialogue glossary rows and the last load
///     snapshot for debugger inspection.
/// </summary>
public static class StructuredDialogueGlossaryStore
{
    private static readonly object SyncLock = new();
    private static List<StructuredDialogueGlossaryEntry> currentEntries = [];
    private static string? lastFailureDetail;
    private static DateTime? lastLoadObservedAtUtc;
    private static string? lastLoadPath;
    private static bool? lastLoadSucceeded;
    private static int lastSkippedEntryCount;

    /// <summary>
    ///     Describes the current shared structured dialogue glossary state.
    /// </summary>
    /// <param name="LastLoadObservedAtUtc">The last load observation time.</param>
    /// <param name="LastLoadSucceeded">Whether the last load succeeded.</param>
    /// <param name="LastLoadPath">The last attempted file path.</param>
    /// <param name="EntryCount">The number of active glossary rows.</param>
    /// <param name="SkippedEntryCount">
    ///     The number of malformed rows skipped by the last successful load.
    /// </param>
    /// <param name="LastLoadFailureDetail">
    ///     The failure detail when the last load failed.
    /// </param>
    public readonly record struct StructuredDialogueGlossarySnapshot(
        DateTime? LastLoadObservedAtUtc,
        bool? LastLoadSucceeded,
        string? LastLoadPath,
        int EntryCount,
        int SkippedEntryCount,
        string? LastLoadFailureDetail);

    /// <summary>
    ///     Clears the retained glossary rows and snapshot state.
    /// </summary>
    public static void Clear()
    {
        lock (SyncLock)
        {
            currentEntries = [];
            lastFailureDetail = null;
            lastLoadObservedAtUtc = null;
            lastLoadPath = null;
            lastLoadSucceeded = null;
            lastSkippedEntryCount = 0;
        }
    }

    /// <summary>
    ///     Refreshes the glossary rows from one operator-provided file path.
    /// </summary>
    /// <param name="filePath">The glossary file path.</param>
    /// <returns>Whether the refresh succeeded.</returns>
    public static bool Refresh(string? filePath)
    {
        var observedAtUtc = DateTime.UtcNow;
        try
        {
            var normalizedPath = string.IsNullOrWhiteSpace(filePath)
                ? null
                : Path.GetFullPath(filePath.Trim());
            var result = StructuredDialogueGlossaryLoader.LoadFromFile(
                normalizedPath ?? string.Empty);

            lock (SyncLock)
            {
                currentEntries = result.Succeeded
                    ? result.Entries.ToList()
                    : [];
                lastLoadObservedAtUtc = observedAtUtc;
                lastLoadPath = normalizedPath;
                lastLoadSucceeded = result.Succeeded;
                lastSkippedEntryCount = result.SkippedEntryCount;
                lastFailureDetail = result.Succeeded
                    ? null
                    : result.FailureDetail;
            }

            return result.Succeeded;
        }
        catch (Exception ex)
        {
            lock (SyncLock)
            {
                currentEntries = [];
                lastLoadObservedAtUtc = observedAtUtc;
                lastLoadPath = filePath;
                lastLoadSucceeded = false;
                lastSkippedEntryCount = 0;
                lastFailureDetail = ex.Message;
            }

            return false;
        }
    }

    /// <summary>
    ///     Gets the current active glossary rows filtered for the requested
    ///     source and target languages.
    /// </summary>
    /// <param name="sourceLanguage">The source language to match.</param>
    /// <param name="targetLanguage">The target language to match.</param>
    /// <returns>The filtered glossary rows.</returns>
    public static IReadOnlyList<StructuredDialogueGlossaryEntry> GetEntries(
        string? sourceLanguage,
        string? targetLanguage)
    {
        lock (SyncLock)
        {
            return currentEntries
                .Where(entry => LanguageMatches(entry.SourceLanguage, sourceLanguage))
                .Where(entry => LanguageMatches(entry.TargetLanguage, targetLanguage))
                .ToList();
        }
    }

    /// <summary>
    ///     Gets the current glossary snapshot for debugger inspection.
    /// </summary>
    /// <returns>The current glossary snapshot.</returns>
    public static StructuredDialogueGlossarySnapshot GetSnapshot()
    {
        lock (SyncLock)
        {
            return new StructuredDialogueGlossarySnapshot(
                lastLoadObservedAtUtc,
                lastLoadSucceeded,
                lastLoadPath,
                currentEntries.Count,
                lastSkippedEntryCount,
                lastFailureDetail);
        }
    }

    /// <summary>
    ///     Determines whether one optional language scope matches the active
    ///     request language.
    /// </summary>
    /// <param name="entryLanguage">The glossary row language scope.</param>
    /// <param name="requestedLanguage">The requested runtime language.</param>
    /// <returns>
    ///     <see langword="true" /> when the row should remain active for the
    ///     request.
    /// </returns>
    private static bool LanguageMatches(
        string? entryLanguage,
        string? requestedLanguage)
    {
        if (string.IsNullOrWhiteSpace(entryLanguage) ||
            string.IsNullOrWhiteSpace(requestedLanguage))
        {
            return true;
        }

        return string.Equals(
            entryLanguage.Trim(),
            requestedLanguage.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
