// <copyright file="PersistenceContracts.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Persistence;

/// <summary>
///     Specifies the outcome of attempting to admit persistence work.
/// </summary>
internal enum PersistenceAdmissionStatus
{
  /// <summary>Specifies that a new work item was accepted.</summary>
  Accepted,

  /// <summary>Specifies that work joined an existing pending item.</summary>
  Joined,

  /// <summary>Specifies that pending work was replaced with newer work.</summary>
  Replaced,

  /// <summary>Specifies that no capacity was available for the work item.</summary>
  RejectedCapacity,

  /// <summary>Specifies that coordinator completion rejected the work item.</summary>
  RejectedShutdown,
}

/// <summary>
///     Specifies the terminal outcome of admitted persistence work.
/// </summary>
internal enum PersistenceCompletionStatus
{
  /// <summary>Specifies that work committed a changed result.</summary>
  Succeeded,

  /// <summary>Specifies that work completed without a database mutation.</summary>
  Unchanged,

  /// <summary>Specifies that work terminated with an error.</summary>
  Failed,

  /// <summary>Specifies that work terminated due to cancellation.</summary>
  Cancelled,

  /// <summary>Specifies that work was rejected before admission.</summary>
  Rejected,
}

/// <summary>
///     Represents the terminal result of one persistence read.
/// </summary>
/// <typeparam name="T">The value returned by the read.</typeparam>
/// <param name="Status">The terminal completion status.</param>
/// <param name="Value">The value produced by the read, if any.</param>
/// <param name="Error">The terminal error, if any.</param>
internal readonly record struct PersistenceReadResult<T>(
    PersistenceCompletionStatus Status,
    T? Value,
    Exception? Error);

/// <summary>
///     Represents the terminal result of one persistence write.
/// </summary>
/// <param name="Status">The terminal completion status.</param>
/// <param name="AffectedRows">The number of rows changed by the write.</param>
/// <param name="Error">The terminal error, if any.</param>
internal readonly record struct PersistenceWriteResult(
    PersistenceCompletionStatus Status,
    int AffectedRows,
    Exception? Error);

/// <summary>
///     Represents whether a write mutation changed tracked persistence state.
/// </summary>
/// <param name="Changed">
///     <see langword="true" /> when the mutation changed state; otherwise,
///     <see langword="false" />.
/// </param>
internal readonly record struct PersistenceWriteMutation(bool Changed)
{
  /// <summary>
  ///     Gets a mutation result that changed tracked state.
  /// </summary>
  internal static PersistenceWriteMutation ChangedResult { get; } = new(true);

  /// <summary>
  ///     Gets a mutation result that left tracked state unchanged.
  /// </summary>
  internal static PersistenceWriteMutation UnchangedResult { get; } = new(false);
}

/// <summary>
///     Validates the delegates required by one positional persistence write
///     request.
/// </summary>
internal abstract record PersistenceWriteRequestValidation
{
  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="PersistenceWriteRequestValidation" /> class.
  /// </summary>
  /// <param name="ApplyAsync">The mutation applied within a transaction.</param>
  /// <param name="PublishAfterCommit">The projection published after commit.</param>
  /// <exception cref="ArgumentNullException">
  ///     <paramref name="ApplyAsync" /> or
  ///     <paramref name="PublishAfterCommit" /> is <see langword="null" />.
  /// </exception>
  protected PersistenceWriteRequestValidation(
      Func<EchoglossianDbContext, CancellationToken, Task<PersistenceWriteMutation>> ApplyAsync,
      Action PublishAfterCommit)
  {
    ArgumentNullException.ThrowIfNull(ApplyAsync);
    ArgumentNullException.ThrowIfNull(PublishAfterCommit);
  }
}

/// <summary>
///     Describes one write that can be applied and published after commit.
/// </summary>
/// <param name="Key">The canonical identity of the requested write.</param>
/// <param name="Priority">The bounded lane that receives the write.</param>
/// <param name="ApplyAsync">The mutation applied within a transaction.</param>
/// <param name="PublishAfterCommit">The projection published after commit.</param>
internal sealed record PersistenceWriteRequest(
    PersistenceWorkKey Key,
    PersistencePriority Priority,
    Func<EchoglossianDbContext, CancellationToken, Task<PersistenceWriteMutation>> ApplyAsync,
    Action PublishAfterCommit)
    : PersistenceWriteRequestValidation(ApplyAsync, PublishAfterCommit);
