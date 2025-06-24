// <copyright file="TextText.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
public sealed class TextTextureGenerator
{
  private readonly ITextureProvider textureProvider;

  public TextTextureGenerator(ITextureProvider textureProvider)
  {
    this.textureProvider = textureProvider;
  }

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
