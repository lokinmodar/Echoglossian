// <copyright file="TextTextureGenerator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;



/// <summary>
/// Helper for generating textures from text using RTL-safe rendering.
/// </summary>
public static class TextTextureGenerator
{
  /// <summary>
  /// Renders a string into an ImGui-compatible texture.
  /// </summary>
  /// <param name="text">Text to render.</param>
  /// <param name="fontPath">Path to the TTF font file.</param>
  /// <param name="fontSize">Font size to use.</param>
  /// <param name="textColor">Text color.</param>
  /// <param name="backgroundColor">Background color.</param>
  /// <returns>An ITextureWrap containing the rendered string as a texture.</returns>
  public static ITextureWrap CreateTextTexture(
    string text,
    string fontPath,
    float fontSize,
    Color? textColor = null,
    Color? backgroundColor = null)
  {
    using TextImageRenderer renderer = new(fontPath, fontSize);
    using Bitmap bmp = renderer.RenderShapedText(
      text,
      textColor ?? Color.White,
      backgroundColor ?? Color.Black);

    byte[] rgba = ImageConverter.ConvertBitmapToRgba(bmp);
    return Dalamud.Interface.UiBuilder.LoadImageRaw(rgba, bmp.Width, bmp.Height, 4);
  }
}
