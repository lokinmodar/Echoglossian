// <copyright file="TranslationBrokerRateLimitRetryContext.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Carries the existing broker's rate-limit retry state through one
///     asynchronous resolver execution.
/// </summary>
internal static class TranslationBrokerRateLimitRetryContext
{
  private static readonly AsyncLocal<int> RetryDepth = new();

  /// <summary>
  ///     Gets a value indicating whether the current resolver execution is a
  ///     broker-owned rate-limit retry.
  /// </summary>
  internal static bool IsActive => RetryDepth.Value > 0;

  /// <summary>
  ///     Enters the context for a broker-owned rate-limit retry.
  /// </summary>
  /// <param name="rateLimitAttempt">The zero-based broker retry attempt.</param>
  /// <returns>A scope that restores the prior context on disposal.</returns>
  internal static IDisposable Enter(int rateLimitAttempt)
  {
    if (rateLimitAttempt <= 0)
    {
      return EmptyScope.Instance;
    }

    RetryDepth.Value++;
    return new RetryScope();
  }

  private sealed class EmptyScope : IDisposable
  {
    /// <summary>Gets the singleton empty retry scope.</summary>
    internal static EmptyScope Instance { get; } = new();

    /// <inheritdoc />
    public void Dispose()
    {
    }
  }

  private sealed class RetryScope : IDisposable
  {
    /// <inheritdoc />
    public void Dispose()
    {
      RetryDepth.Value--;
    }
  }
}
