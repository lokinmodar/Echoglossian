// <copyright file="PluginWindowBackendStatus.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.PluginWindows;

/// <summary>
///     Describes the requested and active plugin-window preview backend.
/// </summary>
/// <param name="RequestedMode">The backend requested by the user.</param>
/// <param name="EffectiveMode">The backend currently in use.</param>
/// <param name="HostedRequested">Whether a hosted backend was requested.</param>
/// <param name="HostedAvailable">Whether a hosted backend is available.</param>
/// <param name="FallbackReason">The reason the requested backend was not used.</param>
internal sealed record PluginWindowBackendStatus(
    PluginWindowPreviewBackendMode RequestedMode,
    PluginWindowPreviewBackendMode EffectiveMode,
    bool HostedRequested,
    bool HostedAvailable,
    string? FallbackReason);
