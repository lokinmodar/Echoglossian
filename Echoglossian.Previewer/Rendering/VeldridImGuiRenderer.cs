// <copyright file="VeldridImGuiRenderer.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dalamud.Bindings.ImGui;

using Veldrid;
using Veldrid.Sdl2;
using Veldrid.SPIRV;

namespace Echoglossian.Previewer.Rendering;

/// <summary>
///     Renders draw data produced by <see cref="ImGui" /> with Veldrid.
/// </summary>
internal sealed unsafe class VeldridImGuiRenderer : IDisposable
{
    private const uint InitialVertexBufferSize = 10000;
    private const uint InitialIndexBufferSize = 2000;
    private const uint ProjectionBufferSize = 64;
    private const ulong FontTextureId = ulong.MaxValue;

    private readonly GraphicsDevice graphicsDevice;
    private readonly ResourceFactory factory;
    private readonly VeldridTextureRegistry textureRegistry;
    private readonly Dictionary<nint, ResourceSet> textureResourceSets = [];
    private readonly Sampler sampler;
    private readonly DeviceBuffer projectionBuffer;
    private readonly ResourceLayout projectionLayout;
    private readonly ResourceLayout textureLayout;
    private readonly ResourceSet projectionResourceSet;
    private readonly Shader[] shaders;
    private readonly Pipeline pipeline;
    private Texture? fontTexture;
    private TextureView? fontTextureView;
    private ResourceSet? fontTextureResourceSet;
    private DeviceBuffer vertexBuffer;
    private DeviceBuffer indexBuffer;
    private uint vertexBufferSize = InitialVertexBufferSize;
    private uint indexBufferSize = InitialIndexBufferSize;
    private DateTime lastFrame = DateTime.UtcNow;
    private int disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VeldridImGuiRenderer" />
    ///     class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="outputDescription">The framebuffer output description.</param>
    /// <param name="textureRegistry">The preview texture registry.</param>
    /// <param name="width">The initial display width.</param>
    /// <param name="height">The initial display height.</param>
    internal VeldridImGuiRenderer(
        GraphicsDevice graphicsDevice,
        OutputDescription outputDescription,
        VeldridTextureRegistry textureRegistry,
        int width,
        int height)
    {
        this.graphicsDevice = graphicsDevice ??
            throw new ArgumentNullException(nameof(graphicsDevice));
        this.factory = graphicsDevice.ResourceFactory;
        this.textureRegistry = textureRegistry ??
            throw new ArgumentNullException(nameof(textureRegistry));

        this.vertexBuffer = this.factory.CreateBuffer(
            new BufferDescription(
                InitialVertexBufferSize,
                BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        this.indexBuffer = this.factory.CreateBuffer(
            new BufferDescription(
                InitialIndexBufferSize,
                BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        this.projectionBuffer = this.factory.CreateBuffer(
            new BufferDescription(
                ProjectionBufferSize,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        this.sampler = this.factory.CreateSampler(
            new SamplerDescription(
                SamplerAddressMode.Clamp,
                SamplerAddressMode.Clamp,
                SamplerAddressMode.Clamp,
                SamplerFilter.MinLinear_MagLinear_MipLinear,
                null,
                0,
                0,
                0,
                0,
                SamplerBorderColor.TransparentBlack));
        this.projectionLayout = this.factory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription(
                    "ProjectionMatrixBuffer",
                    ResourceKind.UniformBuffer,
                    ShaderStages.Vertex),
                new ResourceLayoutElementDescription(
                    "MainSampler",
                    ResourceKind.Sampler,
                    ShaderStages.Fragment)));
        this.textureLayout = this.factory.CreateResourceLayout(
            new ResourceLayoutDescription(
                new ResourceLayoutElementDescription(
                    "MainTexture",
                    ResourceKind.TextureReadOnly,
                    ShaderStages.Fragment)));
        this.projectionResourceSet = this.factory.CreateResourceSet(
            new ResourceSetDescription(
                this.projectionLayout,
                this.projectionBuffer,
                this.sampler));
        this.shaders = this.CreateShaders();
        this.pipeline = this.CreatePipeline(outputDescription);

        this.textureRegistry.TextureUnregistered += this.OnTextureUnregistered;
        this.SetPerFrameImGuiData(width, height, 1 / 60f);
        this.RecreateFontDeviceTexture();
    }

    /// <summary>
    ///     Updates ImGui input and starts a new frame.
    /// </summary>
    /// <param name="deltaSeconds">The elapsed time in seconds.</param>
    /// <param name="snapshot">The current input snapshot.</param>
    /// <param name="width">The current framebuffer width.</param>
    /// <param name="height">The current framebuffer height.</param>
    /// <param name="focused">A value indicating whether the host window is focused.</param>
    internal void Update(
        float deltaSeconds,
        InputSnapshot snapshot,
        int width,
        int height,
        bool focused)
    {
        this.ThrowIfDisposed();

        ImGuiIOPtr io = ImGui.GetIO();
        this.SetPerFrameImGuiData(
            width,
            height,
            deltaSeconds > 0 ? deltaSeconds : this.GetDeltaSeconds());

        this.UpdateInput(snapshot, focused);
        ImGui.NewFrame();
    }

    /// <summary>
    ///     Renders the current ImGui frame.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="commandList">The command list receiving draw commands.</param>
    internal void Render(GraphicsDevice graphicsDevice, CommandList commandList)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(commandList);
        this.ThrowIfDisposed();

        ImGui.Render();
        this.RenderImDrawData(ImGui.GetDrawData(), commandList);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.textureRegistry.TextureUnregistered -= this.OnTextureUnregistered;
        foreach (var resourceSet in this.textureResourceSets.Values)
        {
            resourceSet.Dispose();
        }

        this.textureResourceSets.Clear();
        this.fontTextureResourceSet?.Dispose();
        this.fontTextureView?.Dispose();
        this.fontTexture?.Dispose();
        this.pipeline.Dispose();
        foreach (var shader in this.shaders)
        {
            shader.Dispose();
        }

        this.projectionResourceSet.Dispose();
        this.textureLayout.Dispose();
        this.projectionLayout.Dispose();
        this.sampler.Dispose();
        this.projectionBuffer.Dispose();
        this.indexBuffer.Dispose();
        this.vertexBuffer.Dispose();
    }

    private void RenderImDrawData(ImDrawDataPtr drawData, CommandList commandList)
    {
        if (drawData.CmdListsCount == 0)
        {
            return;
        }

        uint totalVertexSize = checked((uint)(drawData.TotalVtxCount *
            Unsafe.SizeOf<ImDrawVert>()));
        if (totalVertexSize > this.vertexBufferSize)
        {
            this.vertexBuffer.Dispose();
            this.vertexBufferSize = (uint)Math.Max(
                this.vertexBufferSize * 1.5f,
                totalVertexSize);
            this.vertexBuffer = this.factory.CreateBuffer(
                new BufferDescription(
                    this.vertexBufferSize,
                    BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        }

        uint totalIndexSize = checked((uint)(drawData.TotalIdxCount *
            sizeof(ushort)));
        if (totalIndexSize > this.indexBufferSize)
        {
            this.indexBuffer.Dispose();
            this.indexBufferSize = (uint)Math.Max(
                this.indexBufferSize * 1.5f,
                totalIndexSize);
            this.indexBuffer = this.factory.CreateBuffer(
                new BufferDescription(
                    this.indexBufferSize,
                    BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        }

        var vertexOffset = 0u;
        var indexOffset = 0u;
        for (var listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
        {
            ImDrawListPtr commandListPtr = drawData.CmdLists[listIndex];
            ImDrawVert* vertexData = commandListPtr.VtxBuffer.Data;
            ushort* indexData = commandListPtr.IdxBuffer.Data;
            commandList.UpdateBuffer(
                this.vertexBuffer,
                vertexOffset,
                (IntPtr)vertexData,
                checked((uint)(commandListPtr.VtxBuffer.Size *
                    Unsafe.SizeOf<ImDrawVert>())));
            commandList.UpdateBuffer(
                this.indexBuffer,
                indexOffset,
                (IntPtr)indexData,
                checked((uint)(commandListPtr.IdxBuffer.Size * sizeof(ushort))));
            vertexOffset += checked((uint)(commandListPtr.VtxBuffer.Size *
                Unsafe.SizeOf<ImDrawVert>()));
            indexOffset += checked((uint)(commandListPtr.IdxBuffer.Size *
                sizeof(ushort)));
        }

        var scale = drawData.FramebufferScale;
        var width = (uint)(drawData.DisplaySize.X * scale.X);
        var height = (uint)(drawData.DisplaySize.Y * scale.Y);
        if (width == 0 || height == 0)
        {
            return;
        }

        this.UpdateProjectionMatrix(drawData);

        commandList.SetVertexBuffer(0, this.vertexBuffer);
        commandList.SetIndexBuffer(this.indexBuffer, IndexFormat.UInt16);
        commandList.SetPipeline(this.pipeline);
        commandList.SetGraphicsResourceSet(0, this.projectionResourceSet);

        var vertexBase = 0;
        var indexBase = 0u;
        var clipOffset = drawData.DisplayPos;

        for (var listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
        {
            ImDrawListPtr commandListPtr = drawData.CmdLists[listIndex];

            for (var commandIndex = 0; commandIndex < commandListPtr.CmdBuffer.Size; commandIndex++)
            {
                ImDrawCmd drawCommand = commandListPtr.CmdBuffer[commandIndex];
                if (drawCommand.UserCallback != null)
                {
                    throw new NotSupportedException(
                        "The preview ImGui renderer does not support ImDrawCmd user callbacks.");
                }

                var clipRect = drawCommand.ClipRect;
                var left = (uint)Math.Max((clipRect.X - clipOffset.X) * scale.X, 0);
                var top = (uint)Math.Max((clipRect.Y - clipOffset.Y) * scale.Y, 0);
                var right = (uint)Math.Min((clipRect.Z - clipOffset.X) * scale.X, width);
                var bottom = (uint)Math.Min((clipRect.W - clipOffset.Y) * scale.Y, height);
                if (right <= left || bottom <= top)
                {
                    continue;
                }

                commandList.SetScissorRect(0, left, top, right - left, bottom - top);
                commandList.SetGraphicsResourceSet(
                    1,
                    this.GetTextureResourceSet(drawCommand.TextureId));
                commandList.DrawIndexed(
                    drawCommand.ElemCount,
                    1,
                    drawCommand.IdxOffset + indexBase,
                    (int)drawCommand.VtxOffset + vertexBase,
                    0);
            }

            indexBase += checked((uint)commandListPtr.IdxBuffer.Size);
            vertexBase += commandListPtr.VtxBuffer.Size;
        }
    }

    private ResourceSet GetTextureResourceSet(ImTextureID textureId)
    {
        ulong rawId = textureId.Handle;
        if (rawId == FontTextureId)
        {
            return this.fontTextureResourceSet ??
                throw new InvalidOperationException("The ImGui font texture is unavailable.");
        }

        nint id = checked((nint)rawId);
        if (this.textureResourceSets.TryGetValue(id, out ResourceSet? resourceSet))
        {
            return resourceSet;
        }

        TextureView textureView = this.textureRegistry.Resolve(id);
        resourceSet = this.factory.CreateResourceSet(
            new ResourceSetDescription(this.textureLayout, textureView));
        this.textureResourceSets.Add(id, resourceSet);
        return resourceSet;
    }

    private void RecreateFontDeviceTexture()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        byte* pixels = null;
        var width = 0;
        var height = 0;
        var bytesPerPixel = 0;
        io.Fonts.GetTexDataAsRGBA32(
            0,
            ref pixels,
            ref width,
            ref height,
            ref bytesPerPixel);

        this.fontTextureResourceSet?.Dispose();
        this.fontTextureView?.Dispose();
        this.fontTexture?.Dispose();

        this.fontTexture = this.factory.CreateTexture(
            TextureDescription.Texture2D(
                checked((uint)width),
                checked((uint)height),
                mipLevels: 1,
                arrayLayers: 1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled));
        this.graphicsDevice.UpdateTexture(
            this.fontTexture,
            (IntPtr)pixels,
            checked((uint)(width * height * bytesPerPixel)),
            0,
            0,
            0,
            checked((uint)width),
            checked((uint)height),
            1,
            0,
            0);
        this.fontTextureView = this.factory.CreateTextureView(this.fontTexture);
        this.fontTextureResourceSet = this.factory.CreateResourceSet(
            new ResourceSetDescription(this.textureLayout, this.fontTextureView));
        io.Fonts.SetTexID(0, new ImTextureID(FontTextureId));
        io.Fonts.ClearTexData();
    }

    private Pipeline CreatePipeline(OutputDescription outputDescription)
    {
        var vertexLayouts = new[]
        {
            new VertexLayoutDescription(
                checked((uint)Unsafe.SizeOf<ImDrawVert>()),
                new VertexElementDescription(
                    "in_position",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float2),
                new VertexElementDescription(
                    "in_texCoord",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Float2),
                new VertexElementDescription(
                    "in_color",
                    VertexElementSemantic.TextureCoordinate,
                    VertexElementFormat.Byte4_Norm)),
        };

        var pipelineDescription = new GraphicsPipelineDescription(
            BlendStateDescription.SingleAlphaBlend,
            new DepthStencilStateDescription(
                depthTestEnabled: false,
                depthWriteEnabled: false,
                comparisonKind: ComparisonKind.Always),
            new RasterizerStateDescription(
                FaceCullMode.None,
                PolygonFillMode.Solid,
                FrontFace.Clockwise,
                depthClipEnabled: true,
                scissorTestEnabled: true),
            PrimitiveTopology.TriangleList,
            new ShaderSetDescription(vertexLayouts, this.shaders),
            new[] { this.projectionLayout, this.textureLayout },
            outputDescription);

        return this.factory.CreateGraphicsPipeline(pipelineDescription);
    }

    private Shader[] CreateShaders()
    {
        byte[] vertexBytes = LoadEmbeddedShader("imgui.vert.glsl");
        byte[] fragmentBytes = LoadEmbeddedShader("imgui.frag.glsl");

        var descriptions = new[]
        {
            new ShaderDescription(
                ShaderStages.Vertex,
                vertexBytes,
                "main"),
            new ShaderDescription(
                ShaderStages.Fragment,
                fragmentBytes,
                "main"),
        };

        return this.factory.CreateFromSpirv(descriptions[0], descriptions[1]);
    }

    private static byte[] LoadEmbeddedShader(string fileName)
    {
        Assembly assembly = typeof(VeldridImGuiRenderer).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException(
                $"Embedded shader resource was not found: {fileName}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private void SetPerFrameImGuiData(int width, int height, float deltaSeconds)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new Vector2(width, height);
        io.DisplayFramebufferScale = Vector2.One;
        io.DeltaTime = deltaSeconds;
    }

    private void UpdateInput(InputSnapshot snapshot, bool focused)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddFocusEvent(focused);
        Vector2 mousePosition = snapshot.MousePosition;
        io.AddMousePosEvent(mousePosition.X, mousePosition.Y);
        io.AddMouseWheelEvent(snapshot.WheelDelta, 0);

        for (var button = 0; button < 5; button++)
        {
            io.AddMouseButtonEvent(button, snapshot.IsMouseDown((MouseButton)button));
        }

        foreach (var character in snapshot.KeyCharPresses)
        {
            io.AddInputCharacter(character);
        }

        HashSet<Key> pressedKeys = [];
        foreach (KeyEvent keyEvent in snapshot.KeyEvents)
        {
            if (keyEvent.Down)
            {
                pressedKeys.Add(keyEvent.Key);
            }

            ImGuiKey key = MapKey(keyEvent.Key);
            if (key != ImGuiKey.None)
            {
                io.AddKeyEvent(key, keyEvent.Down);
            }
        }

        io.AddKeyEvent(ImGuiKey.ModCtrl, pressedKeys.Contains(Key.ControlLeft) ||
            pressedKeys.Contains(Key.ControlRight));
        io.AddKeyEvent(ImGuiKey.ModShift, pressedKeys.Contains(Key.ShiftLeft) ||
            pressedKeys.Contains(Key.ShiftRight));
        io.AddKeyEvent(ImGuiKey.ModAlt, pressedKeys.Contains(Key.AltLeft) ||
            pressedKeys.Contains(Key.AltRight));
        io.AddKeyEvent(ImGuiKey.ModSuper, pressedKeys.Contains(Key.WinLeft) ||
            pressedKeys.Contains(Key.WinRight));
    }

    private static ImGuiKey MapKey(Key key)
    {
        return key switch
        {
            Key.Tab => ImGuiKey.Tab,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.Delete => ImGuiKey.Delete,
            Key.BackSpace => ImGuiKey.Backspace,
            Key.Enter => ImGuiKey.Enter,
            Key.Escape => ImGuiKey.Escape,
            Key.A => ImGuiKey.A,
            Key.C => ImGuiKey.C,
            Key.V => ImGuiKey.V,
            Key.X => ImGuiKey.X,
            Key.Y => ImGuiKey.Y,
            Key.Z => ImGuiKey.Z,
            _ => ImGuiKey.None,
        };
    }

    private void UpdateProjectionMatrix(ImDrawDataPtr drawData)
    {
        var left = drawData.DisplayPos.X;
        var right = drawData.DisplayPos.X + drawData.DisplaySize.X;
        var top = drawData.DisplayPos.Y;
        var bottom = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        var matrix = new Matrix4x4(
            2.0f / (right - left),
            0,
            0,
            0,
            0,
            2.0f / (top - bottom),
            0,
            0,
            0,
            0,
            -1,
            0,
            (right + left) / (left - right),
            (top + bottom) / (bottom - top),
            0,
            1);

        this.graphicsDevice.UpdateBuffer(this.projectionBuffer, 0, ref matrix);
    }

    private float GetDeltaSeconds()
    {
        var now = DateTime.UtcNow;
        var delta = Math.Max((float)(now - this.lastFrame).TotalSeconds, 1 / 60f);
        this.lastFrame = now;
        return delta;
    }

    private void OnTextureUnregistered(nint textureId)
    {
        if (this.textureResourceSets.Remove(textureId, out ResourceSet? resourceSet))
        {
            resourceSet.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (this.disposed != 0)
        {
            throw new ObjectDisposedException(nameof(VeldridImGuiRenderer));
        }
    }
}
