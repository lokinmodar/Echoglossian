// <copyright file="PayloadStabilityTracker.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Tracks whether one payload signature has remained unchanged for long
///     enough to be considered stable.
/// </summary>
internal sealed class PayloadStabilityTracker
{
    private readonly TimeSpan minimumStableDuration;
    private readonly int minimumObservations;

    private int consecutiveObservations;
    private DateTime firstObservedUtc;
    private string? lastPayloadSignature;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="PayloadStabilityTracker" /> class.
    /// </summary>
    /// <param name="minimumObservations">
    ///     The minimum number of consecutive observations required before a
    ///     payload may be considered stable.
    /// </param>
    /// <param name="minimumStableDuration">
    ///     The minimum wall-clock duration during which the same payload must
    ///     remain unchanged before it is considered stable.
    /// </param>
    public PayloadStabilityTracker(
        int minimumObservations,
        TimeSpan minimumStableDuration)
    {
        this.minimumObservations = Math.Max(1, minimumObservations);
        this.minimumStableDuration = minimumStableDuration;
    }

    /// <summary>
    ///     Records one payload observation and reports whether the payload is
    ///     now stable.
    /// </summary>
    /// <param name="payloadSignature">
    ///     The stable signature of the observed payload.
    /// </param>
    /// <param name="observedAtUtc">The observation timestamp in UTC.</param>
    /// <returns>
    ///     <see langword="true" /> when the same payload has been observed for
    ///     long enough to be considered stable; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public bool Observe(string payloadSignature, DateTime observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSignature);

        if (!string.Equals(
                this.lastPayloadSignature,
                payloadSignature,
                StringComparison.Ordinal))
        {
            this.lastPayloadSignature = payloadSignature;
            this.firstObservedUtc = observedAtUtc;
            this.consecutiveObservations = 1;
            return false;
        }

        this.consecutiveObservations++;
        return this.consecutiveObservations >= this.minimumObservations &&
               observedAtUtc - this.firstObservedUtc >=
               this.minimumStableDuration;
    }

    /// <summary>
    ///     Clears the current observation state.
    /// </summary>
    public void Reset()
    {
        this.lastPayloadSignature = null;
        this.firstObservedUtc = DateTime.MinValue;
        this.consecutiveObservations = 0;
    }
}
