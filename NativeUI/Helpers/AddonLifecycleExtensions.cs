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
  /// debugging add-on behavior without interfering with other event listeners.</remarks>
  public static class AddonLifecycleExtensions
  {
    /// <summary>
    /// Registers a logger as a listener for key lifecycle events of the specified addon.
    /// </summary>
    /// <remarks>This method attaches a logger to several important lifecycle events, enabling logging for
    /// setup, refresh, event reception, requested updates, and finalization phases of the addon. This can assist in
    /// monitoring and debugging addon behavior throughout its lifecycle.</remarks>
    /// <param name="addonLifecycle">The addon lifecycle manager used to register event listeners.</param>
    /// <param name="addonName">The name of the addon for which lifecycle event listeners are registered.</param>
    public static void LogAddon(this IAddonLifecycle addonLifecycle, string addonName)
    {


       addonLifecycle.RegisterListener(AddonEvent.PreSetup, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreUpdate, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreDraw, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreFinalize, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreRefresh, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreOpen, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreClose, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreShow, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreHide, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreMove, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreMouseOver, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreMouseOut, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PreFocus, addonName, Logger);

      addonLifecycle.RegisterListener(AddonEvent.PostSetup, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostUpdate, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostDraw, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostRefresh, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostOpen, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostClose, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostShow, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostHide, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostMove, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostMouseOver, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostMouseOut, addonName, Logger);
      addonLifecycle.RegisterListener(AddonEvent.PostFocus, addonName, Logger);


    }

    /// <summary>
    /// Writes a debug log entry for the specified add-on event and its associated arguments.
    /// </summary>
    /// <param name="type">The event type that triggered the logging operation.</param>
    /// <param name="args">The arguments containing details about the add-on, including its name and context.</param>
    private static void Logger(AddonEvent type, AddonArgs args)
        => PluginLog.Debug($"{args.AddonName} called {type}");

    /// <summary>
    /// Removes logging event listeners for the specified add-on from the provided add-on lifecycle instance.
    /// </summary>
    /// <remarks>This method unregisters logging listeners for several key lifecycle events, ensuring that log
    /// output related to the specified add-on is no longer generated. Use this method when you want to stop logging
    /// activity for an add-on without affecting its other event listeners.</remarks>
    /// <param name="addonLifecycle">The add-on lifecycle instance from which logging event listeners will be removed. Cannot be null.</param>
    /// <param name="addonName">The name of the add-on whose logging event listeners are to be removed. Cannot be null or empty.</param>
    public static void UnLogAddon(this IAddonLifecycle addonLifecycle, string addonName)
    {
      addonLifecycle.UnregisterListener(AddonEvent.PreSetup, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreUpdate, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreDraw, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreRefresh, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreOpen, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreClose, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreShow, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreHide, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreMove, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreMouseOver, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreMouseOut, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PreFocus, addonName, Logger);

      addonLifecycle.UnregisterListener(AddonEvent.PostSetup, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostDraw, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostReceiveEvent, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostOpen, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostClose, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostShow, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostHide, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostMove, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostMouseOver, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostMouseOut, addonName, Logger);
      addonLifecycle.UnregisterListener(AddonEvent.PostFocus, addonName, Logger);

    }
  }
}
