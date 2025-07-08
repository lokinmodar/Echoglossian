// <copyright file="AddonHandlerRegistrar.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Plugin.Services;
using Echoglossian.NativeUI.Handlers;

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Utility for registering and unregistering addon translation handlers.
/// </summary>
public static class AddonHandlerRegistrar
{
  /// <summary>
  /// Registers an addon translation handler with the specified addon lifecycle.
  /// </summary>
  /// <param name="addonName">The name of the addon to register the handler for.</param>
  /// <param name="handler">The handler responsible for addon translation.</param>
  /// <param name="addonLifecycle">The lifecycle manager for the addon.</param>
  public static void Register(string addonName, IAddonTranslationHandler handler, IAddonLifecycle addonLifecycle)
  {
    foreach (var (evt, del) in handler.GetEventHandlers())
    {
      addonLifecycle.RegisterListener(evt, new[] { addonName }, del);
    }
  }

  /// <summary>
  /// Registers multiple addon translation handlers with the specified addon lifecycle.
  /// </summary>
  /// <param name="handlers">A collection of addon names and their corresponding handlers.</param>
  /// <param name="addonLifecycle">The lifecycle manager for the addons.</param>
  public static void RegisterMany(IEnumerable<(string AddonName, IAddonTranslationHandler Handler)> handlers, IAddonLifecycle addonLifecycle)
  {
    foreach (var (addonName, handler) in handlers)
    {
      Register(addonName, handler, addonLifecycle);
    }
  }

  /// <summary>
  /// Unregisters an addon translation handler from the specified addon lifecycle.
  /// </summary>
  /// <param name="addonName">The name of the addon to unregister the handler for.</param>
  /// <param name="handler">The handler responsible for addon translation.</param>
  /// <param name="addonLifecycle">The lifecycle manager for the addon.</param>
  public static void Unregister(string addonName, IAddonTranslationHandler handler, IAddonLifecycle addonLifecycle)
  {
    foreach (var (evt, del) in handler.GetEventHandlers())
    {
      addonLifecycle.UnregisterListener(evt, new[] { addonName }, del);
    }
  }

  /// <summary>
  /// Unregisters multiple addon translation handlers from the specified addon lifecycle.
  /// </summary>
  /// <param name="handlers">A collection of addon names and their corresponding handlers.</param>
  /// <param name="addonLifecycle">The lifecycle manager for the addons.</param>
  public static void UnregisterMany(IEnumerable<(string AddonName, IAddonTranslationHandler Handler)> handlers, IAddonLifecycle addonLifecycle)
  {
    foreach (var (addonName, handler) in handlers)
    {
      Unregister(addonName, handler, addonLifecycle);
    }
  }
}
