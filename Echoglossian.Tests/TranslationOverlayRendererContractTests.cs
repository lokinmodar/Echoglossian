// <copyright file="TranslationOverlayRendererContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Guards source-level renderer contracts that are hard to exercise without a
/// live ImGui runtime.
/// </summary>
public sealed class TranslationOverlayRendererContractTests
{
    /// <summary>
    /// Ensures plain-ImGui empty-line measurement uses the same spacing advance
    /// as the draw path.
    /// </summary>
    [Fact]
    public void MeasureOverlayTextSize_EmptyLinesUseItemSpacingAdvance()
    {
        var source = File.ReadAllText(Path.Combine(
            this.RepositoryRoot,
            "UIOverlays",
            "TranslationOverlay",
            "TranslationOverlayRenderer.cs"));
        var methodBody = ExtractMethodBody(
            source,
            "private Vector2 MeasureOverlayTextSize(");

        Assert.Contains(
            "ImGui.GetStyle().ItemSpacing.Y",
            methodBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "? ImGui.GetTextLineHeight()",
            methodBody,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures ActionDetail and ItemDetail overlays use their dedicated
    ///     persisted override bucket for texture width and line-height instead
    ///     of falling through to the hover-tooltip or Tooltip addon paths.
    /// </summary>
    [Fact]
    public void StructuredDetailOverlays_UseDedicatedRendererOverrideBucket()
    {
        var source = File.ReadAllText(Path.Combine(
            this.RepositoryRoot,
            "UIOverlays",
            "TranslationOverlay",
            "TranslationOverlayRenderer.cs"));

        Assert.Contains(
            "TranslationOverlaySurfaceId.ActionDetail",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TranslationOverlaySurfaceId.ItemDetail",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ActionItemDetailOverlayPadding",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ActionItemDetailOverlayLineHeightScale",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures overlay text-layout requests derive both target-language
    /// metadata values from the configured language instead of mixing config
    /// identifiers with the mutable global selected-language code.
    /// </summary>
    [Fact]
    public void Draw_UsesConfiguredTargetLanguageCodeForTextLayoutRequest()
    {
        var source = File.ReadAllText(Path.Combine(
            this.RepositoryRoot,
            "UIOverlays",
            "TranslationOverlay",
            "TranslationOverlayRenderer.cs"));

        Assert.Contains(
            "RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.configuration.Lang)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SelectedLanguage.Code",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the repository root discovered from the test output directory.
    /// </summary>
    private string RepositoryRoot => FindRepositoryRoot();

    /// <summary>
    /// Extracts the body for one source method.
    /// </summary>
    /// <param name="source">The full source file.</param>
    /// <param name="signature">The method signature text.</param>
    /// <returns>The method body text including braces.</returns>
    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Could not find method signature: {signature}");

        var bodyStart = source.IndexOf('{', signatureIndex);
        Assert.True(bodyStart >= 0, $"Could not find body start for: {signature}");

        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(bodyStart, (index - bodyStart) + 1);
                }
            }
        }

        throw new InvalidOperationException($"Could not find body end for: {signature}");
    }

    /// <summary>
    /// Finds the repository root by walking upward from the test output
    /// directory until the solution file is found.
    /// </summary>
    /// <returns>The absolute repository-root path.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Echoglossian.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Echoglossian repository root.");
    }
}
