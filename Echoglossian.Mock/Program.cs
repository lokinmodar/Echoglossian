// <copyright file="Program.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using DalaMock.Core.Configuration;
using DalaMock.Core.Mocks;
using DalaMock.Core.Plugin;
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
    /// <param name="args">Optional command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--check-compatibility", StringComparison.Ordinal))
        {
            return DalaMockHostCompatibilityGuard.WriteCommandLineStatus(Console.Out);
        }

        DalaMockHostCompatibilityGuard.ThrowIfIncompatible();

        var mockContainer = new MockContainer(
            new MockDalamudConfiguration
            {
                CreateWindow = true,
            },
            builder => { },
            [],
            false);
        try
        {
            var pluginLoader = mockContainer.GetPluginLoader();
            var mockPlugin = pluginLoader.AddPlugin(typeof(global::Echoglossian.Echoglossian));

            await pluginLoader.StartPlugin(mockPlugin);

            mockContainer.GetMockUi().Run();
        }
        finally
        {
            switch (mockContainer)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }

        return 0;
    }
}
