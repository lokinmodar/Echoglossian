// <copyright file="VeldridTextTextureFactoryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

using Echoglossian.LanguagesHandling;
using Echoglossian.PluginUI.Runtime;
using Echoglossian.Previewer.Fonts;
using Echoglossian.Previewer.Hosting;
using Echoglossian.Previewer.Rendering;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

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
    /// Ensures preview overlay composition supplies TranslationOverlayRenderer
    /// with an RTL service backed by the Veldrid texture factory path.
    /// </summary>
    [Fact]
    public void PreviewOverlayRendererFactory_Create_RtlRequestUsesVeldridTextureFactory()
    {
        var uploadCount = 0;
        VeldridTextTextureUpload? capturedUpload = null;
        TextureCreationRequest? capturedRequest = null;
        var fontSelection = PreviewFontCatalog.Resolve(
            new LanguageInfo(
                "ar",
                "Arabic",
                "NotoSansArabic-Medium.ttf",
                string.Empty,
                new List<int>()),
            18,
            FindRepositoryRoot());
        var rendererFactory = new PreviewOverlayRendererFactory(
            () => new VeldridTextTextureFactory(
                upload =>
                {
                    Interlocked.Increment(ref uploadCount);
                    capturedUpload = upload;
                    return new FakeTextureWrap(upload.Width, upload.Height);
                },
                request => capturedRequest = request));
        using var composition = rendererFactory.Create(
            new Config { FontSize = fontSelection.FontSize },
            new FakeUiFontRuntime(),
            fontSelection);
        var request = new TextLayoutRequest(
            "\u0645\u0631\u062d\u0628\u0627",
            2,
            "ar",
            480f,
            1f,
            ShouldUseGeneralFont: false,
            Vector4.One,
            Vector4.Zero,
            TranslationOverlaySurfaceId.Talk,
            CenterAligned: false);

        var renderedBlock = WaitForRenderedBlock(
            composition.RtlTexturePresentationService,
            request);
        renderedBlock.Texture!.Dispose();

        Assert.Equal(1, Volatile.Read(ref uploadCount));
        var upload = Assert.IsType<VeldridTextTextureUpload>(capturedUpload);
        Assert.True(upload.Width > 0);
        Assert.True(upload.Height > 0);
        Assert.NotNull(capturedRequest);
        Assert.Equal(fontSelection.SpecialFontPath, capturedRequest.FontPath);
    }

    /// <summary>
    /// Pumps draw-thread publication until the composed service returns a
    /// texture-backed render block.
    /// </summary>
    /// <param name="service">The preview-backed presentation service.</param>
    /// <param name="request">The text layout request.</param>
    /// <returns>The completed render block.</returns>
    private static RenderedTextBlock WaitForRenderedBlock(
        RtlTexturePresentationService service,
        TextLayoutRequest request)
    {
        RenderedTextBlock? renderedBlock = null;
        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                service.BeginDrawFrame();
                renderedBlock = service.TryRender(request);
                return renderedBlock != null;
            },
            TimeSpan.FromSeconds(5)));
        return renderedBlock!;
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Echoglossian repository root.");
    }

    /// <summary>
    /// Provides a minimal texture wrapper for upload cancellation tests.
    /// </summary>
    private sealed class FakeTextureWrap : IDalamudTextureWrap
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FakeTextureWrap"/> class.
        /// </summary>
        public FakeTextureWrap()
            : this(1, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeTextureWrap"/> class.
        /// </summary>
        /// <param name="width">The fake texture width.</param>
        /// <param name="height">The fake texture height.</param>
        public FakeTextureWrap(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        public ImTextureID Handle => default;

        public int Width { get; }

        public int Height { get; }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Provides a no-op font runtime for tests that do not enter ImGui draw.
    /// </summary>
    private sealed class FakeUiFontRuntime : IUiFontRuntime
    {
        /// <inheritdoc/>
        public IDisposable Push(UiFontKind fontKind)
        {
            return EmptyScope.Instance;
        }
    }

    /// <summary>
    /// Represents a no-op disposable scope.
    /// </summary>
    private sealed class EmptyScope : IDisposable
    {
        /// <summary>
        /// Gets the shared empty scope instance.
        /// </summary>
        public static EmptyScope Instance { get; } = new();

        /// <inheritdoc/>
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
