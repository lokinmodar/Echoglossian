// <copyright file="LlmCapabilityObservationRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.DBHelpers;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Persistence;

namespace Echoglossian.Translators.Capabilities;

/// <summary>Owns registration of the one runtime capability observation writer.</summary>
internal static class LlmCapabilityObservationRuntime
{
    private static LlmCapabilityObservationWriter? writer;

    /// <summary>Registers the process-lifetime writer.</summary>
    internal static void Register(LlmCapabilityObservationWriter value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var previous = Interlocked.CompareExchange(ref writer, value, null);
        if (previous is not null && !ReferenceEquals(previous, value))
        {
            throw new InvalidOperationException("A capability observation writer is already registered.");
        }
    }

    /// <summary>Unregisters the supplied writer and disables late publication.</summary>
    internal static void Unregister(LlmCapabilityObservationWriter value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (ReferenceEquals(Interlocked.CompareExchange(ref writer, null, value), value))
        {
            value.DisablePublication();
        }
    }

    /// <summary>Schedules an observation if the runtime is available.</summary>
    internal static PersistenceAdmissionStatus TryRecord(LlmModelCapabilityObservation observation, out Task<PersistenceWriteResult> completion)
    {
        var current = Volatile.Read(ref writer);
        if (current is null)
        {
            completion = Task.FromResult(new PersistenceWriteResult(PersistenceCompletionStatus.Rejected, 0, null));
            return PersistenceAdmissionStatus.RejectedShutdown;
        }
        return current.TryRecord(observation, out completion);
    }
}
