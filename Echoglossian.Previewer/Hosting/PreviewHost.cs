// <copyright file="PreviewHost.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Bindings.ImGui;

using Echoglossian.Previewer.Rendering;

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
        this.graphicsDevice.SwapBuffers(this.graphicsDevice.MainSwapchain);
        this.graphicsDevice.WaitForIdle();
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
