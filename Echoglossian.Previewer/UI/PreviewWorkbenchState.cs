// <copyright file="PreviewWorkbenchState.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Scenarios;

namespace Echoglossian.Previewer.UI;

/// <summary>
/// Holds mutable state shared by the unified preview shell and plugin windows.
/// </summary>
internal sealed class PreviewWorkbenchState
{
    /// <summary>Gets the initial preview scenario.</summary>
    internal PreviewScenario Scenario { get; private set; } = PreviewScenarioCatalog.Defaults[0];

    /// <summary>Gets or sets the active logical viewport.</summary>
    internal PreviewViewportPreset Viewport { get; set; } = PreviewScenarioCatalog.ViewportPresets[1];

    /// <summary>Gets or sets a value indicating whether the overlay is visible.</summary>
    internal bool OverlayVisible { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the config window is open.</summary>
    internal bool ConfigWindowOpen { get; set; }

    /// <summary>Gets or sets a value indicating whether the DB manager is open.</summary>
    internal bool DbManagerWindowOpen { get; set; }

    /// <summary>Gets or sets a value indicating whether translator metrics is open.</summary>
    internal bool TranslatorMetricsWindowOpen { get; set; }

    /// <summary>Gets or sets the active screenshot capture target.</summary>
    internal PreviewCaptureTarget CaptureTarget { get; set; } = PreviewCaptureTarget.FullFrame;

    /// <summary>
    /// Creates the default state for one preview workbench session.
    /// </summary>
    /// <param name="scenario">The initial overlay scenario.</param>
    /// <param name="viewport">The initial logical viewport.</param>
    /// <returns>The initialized workbench state.</returns>
    internal static PreviewWorkbenchState CreateDefault(
        PreviewScenario scenario,
        PreviewViewportPreset viewport)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(viewport);

        return new PreviewWorkbenchState
        {
            Scenario = scenario,
            Viewport = viewport,
            OverlayVisible = true,
        };
    }
}
