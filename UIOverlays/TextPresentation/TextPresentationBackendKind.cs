// <copyright file="TextPresentationBackendKind.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Identifies the backend used to present translated text on ImGui-owned
/// surfaces.
/// </summary>
internal enum TextPresentationBackendKind
{
  /// <summary>
  /// Uses the existing plain ImGui text APIs.
  /// </summary>
  PlainImGui,

  /// <summary>
  /// Uses a texture generated from RTL text.
  /// </summary>
  RtlTexture,
}
