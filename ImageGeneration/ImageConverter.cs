// <copyright file="ImageConverter.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

/*namespace Echoglossian.ImageGeneration;


/// <summary>
/// Converts Bitmaps to ImGui-compatible RGBA byte buffers.
/// </summary>
public static class ImageConverter
{
  /// <summary>
  /// Converts a 32bpp ARGB bitmap into an RGBA byte buffer.
  /// </summary>
  /// <param name="bitmap">The source bitmap.</param>
  /// <returns>A byte array in RGBA format.</returns>
  public static byte[] ConvertBitmapToRgba(Bitmap bitmap)
  {
    Rectangle rect = new(0, 0, bitmap.Width, bitmap.Height);
    BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

    int byteCount = bmpData.Stride * bitmap.Height;
    byte[] buffer = new byte[byteCount];
    Marshal.Copy(bmpData.Scan0, buffer, 0, byteCount);
    bitmap.UnlockBits(bmpData);

    for (int i = 0; i < buffer.Length; i += 4)
    {
      byte a = buffer[i + 3];
      byte r = buffer[i + 2];
      byte g = buffer[i + 1];
      byte b = buffer[i + 0];

      buffer[i + 0] = r;
      buffer[i + 1] = g;
      buffer[i + 2] = b;
      buffer[i + 3] = a;
    }

    return buffer;
  }
}
*/