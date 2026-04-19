// <copyright file="IPluginUnloadAwareAddonHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Handlers;

/// <summary>
///     Provides a plugin-unload cleanup hook for addon handlers that mutate or
///     restore native UI state outside the normal addon hide/finalize path.
/// </summary>
public interface IPluginUnloadAwareAddonHandler
{
    /// <summary>
    ///     Performs best-effort cleanup before the plugin unregisters addon
    ///     lifecycle listeners and clears shared caches.
    /// </summary>
    void OnPluginUnload();
}
