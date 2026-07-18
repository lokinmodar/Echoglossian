// <copyright file="StartedPlugin.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Plugin;
using Echoglossian.Mock.Hosting;
using Echoglossian.PluginRuntime.Startup;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading;

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
    /// <param name="pluginSavePath">The rail-owned plugin save path used for this startup run.</param>
    /// <param name="configPath">The rail-owned plugin config path used for this startup run.</param>
    public StartedPlugin(
        MockContainer container,
        global::Echoglossian.Echoglossian plugin,
        DirectoryInfo stateRoot,
        DirectoryInfo pluginSavePath,
        FileInfo configPath)
    {
        this.Container = container;
        this.Plugin = plugin;
        this.StateRoot = stateRoot;
        this.PluginSavePath = pluginSavePath;
        this.ConfigPath = configPath;
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
    /// Gets the rail-owned plugin save path used for this startup run.
    /// </summary>
    public DirectoryInfo PluginSavePath { get; }

    /// <summary>
    /// Gets the rail-owned plugin config path used for this startup run.
    /// </summary>
    public FileInfo ConfigPath { get; }

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
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
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
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (this.StateRoot.Exists)
                {
                    this.StateRoot.Delete(true);
                }

                return;
            }
            catch (IOException)
            {
                if (attempt == 9)
                {
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                this.StateRoot.Refresh();
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 9)
                {
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                this.StateRoot.Refresh();
            }
        }
    }
}
