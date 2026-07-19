// <copyright file="PlayerScopedFrameworkReadinessGate.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian;

/// <summary>
///     Requires player-scoped framework prerequisites to remain stable before
///     native player-dependent work is allowed to run.
/// </summary>
internal sealed class PlayerScopedFrameworkReadinessGate
{
  private readonly TimeSpan stabilizationWindow;

  private DateTime readySinceUtc = DateTime.MinValue;

  private byte stableClassJobId;

  /// <summary>
  ///     Initializes a new instance of the
  ///     <see cref="PlayerScopedFrameworkReadinessGate" /> class.
  /// </summary>
  /// <param name="stabilizationWindow">
  ///     The continuous ready interval required before access is allowed.
  /// </param>
  public PlayerScopedFrameworkReadinessGate(TimeSpan stabilizationWindow)
  {
    this.stabilizationWindow = stabilizationWindow;
  }

  /// <summary>
  ///     Gets whether the current player-scoped framework state is ready and
  ///     has remained stable long enough for background prefetch runtimes.
  /// </summary>
  /// <param name="isLoggedIn">Whether Dalamud reports an active login.</param>
  /// <param name="hasTerritory">
  ///     Whether <c>IClientState.TerritoryType</c> has been populated.
  /// </param>
  /// <param name="hasObjectTableLocalPlayer">
  ///     Whether <c>IObjectTable.LocalPlayer</c> is available.
  /// </param>
  /// <param name="currentClassJobId">The current player class/job id.</param>
  /// <param name="nowUtc">The current UTC timestamp.</param>
  /// <returns><see langword="true" /> when access is stable.</returns>
  public bool IsReady(
      bool isLoggedIn,
      bool hasTerritory,
      bool hasObjectTableLocalPlayer,
      byte currentClassJobId,
      DateTime nowUtc)
  {
    if (!isLoggedIn ||
        !hasTerritory ||
        !hasObjectTableLocalPlayer ||
        currentClassJobId == 0)
    {
      this.Reset();
      return false;
    }

    if (this.readySinceUtc == DateTime.MinValue ||
        this.stableClassJobId != currentClassJobId)
    {
      this.readySinceUtc = nowUtc;
      this.stableClassJobId = currentClassJobId;
      return this.stabilizationWindow <= TimeSpan.Zero;
    }

    return nowUtc - this.readySinceUtc >= this.stabilizationWindow;
  }

  /// <summary>
  ///     Clears the tracked stable-ready interval.
  /// </summary>
  public void Reset()
  {
    this.readySinceUtc = DateTime.MinValue;
    this.stableClassJobId = 0;
  }
}
