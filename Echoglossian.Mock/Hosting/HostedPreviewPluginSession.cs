// <copyright file="HostedPreviewPluginSession.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Plugin;
using System;
using System.IO;
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
        switch (this.Container)
        {
            case IDisposable disposable:
                disposable.Dispose();
                break;
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
        switch (this.Container)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
