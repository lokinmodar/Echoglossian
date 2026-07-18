// <copyright file="PreviewHost.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.Previewer.Rendering;
using Echoglossian.Previewer.Screenshots;

using DrawingRectangle = System.Drawing.Rectangle;

using System.Numerics;

using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Echoglossian.Previewer.Hosting;

/// <summary>
///     Owns a standalone Veldrid window, graphics device, and ImGui context.
/// </summary>
internal sealed unsafe class PreviewHost : IDisposable
{
    private readonly Sdl2Window window;
    private readonly GraphicsDevice graphicsDevice;
    private readonly CommandList commandList;
    private readonly VeldridTextureRegistry textureRegistry;
    private readonly VeldridImGuiRenderer imGuiRenderer;
    private readonly ImGuiContextPtr context;
    private DateTime lastFrame = DateTime.UtcNow;
    private int disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreviewHost" /> class.
    /// </summary>
    /// <param name="options">The host creation options.</param>
    internal PreviewHost(PreviewHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.context = ImGui.CreateContext();
        ImGui.SetCurrentContext(this.context);
        ImGuiIOPtr io = ImGui.GetIO();
        io.IniFilename = null;

        var windowCreateInfo = new WindowCreateInfo(
            100,
            100,
            options.Width,
            options.Height,
            WindowState.Normal,
            options.Title);

        this.window = VeldridStartup.CreateWindow(ref windowCreateInfo);
        if (options.StartHidden)
        {
            this.window.Visible = false;
        }

        var graphicsDeviceOptions = new GraphicsDeviceOptions(
            debug: false,
            swapchainDepthFormat: null,
            syncToVerticalBlank: false,
            resourceBindingModel: ResourceBindingModel.Improved,
            preferStandardClipSpaceYDirection: true,
            preferDepthRangeZeroToOne: true);

        this.graphicsDevice = VeldridStartup.CreateGraphicsDevice(
            this.window,
            graphicsDeviceOptions,
            GraphicsBackend.Direct3D11);
        this.commandList = this.graphicsDevice.ResourceFactory.CreateCommandList();
        this.textureRegistry = new VeldridTextureRegistry();
        this.imGuiRenderer = new VeldridImGuiRenderer(
            this.graphicsDevice,
            this.graphicsDevice.MainSwapchain.Framebuffer.OutputDescription,
            this.textureRegistry,
            options.Width,
            options.Height);
    }

    /// <summary>
    ///     Draws and presents exactly one ImGui frame.
    /// </summary>
    /// <param name="draw">The ImGui draw callback.</param>
    internal void RunFrame(Action draw)
    {
        this.RunFrame(draw, beforePresent: null);
    }

    /// <summary>
    ///     Draws exactly one ImGui frame and optionally captures it before
    ///     swapchain presentation.
    /// </summary>
    /// <param name="draw">The ImGui draw callback.</param>
    /// <param name="beforePresent">An optional callback invoked before presentation.</param>
    internal void RunFrame(Action draw, Action? beforePresent)
    {
        ArgumentNullException.ThrowIfNull(draw);
        this.ThrowIfDisposed();

        var snapshot = this.window.PumpEvents();
        this.imGuiRenderer.Update(
            this.GetDeltaSeconds(),
            snapshot,
            this.window.Width,
            this.window.Height,
            this.window.Focused);

        draw();

        this.commandList.Begin();
        this.commandList.SetFramebuffer(this.graphicsDevice.MainSwapchain.Framebuffer);
        this.commandList.ClearColorTarget(0, RgbaFloat.Black);
        this.imGuiRenderer.Render(this.commandList);
        this.commandList.End();

        this.graphicsDevice.SubmitCommands(this.commandList);
        // Readback paths synchronize during capture; the interactive loop should
        // stay asynchronous so preview rendering can run at full frame rate.
        beforePresent?.Invoke();
        this.graphicsDevice.SwapBuffers(this.graphicsDevice.MainSwapchain);
    }

    /// <summary>
    ///     Captures the currently rendered frame to a PNG file.
    /// </summary>
    /// <param name="path">The destination PNG path.</param>
    /// <param name="crop">An optional physical pixel crop rectangle.</param>
    internal void CapturePng(string path, DrawingRectangle? crop = null)
    {
        this.ThrowIfDisposed();
        var colorTarget = this.graphicsDevice.MainSwapchain.Framebuffer.ColorTargets[0].Target;
        VeldridScreenshotCapture.CapturePng(
            this.graphicsDevice,
            this.commandList,
            colorTarget,
            path,
            crop);
    }

    /// <summary>
    /// Gets the current physical swapchain framebuffer size in pixels.
    /// </summary>
    internal Vector2 FramebufferSize
    {
        get
        {
            this.ThrowIfDisposed();
            return new Vector2(
                this.graphicsDevice.MainSwapchain.Framebuffer.Width,
                this.graphicsDevice.MainSwapchain.Framebuffer.Height);
        }
    }

