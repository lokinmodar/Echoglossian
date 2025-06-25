// <copyright file="TextText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

/// <summary>
/// Generates textures from text using a specified font and size.
/// </summary>
public sealed class TextTextureGenerator
{
  private readonly ITextureProvider textureProvider;

  /// <summary>
  /// Initializes a new instance of the <see cref="TextTextureGenerator"/> class.
  /// </summary>
  /// <param name="textureProvider"></param>
  public TextTextureGenerator(ITextureProvider textureProvider)
  {
    this.textureProvider = textureProvider;
  }

  /// <summary>
  /// Creates a texture from the specified text using the given font and size.
  /// </summary>
  /// <param name="text"></param>
  /// <param name="fontPath"></param>
  /// <param name="fontSize"></param>
  /// <param name="textColor"></param>
  /// <param name="backgroundColor"></param>
  /// <param name="fontStyle"></param>
  /// <param name="maxWidth"></param>
  /// <returns></returns>
  public async Task<IDalamudTextureWrap> CreateTextTextureAsync(
      string text,
      string fontPath,
      float fontSize,
      Color? textColor = null,
      Color? backgroundColor = null,
      FontStyle fontStyle = FontStyle.Regular,
      int? maxWidth = null)
  {
    using TextImageRenderer renderer = new(fontPath, fontSize, fontStyle);
    using Bitmap bmp = renderer.RenderShapedText(
        text,
        textColor ?? Color.White,
        backgroundColor ?? Color.Black,
        maxWidth);

    using MemoryStream ms = new();
    bmp.Save(ms, ImageFormat.Png);
    ms.Position = 0;

    return await this.textureProvider.CreateFromImageAsync(ms);
  }
}
