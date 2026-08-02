// <copyright file="DistanceAwareOverlayRendererContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the shared renderer contract for runtime overlay presentation modifiers.
/// </summary>
public sealed class DistanceAwareOverlayRendererContractTests
{
    /// <summary>
    /// Ensures runtime presentation state is retained by an overlay.
    /// </summary>
    [Fact]
    public void TranslationOverlay_declares_runtime_scale_and_alpha_modifiers()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "UIOverlays",
            "TranslationOverlay",
            "TranslationOverlay.cs"));

        Assert.Contains("public float RenderScale", source, StringComparison.Ordinal);
        Assert.Contains("public float RenderAlpha", source, StringComparison.Ordinal);
        Assert.Contains("UpdateRuntimePresentation(", source, StringComparison.Ordinal);
        Assert.Contains("ClearRuntimePresentation()", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a render request carries runtime scale and alpha modifiers.
    /// </summary>
    [Fact]
    public void TranslationOverlayRenderRequest_carries_runtime_modifiers()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "UIOverlays",
            "TranslationOverlay",
            "TranslationOverlayRenderRequest.cs"));

        Assert.Contains("float ScaleMultiplier", source, StringComparison.Ordinal);
        Assert.Contains("float AlphaMultiplier", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the renderer applies runtime modifiers to font scale and opacity.
    /// </summary>
    [Fact]
    public void TranslationOverlayRenderer_applies_runtime_modifiers_to_scale_and_alpha()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "UIOverlays",
            "TranslationOverlay",
            "TranslationOverlayRenderer.cs"));

        Assert.Contains("request.ScaleMultiplier", source, StringComparison.Ordinal);
        Assert.Contains("request.AlphaMultiplier", source, StringComparison.Ordinal);
        Assert.Contains("ImGuiStyleVar.Alpha", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
