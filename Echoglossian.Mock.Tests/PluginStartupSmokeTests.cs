// <copyright file="PluginStartupSmokeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginRuntime.Startup;

using FluentAssertions;
using System.Threading.Tasks;
using Xunit;

namespace Echoglossian.Mock.Tests;

/// <summary>
/// Covers startup and shutdown milestones under DalaMock.
/// </summary>
public class PluginStartupSmokeTests
{
    [Fact]
    public async Task StartPluginAsync_marks_expected_startup_stages()
    {
        var started = await new TestBoot().StartPluginAsync();

        var snapshot = started.Plugin.StartupAudit.CaptureSnapshot();

        snapshot.HasStage(PluginStartupStage.CommandHandlersRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.PluginUiRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.RuntimeServicesBuilt).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.RuntimeCachesPreloaded).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.FrameworkUpdateRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.AddonHandlersRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.OverlaysRegistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.StartupComplete).Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_marks_expected_shutdown_stages()
    {
        var started = await new TestBoot().StartPluginAsync();

        PrepareForHeadlessDispose(started.Plugin);
        started.Plugin.Dispose();

        var snapshot = started.Plugin.StartupAudit.CaptureSnapshot();

        snapshot.HasStage(PluginStartupStage.DisposeStarted).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.PluginUiUnregistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.FrameworkUpdateUnregistered).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.RuntimeServicesDisposed).Should().BeTrue();
        snapshot.HasStage(PluginStartupStage.DisposeComplete).Should().BeTrue();
    }

    /// <summary>
    /// Replaces the registered addon-handler list with an empty instance so the
    /// headless shutdown rail can validate plugin-level disposal without native
    /// UI restoration that requires a live AtkStage.
    /// </summary>
    /// <param name="plugin">The started production plugin.</param>
    /// <exception cref="System.InvalidOperationException">Thrown when the registered addon-handler field cannot be located or instantiated.</exception>
    private static void PrepareForHeadlessDispose(global::Echoglossian.Echoglossian plugin)
    {
        var field = typeof(global::Echoglossian.Echoglossian).GetField(
            "registeredAddonHandlers",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field is null)
        {
            throw new System.InvalidOperationException("Unable to locate Echoglossian.registeredAddonHandlers for headless dispose preparation.");
        }

        var emptyHandlers = System.Activator.CreateInstance(field.FieldType);
        if (emptyHandlers is null)
        {
            throw new System.InvalidOperationException("Unable to create an empty registeredAddonHandlers list for headless dispose preparation.");
        }

        field.SetValue(plugin, emptyHandlers);
    }
}
