// <copyright file="VeldridScreenshotCapture.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TranslationOverlay;

using System.Drawing.Imaging;

using Veldrid;

using DrawingRectangle = System.Drawing.Rectangle;

namespace Echoglossian.Previewer.Screenshots;

/// <summary>
/// Captures rendered Veldrid targets into PNG files.
/// </summary>
internal static class VeldridScreenshotCapture
{
    /// <summary>
    /// Calculates a clamped selected-surface crop in physical framebuffer pixels.
    /// </summary>
    /// <param name="result">The exact overlay render result.</param>
    /// <param name="logicalViewportWidth">The logical viewport width.</param>
    /// <param name="logicalViewportHeight">The logical viewport height.</param>
    /// <param name="logicalMargin">The logical crop margin.</param>
    /// <param name="framebufferScale">The logical-to-physical framebuffer scale.</param>
    /// <returns>The clamped physical crop rectangle.</returns>
    internal static DrawingRectangle CalculateSurfaceCrop(
        TranslationOverlayRenderResult result,
        int logicalViewportWidth,
        int logicalViewportHeight,
        float logicalMargin,
        float framebufferScale)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.WasDrawn ||
            result.Size.X <= 0f ||
            result.Size.Y <= 0f ||
            logicalViewportWidth <= 0 ||
            logicalViewportHeight <= 0 ||
            framebufferScale <= 0f)
        {
            return DrawingRectangle.Empty;
        }

        var scale = framebufferScale;
        var left = (result.Position.X - logicalMargin) * scale;
        var top = (result.Position.Y - logicalMargin) * scale;
        var right = (result.Position.X + result.Size.X + logicalMargin) * scale;
        var bottom = (result.Position.Y + result.Size.Y + logicalMargin) * scale;
        var maxRight = logicalViewportWidth * scale;
        var maxBottom = logicalViewportHeight * scale;

        var x = Math.Clamp((int)Math.Floor(left), 0, (int)Math.Ceiling(maxRight));
        var y = Math.Clamp((int)Math.Floor(top), 0, (int)Math.Ceiling(maxBottom));
        var clampedRight = Math.Clamp((int)Math.Ceiling(right), 0, (int)Math.Ceiling(maxRight));
        var clampedBottom = Math.Clamp((int)Math.Ceiling(bottom), 0, (int)Math.Ceiling(maxBottom));

        if (clampedRight <= x || clampedBottom <= y)
        {
            return DrawingRectangle.Empty;
        }

        return new DrawingRectangle(x, y, clampedRight - x, clampedBottom - y);
    }

    /// <summary>
    /// Captures the current texture contents as a PNG.
    /// </summary>
    /// <param name="graphicsDevice">The owning graphics device.</param>
    /// <param name="commandList">The command list used for the copy.</param>
    /// <param name="source">The rendered source target.</param>
    /// <param name="path">The destination PNG path.</param>
    /// <param name="crop">An optional crop rectangle.</param>
    internal static void CapturePng(
        GraphicsDevice graphicsDevice,
        CommandList commandList,
        Texture source,
        string path,
        DrawingRectangle? crop = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(commandList);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var targetCrop = crop ?? new DrawingRectangle(
            0,
            0,
            checked((int)source.Width),
            checked((int)source.Height));
        targetCrop = DrawingRectangle.Intersect(
            targetCrop,
            new DrawingRectangle(0, 0, checked((int)source.Width), checked((int)source.Height)));
        if (targetCrop.Width <= 0 || targetCrop.Height <= 0)
        {
            throw new InvalidOperationException("The requested screenshot crop is empty.");
        }

        using var staging = graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                source.Width,
                source.Height,
                mipLevels: 1,
                arrayLayers: 1,
                source.Format,
                TextureUsage.Staging));

        commandList.Begin();
        commandList.CopyTexture(source, staging);
        commandList.End();
        graphicsDevice.SubmitCommands(commandList);
        graphicsDevice.WaitForIdle();

        var mapped = graphicsDevice.Map(staging, MapMode.Read);
        try
        {
            var pixels = ReadPixels(mapped, source.Format, targetCrop);
            WritePng(path, targetCrop.Width, targetCrop.Height, pixels);
        }
        finally
        {
            graphicsDevice.Unmap(staging);
        }
    }

    private static unsafe byte[] ReadPixels(
        MappedResource mapped,
        Veldrid.PixelFormat format,
        DrawingRectangle crop)
    {
        var pixels = new byte[checked(crop.Width * crop.Height * 4)];
        var sourceBase = (byte*)mapped.Data.ToPointer();
        var rowPitch = checked((int)mapped.RowPitch);
        var destinationOffset = 0;
        for (var y = 0; y < crop.Height; y++)
        {
            var sourceRow = sourceBase + ((crop.Y + y) * rowPitch) + (crop.X * 4);
            for (var x = 0; x < crop.Width; x++)
            {
                var sourceOffset = x * 4;
                if (format is Veldrid.PixelFormat.B8_G8_R8_A8_UNorm or
                    Veldrid.PixelFormat.B8_G8_R8_A8_UNorm_SRgb)
                {
                    pixels[destinationOffset++] = sourceRow[sourceOffset + 2];
                    pixels[destinationOffset++] = sourceRow[sourceOffset + 1];
                    pixels[destinationOffset++] = sourceRow[sourceOffset];
                    pixels[destinationOffset++] = sourceRow[sourceOffset + 3];
                }
                else
                {
                    pixels[destinationOffset++] = sourceRow[sourceOffset];
                    pixels[destinationOffset++] = sourceRow[sourceOffset + 1];
                    pixels[destinationOffset++] = sourceRow[sourceOffset + 2];
                    pixels[destinationOffset++] = sourceRow[sourceOffset + 3];
                }
            }
        }

        return pixels;
    }

    private static unsafe void WritePng(
        string path,
        int width,
        int height,
        byte[] rgbaPixels)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var bitmap = new System.Drawing.Bitmap(
            width,
            height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var rectangle = new DrawingRectangle(0, 0, width, height);
        var data = bitmap.LockBits(
            rectangle,
            ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var destinationBase = (byte*)data.Scan0.ToPointer();
            var sourceOffset = 0;
            for (var y = 0; y < height; y++)
            {
                var destinationRow = destinationBase + (y * data.Stride);
                for (var x = 0; x < width; x++)
                {
                    var destinationOffset = x * 4;
                    destinationRow[destinationOffset] = rgbaPixels[sourceOffset + 2];
                    destinationRow[destinationOffset + 1] = rgbaPixels[sourceOffset + 1];
                    destinationRow[destinationOffset + 2] = rgbaPixels[sourceOffset];
                    destinationRow[destinationOffset + 3] = rgbaPixels[sourceOffset + 3];
                    sourceOffset += 4;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        bitmap.Save(path, ImageFormat.Png);
    }
}
