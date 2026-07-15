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
    private readonly Texture texture;
    private readonly TextureView textureView;
    private readonly VeldridTextureRegistry registry;
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
    {
        this.texture = texture ?? throw new ArgumentNullException(nameof(texture));
        this.textureView = textureView ??
            throw new ArgumentNullException(nameof(textureView));
        this.registry = registry ??
            throw new ArgumentNullException(nameof(registry));
        this.textureId = this.registry.Register(this.textureView);
    }

    /// <inheritdoc />
    public ImTextureID Handle => new(this.textureId);

    /// <inheritdoc />
    public int Width => checked((int)this.texture.Width);

    /// <inheritdoc />
    public int Height => checked((int)this.texture.Height);

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
        this.textureView.Dispose();
        this.texture.Dispose();
    }
}
