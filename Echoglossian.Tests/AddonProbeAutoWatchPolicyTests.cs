// <copyright file="AddonProbeAutoWatchPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the debug-only login gating for automatic addon-probe watches.
/// </summary>
public class AddonProbeAutoWatchPolicyTests
{
#if DEBUG
    /// <summary>
    ///     Ensures the login auto-probe starts only when the client is logged
    ///     in and the current login session has not already started or
    ///     suppressed the watch set.
    /// </summary>
    [Fact]
    public void ShouldStartForCurrentLogin_ReturnsTrue_WhenLoginSessionIsEligible()
    {
        var shouldStart = AddonProbeAutoWatchPolicy.ShouldStartForCurrentLogin(
            isLoggedIn: true,
            hasStartedForCurrentLogin: false,
            isSuppressedUntilLogout: false,
            activeManagedWatchCount: 0);

        Assert.True(shouldStart);
    }

    /// <summary>
    ///     Ensures the login auto-probe stays off when the current login session
    ///     already started or the user suppressed it with the stop command.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, 0)]
    [InlineData(true, true, false, 0)]
    [InlineData(true, false, true, 0)]
    [InlineData(true, false, false, 1)]
    public void ShouldStartForCurrentLogin_ReturnsFalse_WhenSessionIsNotEligible(
        bool isLoggedIn,
        bool hasStartedForCurrentLogin,
        bool isSuppressedUntilLogout,
        int activeManagedWatchCount)
    {
        var shouldStart = AddonProbeAutoWatchPolicy.ShouldStartForCurrentLogin(
            isLoggedIn,
            hasStartedForCurrentLogin,
            isSuppressedUntilLogout,
            activeManagedWatchCount);

        Assert.False(shouldStart);
    }

    /// <summary>
    ///     Ensures a logout transition resets the one-login-session auto-probe
    ///     gate.
    /// </summary>
    [Fact]
    public void ShouldResetForLogout_ReturnsTrue_OnLogoutTransition()
    {
        var shouldReset = AddonProbeAutoWatchPolicy.ShouldResetForLogout(
            wasLoggedIn: true,
            isLoggedIn: false);

        Assert.True(shouldReset);
    }
#endif
}
