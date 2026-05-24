// <copyright file="AddonProbeAutoWatchHelpers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

namespace Echoglossian;

#if DEBUG
/// <summary>
///     Hosts debug-only automatic addon-probe watches that start on login for
///     selected addons with early hidden-state value.
/// </summary>
public partial class Echoglossian
{
  private static readonly TimeSpan DefaultAutoAddonProbeDuration =
      TimeSpan.FromMinutes(15);

  private static readonly (string AddonName, int Index)[] DefaultAutoAddonProbeTargets =
  [
      ("_BattleTalk", 0),
      ("_MiniTalk", 0),
  ];

  /// <summary>
  ///     Ticks manual and automatic addon-probe watches, including the
  ///     debug-only login bootstrap for the default watch set.
  /// </summary>
  private void TickAddonProbeInfrastructure()
  {
    this.TickManualAddonProbeWatch();
    this.TickManagedAddonProbeWatches();
    this.TickAutoAddonProbeWatches();
  }

  /// <summary>
  ///     Gets how many addon-probe watches are currently active across manual
  ///     and managed watch sets.
  /// </summary>
  /// <returns>The active probe-watch count.</returns>
  private int GetActiveAddonProbeWatchCount()
  {
    return (this.addonProbeWatch is null ? 0 : 1) +
           this.addonManagedProbeWatches.Count;
  }

  /// <summary>
  ///     Stops every active addon-probe watch known to the plugin.
  /// </summary>
  /// <param name="suppressAutoUntilLogout">
  ///     Whether the current login session should suppress the default
  ///     automatic watch set after the stop completes.
  /// </param>
  private void StopAllAddonProbeWatches(bool suppressAutoUntilLogout = false)
  {
    this.StopManualAddonProbeWatch();
    this.StopManagedAddonProbeWatches();

    if (suppressAutoUntilLogout)
    {
      this.autoAddonProbeSuppressedUntilLogout = true;
      this.autoAddonProbeStartedForCurrentLogin = true;
    }
  }

  /// <summary>
  ///     Ticks the currently active manual addon-probe watch, if any.
  /// </summary>
  private void TickManualAddonProbeWatch()
  {
    this.addonProbeWatch?.Tick();
    if (this.addonProbeWatch?.IsDisposed == true)
    {
      this.addonProbeWatch = null;
    }
  }

  /// <summary>
  ///     Ticks every managed addon-probe watch and prunes the disposed ones.
  /// </summary>
  private void TickManagedAddonProbeWatches()
  {
    for (var index = this.addonManagedProbeWatches.Count - 1; index >= 0; index--)
    {
      var watch = this.addonManagedProbeWatches[index];
      watch.Tick();
      if (watch.IsDisposed)
      {
        this.addonManagedProbeWatches.RemoveAt(index);
      }
    }
  }

  /// <summary>
  ///     Starts the default automatic addon-probe watch set when the player
  ///     logs in, then resets the gate on logout.
  /// </summary>
  private void TickAutoAddonProbeWatches()
  {
    var isLoggedIn = ClientStateInterface.IsLoggedIn;
    if (AddonProbeAutoWatchPolicy.ShouldResetForLogout(
            this.autoAddonProbeWasLoggedIn,
            isLoggedIn))
    {
      this.StopManagedAddonProbeWatches();
      this.autoAddonProbeStartedForCurrentLogin = false;
      this.autoAddonProbeSuppressedUntilLogout = false;
    }

    this.autoAddonProbeWasLoggedIn = isLoggedIn;

    if (!AddonProbeAutoWatchPolicy.ShouldStartForCurrentLogin(
            isLoggedIn,
            this.autoAddonProbeStartedForCurrentLogin,
            this.autoAddonProbeSuppressedUntilLogout,
            this.addonManagedProbeWatches.Count))
    {
      return;
    }

    this.StartDefaultAutoAddonProbeWatches();
    this.autoAddonProbeStartedForCurrentLogin = true;
  }

  /// <summary>
  ///     Starts the default automatic watch set used to capture early
  ///     structural state for pooled dialogue addons.
  /// </summary>
  private void StartDefaultAutoAddonProbeWatches()
  {
    foreach (var (addonName, index) in DefaultAutoAddonProbeTargets)
    {
      this.StartManagedAddonProbeWatch(
          addonName,
          index,
          DefaultAutoAddonProbeDuration,
          "login-auto-start");
    }
  }

  /// <summary>
  ///     Starts one managed addon-probe watch and tracks it for later stop or
  ///     pruning.
  /// </summary>
  /// <param name="addonName">The addon name to watch.</param>
  /// <param name="index">The addon instance index.</param>
  /// <param name="duration">How long the watch should stay alive.</param>
  /// <param name="reason">The debug log reason attached to the startup.</param>
  private void StartManagedAddonProbeWatch(
      string addonName,
      int index,
      TimeSpan duration,
      string reason)
  {
    var watch = AddonStructureProbe.StartWatch(
        GameGuiInterface,
        PluginLog,
        addonName,
        index,
        duration);
    this.addonManagedProbeWatches.Add(watch);

    PluginRuntimeLog.Information(
        $"[AddonProbe] started managed watch addon='{addonName}' index={index} reason='{reason}' duration={(int)duration.TotalSeconds}s");
  }

  /// <summary>
  ///     Stops the current manual addon-probe watch, if any.
  /// </summary>
  private void StopManualAddonProbeWatch()
  {
    this.addonProbeWatch?.Stop();
    this.addonProbeWatch = null;
  }

  /// <summary>
  ///     Stops every managed automatic addon-probe watch.
  /// </summary>
  private void StopManagedAddonProbeWatches()
  {
    foreach (var watch in this.addonManagedProbeWatches)
    {
      watch.Stop();
    }

    this.addonManagedProbeWatches.Clear();
  }
}
#endif
