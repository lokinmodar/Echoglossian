// <copyright file="IUiFontRuntime.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Runtime;

/// <summary>
/// Provides the active UI font stack to shared ImGui renderers.
/// </summary>
internal interface IUiFontRuntime
{
    /// <summary>
    /// Pushes the requested font and returns a scope that restores the prior
    /// ImGui font state when disposed.
    /// </summary>
    /// <param name="fontKind">The font family to push.</param>
    /// <returns>A scope that restores the prior font state.</returns>
    IDisposable Push(UiFontKind fontKind);
}
