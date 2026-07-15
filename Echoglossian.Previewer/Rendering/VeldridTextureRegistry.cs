// <copyright file="VeldridTextureRegistry.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Veldrid;

namespace Echoglossian.Previewer.Rendering;

/// <summary>
///     Maps stable ImGui texture identifiers to Veldrid texture views.
/// </summary>
internal sealed class VeldridTextureRegistry : IPreviewTextureRegistry
{
    private readonly object syncRoot = new();
    private readonly Dictionary<nint, TextureView> textureViews = [];
    private long nextTextureId = 1;

    /// <summary>
    ///     Occurs after a texture identifier has been removed.
    /// </summary>
    internal event Action<nint>? TextureUnregistered;

    /// <summary>
    ///     Registers a texture view under a new non-zero identifier.
    /// </summary>
    /// <param name="textureView">The texture view to register.</param>
    /// <returns>The identifier to pass to ImGui.</returns>
    public nint Register(TextureView textureView)
    {
        ArgumentNullException.ThrowIfNull(textureView);

        lock (this.syncRoot)
        {
            nint textureId = checked((nint)this.nextTextureId);
            this.nextTextureId = checked(this.nextTextureId + 1);
            this.textureViews.Add(textureId, textureView);
            return textureId;
        }
    }

    /// <summary>
    ///     Removes a registered texture identifier.
    /// </summary>
    /// <param name="textureId">The identifier to remove.</param>
    /// <returns>
    ///     <see langword="true" /> if the identifier was removed; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public bool Unregister(nint textureId)
    {
        bool removed;

        lock (this.syncRoot)
        {
            removed = this.textureViews.Remove(textureId);
        }

        if (removed)
        {
            this.TextureUnregistered?.Invoke(textureId);
        }

        return removed;
    }

    /// <summary>
    ///     Resolves a registered identifier to its texture view.
    /// </summary>
    /// <param name="textureId">The identifier to resolve.</param>
    /// <returns>The registered texture view.</returns>
    /// <exception cref="InvalidOperationException">
    ///     The identifier is zero or is not registered.
    /// </exception>
    public TextureView Resolve(nint textureId)
    {
        lock (this.syncRoot)
        {
            if (textureId != 0 &&
                this.textureViews.TryGetValue(textureId, out TextureView? view))
            {
                return view;
            }
        }

        throw new InvalidOperationException(
            $"No Veldrid texture is registered for ImGui texture ID {textureId}.");
    }
}
