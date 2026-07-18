// <copyright file="TestBoot.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Configuration;
using DalaMock.Core.Plugin;
using Echoglossian.Mock.Hosting;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Builds a headless DalaMock container and starts the real Echoglossian plugin inside it.
/// </summary>
internal sealed class TestBoot
{
    private readonly Func<DirectoryInfo> runStateRootFactory;
    private readonly Func<Func<Task>, Task> startPluginRunner;
    private readonly Func<MockContainer, global::Echoglossian.Echoglossian, DirectoryInfo, DirectoryInfo, FileInfo, Task> postStartValidation;
    private readonly bool useHostedSessionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestBoot"/> class.
    /// </summary>
    public TestBoot()
        : this(null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestBoot"/> class with
    /// deterministic test seams for startup-failure coverage.
    /// </summary>
    /// <param name="runStateRootFactory">Creates the isolated local state root for the current run.</param>
    /// <param name="startPluginRunner">Runs or overrides the plugin-startup action.</param>
    /// <param name="postStartValidation">Runs additional validation before returning the started plugin rail.</param>
    internal TestBoot(
        Func<DirectoryInfo>? runStateRootFactory,
        Func<Func<Task>, Task>? startPluginRunner,
        Func<MockContainer, global::Echoglossian.Echoglossian, DirectoryInfo, DirectoryInfo, FileInfo, Task>? postStartValidation)
    {
        this.useHostedSessionFactory = runStateRootFactory is null &&
            startPluginRunner is null &&
            postStartValidation is null;
        this.runStateRootFactory = runStateRootFactory ?? this.CreateRunStateRoot;
        this.startPluginRunner = startPluginRunner ?? RunStartPluginAsync;
        this.postStartValidation = postStartValidation ?? NoOpPostStartValidationAsync;
    }

    /// <summary>
    /// Starts the real plugin under a headless DalaMock container.
    /// </summary>
    /// <returns>The started plugin and its owning mock container.</returns>
    public async Task<StartedPlugin> StartPluginAsync()
    {
        var stateRoot = this.runStateRootFactory();
        var pluginSavePath = this.CreateLocalPluginSavePath(stateRoot);
        var configPath = new FileInfo(Path.Combine(stateRoot.FullName, "test.json"));
        MockContainer? mockContainer = null;
        global::Echoglossian.Echoglossian? plugin = null;

        try
        {
            if (this.useHostedSessionFactory)
            {
                var session = await HostedPreviewPluginSessionFactory.StartAsync(
                    new HostedPreviewPluginOptions(
                        stateRoot,
                        pluginSavePath,
                        configPath,
                        DatabasePath: null,
                        CreateWindow: false));
                mockContainer = session.Container;
                plugin = session.Plugin;
                await this.postStartValidation(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
                return new StartedPlugin(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
            }

            mockContainer = new MockContainer(
                new MockDalamudConfiguration
                {
                    CreateWindow = false,
                    GamePath = this.ResolveSqpackDirectory(),
                    PluginSavePath = pluginSavePath,
                },
                builder => { },
                [],
                false);

            var pluginLoader = mockContainer.GetPluginLoader();
            var mockPlugin = pluginLoader.AddPlugin(typeof(EchoglossianAsyncPluginAdapter));
            var pluginLoadSettings = new PluginLoadSettings(
                stateRoot,
                configPath)
            {
                AssemblyLocation = typeof(global::Echoglossian.Echoglossian).Assembly.Location,
            };

            await this.startPluginRunner(() => pluginLoader.StartPlugin(mockPlugin, pluginLoadSettings));

            if (mockPlugin.DalamudPlugin is not EchoglossianAsyncPluginAdapter adapter ||
                adapter.Plugin is null)
            {
                throw new InvalidOperationException("DalaMock did not build Echoglossian.");
            }

            plugin = adapter.Plugin;
            await this.postStartValidation(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
            return new StartedPlugin(mockContainer, plugin, stateRoot, pluginSavePath, configPath);
        }
        catch
        {
            this.TryCleanupFailedStartup(mockContainer, plugin, stateRoot);
            throw;
        }
    }

    /// <summary>
    /// Creates an isolated local state root for a single DalaMock startup run.
    /// </summary>
    /// <returns>The created local state root.</returns>
    private DirectoryInfo CreateRunStateRoot()
    {
        var pluginSavePath = new DirectoryInfo(
            Path.Combine(
                Path.GetTempPath(),
                "Echoglossian.Mock.Tests",
                Guid.NewGuid().ToString("N")));
        pluginSavePath.Create();
        return pluginSavePath;
    }

    /// <summary>
    /// Creates the plugin-save directory used by the DalaMock startup rail.
    /// </summary>
    /// <param name="stateRoot">The isolated local state root for the current run.</param>
    /// <returns>The created plugin-save directory.</returns>
    private DirectoryInfo CreateLocalPluginSavePath(DirectoryInfo stateRoot)
    {
        var pluginSavePath = new DirectoryInfo(Path.Combine(stateRoot.FullName, ".dalamock"));
        pluginSavePath.Create();
        return pluginSavePath;
    }

    /// <summary>
    /// Resolves the local FFXIV sqpack directory required by DalaMock.
    /// </summary>
    /// <returns>The local sqpack directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid local sqpack directory can be found.</exception>
    private DirectoryInfo ResolveSqpackDirectory()
    {
        foreach (var candidate in this.GetSqpackPathCandidates())
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return new DirectoryInfo(candidate);
            }
        }

        throw new InvalidOperationException("Unable to resolve a local FFXIV sqpack directory for DalaMock.");
    }

    /// <summary>
    /// Gets the local sqpack path candidates that should be checked for DalaMock.
    /// </summary>
    /// <returns>The sqpack path candidates.</returns>
    private IEnumerable<string?> GetSqpackPathCandidates()
    {
        yield return Environment.GetEnvironmentVariable("EXD_DATA_DIR");

        var launcherGamePath = this.TryReadLauncherGamePath();
        if (!string.IsNullOrWhiteSpace(launcherGamePath))
        {
            yield return Path.Combine(launcherGamePath, "game", "sqpack");
        }

        yield return @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";
        yield return @"C:\Program Files\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";
        yield return @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
    }

    /// <summary>
    /// Reads the local XIVLauncher game path when it is available.
    /// </summary>
    /// <returns>The configured XIVLauncher game path, or <see langword="null"/> when it is unavailable.</returns>
    private string? TryReadLauncherGamePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var launcherConfigPath = Path.Combine(appDataPath, "XIVLauncher", "launcherConfigV3.json");
        return TryReadLauncherGamePath(launcherConfigPath);
    }

    /// <summary>
    /// Reads the local XIVLauncher game path from a specific config file path
    /// when it is available and readable.
    /// </summary>
    /// <param name="launcherConfigPath">The launcher config file path to inspect.</param>
    /// <returns>The configured XIVLauncher game path, or <see langword="null"/> when it is unavailable.</returns>
    internal static string? TryReadLauncherGamePath(string launcherConfigPath)
    {
        if (!File.Exists(launcherConfigPath))
        {
            return null;
        }

        try
        {
            using var launcherConfigStream = File.OpenRead(launcherConfigPath);
            using var launcherConfigDocument = JsonDocument.Parse(launcherConfigStream);
            if (!launcherConfigDocument.RootElement.TryGetProperty("GamePath", out var gamePathElement))
            {
                return null;
            }

            return gamePathElement.GetString();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Runs the default plugin-startup action.
    /// </summary>
    /// <param name="startPlugin">The plugin-startup action to run.</param>
    /// <returns>A task that completes once startup finishes.</returns>
    private static Task RunStartPluginAsync(Func<Task> startPlugin)
    {
        return startPlugin();
    }

    /// <summary>
    /// Performs no additional post-start validation.
    /// </summary>
    /// <param name="container">The owning mock container.</param>
    /// <param name="plugin">The started production plugin.</param>
    /// <param name="stateRoot">The isolated local state root for the run.</param>
    /// <param name="pluginSavePath">The rail-owned plugin save path for the run.</param>
    /// <param name="configPath">The rail-owned config path for the run.</param>
    /// <returns>A completed task.</returns>
    private static Task NoOpPostStartValidationAsync(
        MockContainer container,
        global::Echoglossian.Echoglossian plugin,
        DirectoryInfo stateRoot,
        DirectoryInfo pluginSavePath,
        FileInfo configPath)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs best-effort cleanup for startup failures that happen before the
    /// rail can return a <see cref="StartedPlugin"/> owner.
    /// </summary>
    /// <param name="mockContainer">The owning DalaMock container, when available.</param>
    /// <param name="plugin">The started production plugin, when startup reached construction.</param>
    /// <param name="stateRoot">The isolated local state root created for the failed run.</param>
    private void TryCleanupFailedStartup(
        MockContainer? mockContainer,
        global::Echoglossian.Echoglossian? plugin,
        DirectoryInfo stateRoot)
    {
        try
        {
            if (plugin is not null)
            {
                HeadlessPluginCleanup.PrepareForHeadlessDispose(plugin);
            }

            this.TryDisposeContainerAndPluginIfNeeded(mockContainer, plugin);
        }
        catch
        {
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            this.TryDeleteStateRoot(stateRoot);
        }
    }

    /// <summary>
    /// Disposes the owning mock container and falls back to direct plugin
    /// disposal when the container did not unload the plugin.
    /// </summary>
    /// <param name="mockContainer">The owning DalaMock container, when available.</param>
    /// <param name="plugin">The started production plugin, when available.</param>
    private void TryDisposeContainerAndPluginIfNeeded(
        MockContainer? mockContainer,
        global::Echoglossian.Echoglossian? plugin)
    {
        switch (mockContainer)
        {
            case IAsyncDisposable asyncDisposable:
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        if (plugin is not null &&
            !plugin.StartupAudit.CaptureSnapshot().HasStage(global::Echoglossian.PluginRuntime.Startup.PluginStartupStage.DisposeStarted))
        {
            plugin.Dispose();
        }
    }

    /// <summary>
    /// Deletes the isolated local state created for a failed startup run when
    /// the host has fully released its file handles.
    /// </summary>
    /// <param name="stateRoot">The isolated local state root for the failed run.</param>
    private void TryDeleteStateRoot(DirectoryInfo stateRoot)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (stateRoot.Exists)
                {
                    stateRoot.Delete(true);
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
                stateRoot.Refresh();
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 9)
                {
                    return;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                stateRoot.Refresh();
            }
        }
    }
}
