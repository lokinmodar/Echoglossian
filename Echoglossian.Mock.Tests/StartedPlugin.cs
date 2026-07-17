// <copyright file="StartedPlugin.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Plugin;
using Echoglossian.PluginRuntime.Startup;
using System;
using System.IO;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Represents a started DalaMock plugin instance, its owning mock container, and
/// the isolated local state created for that startup run.
/// </summary>
internal sealed class StartedPlugin : IDisposable
{
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartedPlugin"/> class.
    /// </summary>
    /// <param name="container">The owning DalaMock container.</param>
    /// <param name="plugin">The started production plugin instance.</param>
    /// <param name="stateRoot">The isolated local state root used for this startup run.</param>
    public StartedPlugin(
        MockContainer container,
        global::Echoglossian.Echoglossian plugin,
        DirectoryInfo stateRoot)
    {
        this.Container = container;
        this.Plugin = plugin;
        this.StateRoot = stateRoot;
    }

    /// <summary>
    /// Gets the owning DalaMock container.
    /// </summary>
    public MockContainer Container { get; }

    /// <summary>
    /// Gets the started production plugin instance.
    /// </summary>
    public global::Echoglossian.Echoglossian Plugin { get; }

    /// <summary>
    /// Gets the isolated local state root used for this startup run.
    /// </summary>
    public DirectoryInfo StateRoot { get; }

    /// <summary>
    /// Disposes the started plugin rail and removes its isolated local state.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        try
        {
            HeadlessPluginCleanup.PrepareForHeadlessDispose(this.Plugin);
            this.DisposeContainerAndPluginIfNeeded();
        }
        finally
        {
            this.TryDeleteStateRoot();
        }
    }

    /// <summary>
    /// Disposes the owning mock container and falls back to direct plugin
    /// disposal when the container does not unload the plugin.
    /// </summary>
    private void DisposeContainerAndPluginIfNeeded()
    {
        switch (this.Container)
        {
            case IAsyncDisposable asyncDisposable:
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        if (!this.Plugin.StartupAudit.CaptureSnapshot().HasStage(PluginStartupStage.DisposeStarted))
        {
            this.Plugin.Dispose();
        }
    }

    /// <summary>
    /// Deletes the isolated local state created for this startup run when the
    /// host has fully released its file handles.
    /// </summary>
    private void TryDeleteStateRoot()
    {
        try
        {
            if (this.StateRoot.Exists)
            {
                this.StateRoot.Delete(true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Applies the headless unload preparation required before disposing the
/// production plugin under DalaMock without a live native UI.
/// </summary>
internal static class HeadlessPluginCleanup
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
