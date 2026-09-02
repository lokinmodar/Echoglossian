// <copyright file="PersistenceCoordinatorOptions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Persistence;

/// <summary>
///     Defines immutable bounds for persistence coordination.
/// </summary>
internal sealed class PersistenceCoordinatorOptions
{
  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="PersistenceCoordinatorOptions" /> class.
  /// </summary>
  /// <param name="interactiveCapacity">The maximum interactive work items.</param>
  /// <param name="backgroundCapacity">The maximum background work items.</param>
  /// <param name="readerConcurrency">The maximum concurrent readers.</param>
  /// <param name="maxBatchSize">The maximum writes in one batch.</param>
  /// <param name="batchCollectionWindow">The maximum batch collection interval.</param>
  /// <param name="maxAttempts">The maximum total write attempts.</param>
  /// <param name="retryDelays">The delays between write attempts.</param>
  /// <param name="contextPoolSize">The maximum pooled contexts.</param>
  /// <param name="sqliteDefaultTimeoutSeconds">The SQLite default timeout.</param>
  /// <param name="shutdownTimeout">The maximum drain interval during shutdown.</param>
  internal PersistenceCoordinatorOptions(
      int interactiveCapacity,
      int backgroundCapacity,
      int readerConcurrency,
      int maxBatchSize,
      TimeSpan batchCollectionWindow,
      int maxAttempts,
      IReadOnlyList<TimeSpan> retryDelays,
      int contextPoolSize,
      int sqliteDefaultTimeoutSeconds,
      TimeSpan shutdownTimeout)
  {
    ValidatePositive(interactiveCapacity, nameof(interactiveCapacity));
    ValidatePositive(backgroundCapacity, nameof(backgroundCapacity));
    ValidatePositive(readerConcurrency, nameof(readerConcurrency));
    ValidatePositive(maxBatchSize, nameof(maxBatchSize));
    ValidatePositive(maxAttempts, nameof(maxAttempts));
    ValidatePositive(contextPoolSize, nameof(contextPoolSize));
    ValidatePositive(sqliteDefaultTimeoutSeconds, nameof(sqliteDefaultTimeoutSeconds));
    ValidatePositive(batchCollectionWindow, nameof(batchCollectionWindow));
    ValidateNonNegative(shutdownTimeout, nameof(shutdownTimeout));
    ArgumentNullException.ThrowIfNull(retryDelays);

    if (retryDelays.Count != maxAttempts - 1)
    {
      throw new ArgumentException(
          "Retry delays must contain one entry between each attempt.",
          nameof(retryDelays));
    }

    foreach (var retryDelay in retryDelays)
    {
      if (retryDelay < TimeSpan.Zero)
      {
        throw new ArgumentOutOfRangeException(
            nameof(retryDelays),
            "Retry delays cannot be negative.");
      }
    }

    this.InteractiveCapacity = interactiveCapacity;
    this.BackgroundCapacity = backgroundCapacity;
    this.ReaderConcurrency = readerConcurrency;
    this.MaxBatchSize = maxBatchSize;
    this.BatchCollectionWindow = batchCollectionWindow;
    this.MaxAttempts = maxAttempts;
    this.RetryDelays = Array.AsReadOnly(retryDelays.ToArray());
    this.ContextPoolSize = contextPoolSize;
    this.SqliteDefaultTimeoutSeconds = sqliteDefaultTimeoutSeconds;
    this.ShutdownTimeout = shutdownTimeout;
  }

  /// <summary>
  ///     Gets the approved internal coordinator bounds.
  /// </summary>
  internal static PersistenceCoordinatorOptions Default { get; } = new(
      64,
      256,
      2,
      32,
      TimeSpan.FromMilliseconds(5),
      3,
      new[] { TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(100) },
      4,
      1,
      TimeSpan.FromSeconds(5));

  /// <summary>Gets the maximum interactive work items.</summary>
  internal int InteractiveCapacity { get; }

  /// <summary>Gets the maximum background work items.</summary>
  internal int BackgroundCapacity { get; }

  /// <summary>Gets the maximum concurrent readers.</summary>
  internal int ReaderConcurrency { get; }

  /// <summary>Gets the maximum writes in one batch.</summary>
  internal int MaxBatchSize { get; }

  /// <summary>Gets the maximum batch collection interval.</summary>
  internal TimeSpan BatchCollectionWindow { get; }

  /// <summary>Gets the maximum total write attempts.</summary>
  internal int MaxAttempts { get; }

  /// <summary>Gets the delays between write attempts.</summary>
  internal IReadOnlyList<TimeSpan> RetryDelays { get; }

  /// <summary>Gets the maximum pooled contexts.</summary>
  internal int ContextPoolSize { get; }

  /// <summary>Gets the SQLite default timeout.</summary>
  internal int SqliteDefaultTimeoutSeconds { get; }

  /// <summary>Gets the maximum drain interval during shutdown.</summary>
  internal TimeSpan ShutdownTimeout { get; }

  /// <summary>
  ///     Validates a positive integer setting.
  /// </summary>
  /// <param name="value">The setting value.</param>
  /// <param name="parameterName">The setting parameter name.</param>
  /// <exception cref="ArgumentOutOfRangeException">
  ///     <paramref name="value" /> is not positive.
  /// </exception>
  private static void ValidatePositive(int value, string parameterName)
  {
    if (value <= 0)
    {
      throw new ArgumentOutOfRangeException(parameterName, "The value must be positive.");
    }
  }

  /// <summary>
  ///     Validates a positive interval setting.
  /// </summary>
  /// <param name="value">The interval value.</param>
  /// <param name="parameterName">The interval parameter name.</param>
  /// <exception cref="ArgumentOutOfRangeException">
  ///     <paramref name="value" /> is not positive.
  /// </exception>
  private static void ValidatePositive(TimeSpan value, string parameterName)
  {
    if (value <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(parameterName, "The value must be positive.");
    }
  }

  private static void ValidateNonNegative(TimeSpan value, string parameterName)
  {
    if (value < TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(parameterName, "The value cannot be negative.");
    }
  }
}
