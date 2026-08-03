// <copyright file="DbFirstPreDrawRefreshPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.Common;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the DB-first pre-draw short-circuit policy.
/// </summary>
public class DbFirstPreDrawRefreshPolicyTests
{
    /// <summary>
    /// Ensures one pending post-translation refresh bypasses the normal
    /// short-circuit even when the current display mode is unchanged.
    /// </summary>
    [Fact]
    public void ShouldShortCircuit_PendingRefreshRequested_ReturnsFalse()
    {
        var shouldShortCircuit = DbFirstPreDrawRefreshPolicy.ShouldShortCircuit(
            sameDisplayMode: true,
            shouldContinueAppliedStateRefresh: false,
            refreshRequested: true,
            hasRuntimeState: true,
            usesHoverTooltips: false,
            hasLastResolvedState: true,
            hasVisibleAddon: true);

        Assert.False(shouldShortCircuit);
    }

    /// <summary>
    /// Ensures the current runtime can still short-circuit once no pending
    /// refresh is waiting and the display mode is stable.
    /// </summary>
    [Fact]
    public void ShouldShortCircuit_RuntimeAlreadyApplied_ReturnsTrue()
    {
        var shouldShortCircuit = DbFirstPreDrawRefreshPolicy.ShouldShortCircuit(
            sameDisplayMode: true,
            shouldContinueAppliedStateRefresh: false,
            refreshRequested: false,
            hasRuntimeState: true,
            usesHoverTooltips: false,
            hasLastResolvedState: true,
            hasVisibleAddon: true);

        Assert.True(shouldShortCircuit);
    }

    /// <summary>
    /// Ensures a hidden addon cannot retain hover targets solely because its
    /// previous payload is still resolved in memory.
    /// </summary>
    [Fact]
    public void ShouldShortCircuit_HiddenHoverAddon_ReturnsFalse()
    {
        var shouldShortCircuit = DbFirstPreDrawRefreshPolicy.ShouldShortCircuit(
            sameDisplayMode: true,
            shouldContinueAppliedStateRefresh: false,
            refreshRequested: false,
            hasRuntimeState: true,
            usesHoverTooltips: true,
            hasLastResolvedState: true,
            requiresHoverTooltipLifetimeRefresh: true,
            hasVisibleAddon: false);

        Assert.False(shouldShortCircuit);
    }

    /// <summary>
    /// Ensures custom hover-fallback modes also refuse to short-circuit when
    /// the owning addon is no longer visible.
    /// </summary>
    [Fact]
    public void ShouldShortCircuit_HiddenCustomHoverFallbackAddon_ReturnsFalse()
    {
        var method = typeof(DbFirstPreDrawRefreshPolicy).GetMethod(
            nameof(DbFirstPreDrawRefreshPolicy.ShouldShortCircuit),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(8, method.GetParameters().Length);

        var shouldShortCircuit = Assert.IsType<bool>(method.Invoke(
            null,
            [true, false, false, true, false, false, true, false]));

        Assert.False(shouldShortCircuit);
    }
}
