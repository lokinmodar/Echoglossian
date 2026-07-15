// <copyright file="TextPresentationResolver.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.UIOverlays.TextPresentation;

/// <summary>
/// Resolves which presentation backend should be used for a text request.
/// </summary>
internal static class TextPresentationResolver
{
  /// <summary>
  /// Resolves the backend kind for the provided request.
  /// </summary>
  /// <param name="request">The presentation request.</param>
  /// <returns>The resolved backend kind.</returns>
  public static TextPresentationBackendKind ResolveBackendKind(
      TextLayoutRequest request)
  {
    return LanguagePresentationPolicy.UsesTexturePresentation(
            request.LanguageId)
        ? TextPresentationBackendKind.RtlTexture
        : TextPresentationBackendKind.PlainImGui;
  }
}
