// <copyright file="AddonHandlerRegistrar.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

using System.Runtime.CompilerServices;

/// <summary>
///     Utility for registering and unregistering addon translation handlers.
/// </summary>
public static class AddonHandlerRegistrar
{
    private static readonly ConditionalWeakTable<
        IAddonTranslationHandler,
        Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>>
        StableHandlerDelegates = new();

    /// <summary>
    ///     Registers an addon translation handler with the specified addon lifecycle.
    /// </summary>
    /// <param name="addonName">The name of the addon to register the handler for.</param>
    /// <param name="handler">The handler responsible for addon translation.</param>
    /// <param name="addonLifecycle">The lifecycle manager for the addon.</param>
    public static void Register(
        string addonName,
        IAddonTranslationHandler handler,
        IAddonLifecycle addonLifecycle)
    {
        foreach (var (evt, del) in GetStableEventHandlers(handler))
        {
            addonLifecycle.RegisterListener(evt, new[] { addonName }, del);
        }
    }

    /// <summary>
    ///     Registers multiple addon translation handlers with the specified addon
    ///     lifecycle.
    /// </summary>
    /// <param name="handlers">
    ///     A collection of addon names and their corresponding
    ///     handlers.
    /// </param>
    /// <param name="addonLifecycle">The lifecycle manager for the addons.</param>
    public static void RegisterMany(
        IEnumerable<(string AddonName, IAddonTranslationHandler Handler)>
            handlers,
        IAddonLifecycle addonLifecycle)
    {
        foreach (var (addonName, handler) in handlers)
        {
            Register(addonName, handler, addonLifecycle);
        }
    }

    /// <summary>
    ///     Unregisters an addon translation handler from the specified addon
    ///     lifecycle.
    /// </summary>
    /// <param name="addonName">The name of the addon to unregister the handler for.</param>
    /// <param name="handler">The handler responsible for addon translation.</param>
    /// <param name="addonLifecycle">The lifecycle manager for the addon.</param>
    public static void Unregister(
        string addonName,
        IAddonTranslationHandler handler,
        IAddonLifecycle addonLifecycle)
    {
        if (!StableHandlerDelegates.TryGetValue(handler, out var eventHandlers))
        {
            return;
        }

        foreach (var (evt, del) in eventHandlers)
        {
            addonLifecycle.UnregisterListener(evt, new[] { addonName }, del);
        }
    }

    /// <summary>
    ///     Unregisters multiple addon translation handlers from the specified addon
    ///     lifecycle.
    /// </summary>
    /// <param name="handlers">
    ///     A collection of addon names and their corresponding
    ///     handlers.
    /// </param>
    /// <param name="addonLifecycle">The lifecycle manager for the addons.</param>
    public static void UnregisterMany(
        IEnumerable<(string AddonName, IAddonTranslationHandler Handler)>
            handlers,
        IAddonLifecycle addonLifecycle)
    {
        foreach (var (addonName, handler) in handlers)
        {
            Unregister(addonName, handler, addonLifecycle);
        }
    }

    /// <summary>
    ///     Returns one stable event-handler map for the lifetime of the supplied
    ///     addon handler instance so register and unregister use the same
    ///     delegate instances.
    /// </summary>
    /// <param name="handler">The addon handler instance.</param>
    /// <returns>The stable event-handler map for that handler instance.</returns>
    private static Dictionary<AddonEvent, IAddonLifecycle.AddonEventDelegate>
        GetStableEventHandlers(IAddonTranslationHandler handler)
    {
        return StableHandlerDelegates.GetValue(
            handler,
            static addonHandler => addonHandler.GetEventHandlers());
    }
}
