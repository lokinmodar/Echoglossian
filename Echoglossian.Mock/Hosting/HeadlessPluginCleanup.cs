// <copyright file="HeadlessPluginCleanup.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;

namespace Echoglossian.Mock.Hosting;

/// <summary>
/// Applies the headless unload preparation required before disposing the
/// production plugin under DalaMock without a live native UI.
/// </summary>
public static class HeadlessPluginCleanup
{
    /// <summary>
    /// Replaces the registered addon-handler list with an empty instance so the
    /// headless shutdown rail can validate plugin-level disposal without native
    /// UI restoration that requires a live AtkStage.
    /// </summary>
    /// <param name="plugin">The started production plugin.</param>
    /// <exception cref="InvalidOperationException">Thrown when the registered addon-handler field cannot be located or instantiated.</exception>
    public static void PrepareForHeadlessDispose(global::Echoglossian.Echoglossian plugin)
    {
        var field = typeof(global::Echoglossian.Echoglossian).GetField(
            "registeredAddonHandlers",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field is null)
        {
            throw new InvalidOperationException("Unable to locate Echoglossian.registeredAddonHandlers for headless dispose preparation.");
        }

        var emptyHandlers = Activator.CreateInstance(field.FieldType);
        if (emptyHandlers is null)
        {
            throw new InvalidOperationException("Unable to create an empty registeredAddonHandlers list for headless dispose preparation.");
        }

        field.SetValue(plugin, emptyHandlers);
    }
}
