// <copyright file="HostedPreviewPluginSession.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Plugin;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Echoglossian.Mock.Hosting;

/// <summary>
/// Owns a started DalaMock container and its hosted Echoglossian plugin.
/// </summary>
public sealed class HostedPreviewPluginSession : IAsyncDisposable, IDisposable
{
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostedPreviewPluginSession"/> class.
    /// </summary>
    /// <param name="container">The owning DalaMock container.</param>
    /// <param name="plugin">The started production plugin instance.</param>
    /// <param name="stateRoot">The preview-owned state root.</param>
    /// <param name="pluginSavePath">The preview-owned plugin save path.</param>
    /// <param name="configPath">The preview-owned configuration path.</param>
    public HostedPreviewPluginSession(
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

    /// <summary>Gets the owning DalaMock container.</summary>
    public MockContainer Container { get; }

    /// <summary>Gets the started production plugin.</summary>
    public global::Echoglossian.Echoglossian Plugin { get; }

    /// <summary>Gets the preview-owned state root.</summary>
    public DirectoryInfo StateRoot { get; }

    /// <summary>Gets the preview-owned plugin save path.</summary>
    public DirectoryInfo PluginSavePath { get; }

    /// <summary>Gets the preview-owned configuration path.</summary>
    public FileInfo ConfigPath { get; }

    /// <inheritdoc/>
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
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        try
        {
            HeadlessPluginCleanup.PrepareForHeadlessDispose(this.Plugin);
            await this.DisposeContainerAndPluginIfNeededAsync();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

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

        if (!this.HasPluginStartedDispose())
        {
            this.Plugin.Dispose();
        }
    }

    private async Task DisposeContainerAndPluginIfNeededAsync()
    {
        switch (this.Container)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        if (!this.HasPluginStartedDispose())
        {
            this.Plugin.Dispose();
        }
    }

    private bool HasPluginStartedDispose()
    {
        var startupAuditProperty = typeof(global::Echoglossian.Echoglossian).GetProperty(
            "StartupAudit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (startupAuditProperty?.GetValue(this.Plugin) is not { } startupAudit)
        {
            return false;
        }

        var captureSnapshotMethod = startupAudit.GetType().GetMethod(
            "CaptureSnapshot",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (captureSnapshotMethod?.Invoke(startupAudit, null) is not { } snapshot)
        {
            return false;
        }

        var pluginStartupStageType = snapshot.GetType().Assembly.GetType(
            "Echoglossian.PluginRuntime.Startup.PluginStartupStage");
        if (pluginStartupStageType is null)
        {
            return false;
        }

        var hasStageMethod = snapshot.GetType().GetMethod(
            "HasStage",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (hasStageMethod is null)
        {
            return false;
        }

        var disposeStarted = Enum.Parse(pluginStartupStageType, "DisposeStarted");
        return hasStageMethod.Invoke(snapshot, [disposeStarted]) is true;
    }
}
