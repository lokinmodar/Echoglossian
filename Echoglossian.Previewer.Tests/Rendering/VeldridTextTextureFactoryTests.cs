// <copyright file="VeldridTextTextureFactoryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Drawing;
using System.Drawing.Imaging;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

using Echoglossian.Previewer.Rendering;

using Xunit;

namespace Echoglossian.Previewer.Tests.Rendering;

/// <summary>
/// Covers Veldrid-ready pixel preparation for preview text textures.
/// </summary>
public sealed class VeldridTextTextureFactoryTests
{
    /// <summary>
    /// Ensures the bitmap's top-left RGBA pixels preserve row orientation and
    /// report the source dimensions for a Veldrid upload.
    /// </summary>
    [Fact]
    public void ConvertBitmapToRgba_PreservesTopLeftRowOrderAndDimensions()
    {
        using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 1, 2, 3));
        bitmap.SetPixel(1, 0, Color.FromArgb(255, 4, 5, 6));
        bitmap.SetPixel(0, 1, Color.FromArgb(255, 7, 8, 9));
        bitmap.SetPixel(1, 1, Color.FromArgb(255, 10, 11, 12));

        var upload = VeldridTextTextureFactory.ConvertBitmapToRgba(bitmap);

        Assert.Equal(2, upload.Width);
        Assert.Equal(2, upload.Height);
        Assert.Equal(
            new byte[]
            {
                1, 2, 3, 255,
                4, 5, 6, 255,
                7, 8, 9, 255,
                10, 11, 12, 255,
            },
            upload.Pixels);
    }

    /// <summary>
    /// Ensures transparent source pixels retain zero alpha in RGBA output.
    /// </summary>
    [Fact]
    public void ConvertBitmapToRgba_TransparentPixel_PreservesAlpha()
    {
        using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.FromArgb(0, 80, 90, 100));

        var upload = VeldridTextTextureFactory.ConvertBitmapToRgba(bitmap);

        Assert.Equal(new byte[] { 80, 90, 100, 0 }, upload.Pixels);
    }

    /// <summary>
    /// Ensures cancellation stops a prepared upload before it reaches the GPU
    /// creation delegate.
    /// </summary>
    [Fact]
    public async Task CreateTextureAsync_CancelledBeforeUpload_SkipsTextureCreation()
    {
        var uploadCount = 0;
        var factory = new VeldridTextTextureFactory(
            _ =>
            {
                Interlocked.Increment(ref uploadCount);
                return new FakeTextureWrap();
            });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var upload = new VeldridTextTextureUpload(
            1,
            1,
            new byte[] { 1, 2, 3, 4 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => factory.CreateTextureAsync(upload, cancellation.Token));

        Assert.Equal(0, Volatile.Read(ref uploadCount));
    }

    /// <summary>
    /// Ensures disposing a preview wrap releases its resources and removes
    /// the matching ImGui texture identifier exactly once.
    /// </summary>
    [Fact]
    public void PreviewTextureWrap_Dispose_ReleasesResourcesAndUnregistersTexture()
    {
        var resources = new FakeTextureResources();
        var registry = new FakeTextureRegistry();
        var texture = new PreviewTextureWrap(resources, registry);

        texture.Dispose();
        texture.Dispose();

        Assert.Equal(1, resources.DisposeCount);
        Assert.Equal(new nint(23), registry.UnregisteredTextureId);
    }

    /// <summary>
    /// Provides a minimal texture wrapper for upload cancellation tests.
    /// </summary>
    private sealed class FakeTextureWrap : IDalamudTextureWrap
    {
        public ImTextureID Handle => default;

        public int Width => 1;

        public int Height => 1;

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Tracks disposal without allocating a Veldrid device resource.
    /// </summary>
    private sealed class FakeTextureResources : IPreviewTextureResources
    {
        private int disposeCount;

        /// <summary>
        /// Gets the number of resource disposals.
        /// </summary>
        public int DisposeCount => Volatile.Read(ref this.disposeCount);

        public Veldrid.TextureView TextureView => null!;

        public int Width => 1;

        public int Height => 1;

        /// <inheritdoc />
        public void Dispose()
        {
            Interlocked.Increment(ref this.disposeCount);
        }
    }

    /// <summary>
    /// Tracks ImGui texture identifier release without a renderer instance.
    /// </summary>
    private sealed class FakeTextureRegistry : IPreviewTextureRegistry
    {
        /// <summary>
        /// Gets the unregistered texture identifier.
        /// </summary>
        public nint UnregisteredTextureId { get; private set; }

        /// <inheritdoc />
        public nint Register(Veldrid.TextureView textureView)
        {
            return 23;
        }

        /// <inheritdoc />
        public bool Unregister(nint textureId)
        {
            this.UnregisteredTextureId = textureId;
            return true;
        }
    }
}
