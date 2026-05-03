// <copyright file="AddonLifecycleExtensions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers
{
  /// <summary>
  /// Provides extension methods for registering and removing logging listeners for key lifecycle events of add-ons
  /// using an add-on lifecycle manager.
  /// </summary>
  /// <remarks>These extension methods enable or disable logging for important phases in an add-on's lifecycle,
  /// such as setup, refresh, event reception, requested updates, and finalization. They are useful for monitoring and
  /// debugging add-on behavior without interfering with other event listeners. When debugging addon behavior, prefer
  /// this helper together with the addon structure probe instead of adding ad hoc logging inside feature handlers.</remarks>
public static class AddonLifecycleExtensions
{
    private static readonly AddonEvent[] DefaultLoggedEvents =
    [
      AddonEvent.PreSetup,
      AddonEvent.PreUpdate,
      AddonEvent.PreDraw,
      AddonEvent.PreFinalize,
      AddonEvent.PreRequestedUpdate,
      AddonEvent.PreRefresh,
      AddonEvent.PreReceiveEvent,
      AddonEvent.PreOpen,
      AddonEvent.PreClose,
      AddonEvent.PreShow,
      AddonEvent.PreHide,
      AddonEvent.PreMove,
      AddonEvent.PreMouseOver,
      AddonEvent.PreMouseOut,
      AddonEvent.PreFocus,
      AddonEvent.PostSetup,
      AddonEvent.PostUpdate,
      AddonEvent.PostDraw,
      AddonEvent.PostRequestedUpdate,
      AddonEvent.PostRefresh,
      AddonEvent.PostReceiveEvent,
      AddonEvent.PostOpen,
      AddonEvent.PostClose,
      AddonEvent.PostShow,
      AddonEvent.PostHide,
      AddonEvent.PostMove,
      AddonEvent.PostMouseOver,
      AddonEvent.PostMouseOut,
      AddonEvent.PostFocus,
    ];

    /// <summary>
    /// Registers a logger as a listener for key lifecycle events of the specified addon.
    /// </summary>
    /// <remarks>This method attaches a logger to several important lifecycle events, enabling logging for
    /// setup, refresh, event reception, requested updates, and finalization phases of the addon. This can assist in
    /// monitoring and debugging addon behavior throughout its lifecycle.</remarks>
    /// <param name="addonLifecycle">The addon lifecycle manager used to register event listeners.</param>
    /// <param name="addonName">The name of the addon for which lifecycle event listeners are registered.</param>
    /// <param name="events">Optional lifecycle event set to log. Defaults to the full event set.</param>
    public static void LogAddon(
        this IAddonLifecycle addonLifecycle,
        string addonName,
        IReadOnlyCollection<AddonEvent>? events = null)
    {
      foreach (var evt in events ?? DefaultLoggedEvents)
      {
        addonLifecycle.RegisterListener(evt, addonName, Logger);
      }
    }

    /// <summary>
    /// Writes a debug log entry for the specified add-on event and its associated arguments.
    /// </summary>
    /// <param name="type">The event type that triggered the logging operation.</param>
    /// <param name="args">The arguments containing details about the add-on, including its name and context.</param>
    private static void Logger(AddonEvent type, AddonArgs args)
        => PluginRuntimeLog.Debug($"{args.AddonName} called {type}");

    /// <summary>
    /// Removes logging event listeners for the specified add-on from the provided add-on lifecycle instance.
    /// </summary>
    /// <remarks>This method unregisters logging listeners for several key lifecycle events, ensuring that log
    /// output related to the specified add-on is no longer generated. Use this method when you want to stop logging
    /// activity for an add-on without affecting its other event listeners.</remarks>
    /// <param name="addonLifecycle">The add-on lifecycle instance from which logging event listeners will be removed. Cannot be null.</param>
    /// <param name="addonName">The name of the add-on whose logging event listeners are to be removed. Cannot be null or empty.</param>
    /// <param name="events">Optional lifecycle event set to unlog. Defaults to the full event set.</param>
    public static void UnLogAddon(
        this IAddonLifecycle addonLifecycle,
        string addonName,
        IReadOnlyCollection<AddonEvent>? events = null)
    {
      foreach (var evt in events ?? DefaultLoggedEvents)
      {
        addonLifecycle.UnregisterListener(evt, addonName, Logger);
      }
    }
  }
}


