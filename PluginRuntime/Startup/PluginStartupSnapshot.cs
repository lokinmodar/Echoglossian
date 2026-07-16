// <copyright file="PluginStartupSnapshot.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Frozen;

namespace Echoglossian.PluginRuntime.Startup;

/// <summary>
/// Captures a stable view of the plugin startup audit at a point in time.
/// </summary>
internal sealed class PluginStartupSnapshot
{
    private readonly FrozenSet<PluginStartupStage> completedStages;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginStartupSnapshot"/> class.
    /// </summary>
    /// <param name="completedStages">The completed stages.</param>
    public PluginStartupSnapshot(IEnumerable<PluginStartupStage> completedStages)
    {
        this.completedStages = completedStages.ToFrozenSet();
    }

    /// <summary>
    /// Gets the completed startup stages.
    /// </summary>
    public IReadOnlyCollection<PluginStartupStage> CompletedStages => this.completedStages;

    /// <summary>
    /// Returns a value indicating whether the snapshot contains the requested stage.
    /// </summary>
    /// <param name="stage">The stage to check.</param>
    /// <returns><see langword="true"/> when the stage has been recorded; otherwise <see langword="false"/>.</returns>
    public bool HasStage(PluginStartupStage stage)
    {
        return this.completedStages.Contains(stage);
    }
}
