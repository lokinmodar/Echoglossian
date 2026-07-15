// <copyright file="TextText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.ImageGeneration;

/// <summary>
/// Generates textures from text using a specified font and size.
/// </summary>
public sealed class TextTextureGenerator
{
  private readonly ITextureProvider textureProvider;

  /// <summary>
  /// Initializes a new instance of the <see cref="TextTextureGenerator"/> class.
  /// </summary>
  /// <param name="textureProvider">The texture provider used to upload images.</param>
  public TextTextureGenerator(ITextureProvider textureProvider)
  {
    this.textureProvider = textureProvider;
  }

  /// <summary>
  /// Creates a texture from the specified text using the given font and size.
  /// </summary>
  /// <param name="text">The logical text to rasterize.</param>
  /// <param name="fontPath">The font file path used for rasterization.</param>
  /// <param name="fontSize">The font size in pixels.</param>
  /// <param name="textColor">The rasterized foreground color.</param>
  /// <param name="backgroundColor">The rasterized background color.</param>
  /// <param name="fontStyle">The font style used during rasterization.</param>
  /// <param name="maxWidth">Optional pixel width used for wrapping.</param>
  /// <param name="lineHeightScale">
  /// Shared line-height scale used during multiline rasterization.
  /// </param>
  /// <returns>The uploaded Dalamud texture.</returns>
  public async Task<IDalamudTextureWrap> CreateTextTextureAsync(
      string text,
      string fontPath,
      float fontSize,
      Color? textColor = null,
      Color? backgroundColor = null,
      FontStyle fontStyle = FontStyle.Regular,
      int? maxWidth = null,
      float lineHeightScale = 1.0f)
  {
    using TextImageRenderer renderer =
        new(fontPath, fontSize, fontStyle, lineHeightScale);
    using Bitmap bmp = renderer.RenderShapedText(
        text,
        textColor ?? Color.White,
        backgroundColor ?? Color.Transparent,
        maxWidth);

    using MemoryStream ms = new();
    bmp.Save(ms, ImageFormat.Png);
    ms.Position = 0;

    return await this.textureProvider.CreateFromImageAsync(ms);
  }
}
