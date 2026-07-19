// <copyright file="PlayerScopedFrameworkReadinessGateTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the player-scoped framework readiness gate used to avoid running
///     native player-dependent work during login and zone transition races.
/// </summary>
public sealed class PlayerScopedFrameworkReadinessGateTests
{
    /// <summary>
    ///     Ensures a newly valid player state must remain valid for the
    ///     stabilization window before player-scoped prefetch work can run.
    /// </summary>
    [Fact]
    public void IsReady_waits_for_stable_player_state()
    {
        var gate = new PlayerScopedFrameworkReadinessGate(TimeSpan.FromSeconds(2));
        var startedAt = new DateTime(2026, 7, 19, 14, 31, 33, DateTimeKind.Utc);

        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt));
        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(1)));
        Assert.True(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(2).AddMilliseconds(1)));
    }

    /// <summary>
    ///     Ensures the stabilization window is restarted when required player
    ///     state disappears during a transition.
    /// </summary>
    [Fact]
    public void IsReady_resets_when_player_state_disappears()
    {
        var gate = new PlayerScopedFrameworkReadinessGate(TimeSpan.FromSeconds(2));
        var startedAt = new DateTime(2026, 7, 19, 14, 31, 33, DateTimeKind.Utc);

        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt));
        Assert.True(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(3)));

        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 0,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(4)));
        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(5)));
        Assert.True(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(7).AddMilliseconds(1)));
    }

    /// <summary>
    ///     Ensures a class/job change restarts the readiness window because
    ///     action and trait prefetch queues are class/job scoped.
    /// </summary>
    [Fact]
    public void IsReady_resets_when_class_job_changes()
    {
        var gate = new PlayerScopedFrameworkReadinessGate(TimeSpan.FromSeconds(2));
        var startedAt = new DateTime(2026, 7, 19, 14, 31, 33, DateTimeKind.Utc);

        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt));
        Assert.True(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(3)));

        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 38,
            nowUtc: startedAt.AddSeconds(4)));
        Assert.True(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 38,
            nowUtc: startedAt.AddSeconds(6).AddMilliseconds(1)));
    }

    /// <summary>
    ///     Ensures a territory change restarts the readiness window because
    ///     player-scoped native state is rebuilt across zone transitions.
    /// </summary>
    [Fact]
    public void IsReady_resets_when_territory_changes()
    {
        var gate = new PlayerScopedFrameworkReadinessGate(TimeSpan.FromSeconds(2));
        var startedAt = new DateTime(2026, 7, 19, 14, 31, 33, DateTimeKind.Utc);

        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt));
        Assert.True(gate.IsReady(
            isLoggedIn: true,
            territoryType: 144,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(3)));

        Assert.False(gate.IsReady(
            isLoggedIn: true,
            territoryType: 145,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(4)));
        Assert.True(gate.IsReady(
            isLoggedIn: true,
            territoryType: 145,
            hasValidObjectTableLocalPlayer: true,
            currentClassJobId: 37,
            nowUtc: startedAt.AddSeconds(6).AddMilliseconds(1)));
    }
}
