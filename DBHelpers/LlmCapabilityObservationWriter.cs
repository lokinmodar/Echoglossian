// <copyright file="LlmCapabilityObservationWriter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Cache;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Echoglossian.DBHelpers;

/// <summary>Schedules capability observations through the exclusive runtime writer.</summary>
internal sealed class LlmCapabilityObservationWriter
{
    private const string Domain = "llm-capability-observation";
    private readonly IPersistenceCoordinator coordinator;
    private int publicationEnabled = 1;

    /// <summary>Initializes a new instance of the writer.</summary>
    /// <param name="coordinator">The process-lifetime persistence coordinator.</param>
    internal LlmCapabilityObservationWriter(IPersistenceCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <summary>Stops post-commit projection publication.</summary>
    internal void DisablePublication() => Interlocked.Exchange(ref this.publicationEnabled, 0);

    /// <summary>Schedules an immutable observation snapshot.</summary>
    /// <param name="observation">The observation to record.</param>
    /// <param name="completion">The completion for the scheduled work.</param>
    /// <returns>The non-blocking admission outcome.</returns>
    internal PersistenceAdmissionStatus TryRecord(
        LlmModelCapabilityObservation observation,
        out Task<PersistenceWriteResult> completion)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var snapshot = Clone(observation);
        if (snapshot.ObservedAtUtc == default)
        {
            snapshot.ObservedAtUtc = DateTime.UtcNow;
        }

        return this.coordinator.TryScheduleWrite(
            new PersistenceWriteRequest(
                new PersistenceWorkKey(Domain, BuildIdentity(snapshot)),
                PersistencePriority.Interactive,
                async (context, cancellationToken) =>
                {
                    var existing = await context.LlmModelCapabilityObservations.FirstOrDefaultAsync(
                        row =>
                            row.Engine == snapshot.Engine && row.ProviderScope == snapshot.ProviderScope &&
                            row.EndpointScope == snapshot.EndpointScope && row.ModelId == snapshot.ModelId &&
                            row.ParameterName == snapshot.ParameterName && row.StatusCode == snapshot.StatusCode &&
                            row.MessageExcerpt == snapshot.MessageExcerpt,
                        cancellationToken).ConfigureAwait(false);
                    if (existing is null)
                    {
                        await context.LlmModelCapabilityObservations.AddAsync(Clone(snapshot), cancellationToken).ConfigureAwait(false);
                        return PersistenceWriteMutation.ChangedResult;
                    }

                    if (existing.ProviderErrorCode == snapshot.ProviderErrorCode && existing.ObservedAtUtc == snapshot.ObservedAtUtc)
                    {
                        return PersistenceWriteMutation.UnchangedResult;
                    }

                    existing.ProviderErrorCode = snapshot.ProviderErrorCode;
                    existing.ObservedAtUtc = snapshot.ObservedAtUtc;
                    return PersistenceWriteMutation.ChangedResult;
                },
                () =>
                {
                    if (Volatile.Read(ref this.publicationEnabled) != 0)
                    {
                        LlmCapabilityCacheManager.PublishObservation(Clone(snapshot));
                    }
                }),
            out completion);
    }

    private static string BuildIdentity(LlmModelCapabilityObservation value) => string.Concat(
        Segment(value.Engine), Segment(value.ProviderScope), Segment(value.EndpointScope), Segment(value.ModelId),
        Segment(value.ParameterName), Segment(value.StatusCode.ToString(CultureInfo.InvariantCulture)), Segment(value.MessageExcerpt));

    private static string Segment(string value) => string.Concat(value.Length.ToString(CultureInfo.InvariantCulture), ":", value);

    private static LlmModelCapabilityObservation Clone(LlmModelCapabilityObservation value) => new()
    {
        Engine = value.Engine, ProviderScope = value.ProviderScope, EndpointScope = value.EndpointScope,
        ModelId = value.ModelId, ParameterName = value.ParameterName, StatusCode = value.StatusCode,
        ProviderErrorCode = value.ProviderErrorCode, MessageExcerpt = value.MessageExcerpt, ObservedAtUtc = value.ObservedAtUtc,
    };
}
