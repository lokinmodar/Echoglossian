// <copyright file="HostedPreviewPluginSessionFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DalaMock.Core.Configuration;
using DalaMock.Core.Plugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Echoglossian.Mock.Hosting;

/// <summary>
/// Starts the production plugin inside a preview-owned DalaMock session.
/// </summary>
public static class HostedPreviewPluginSessionFactory
{
    /// <summary>
    /// Starts a DalaMock session using only the caller-supplied preview paths.
    /// </summary>
    /// <param name="options">The preview-owned session paths and UI mode.</param>
    /// <param name="cancellationToken">Cancels startup before the plugin is loaded.</param>
    /// <returns>The started hosted plugin session.</returns>
    /// <exception cref="InvalidOperationException">Thrown when DalaMock does not build the production plugin.</exception>
    public static async Task<HostedPreviewPluginSession> StartAsync(
        HostedPreviewPluginOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.StateRoot.Create();
        options.PluginSavePath.Create();
        var effectiveDatabasePath = PrepareHostedDatabase(options);

        var container = new MockContainer(
            new MockDalamudConfiguration
            {
                CreateWindow = options.CreateWindow,
                GamePath = ResolveSqpackDirectory(),
                PluginSavePath = options.PluginSavePath,
            },
            builder => { },
            [],
            false);

        try
        {
            var loader = container.GetPluginLoader();
            var mockPlugin = loader.AddPlugin(typeof(EchoglossianAsyncPluginAdapter));
            var settings = new PluginLoadSettings(options.StateRoot, options.ConfigPath)
            {
                AssemblyLocation = typeof(global::Echoglossian.Echoglossian).Assembly.Location,
            };

            await loader.StartPlugin(mockPlugin, settings);
            cancellationToken.ThrowIfCancellationRequested();

            if (mockPlugin.DalamudPlugin is not EchoglossianAsyncPluginAdapter adapter ||
                adapter.Plugin is null)
            {
                throw new InvalidOperationException("DalaMock did not build Echoglossian.");
            }

            var plugin = adapter.Plugin;

            VerifyHostedDatabasePath(effectiveDatabasePath);

            return new HostedPreviewPluginSession(
                container,
                plugin,
                options.StateRoot,
                options.PluginSavePath,
                options.ConfigPath);
        }
        catch
        {
            await DisposeContainerAsync(container);
            throw;
        }
    }

    /// <summary>
    /// Copies the preview database to the path opened by the production plugin's database manager.
    /// </summary>
    /// <param name="options">The preview-owned hosted-session options.</param>
    /// <returns>The expected production database path, or <see langword="null"/> when no database was supplied.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the supplied preview database is unavailable.</exception>
    private static string? PrepareHostedDatabase(HostedPreviewPluginOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return null;
        }

        var sourceDatabasePath = Path.GetFullPath(options.DatabasePath);
        if (!File.Exists(sourceDatabasePath))
        {
            throw new InvalidOperationException(
                $"The hosted preview database does not exist: {sourceDatabasePath}");
        }

        var destinationDirectory = Path.Combine(
            options.StateRoot.FullName,
            typeof(global::Echoglossian.Echoglossian).Assembly.GetName().Name!);
        Directory.CreateDirectory(destinationDirectory);
        var destinationDatabasePath = Path.Combine(destinationDirectory, "Echoglossian.db");
        File.Copy(sourceDatabasePath, destinationDatabasePath, overwrite: true);
        return destinationDatabasePath;
    }

    /// <summary>
    /// Verifies that DalaMock resolved the production plugin configuration directory used for the seeded database.
    /// </summary>
    /// <param name="effectiveDatabasePath">The path seeded before hosted startup.</param>
    /// <exception cref="InvalidOperationException">Thrown when DalaMock resolves a different plugin configuration directory.</exception>
    private static void VerifyHostedDatabasePath(string? effectiveDatabasePath)
    {
        if (effectiveDatabasePath is null)
        {
            return;
        }

        var resolvedDatabasePath = Path.GetFullPath(Path.Combine(
            global::Echoglossian.Echoglossian.ConfigDirectory,
            "Echoglossian.db"));
        if (!string.Equals(
                effectiveDatabasePath,
                resolvedDatabasePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"DalaMock resolved hosted plugin database path '{resolvedDatabasePath}', " +
                $"but preview seeded '{effectiveDatabasePath}'.");
        }
    }

    /// <summary>
    /// Resolves the local FFXIV sqpack directory required by DalaMock.
    /// </summary>
    /// <returns>The local sqpack directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid local sqpack directory can be found.</exception>
    private static DirectoryInfo ResolveSqpackDirectory()
    {
        foreach (var candidate in GetSqpackPathCandidates())
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
    /// <returns>The ordered local sqpack path candidates.</returns>
    private static IEnumerable<string?> GetSqpackPathCandidates()
    {
        yield return Environment.GetEnvironmentVariable("EXD_DATA_DIR");

        var launcherGamePath = TryReadLauncherGamePath();
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
    /// <returns>The configured XIVLauncher game path, or <see langword="null"/> when unavailable.</returns>
    private static string? TryReadLauncherGamePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var launcherConfigPath = Path.Combine(appDataPath, "XIVLauncher", "launcherConfigV3.json");
        if (!File.Exists(launcherConfigPath))
        {
            return null;
        }

        try
        {
            using var launcherConfigStream = File.OpenRead(launcherConfigPath);
            using var launcherConfigDocument = JsonDocument.Parse(launcherConfigStream);
            return launcherConfigDocument.RootElement.TryGetProperty("GamePath", out var gamePathElement)
                ? gamePathElement.GetString()
                : null;
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
    /// Disposes a DalaMock container after failed startup.
    /// </summary>
    /// <param name="container">The container to dispose.</param>
    /// <returns>A task that completes when disposal finishes.</returns>
    private static async Task DisposeContainerAsync(MockContainer container)
    {
        switch (container)
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
