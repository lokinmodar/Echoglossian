// <copyright file="IPersistenceCoordinator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Persistence;

/// <summary>
///     Coordinates bounded asynchronous persistence operations.
/// </summary>
internal interface IPersistenceCoordinator : IAsyncDisposable
{
  /// <summary>Attempts to schedule or join one bounded read.</summary>
  /// <typeparam name="T">The value returned by the read.</typeparam>
  /// <param name="key">The canonical identity for coalescing.</param>
  /// <param name="priority">The bounded lane that receives the read.</param>
  /// <param name="readAsync">The short-lived context query.</param>
  /// <param name="publish">The successful result publication.</param>
  /// <param name="completion">The terminal read completion.</param>
  /// <returns>The non-blocking admission outcome.</returns>
  PersistenceAdmissionStatus TryScheduleRead<T>(
      PersistenceWorkKey key,
      PersistencePriority priority,
      Func<EchoglossianDbContext, CancellationToken, Task<T>> readAsync,
      Action<T>? publish,
      out Task<PersistenceReadResult<T>> completion);

  /// <summary>Attempts to schedule or join one bounded write.</summary>
  /// <param name="request">The canonical write request.</param>
  /// <param name="completion">The terminal write completion.</param>
  /// <returns>The non-blocking admission outcome.</returns>
  PersistenceAdmissionStatus TryScheduleWrite(
      PersistenceWriteRequest request,
      out Task<PersistenceWriteResult> completion);

  /// <summary>Gets an immutable diagnostic metrics snapshot.</summary>
  /// <returns>The current metrics snapshot.</returns>
  PersistenceMetricsSnapshot GetMetrics();

  /// <summary>Stops accepting new persistence operations.</summary>
  void StopAccepting();

  /// <summary>Completes accepted work.</summary>
  /// <param name="cancellationToken">The cancellation token for waiting.</param>
  /// <returns>A task that completes when accepted work drains.</returns>
  Task CompleteAsync(CancellationToken cancellationToken = default);
}
