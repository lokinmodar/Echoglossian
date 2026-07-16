// <copyright file="PreviewHostOptions.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Previewer.Hosting;

/// <summary>
///     Defines the window settings for a standalone preview host.
/// </summary>
internal sealed class PreviewHostOptions
{
    /// <summary>
    ///     Gets the client width in pixels.
    /// </summary>
    internal int Width { get; init; } = 1280;

    /// <summary>
    ///     Gets the client height in pixels.
    /// </summary>
    internal int Height { get; init; } = 720;

    /// <summary>
    ///     Gets the host window title.
    /// </summary>
    internal string Title { get; init; } = "Echoglossian Previewer";

    /// <summary>
    ///     Gets a value indicating whether the window starts hidden.
    /// </summary>
    internal bool StartHidden { get; init; }
}
