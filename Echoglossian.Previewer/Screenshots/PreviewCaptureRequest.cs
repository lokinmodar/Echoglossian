// <copyright file="PreviewCaptureRequest.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Previewer.UI;

namespace Echoglossian.Previewer.Screenshots;

/// <summary>
/// Describes an interactive screenshot request before it is bound to preview state.
/// </summary>
/// <param name="Mode">The screenshot output mode.</param>
/// <param name="CaptureTarget">The preview surface to crop into the screenshot.</param>
internal sealed record PreviewCaptureRequest(
    ScreenshotMode Mode,
    PreviewCaptureTarget CaptureTarget);
