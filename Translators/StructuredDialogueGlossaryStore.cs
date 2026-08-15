// <copyright file="StructuredDialogueGlossaryStore.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Immutable;

using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

/// <summary>
///     Retains the currently loaded dialogue glossary rows and the last load
///     snapshot for debugger inspection.
/// </summary>
public static class StructuredDialogueGlossaryStore
{
    private static readonly Func<string, CancellationToken, Task<StructuredDialogueGlossaryLoadResult>>
        DefaultLoader = StructuredDialogueGlossaryLoader.LoadFromFileAsync;
    private static Func<string, CancellationToken, Task<StructuredDialogueGlossaryLoadResult>>
        loadFromFileAsync = DefaultLoader;
    private static StoreState currentState = StoreState.Empty;
    private static long requestedGeneration;

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
        var generation = Interlocked.Increment(ref requestedGeneration);
        PublishState(StoreState.Empty with { Generation = generation });
    }

    /// <summary>
    ///     Refreshes the glossary rows from one operator-provided file path.
    /// </summary>
    /// <param name="filePath">The glossary file path.</param>
    /// <returns>Whether the refresh succeeded.</returns>
    public static bool Refresh(string? filePath)
    {
        return RefreshAsync(filePath, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Refreshes the glossary rows from one operator-provided file path
    ///     without blocking the caller thread.
    /// </summary>
    /// <param name="filePath">The glossary file path.</param>
    /// <param name="cancellationToken">The token that cancels the load.</param>
    /// <returns>A task containing the refresh result.</returns>
    public static async Task<bool> RefreshAsync(
        string? filePath,
        CancellationToken cancellationToken)
    {
        var generation = Interlocked.Increment(ref requestedGeneration);
        var observedAtUtc = DateTime.UtcNow;
        var normalizedPath = string.IsNullOrWhiteSpace(filePath)
            ? null
            : Path.GetFullPath(filePath.Trim());
        StructuredDialogueGlossaryLoadResult result;

        try
        {
            result = await loadFromFileAsync(
                normalizedPath ?? string.Empty,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            result = new StructuredDialogueGlossaryLoadResult(
                false,
                [],
                0,
                $"{ex.GetType().Name}: {ex.Message}");
        }

        if (generation != Volatile.Read(ref requestedGeneration))
        {
            return result.Succeeded;
        }

        PublishState(new StoreState(
            generation,
            result.Succeeded
                ? result.Entries.ToImmutableArray()
                : ImmutableArray<StructuredDialogueGlossaryEntry>.Empty,
            observedAtUtc,
            result.Succeeded,
            normalizedPath,
            result.SkippedEntryCount,
            result.Succeeded
                ? null
                : result.FailureDetail));
        return result.Succeeded;
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
        var state = Volatile.Read(ref currentState);
        return state.Entries
            .Where(entry => LanguageMatches(entry.SourceLanguage, sourceLanguage))
            .Where(entry => LanguageMatches(entry.TargetLanguage, targetLanguage))
            .ToArray();
    }

    /// <summary>
    ///     Gets the current glossary snapshot for debugger inspection.
    /// </summary>
    /// <returns>The current glossary snapshot.</returns>
    public static StructuredDialogueGlossarySnapshot GetSnapshot()
    {
        var state = Volatile.Read(ref currentState);
        return new StructuredDialogueGlossarySnapshot(
            state.LastLoadObservedAtUtc,
            state.LastLoadSucceeded,
            state.LastLoadPath,
            state.Entries.Length,
            state.LastSkippedEntryCount,
            state.LastLoadFailureDetail);
    }

    /// <summary>
    ///     Resets retained state for isolated tests and optionally overrides
    ///     the async file loader.
    /// </summary>
    /// <param name="loadOverride">The async file loader override.</param>
    internal static void ResetForTests(
        Func<string, CancellationToken, Task<StructuredDialogueGlossaryLoadResult>>? loadOverride = null)
    {
        loadFromFileAsync = loadOverride ?? DefaultLoader;
        Interlocked.Exchange(ref requestedGeneration, 0);
        PublishState(StoreState.Empty);
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

    /// <summary>
    ///     Publishes one immutable store state atomically.
    /// </summary>
    /// <param name="state">The immutable state to publish.</param>
    private static void PublishState(StoreState state)
    {
        Volatile.Write(ref currentState, state);
    }

    /// <summary>
    ///     Retains one immutable glossary snapshot plus its published entries.
    /// </summary>
    /// <param name="Generation">The monotonic publication generation.</param>
    /// <param name="Entries">The published immutable glossary entries.</param>
    /// <param name="LastLoadObservedAtUtc">The last load observation time.</param>
    /// <param name="LastLoadSucceeded">Whether the last load succeeded.</param>
    /// <param name="LastLoadPath">The last attempted glossary path.</param>
    /// <param name="LastSkippedEntryCount">The skipped-row count.</param>
    /// <param name="LastLoadFailureDetail">The failure detail when the load failed.</param>
    private sealed record StoreState(
        long Generation,
        ImmutableArray<StructuredDialogueGlossaryEntry> Entries,
        DateTime? LastLoadObservedAtUtc,
        bool? LastLoadSucceeded,
        string? LastLoadPath,
        int LastSkippedEntryCount,
        string? LastLoadFailureDetail)
    {
        /// <summary>
        ///     Gets the empty published store state.
        /// </summary>
        public static StoreState Empty { get; } = new(
            0,
            ImmutableArray<StructuredDialogueGlossaryEntry>.Empty,
            null,
            null,
            null,
            0,
            null);
    }
}
