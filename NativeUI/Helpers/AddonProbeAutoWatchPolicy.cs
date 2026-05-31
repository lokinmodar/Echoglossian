// <copyright file="AddonProbeAutoWatchPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

#if DEBUG
namespace Echoglossian.NativeUI.Helpers;

/// <summary>
///     Encapsulates the login-session gating rules for debug-only automatic
///     addon-probe watches.
/// </summary>
internal static class AddonProbeAutoWatchPolicy
{
  /// <summary>
  ///     Determines whether the current login session should start the default
  ///     automatic addon-probe watch set.
  /// </summary>
  /// <param name="isLoggedIn">Whether the client is currently logged in.</param>
  /// <param name="hasStartedForCurrentLogin">
  ///     Whether the current login session already started its automatic probe
  ///     set.
  /// </param>
  /// <param name="isSuppressedUntilLogout">
  ///     Whether the user explicitly suppressed the automatic probe set until
  ///     the next logout.
  /// </param>
  /// <param name="activeManagedWatchCount">
  ///     How many managed automatic probe watches are currently alive.
  /// </param>
  /// <returns>
  ///     <see langword="true" /> when the automatic probe set should start.
  /// </returns>
  public static bool ShouldStartForCurrentLogin(
      bool isLoggedIn,
      bool hasStartedForCurrentLogin,
      bool isSuppressedUntilLogout,
      int activeManagedWatchCount)
  {
    return isLoggedIn &&
           !hasStartedForCurrentLogin &&
           !isSuppressedUntilLogout &&
           activeManagedWatchCount <= 0;
  }

  /// <summary>
  ///     Determines whether the automatic login-session gate should reset
  ///     because the player logged out.
  /// </summary>
  /// <param name="wasLoggedIn">
  ///     Whether the client was logged in on the previous tick.
  /// </param>
  /// <param name="isLoggedIn">Whether the client is logged in now.</param>
  /// <returns>
  ///     <see langword="true" /> when the automatic probe gate should reset.
  /// </returns>
  public static bool ShouldResetForLogout(bool wasLoggedIn, bool isLoggedIn)
  {
    return wasLoggedIn && !isLoggedIn;
  }
}
#endif
