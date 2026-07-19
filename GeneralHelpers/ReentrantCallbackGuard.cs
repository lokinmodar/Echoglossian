// <copyright file="ReentrantCallbackGuard.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Provides a small non-blocking guard for callbacks that must not process
///     nested invocations while mutating the surface that raised the callback.
/// </summary>
internal sealed class ReentrantCallbackGuard
{
  private int isEntered;

  /// <summary>
  ///     Attempts to enter the protected callback section.
  /// </summary>
  /// <returns>
  ///     A disposable lease when entry succeeds; otherwise, <see langword="null" />.
  /// </returns>
  public ReentrantCallbackLease? TryEnter()
  {
    return Interlocked.Exchange(ref this.isEntered, 1) == 0
        ? new ReentrantCallbackLease(this)
        : null;
  }

  private void Exit()
  {
    Volatile.Write(ref this.isEntered, 0);
  }

  /// <summary>
  ///     Releases one successful <see cref="ReentrantCallbackGuard" /> entry.
  /// </summary>
  internal sealed class ReentrantCallbackLease : IDisposable
  {
    private ReentrantCallbackGuard? owner;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="ReentrantCallbackLease" /> class.
    /// </summary>
    /// <param name="owner">The guard that owns this lease.</param>
    internal ReentrantCallbackLease(ReentrantCallbackGuard owner)
    {
      this.owner = owner;
    }

    /// <inheritdoc />
    public void Dispose()
    {
      Interlocked.Exchange(ref this.owner, null)?.Exit();
    }
  }
}
