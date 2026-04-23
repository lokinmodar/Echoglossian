// <copyright file="DiagnosticTelemetryHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers reusable throttling behavior for investigation telemetry.
/// </summary>
public class DiagnosticTelemetryHelperTests
{
    /// <summary>
    ///     Ensures one repeated identical signature is suppressed within the
    ///     configured cooldown window.
    /// </summary>
    [Fact]
    public void TryBeginEmit_SuppressesRepeatedSignatureWithinCooldown()
    {
        var helper = new DiagnosticTelemetryHelper(
            "CharacterStatus",
            TimeSpan.FromSeconds(2));
        var observedAtUtc = new DateTime(2026, 04, 23, 1, 0, 0, DateTimeKind.Utc);

        var first = helper.TryBeginEmit(
            "mode-switch",
            "sig-a",
            observedAtUtc);
        var second = helper.TryBeginEmit(
            "mode-switch",
            "sig-a",
            observedAtUtc.AddSeconds(1));
        var third = helper.TryBeginEmit(
            "mode-switch",
            "sig-a",
            observedAtUtc.AddSeconds(2));

        Assert.True(first);
        Assert.False(second);
        Assert.True(third);
    }

    /// <summary>
    ///     Ensures one changed signature may emit immediately even within the
    ///     same cooldown window.
    /// </summary>
    [Fact]
    public void TryBeginEmit_AllowsChangedSignatureImmediately()
    {
        var helper = new DiagnosticTelemetryHelper(
            "CharacterStatus",
            TimeSpan.FromSeconds(2));
        var observedAtUtc = new DateTime(2026, 04, 23, 1, 0, 0, DateTimeKind.Utc);

        var first = helper.TryBeginEmit(
            "mode-switch",
            "sig-a",
            observedAtUtc);
        var second = helper.TryBeginEmit(
            "mode-switch",
            "sig-b",
            observedAtUtc.AddMilliseconds(100));

        Assert.True(first);
        Assert.True(second);
    }

    /// <summary>
    ///     Ensures suppression is isolated per category so one noisy branch
    ///     does not block another.
    /// </summary>
    [Fact]
    public void TryBeginEmit_TracksSuppressionPerCategory()
    {
        var helper = new DiagnosticTelemetryHelper(
            "CharacterStatus",
            TimeSpan.FromSeconds(2));
        var observedAtUtc = new DateTime(2026, 04, 23, 1, 0, 0, DateTimeKind.Utc);

        var first = helper.TryBeginEmit(
            "mode-switch",
            "sig-a",
            observedAtUtc);
        var second = helper.TryBeginEmit(
            "hover",
            "sig-a",
            observedAtUtc.AddMilliseconds(100));

        Assert.True(first);
        Assert.True(second);
    }

    /// <summary>
    ///     Ensures reset clears suppression state for one category so a fresh
    ///     investigation pass can emit again immediately.
    /// </summary>
    [Fact]
    public void Reset_ClearsTrackedCategory()
    {
        var helper = new DiagnosticTelemetryHelper(
            "CharacterStatus",
            TimeSpan.FromSeconds(2));
        var observedAtUtc = new DateTime(2026, 04, 23, 1, 0, 0, DateTimeKind.Utc);

        var first = helper.TryBeginEmit(
            "mode-switch",
            "sig-a",
            observedAtUtc);
        helper.Reset("mode-switch");
        var second = helper.TryBeginEmit(
            "mode-switch",
            "sig-a",
            observedAtUtc.AddMilliseconds(100));

        Assert.True(first);
        Assert.True(second);
    }
}
