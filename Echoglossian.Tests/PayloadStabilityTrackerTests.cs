// <copyright file="PayloadStabilityTrackerTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the payload-stability tracker used by recycled UI surfaces.
/// </summary>
public class PayloadStabilityTrackerTests
{
    /// <summary>
    ///     Verifies that one first observation is never treated as stable.
    /// </summary>
    [Fact]
    public void Observe_FirstObservation_IsNotStable()
    {
        var tracker = new PayloadStabilityTracker(
            minimumObservations: 2,
            minimumStableDuration: TimeSpan.FromMilliseconds(150));

        var stable = tracker.Observe(
            "payload-a",
            new DateTime(2026, 4, 19, 23, 0, 0, DateTimeKind.Utc));

        Assert.False(stable);
    }

    /// <summary>
    ///     Verifies that repeated observations still need the minimum stable
    ///     duration.
    /// </summary>
    [Fact]
    public void Observe_SamePayloadTooSoon_RemainsUnstable()
    {
        var tracker = new PayloadStabilityTracker(
            minimumObservations: 2,
            minimumStableDuration: TimeSpan.FromMilliseconds(150));
        var startUtc = new DateTime(
            2026,
            4,
            19,
            23,
            0,
            0,
            DateTimeKind.Utc);

        _ = tracker.Observe("payload-a", startUtc);
        var stable = tracker.Observe(
            "payload-a",
            startUtc.AddMilliseconds(50));

        Assert.False(stable);
    }

    /// <summary>
    ///     Verifies that one repeated payload becomes stable after enough
    ///     observations and time.
    /// </summary>
    [Fact]
    public void Observe_SamePayloadLongEnough_BecomesStable()
    {
        var tracker = new PayloadStabilityTracker(
            minimumObservations: 2,
            minimumStableDuration: TimeSpan.FromMilliseconds(150));
        var startUtc = new DateTime(
            2026,
            4,
            19,
            23,
            0,
            0,
            DateTimeKind.Utc);

        _ = tracker.Observe("payload-a", startUtc);
        var stable = tracker.Observe(
            "payload-a",
            startUtc.AddMilliseconds(200));

        Assert.True(stable);
    }

    /// <summary>
    ///     Verifies that a changed payload resets the stability window.
    /// </summary>
    [Fact]
    public void Observe_ChangedPayload_ResetsStabilityWindow()
    {
        var tracker = new PayloadStabilityTracker(
            minimumObservations: 2,
            minimumStableDuration: TimeSpan.FromMilliseconds(150));
        var startUtc = new DateTime(
            2026,
            4,
            19,
            23,
            0,
            0,
            DateTimeKind.Utc);

        _ = tracker.Observe("payload-a", startUtc);
        _ = tracker.Observe("payload-a", startUtc.AddMilliseconds(200));
        var stable = tracker.Observe(
            "payload-b",
            startUtc.AddMilliseconds(400));

        Assert.False(stable);
    }

    /// <summary>
    ///     Verifies that an explicit reset clears the cached observation.
    /// </summary>
    [Fact]
    public void Reset_ClearsObservedPayload()
    {
        var tracker = new PayloadStabilityTracker(
            minimumObservations: 2,
            minimumStableDuration: TimeSpan.FromMilliseconds(150));
        var startUtc = new DateTime(
            2026,
            4,
            19,
            23,
            0,
            0,
            DateTimeKind.Utc);

        _ = tracker.Observe("payload-a", startUtc);
        tracker.Reset();
        var stable = tracker.Observe(
            "payload-a",
            startUtc.AddMilliseconds(200));

        Assert.False(stable);
    }
}
