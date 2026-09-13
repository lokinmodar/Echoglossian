// <copyright file="ReferenceTextPersistenceWriter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Globalization;
using System.Text;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;

namespace Echoglossian.DBHelpers;

/// <summary>
///     Schedules canonical reference-text reads and writes through the shared
///     persistence coordinator.
/// </summary>
internal sealed class ReferenceTextPersistenceWriter
{
    private const string ReadDomain = "reference-text-read";
    private const string SnapshotDomain = "reference-text-snapshot";
    private const string WriteDomain = "reference-text-write";

    private readonly IPersistenceCoordinator coordinator;
    private readonly CancellationTokenSource operationCancellation = new();
    private readonly object publicationGate = new();
    private int publicationEnabled = 1;
    private int shutdownRequested;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="ReferenceTextPersistenceWriter" /> class.
    /// </summary>
    /// <param name="coordinator">The process-lifetime persistence coordinator.</param>
    internal ReferenceTextPersistenceWriter(IPersistenceCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>
    ///     Stops cache publication and cancels accepted reference-text database
    ///     work when the owning plugin lifetime ends.
    /// </summary>
    internal void DisablePublication()
    {
        lock (this.publicationGate)
        {
            this.publicationEnabled = 0;
        }

        if (Interlocked.Exchange(ref this.shutdownRequested, 1) == 0)
        {
            this.operationCancellation.Cancel();
        }
    }

    /// <summary>Publishes an already completed read under the shared unload gate.</summary>
    /// <param name="publish">The cache publication owned by one read subscriber.</param>
    internal void PublishRead(Action publish)
    {
        lock (this.publicationGate)
        {
            if (this.publicationEnabled != 0)
            {
                publish();
            }
        }
    }

    /// <summary>
    ///     Schedules one canonical reference-text lookup at background
    ///     priority.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="probe">The row defining the lookup identity.</param>
    /// <param name="scope">The translation reuse scope.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <param name="publish">The cache projection invoked after a successful read.</param>
    /// <param name="completion">The terminal read completion.</param>
    /// <param name="priority">The admission lane; visible requests may use interactive priority.</param>
    /// <returns>The non-blocking admission outcome.</returns>
    internal PersistenceAdmissionStatus TryFind<TRow>(
        TRow probe,
        TranslationReuseScope scope,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector,
        Action<TRow>? publish,
        out Task<PersistenceReadResult<TRow?>> completion,
        PersistencePriority priority = PersistencePriority.Background)
        where TRow : ReferenceTextRowBase
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(setSelector);

        return this.coordinator.TryScheduleRead(
            new PersistenceWorkKey(ReadDomain, BuildReadIdentity<TRow>(probe, scope)),
            priority,
            async (context, cancellationToken) =>
            {
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    this.operationCancellation.Token);
                return await ReferenceTextPersistenceHelper
                    .FindReferenceTextAsync(
                        context,
                        probe,
                        scope,
                        setSelector,
                        linkedCancellation.Token)
                    .ConfigureAwait(false);
            },
            row =>
            {
                lock (this.publicationGate)
                {
                    if (row is not null && this.publicationEnabled != 0)
                    {
                        publish?.Invoke(row);
                    }
                }
            },
            out completion);
    }

    /// <summary>
    ///     Schedules one registration-level snapshot of complete canonical
    ///     rows at background priority.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="scope">The translation reuse scope.</param>
    /// <param name="gameVersion">The exact game-version scope.</param>
    /// <param name="snapshotRequest">The bounded registration snapshot request.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <param name="completion">The terminal snapshot completion.</param>
    /// <param name="priority">The admission lane.</param>
    /// <returns>The non-blocking admission outcome.</returns>
    internal PersistenceAdmissionStatus TryLoadCompleteSnapshot<TRow>(
        TranslationReuseScope scope,
        string? gameVersion,
        ReferenceTextSnapshotRequest snapshotRequest,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector,
        out Task<PersistenceReadResult<IReadOnlyList<TRow>>> completion,
        PersistencePriority priority = PersistencePriority.Background)
        where TRow : ReferenceTextRowBase
    {
        ArgumentNullException.ThrowIfNull(snapshotRequest);
        ArgumentNullException.ThrowIfNull(setSelector);
        var sourceLanguages = BuildEquivalentLanguageValues(
            scope.SourceLanguageCode);
        var targetLanguages = BuildEquivalentLanguageValues(
            scope.TargetLanguageCode);

        return this.coordinator.TryScheduleRead(
            new PersistenceWorkKey(
                SnapshotDomain,
                BuildSnapshotIdentity<TRow>(scope, gameVersion, snapshotRequest.Identity)),
            priority,
            async (context, cancellationToken) =>
            {
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    this.operationCancellation.Token);
                var keys = snapshotRequest.Keys
                    .Distinct()
                    .ToArray();
                var referenceIds = keys
                    .Select(key => key.ReferenceId)
                    .Distinct()
                    .ToArray();
                var sourceContentHashes = keys
                    .Select(key => key.SourceContentHash)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var databaseKeys = keys
                    .Select(key => BuildDatabaseSnapshotKey(
                        key.ReferenceId,
                        key.SourceContentHash))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var exactKeys = keys.ToHashSet();
                var candidates = await setSelector(context)
                    .AsNoTracking()
                    .Where(row =>
                        referenceIds.Contains(row.ReferenceId) &&
                        sourceContentHashes.Contains(row.SourceContentHash!) &&
                        databaseKeys.Contains(
                            row.ReferenceId.ToString() + ":" + row.SourceContentHash) &&
                        row.OriginalLang != null &&
                        sourceLanguages.Contains(row.OriginalLang.Trim().ToLower()) &&
                        row.TranslationLang != null &&
                        targetLanguages.Contains(row.TranslationLang.Trim().ToLower()) &&
                        row.GameVersion == gameVersion &&
                        (!scope.RequireMatchingEngine ||
                         row.TranslationEngine == scope.TranslationEngine))
                    .ToListAsync(linkedCancellation.Token)
                    .ConfigureAwait(false);
                return (IReadOnlyList<TRow>)candidates
                    .Where(row =>
                        row.SourceContentHash is not null &&
                        exactKeys.Contains(new ReferenceTextSnapshotKey(
                            row.ReferenceId,
                            row.SourceContentHash)) &&
                        scope.Matches(
                            row.OriginalLang,
                            row.TranslationLang,
                            row.TranslationEngine) &&
                        HasCompleteTranslation(row))
                    .ToArray();
            },
            publish: null,
            out completion);
    }

    /// <summary>
    ///     Schedules one canonical reference-text upsert at background
    ///     priority.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="row">The immutable canonical row to persist.</param>
    /// <param name="setSelector">Selects the matching DbSet.</param>
    /// <param name="publish">The cache projection invoked after commit or a successful unchanged read.</param>
    /// <param name="completion">The terminal write completion.</param>
    /// <param name="priority">The admission lane captured with the originating request.</param>
    /// <returns>The non-blocking admission outcome.</returns>
    internal PersistenceAdmissionStatus TryPersist<TRow>(
        TRow row,
        Func<EchoglossianDbContext, DbSet<TRow>> setSelector,
        Action<TRow>? publish,
        out Task<PersistenceWriteResult> completion,
        PersistencePriority priority = PersistencePriority.Background)
        where TRow : ReferenceTextRowBase
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(setSelector);
        if (!ReferenceTextPersistenceHelper.IsValidForPersistence(row))
        {
            throw new ArgumentException(
                "The reference-text row does not contain a valid persistence identity.",
                nameof(row));
        }

        TRow? persistedRow = null;
        return this.coordinator.TryScheduleWrite(
            new PersistenceWriteRequest(
                new PersistenceWorkKey(WriteDomain, BuildWriteIdentity<TRow>(row)),
                priority,
                async (context, cancellationToken) =>
                {
                    var result = await ReferenceTextPersistenceHelper
                        .UpsertReferenceTextAsync(
                            context,
                            row,
                            setSelector,
                            cancellationToken)
                        .ConfigureAwait(false);
                    persistedRow = result.Row;
                    return result.Changed
                        ? PersistenceWriteMutation.ChangedResult
                        : PersistenceWriteMutation.UnchangedResult;
                },
                () =>
                {
                    lock (this.publicationGate)
                    {
                        if (persistedRow is not null && this.publicationEnabled != 0)
                        {
                            publish?.Invoke(persistedRow);
                        }
                    }
                })
            {
                CancellationToken = this.operationCancellation.Token,
            },
            out completion);
    }

    /// <summary>
    ///     Builds the write coalescing identity from the exact persistence
    ///     lookup semantics.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="row">The row defining the write identity.</param>
    /// <returns>The collision-safe coordinator identity.</returns>
    private static string BuildWriteIdentity<TRow>(TRow row)
        where TRow : ReferenceTextRowBase
    {
        return BuildIdentity(
            typeof(TRow).FullName ?? typeof(TRow).Name,
            row.ReferenceId.ToString(CultureInfo.InvariantCulture),
            NormalizeLanguageIdentity(row.OriginalLang),
            NormalizeLanguageIdentity(row.TranslationLang),
            row.TranslationEngine?.ToString(CultureInfo.InvariantCulture),
            row.GameVersion,
            row.SourceContentHash);
    }

    /// <summary>
    ///     Builds the read coalescing identity from the lookup scope and
    ///     canonical source identity.
    /// </summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="probe">The row defining the source identity.</param>
    /// <param name="scope">The requested translation reuse scope.</param>
    /// <returns>The collision-safe coordinator identity.</returns>
    private static string BuildReadIdentity<TRow>(TRow probe, TranslationReuseScope scope)
        where TRow : ReferenceTextRowBase
    {
        return BuildIdentity(
            typeof(TRow).FullName ?? typeof(TRow).Name,
            probe.ReferenceId.ToString(CultureInfo.InvariantCulture),
            NormalizeLanguageIdentity(scope.SourceLanguageCode),
            NormalizeLanguageIdentity(scope.TargetLanguageCode),
            scope.RequireMatchingEngine
                ? scope.TranslationEngine?.ToString(CultureInfo.InvariantCulture)
                : "*",
            probe.GameVersion,
            probe.SourceContentHash);
    }

    /// <summary>Builds the coalescing identity for one registration snapshot.</summary>
    /// <typeparam name="TRow">The concrete row type.</typeparam>
    /// <param name="scope">The requested translation reuse scope.</param>
    /// <param name="gameVersion">The exact game-version scope.</param>
    /// <param name="registrationIdentity">The incrementally built source-set identity.</param>
    /// <returns>The collision-safe coordinator identity.</returns>
    private static string BuildSnapshotIdentity<TRow>(
        TranslationReuseScope scope,
        string? gameVersion,
        string registrationIdentity)
        where TRow : ReferenceTextRowBase
    {
        return BuildIdentity(
            typeof(TRow).FullName ?? typeof(TRow).Name,
            NormalizeLanguageIdentity(scope.SourceLanguageCode),
            NormalizeLanguageIdentity(scope.TargetLanguageCode),
            scope.RequireMatchingEngine
                ? scope.TranslationEngine?.ToString(CultureInfo.InvariantCulture)
                : "*",
            gameVersion,
            registrationIdentity);
    }

    /// <summary>Identifies one source row captured from a registration generation.</summary>
    /// <param name="ReferenceId">The stable sheet-row identifier.</param>
    /// <param name="SourceContentHash">The exact canonical source hash.</param>
    internal readonly record struct ReferenceTextSnapshotKey(
        uint ReferenceId,
        string SourceContentHash);

    /// <summary>Describes one bounded registration-level snapshot query.</summary>
    /// <param name="Keys">The captured current source identities.</param>
    /// <param name="Identity">The incrementally built coordinator identity.</param>
    internal sealed record ReferenceTextSnapshotRequest(
        IReadOnlyList<ReferenceTextSnapshotKey> Keys,
        string Identity);

    /// <summary>Builds the exact SQL-filterable registration identity.</summary>
    /// <param name="referenceId">The stable sheet-row identifier.</param>
    /// <param name="sourceContentHash">The exact canonical source hash.</param>
    /// <returns>The unambiguous database candidate identity.</returns>
    private static string BuildDatabaseSnapshotKey(
        uint referenceId,
        string sourceContentHash)
    {
        return string.Concat(
            referenceId.ToString(CultureInfo.InvariantCulture),
            ":",
            sourceContentHash);
    }

    /// <summary>Builds the finite SQL candidate set for one normalized language.</summary>
    /// <param name="language">The requested language value.</param>
    /// <returns>Lower-case values accepted by the shared language semantics.</returns>
    private static IReadOnlyList<string> BuildEquivalentLanguageValues(string? language)
    {
        var normalized = RuntimeLanguageHelper.NormalizeLanguage(language)
            .ToLowerInvariant();
        return normalized switch
        {
            "en" => ["en", "english"],
            "de" => ["de", "german", "deutsch"],
            "fr" => ["fr", "french", "français", "francais"],
            "ja" => ["ja", "japanese", "日本語"],
            "zh-cn" => ["zh-cn", "zh", "zh-hans"],
            "zh-tw" => ["zh-tw", "zh-hant"],
            "pt-br" => ["pt-br", "pt"],
            "iw" => ["iw", "he"],
            "no" => ["no", "nb"],
            "tl" => ["tl", "fil"],
            "jw" => ["jw", "jv"],
            _ when !string.IsNullOrWhiteSpace(normalized) => [normalized],
            _ => [],
        };
    }

    /// <summary>Gets whether all source fields have stored translations.</summary>
    /// <param name="row">The persisted canonical row.</param>
    /// <returns>True when the row is complete for its source payload.</returns>
    private static bool HasCompleteTranslation(ReferenceTextRowBase row)
    {
        return !string.IsNullOrWhiteSpace(row.TranslatedName) &&
               (string.IsNullOrWhiteSpace(row.OriginalDescription) ||
                !string.IsNullOrWhiteSpace(row.TranslatedDescription));
    }

    /// <summary>
    ///     Builds an unambiguous string identity from nullable segments.
    /// </summary>
    /// <param name="segments">The identity segments.</param>
    /// <returns>The encoded identity.</returns>
    private static string BuildIdentity(params string?[] segments)
    {
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            var value = segment ?? string.Empty;
            _ = builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Converts one language value into the case-insensitive identity used
    ///     by <see cref="RuntimeLanguageHelper.LanguagesMatch" />.
    /// </summary>
    /// <param name="language">The source or target language value.</param>
    /// <returns>The stable language identity segment.</returns>
    private static string NormalizeLanguageIdentity(string? language)
    {
        return RuntimeLanguageHelper.NormalizeLanguage(language)
            .ToUpperInvariant();
    }
}
