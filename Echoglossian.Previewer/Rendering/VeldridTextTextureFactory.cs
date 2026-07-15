// <copyright file="VeldridTextTextureFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Dalamud.Interface.Textures.TextureWraps;

using Echoglossian.ImageGeneration;
using Echoglossian.UIOverlays.TextPresentation;

using Veldrid;

namespace Echoglossian.Previewer.Rendering;

/// <summary>
///     Rasterizes measured text layouts and uploads their RGBA pixels to the
///     preview host's Veldrid device.
/// </summary>
internal sealed class VeldridTextTextureFactory
{
    private readonly Func<VeldridTextTextureUpload, IDalamudTextureWrap>
        createTexture;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VeldridTextTextureFactory" />
    ///     class for a preview host device.
    /// </summary>
    /// <param name="graphicsDevice">The Veldrid device receiving texture uploads.</param>
    /// <param name="textureRegistry">The ImGui texture registry for the host.</param>
    internal VeldridTextTextureFactory(
        GraphicsDevice graphicsDevice,
        VeldridTextureRegistry textureRegistry)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(textureRegistry);
        this.createTexture = upload => CreateTexture(
            graphicsDevice,
            textureRegistry,
            upload);
    }

    /// <summary>
    ///     Initializes a testable factory with a texture creation operation.
    /// </summary>
    /// <param name="createTexture">The operation that creates one texture wrap.</param>
    internal VeldridTextTextureFactory(
        Func<VeldridTextTextureUpload, IDalamudTextureWrap> createTexture)
    {
        this.createTexture = createTexture ??
            throw new ArgumentNullException(nameof(createTexture));
    }

    /// <summary>
    ///     Rasterizes the existing measured layout and uploads its exact RGBA
    ///     pixel payload to Veldrid.
    /// </summary>
    /// <param name="renderer">The renderer that owns the measured layout.</param>
    /// <param name="layout">The layout to rasterize without remeasuring.</param>
    /// <param name="request">The resolved raster colors and layout inputs.</param>
    /// <param name="cancellationToken">The texture lifecycle cancellation token.</param>
    /// <returns>The registered preview texture wrapper.</returns>
    internal Task<IDalamudTextureWrap> CreateTextureAsync(
        TextImageRenderer renderer,
        TextImageRenderer.TextRasterLayout layout,
        TextureCreationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using Bitmap bitmap = renderer.RenderTextLayout(
            layout,
            request.TextColor,
            request.BackgroundColor);
        cancellationToken.ThrowIfCancellationRequested();

        var upload = ConvertBitmapToRgba(bitmap);
        return this.CreateTextureAsync(upload, cancellationToken);
    }

    /// <summary>
    ///     Creates one texture from an already prepared RGBA upload payload.
    /// </summary>
    /// <param name="upload">The top-left RGBA texture payload.</param>
    /// <param name="cancellationToken">The texture lifecycle cancellation token.</param>
    /// <returns>The registered preview texture wrapper.</returns>
    internal Task<IDalamudTextureWrap> CreateTextureAsync(
        VeldridTextTextureUpload upload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(this.createTexture(upload));
    }

    /// <summary>
    ///     Converts a GDI ARGB bitmap to top-left oriented RGBA bytes for
    ///     <see cref="PixelFormat.R8_G8_B8_A8_UNorm" /> uploads.
    /// </summary>
    /// <param name="bitmap">The 32-bit ARGB source bitmap.</param>
    /// <returns>The Veldrid upload dimensions and RGBA pixel bytes.</returns>
    internal static VeldridTextTextureUpload ConvertBitmapToRgba(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
        {
            throw new ArgumentException(
                "Text texture bitmaps must use 32-bit ARGB pixels.",
                nameof(bitmap));
        }

        var rectangle = new System.Drawing.Rectangle(
            0,
            0,
            bitmap.Width,
            bitmap.Height);
        var rgbaPixels = new byte[checked(bitmap.Width * bitmap.Height * 4)];
        BitmapData bitmapData = bitmap.LockBits(
            rectangle,
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var sourceRow = new byte[checked(bitmap.Width * 4)];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(
                    IntPtr.Add(bitmapData.Scan0, checked(y * bitmapData.Stride)),
                    sourceRow,
                    0,
                    sourceRow.Length);
                var destinationOffset = checked(y * bitmap.Width * 4);
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var sourceOffset = x * 4;
                    var pixelOffset = destinationOffset + sourceOffset;
                    rgbaPixels[pixelOffset] = sourceRow[sourceOffset + 2];
                    rgbaPixels[pixelOffset + 1] = sourceRow[sourceOffset + 1];
                    rgbaPixels[pixelOffset + 2] = sourceRow[sourceOffset];
                    rgbaPixels[pixelOffset + 3] = sourceRow[sourceOffset + 3];
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return new VeldridTextTextureUpload(
            bitmap.Width,
            bitmap.Height,
            rgbaPixels);
    }

    /// <summary>
    ///     Creates and registers one sampled Veldrid texture from RGBA pixels.
    /// </summary>
    /// <param name="graphicsDevice">The device receiving the upload.</param>
    /// <param name="textureRegistry">The host texture registry.</param>
    /// <param name="upload">The exact top-left RGBA bytes to upload.</param>
    /// <returns>The owned texture wrapper.</returns>
    private static IDalamudTextureWrap CreateTexture(
        GraphicsDevice graphicsDevice,
        VeldridTextureRegistry textureRegistry,
        VeldridTextTextureUpload upload)
    {
        Texture? texture = null;
        TextureView? textureView = null;
        try
        {
            texture = graphicsDevice.ResourceFactory.CreateTexture(
                TextureDescription.Texture2D(
                    checked((uint)upload.Width),
                    checked((uint)upload.Height),
                    mipLevels: 1,
                    arrayLayers: 1,
                    Veldrid.PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled));
            graphicsDevice.UpdateTexture(
                texture,
                upload.Pixels,
                0,
                0,
                0,
                checked((uint)upload.Width),
                checked((uint)upload.Height),
                1,
                0,
                0);
            textureView = graphicsDevice.ResourceFactory.CreateTextureView(texture);
            var textureWrap = new PreviewTextureWrap(
                texture,
                textureView,
                textureRegistry);
            texture = null;
            textureView = null;
            return textureWrap;
        }
        finally
        {
            textureView?.Dispose();
            texture?.Dispose();
        }
    }
}

/// <summary>
///     Contains the dimensions and top-left RGBA bytes for one Veldrid upload.
/// </summary>
/// <param name="Width">The texture width in pixels.</param>
/// <param name="Height">The texture height in pixels.</param>
/// <param name="Pixels">The exact RGBA upload bytes.</param>
internal sealed record VeldridTextTextureUpload(
    int Width,
    int Height,
    byte[] Pixels);
