// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Mock.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Echoglossian.Mock;

/// <summary>
/// Hosts a local DalaMock runner for the Echoglossian plugin.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the mock plugin host and runs its UI loop.
    /// </summary>
    private static async Task Main()
    {
        var stateRoot = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, ".dalamock"));
        var pluginSavePath = stateRoot.CreateSubdirectory("plugin-save");
        var configPath = new FileInfo(Path.Combine(stateRoot.FullName, "Echoglossian.json"));

        await using var session = await HostedPreviewPluginSessionFactory.StartAsync(
            new HostedPreviewPluginOptions(
                stateRoot,
                pluginSavePath,
                configPath,
                DatabasePath: null,
                CreateWindow: true));

        session.Container.GetMockUi().Run();
    }
}
