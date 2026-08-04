// <copyright file="RichOriginalTextPresentationPolicy.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Determines whether an original SeString payload can be rendered by the
/// normal ImGui presentation backend.
/// </summary>
internal static class RichOriginalTextPresentationPolicy
{
  /// <summary>
  /// Gets whether formatted SeString rendering is safe for the current
  /// plugin-owned presentation.
  /// </summary>
  /// <param name="backendKind">The selected text presentation backend.</param>
  /// <param name="showsOriginalSwapText">Whether the surface is displaying the original text for swap mode.</param>
  /// <param name="presentation">The optional captured original presentation.</param>
  /// <returns>
  /// <see langword="true" /> when the normal ImGui backend can render the
  /// original SeString bytes; otherwise, <see langword="false" />.
  /// </returns>
  public static bool CanUseFormattedSeString(
    TextPresentationBackendKind backendKind,
    bool showsOriginalSwapText,
    RichOriginalTextPresentation? presentation)
  {
    return backendKind == TextPresentationBackendKind.PlainImGui &&
           showsOriginalSwapText &&
           presentation?.HasSeStringPayload == true;
  }
}
