// <copyright file="ScreenshotRequest.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.Scenarios;

namespace Echoglossian.Previewer.Screenshots;

/// <summary>
/// Describes one preview screenshot capture request.
/// </summary>
/// <param name="Mode">The requested capture mode.</param>
/// <param name="Scenario">The selected preview scenario.</param>
/// <param name="Viewport">The logical viewport.</param>
/// <param name="OutputDirectory">The destination directory.</param>
/// <param name="SurfaceMargin">The logical margin for selected-surface crops.</param>
internal sealed record ScreenshotRequest(
    ScreenshotMode Mode,
    PreviewScenario Scenario,
    PreviewViewportPreset Viewport,
    string OutputDirectory,
    float SurfaceMargin = 8f);
