// <copyright file="PreviewOverlayRendererFactory.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Runtime;
using Echoglossian.Previewer.Rendering;
using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

namespace Echoglossian.Previewer.Hosting;

/// <summary>
/// Creates preview overlay renderer instances with host-backed text texture
/// presentation.
/// </summary>
internal sealed class PreviewOverlayRendererFactory
{
    private readonly Func<VeldridTextTextureFactory> createTextTextureFactory;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PreviewOverlayRendererFactory"/> class.
    /// </summary>
    /// <param name="host">The preview host that owns the texture device.</param>
    internal PreviewOverlayRendererFactory(PreviewHost host)
        : this(host.CreateTextTextureFactory)
    {
    }

    /// <summary>
    /// Initializes a testable instance of the
    /// <see cref="PreviewOverlayRendererFactory"/> class.
    /// </summary>
    /// <param name="createTextTextureFactory">The host-bound texture factory creator.</param>
    internal PreviewOverlayRendererFactory(
        Func<VeldridTextTextureFactory> createTextTextureFactory)
    {
        this.createTextTextureFactory = createTextTextureFactory ??
            throw new ArgumentNullException(nameof(createTextTextureFactory));
    }

    /// <summary>
    /// Creates an owned preview renderer composition.
    /// </summary>
    /// <param name="configuration">The preview configuration.</param>
    /// <param name="fontRuntime">The preview ImGui font runtime.</param>
    /// <returns>The renderer and its owned RTL texture service.</returns>
    internal PreviewOverlayRendererComposition Create(
        Config configuration,
        IUiFontRuntime fontRuntime)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(fontRuntime);

        var textTextureFactory = this.createTextTextureFactory();
        var rtlTexturePresentationService = new RtlTexturePresentationService(
            configuration,
            textTextureFactory.CreateTextureAsync);
        var renderer = new TranslationOverlayRenderer(
            configuration,
            fontRuntime,
            rtlTexturePresentationService);
        return new PreviewOverlayRendererComposition(
            renderer,
            rtlTexturePresentationService);
    }
}

/// <summary>
/// Owns a preview translation overlay renderer and its preview texture service.
/// </summary>
internal sealed class PreviewOverlayRendererComposition : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PreviewOverlayRendererComposition"/> class.
    /// </summary>
    /// <param name="renderer">The composed overlay renderer.</param>
    /// <param name="rtlTexturePresentationService">The preview-backed RTL service.</param>
    internal PreviewOverlayRendererComposition(
        TranslationOverlayRenderer renderer,
        RtlTexturePresentationService rtlTexturePresentationService)
    {
        this.Renderer = renderer ??
            throw new ArgumentNullException(nameof(renderer));
        this.RtlTexturePresentationService = rtlTexturePresentationService ??
            throw new ArgumentNullException(nameof(rtlTexturePresentationService));
    }

    /// <summary>
    /// Gets the composed translation overlay renderer.
    /// </summary>
    internal TranslationOverlayRenderer Renderer { get; }

    /// <summary>
    /// Gets the preview-backed RTL texture presentation service.
    /// </summary>
    internal RtlTexturePresentationService RtlTexturePresentationService { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Renderer.Dispose();
        this.RtlTexturePresentationService.Dispose();
    }
}
