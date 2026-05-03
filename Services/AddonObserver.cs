// <copyright file="AddonObserver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

/// <summary>
/// Observes the visibility state of UI addons and triggers events when they are opened or closed.
/// Used under permission. Original code from <see href="https://github.com/Haselnussbomber/HaselCommon/blob/main/HaselCommon/Services/AddonObserver.cs" />
/// </summary>
namespace Echoglossian.Services;

[RegisterSingleton, AutoConstruct]
public unsafe partial class AddonObserver : IDisposable
{
  private readonly IFramework _framework;

  private readonly HashSet<Pointer<AtkUnitBase>> _visibleUnits = new(256);
  private readonly HashSet<Pointer<AtkUnitBase>> _removedUnits = new(16);
  private readonly Dictionary<Pointer<AtkUnitBase>, string> _nameCache = new(256);

  /// <summary>
  ///  Delegate for addon open/close events.
  /// </summary>
  /// <param name="addonName">Name of the addon.</param>
  public delegate void CallbackDelegate(string addonName);

  /// <summary>
  /// Delegate for addon open events.
  /// </summary>
  public event CallbackDelegate? AddonOpen;

  /// <summary>
  /// Delegate for addon close events.
  /// </summary>
  public event CallbackDelegate? AddonClose;

  /// <summary>
  /// Initializes the AddonObserver service and subscribes to framework updates.
  /// </summary>
  [AutoPostConstruct]
  private void Initialize()
  {
    this._framework.Update += this.OnFrameworkUpdate;
  }

  /// <summary>
  ///  Disposes the AddonObserver service and unsubscribes from framework updates.
  /// </summary>
  public void Dispose()
  {
    this._framework.Update -= this.OnFrameworkUpdate;
  }

  /// <summary>
  /// Checks if an addon is currently visible.
  /// </summary>
  /// <param name="name">Name of the addon.</param>
  /// <returns>True if the addon is visible, otherwise false.</returns>
  public bool IsAddonVisible(string name)
      => this._nameCache.ContainsValue(name);

  /// <summary>
  /// Event handler for framework updates.
  /// </summary>
  /// <param name="framework">The framework instance.</param>
  private void OnFrameworkUpdate(IFramework framework)
  {
    this._visibleUnits.Clear();

    if (!FrameworkAccessGuard.TryGetRaptureAtkUnitManager(out var manager))
    {
      var closedAddonNames = this._nameCache.Values.ToArray();
      this._removedUnits.Clear();
      this._nameCache.Clear();

      foreach (var addonName in closedAddonNames)
      {
        this.AddonClose?.Invoke(addonName);
      }

      return;
    }

    foreach (var atkUnitBase in manager->AllLoadedUnitsList.Entries)
    {
      if (atkUnitBase.Value != null && atkUnitBase.Value->IsReady && atkUnitBase.Value->IsVisible)
      {
        this._visibleUnits.Add(atkUnitBase);
      }
    }

    this._removedUnits.Clear();

    foreach (var (address, name) in this._nameCache)
    {
      if (!this._visibleUnits.Contains(address) && this._removedUnits.Add(address))
      {
        this._nameCache.Remove(address);
        this.AddonClose?.Invoke(name);
      }
    }

    foreach (var address in this._visibleUnits)
    {
      if (this._nameCache.ContainsKey(address))
      {
        continue;
      }

      var name = address.Value->NameString;
      this._nameCache.Add(address, name);
      this.AddonOpen?.Invoke(name);
    }
  }
}