    /// <summary>
    ///     Renders one frame into an offscreen target and captures it as PNG.
    /// </summary>
    /// <param name="draw">The ImGui draw callback.</param>
    /// <param name="path">The destination PNG path.</param>
    /// <param name="cropProvider">An optional provider for a physical pixel crop rectangle.</param>
    internal void CaptureFramePng(
        Action draw,
        string path,
        Func<Vector2, DrawingRectangle?>? cropProvider = null)
    {
        ArgumentNullException.ThrowIfNull(draw);
        this.ThrowIfDisposed();

        var snapshot = this.window.PumpEvents();
        this.imGuiRenderer.Update(
            this.GetDeltaSeconds(),
            snapshot,
            this.window.Width,
            this.window.Height,
            this.window.Focused);

        draw();

        var outputDescription = this.graphicsDevice.MainSwapchain.Framebuffer.OutputDescription;
        using var colorTarget = this.graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                checked((uint)this.window.Width),
                checked((uint)this.window.Height),
                mipLevels: 1,
                arrayLayers: 1,
                outputDescription.ColorAttachments[0].Format,
                TextureUsage.RenderTarget | TextureUsage.Sampled));
        using var framebuffer = this.graphicsDevice.ResourceFactory.CreateFramebuffer(
            new FramebufferDescription(null, colorTarget));
        var crop = cropProvider?.Invoke(new Vector2(colorTarget.Width, colorTarget.Height));

        this.commandList.Begin();
        this.commandList.SetFramebuffer(framebuffer);
        this.commandList.ClearColorTarget(0, RgbaFloat.Black);
        this.imGuiRenderer.Render(this.commandList);
        this.commandList.End();

        this.graphicsDevice.SubmitCommands(this.commandList);
        this.graphicsDevice.WaitForIdle();
        VeldridScreenshotCapture.CapturePng(
            this.graphicsDevice,
            this.commandList,
            colorTarget,
            path,
            crop);
    }

    /// <summary>
    ///     Runs frames until the standalone preview window is closed.
    /// </summary>
    /// <param name="draw">The ImGui draw callback.</param>
    internal void Run(Action draw)
    {
        this.Run(draw, beforePresent: null);
    }

    /// <summary>
    ///     Runs frames until the standalone preview window is closed.
    /// </summary>
    /// <param name="draw">The ImGui draw callback.</param>
    /// <param name="beforePresent">An optional callback invoked before each frame is presented.</param>
    internal void Run(Action draw, Action? beforePresent)
    {
        this.Run(draw, beforePresent, continueRunning: null);
    }

    /// <summary>
    ///     Runs frames until the preview window closes or the caller requests a controlled loop exit.
    /// </summary>
    /// <param name="draw">The ImGui draw callback.</param>
    /// <param name="beforePresent">An optional callback invoked before each frame is presented.</param>
    /// <param name="continueRunning">An optional callback that controls whether another frame should run.</param>
    internal void Run(Action draw, Action? beforePresent, Func<bool>? continueRunning)
    {
        ArgumentNullException.ThrowIfNull(draw);
        this.ThrowIfDisposed();

        while (this.window.Exists && (continueRunning?.Invoke() ?? true))
        {
            this.RunFrame(draw, beforePresent);
        }
    }

    /// <summary>
    ///     Rebuilds the backend font texture after preview fonts are added to
    ///     the active ImGui atlas.
    /// </summary>
    internal void RecreateFontDeviceTexture()
    {
        this.ThrowIfDisposed();
        this.imGuiRenderer.RecreateFontDeviceTexture();
    }

    /// <summary>
    ///     Creates a text texture factory bound to this host's Veldrid device
    ///     and ImGui texture registry.
    /// </summary>
    /// <returns>The host-bound text texture factory.</returns>
    internal VeldridTextTextureFactory CreateTextTextureFactory()
    {
        this.ThrowIfDisposed();
        return new VeldridTextTextureFactory(
            this.graphicsDevice,
            this.textureRegistry);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.imGuiRenderer.Dispose();
        this.commandList.Dispose();
        this.graphicsDevice.Dispose();
        this.window.Close();
        ImGui.DestroyContext(this.context);
    }

    private void ThrowIfDisposed()
    {
        if (this.disposed != 0)
        {
            throw new ObjectDisposedException(nameof(PreviewHost));
        }
    }

    private float GetDeltaSeconds()
    {
        var now = DateTime.UtcNow;
        var delta = Math.Max((float)(now - this.lastFrame).TotalSeconds, 1 / 1000f);
        this.lastFrame = now;
        return delta;
    }
}
