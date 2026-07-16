// <copyright file="PreviewTextureWrap.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

using Veldrid;

namespace Echoglossian.Previewer.Rendering;

/// <summary>
///     Wraps preview-owned Veldrid texture resources for Dalamud UI code.
/// </summary>
internal sealed class PreviewTextureWrap : IDalamudTextureWrap
{
    private readonly IPreviewTextureResources textureResources;
    private readonly IPreviewTextureRegistry registry;
    private readonly nint textureId;
    private int disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreviewTextureWrap" />
    ///     class.
    /// </summary>
    /// <param name="texture">The owned texture.</param>
    /// <param name="textureView">The owned texture view.</param>
    /// <param name="registry">The texture identifier registry.</param>
    internal PreviewTextureWrap(
        Texture texture,
        TextureView textureView,
        VeldridTextureRegistry registry)
        : this(
            new VeldridTextureResources(texture, textureView),
            registry)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PreviewTextureWrap" />
    ///     class with owned preview resources and texture registration.
    /// </summary>
    /// <param name="textureResources">The owned texture resources.</param>
    /// <param name="registry">The texture identifier registry.</param>
    internal PreviewTextureWrap(
        IPreviewTextureResources textureResources,
        IPreviewTextureRegistry registry)
    {
        this.textureResources = textureResources ??
            throw new ArgumentNullException(nameof(textureResources));
        this.registry = registry ??
            throw new ArgumentNullException(nameof(registry));
        this.textureId = this.registry.Register(this.textureResources.TextureView);
    }

    /// <inheritdoc />
    public ImTextureID Handle => new(this.textureId);

    /// <inheritdoc />
    public int Width => this.textureResources.Width;

    /// <inheritdoc />
    public int Height => this.textureResources.Height;

    /// <inheritdoc />
    public Vector2 Size => new(this.Width, this.Height);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.registry.Unregister(this.textureId);
        this.textureResources.Dispose();
    }

    /// <summary>
    ///     Owns the Veldrid texture resources used by one preview wrap.
    /// </summary>
    private sealed class VeldridTextureResources : IPreviewTextureResources
    {
        private readonly Texture texture;
        private readonly TextureView textureView;

        /// <summary>
        ///     Initializes a new instance of the
        ///     <see cref="VeldridTextureResources" /> class.
        /// </summary>
        /// <param name="texture">The owned sampled texture.</param>
        /// <param name="textureView">The owned texture view.</param>
        public VeldridTextureResources(Texture texture, TextureView textureView)
        {
            this.texture = texture ?? throw new ArgumentNullException(nameof(texture));
            this.textureView = textureView ??
                throw new ArgumentNullException(nameof(textureView));
        }

        /// <inheritdoc />
        public TextureView TextureView => this.textureView;

        /// <inheritdoc />
        public int Width => checked((int)this.texture.Width);

        /// <inheritdoc />
        public int Height => checked((int)this.texture.Height);

        /// <inheritdoc />
        public void Dispose()
        {
            this.textureView.Dispose();
            this.texture.Dispose();
        }
    }
}

/// <summary>
///     Registers and releases ImGui texture identifiers for preview resources.
/// </summary>
internal interface IPreviewTextureRegistry
{
    /// <summary>
    ///     Registers one texture view for ImGui rendering.
    /// </summary>
    /// <param name="textureView">The view to register.</param>
    /// <returns>The assigned ImGui texture identifier.</returns>
    nint Register(TextureView textureView);

    /// <summary>
    ///     Removes one ImGui texture registration.
    /// </summary>
    /// <param name="textureId">The texture identifier to remove.</param>
    /// <returns>Whether a registration was removed.</returns>
    bool Unregister(nint textureId);
}

/// <summary>
///     Owns the Veldrid resources and dimensions for one preview texture.
/// </summary>
internal interface IPreviewTextureResources : IDisposable
{
    /// <summary>
    ///     Gets the view submitted to the ImGui texture registry.
    /// </summary>
    TextureView TextureView { get; }

    /// <summary>
    ///     Gets the texture width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    ///     Gets the texture height in pixels.
    /// </summary>
    int Height { get; }
}
