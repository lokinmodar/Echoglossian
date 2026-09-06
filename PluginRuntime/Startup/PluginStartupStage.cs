// <copyright file="PluginStartupStage.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginRuntime.Startup;

/// <summary>
/// Identifies the startup and shutdown milestones that the plugin can expose to local mock tests.
/// </summary>
internal enum PluginStartupStage
{
    CommandHandlersRegistered,
    PluginUiRegistered,
    RuntimeServicesBuilt,
    PersistenceCoordinatorStarted,
    RuntimeCachesPreloaded,
    AddonHandlersRegistered,
    OverlaysRegistered,
    FrameworkUpdateRegistered,
    StartupComplete,
    DisposeStarted,
    PersistenceAdmissionsStopped,
    PluginUiUnregistered,
    FrameworkUpdateUnregistered,
    RuntimeServicesDisposed,
    DisposeComplete,
}
