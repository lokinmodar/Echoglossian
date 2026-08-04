// <copyright file="FrameworkAccessGuard.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Echoglossian;

/// <summary>
/// Provides safe access guards for native framework-backed singleton entry points
/// that are only valid after the client is fully logged into the game world.
/// </summary>
internal static unsafe class FrameworkAccessGuard
{
  private static readonly PlayerScopedFrameworkReadinessGate
      PlayerScopedReadinessGate = new(TimeSpan.FromSeconds(10));

  /// <summary>
  /// Determines whether the current client session is ready for native
  /// framework-backed singleton access.
  /// </summary>
  /// <returns>
  /// <see langword="true" /> when the client is logged into the game;
  /// otherwise, <see langword="false" />.
  /// </returns>
  public static bool IsClientReadyForFrameworkAccess()
  {
    return Echoglossian.ClientStateInterface != null &&
           Echoglossian.ClientStateInterface.IsLoggedIn;
  }

  /// <summary>
  /// Determines whether player-scoped native runtime work can safely touch
  /// game state that may lag behind the login flag during zone transitions.
  /// </summary>
  /// <returns>
  /// <see langword="true" /> when the client is logged in and player state has
  /// a current class/job id; otherwise, <see langword="false" />.
  /// </returns>
  public static bool IsClientReadyForPlayerScopedFrameworkAccess()
  {
    if (!IsClientReadyForFrameworkAccess())
    {
      PlayerScopedReadinessGate.Reset();
      return false;
    }

    try
    {
      var playerState = PlayerState.Instance();
      var localPlayer = Echoglossian.ObjectTableInterface?.LocalPlayer;
      return PlayerScopedReadinessGate.IsReady(
          Echoglossian.ClientStateInterface.IsLoggedIn,
          Echoglossian.ClientStateInterface.TerritoryType,
          localPlayer?.IsValid() == true,
          playerState != null ? playerState->CurrentClassJobId : (byte)0,
          DateTime.UtcNow);
    }
    catch (InvalidOperationException)
    {
      PlayerScopedReadinessGate.Reset();
      return false;
    }
  }

  /// <summary>
  /// Tries to resolve the live <see cref="RaptureAtkUnitManager"/> instance
  /// without throwing during pre-login or transition states.
  /// </summary>
  /// <param name="manager">Receives the live manager pointer when available.</param>
  /// <returns>
  /// <see langword="true" /> when the manager is available; otherwise,
  /// <see langword="false" />.
  /// </returns>
  public static bool TryGetRaptureAtkUnitManager(out RaptureAtkUnitManager* manager)
  {
    manager = null;
    if (!IsClientReadyForFrameworkAccess())
    {
      return false;
    }

    try
    {
      manager = RaptureAtkUnitManager.Instance();
      return manager != null;
    }
    catch (InvalidOperationException)
    {
      return false;
    }
  }

  /// <summary>
  /// Tries to resolve the live <see cref="EventFramework"/> instance without
  /// throwing during pre-login or transition states.
  /// </summary>
  /// <param name="eventFramework">
  /// Receives the live framework pointer when available.
  /// </param>
  /// <returns>
  /// <see langword="true" /> when the framework is available; otherwise,
  /// <see langword="false" />.
  /// </returns>
  public static bool TryGetEventFramework(out EventFramework* eventFramework)
  {
    eventFramework = null;
    if (!IsClientReadyForFrameworkAccess())
    {
      return false;
    }

    try
    {
      eventFramework = EventFramework.Instance();
      return eventFramework != null;
    }
    catch (InvalidOperationException)
    {
      return false;
    }
  }
}
