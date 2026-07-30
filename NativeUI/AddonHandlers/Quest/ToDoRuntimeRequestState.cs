// <copyright file="ToDoRuntimeRequestState.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian.NativeUI.AddonHandlers.Quest;

/// <summary>
///     Captures the immutable translation and persistence scope for one ToDo
///     payload.
/// </summary>
/// <param name="SourceLanguageCode">The captured source-language persistence code.</param>
/// <param name="TargetLanguageCode">The captured configured target-language code.</param>
/// <param name="TranslationEngine">The captured translation engine.</param>
/// <param name="GameVersion">The captured game version.</param>
internal sealed record ToDoTranslationScope(
    string SourceLanguageCode,
    string TargetLanguageCode,
    int TranslationEngine,
    string GameVersion);

/// <summary>
///     Identifies one ToDo translation request and its immutable scope.
/// </summary>
/// <param name="SourceContentHash">The stable non-timer source hash.</param>
/// <param name="Scope">The immutable translation and persistence scope.</param>
internal sealed record ToDoTranslationOperation(
    string SourceContentHash,
    ToDoTranslationScope Scope);

/// <summary>
///     Tracks visible ToDo translation work so timer-driven repaints neither
///     repeat persistence lookups nor let stale completions replace newer work.
/// </summary>
internal sealed class ToDoRuntimeRequestState
{
    private readonly object syncRoot = new();

    private ToDoTranslationOperation? failedOperation;
    private ToDoTranslationOperation? inFlightOperation;
    private long visibleGeneration;
    private ToDoTranslationOperation? visibleOperation;

    /// <summary>
    ///     Records the currently visible operation and returns its generation.
    /// </summary>
    /// <param name="operation">The operation represented by the visible addon.</param>
    /// <returns>The current visible generation.</returns>
    public long ObserveVisibleOperation(ToDoTranslationOperation operation)
    {
        lock (this.syncRoot)
        {
            if (!Equals(this.visibleOperation, operation))
            {
                this.visibleOperation = operation;
                this.visibleGeneration++;
            }

            return this.visibleGeneration;
        }
    }

    /// <summary>
    ///     Gets whether a persistence lookup must be skipped for known pending
    ///     or failed work.
    /// </summary>
    /// <param name="operation">The candidate operation.</param>
    /// <returns><c>true</c> when persistence lookup must be skipped.</returns>
    public bool ShouldSkipPersistenceLookup(ToDoTranslationOperation operation)
    {
        lock (this.syncRoot)
        {
            return Equals(this.inFlightOperation, operation) ||
                   Equals(this.failedOperation, operation);
        }
    }

    /// <summary>
    ///     Marks one visible operation as in flight.
    /// </summary>
    /// <param name="operation">The operation being translated.</param>
    /// <param name="generation">The visible generation that scheduled it.</param>
    /// <returns><c>true</c> when the operation may start.</returns>
    public bool TryStart(ToDoTranslationOperation operation, long generation)
    {
        lock (this.syncRoot)
        {
            if (generation != this.visibleGeneration ||
                !Equals(this.visibleOperation, operation) ||
                this.ShouldSkipPersistenceLookupCore(operation))
            {
                return false;
            }

            this.inFlightOperation = operation;
            return true;
        }
    }

    /// <summary>
    ///     Accepts a completed operation only when it still represents the
    ///     current visible request.
    /// </summary>
    /// <param name="operation">The completed operation.</param>
    /// <param name="generation">The visible generation that scheduled it.</param>
    /// <returns><c>true</c> when the completion may update presentation.</returns>
    public bool TryComplete(ToDoTranslationOperation operation, long generation)
    {
        lock (this.syncRoot)
        {
            if (generation != this.visibleGeneration ||
                !Equals(this.visibleOperation, operation) ||
                !Equals(this.inFlightOperation, operation))
            {
                return false;
            }

            this.inFlightOperation = null;
            this.failedOperation = null;
            return true;
        }
    }

    /// <summary>
    ///     Records a failed operation only when it is still current.
    /// </summary>
    /// <param name="operation">The failed operation.</param>
    /// <param name="generation">The visible generation that scheduled it.</param>
    public void MarkFailed(ToDoTranslationOperation operation, long generation)
    {
        lock (this.syncRoot)
        {
            if (generation != this.visibleGeneration ||
                !Equals(this.visibleOperation, operation) ||
                !Equals(this.inFlightOperation, operation))
            {
                return;
            }

            this.inFlightOperation = null;
            this.failedOperation = operation;
        }
    }

    /// <summary>
    ///     Invalidates all pending work and clears request state.
    /// </summary>
    public void Clear()
    {
        lock (this.syncRoot)
        {
            this.visibleGeneration++;
            this.visibleOperation = null;
            this.inFlightOperation = null;
            this.failedOperation = null;
        }
    }

    private bool ShouldSkipPersistenceLookupCore(ToDoTranslationOperation operation)
    {
        return Equals(this.inFlightOperation, operation) ||
               Equals(this.failedOperation, operation);
    }
}

/// <summary>
///     Resolves the presentation behavior for the dedicated ToDo addon.
/// </summary>
/// <param name="UsesHoverTooltips">Whether hover tooltips are required.</param>
/// <param name="WritesNativeTranslation">Whether native text may be rewritten.</param>
/// <param name="HoverShowsOriginal">Whether hover text should show the source.</param>
internal readonly record struct ToDoPresentationPolicy(
    bool UsesHoverTooltips,
    bool WritesNativeTranslation,
    bool HoverShowsOriginal)
{
    /// <summary>
    ///     Resolves a display policy after applying overlay-only language
    ///     restrictions.
    /// </summary>
    /// <param name="displayMode">The configured display mode.</param>
    /// <param name="overlayOnlyLanguage">Whether native mutation is forbidden.</param>
    /// <returns>The effective dedicated ToDo presentation policy.</returns>
    public static ToDoPresentationPolicy Create(
        JournalTranslationDisplayMode displayMode,
        bool overlayOnlyLanguage)
    {
        return new ToDoPresentationPolicy(
            TranslationDisplayModeHelper.UsesHoverTooltips(
                displayMode,
                overlayOnlyLanguage),
            TranslationDisplayModeHelper.WritesNativeTranslation(
                displayMode,
                overlayOnlyLanguage),
            TranslationDisplayModeHelper.ShowsOriginalTooltips(
                displayMode,
                overlayOnlyLanguage));
    }
}
