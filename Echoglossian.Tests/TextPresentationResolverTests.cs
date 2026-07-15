// <copyright file="TextPresentationResolverTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.UIOverlays.TextPresentation;
using Echoglossian.UIOverlays.TranslationOverlay;

using System.Numerics;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers backend selection for text presentation.
/// </summary>
public class TextPresentationResolverTests
{
    /// <summary>
    /// Ensures plain LTR requests keep using the plain ImGui backend.
    /// </summary>
    [Fact]
    public void Resolve_EnglishRequest_UsesPlainImGuiBackend()
    {
        var request = new TextLayoutRequest(
            "Hello world",
            28,
            "en",
            400f,
            1.0f,
            false,
            Vector4.One,
            new Vector4(0f, 0f, 0f, 0f),
            TranslationOverlaySurfaceId.Talk,
            false);

        Assert.Equal(
            TextPresentationBackendKind.PlainImGui,
            TextPresentationResolver.ResolveBackendKind(request));
    }

    /// <summary>
    /// Ensures RTL requests use the texture backend.
    /// </summary>
    [Fact]
    public void Resolve_ArabicRequest_UsesRtlTextureBackend()
    {
        var request = new TextLayoutRequest(
            "مرحبا بالعالم",
            2,
            "ar",
            400f,
            1.0f,
            false,
            Vector4.One,
            new Vector4(0f, 0f, 0f, 0f),
            TranslationOverlaySurfaceId.Talk,
            false);

        Assert.Equal(
            TextPresentationBackendKind.RtlTexture,
            TextPresentationResolver.ResolveBackendKind(request));
    }

    /// <summary>
    /// Ensures script-sensitive non-RTL requests still use the texture backend.
    /// </summary>
    [Fact]
    public void Resolve_AzerbaijaniRequest_UsesTextureBackend()
    {
        var request = new TextLayoutRequest(
            "Salam dunya",
            6,
            "az",
            400f,
            1.0f,
            false,
            Vector4.One,
            new Vector4(0f, 0f, 0f, 0f),
            TranslationOverlaySurfaceId.Talk,
            false);

        Assert.Equal(
            TextPresentationBackendKind.RtlTexture,
            TextPresentationResolver.ResolveBackendKind(request));
    }
}
