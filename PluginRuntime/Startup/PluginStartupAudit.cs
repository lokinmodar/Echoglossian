// <copyright file="PluginStartupAudit.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginRuntime.Startup;

/// <summary>
/// Records startup and shutdown milestones so local mock tests can assert the plugin lifecycle without changing the public plugin surface.
/// </summary>
internal sealed class PluginStartupAudit
{
    private readonly object gate = new();
    private readonly HashSet<PluginStartupStage> completedStages = [];

    /// <summary>
    /// Records a completed stage.
    /// </summary>
    /// <param name="stage">The stage to record.</param>
    public void Mark(PluginStartupStage stage)
    {
        lock (this.gate)
        {
            this.completedStages.Add(stage);
        }
    }

    /// <summary>
    /// Captures the current startup state.
    /// </summary>
    /// <returns>A stable snapshot of the completed stages.</returns>
    public PluginStartupSnapshot CaptureSnapshot()
    {
        lock (this.gate)
        {
            return new PluginStartupSnapshot(this.completedStages.ToArray());
        }
    }
}
