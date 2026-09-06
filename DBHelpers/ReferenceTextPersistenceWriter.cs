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
    private const string WriteDomain = "reference-text-write";

    private readonly IPersistenceCoordinator coordinator;
    private readonly object publicationGate = new();
    private int publicationEnabled = 1;

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
    ///     Stops publication to caches after accepted work completes.
    /// </summary>
    internal void DisablePublication()
    {
        lock (this.publicationGate)
        {
            this.publicationEnabled = 0;
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
            (context, cancellationToken) => ReferenceTextPersistenceHelper
                .FindReferenceTextAsync(
                    context,
                    probe,
                    scope,
                    setSelector,
                    cancellationToken),
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
                }),
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
