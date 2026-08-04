// <copyright file="HoverTooltipLayoutPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers shared layout policy for texture-backed hover tooltips.
/// </summary>
public class HoverTooltipLayoutPolicyTests
{
    /// <summary>
    ///     Ensures the resolved tooltip font scale honors the hover-specific
    ///     config value.
    /// </summary>
    [Fact]
    public void ResolveTextureFontScale_UsesConfiguredHoverTooltipScale()
    {
        var config = new Config
        {
            HoverTooltipFontScale = 0.8f,
        };

        Assert.Equal(
            0.8f,
            HoverTooltipLayoutPolicy.ResolveTextureFontScale(config));
    }

    /// <summary>
    ///     Ensures the width calculation stays bounded by the configured cap
    ///     while still using a practical minimum on smaller viewports.
    /// </summary>
    [Theory]
    [InlineData(2560f, 720f)]
    [InlineData(1920f, 720f)]
    [InlineData(1280f, 537.6f)]
    [InlineData(480f, 240f)]
    public void ResolveTextureMaxWidth_UsesViewportFractionWithConfigCap(
        float viewportWidth,
        float expected)
    {
        var config = new Config
        {
            HoverTooltipMaxWidth = 720f,
        };

        Assert.Equal(
            expected,
            HoverTooltipLayoutPolicy.ResolveTextureMaxWidth(
                config,
                viewportWidth),
            precision: 3);
    }

    /// <summary>
    ///     Ensures rich SeString originals receive the same stable width cap
    ///     as texture-backed tooltips instead of consulting an auto-sized
    ///     tooltip window's remaining content width.
    /// </summary>
    /// <param name="viewportWidth">The current main-viewport width.</param>
    /// <param name="expected">The expected stable wrap width.</param>
    [Theory]
    [InlineData(1920f, 720f)]
    [InlineData(1280f, 537.6f)]
    public void ResolveRichOriginalImGuiMaxWidth_UsesStableViewportCap(
        float viewportWidth,
        float expected)
    {
        var config = new Config
        {
            HoverTooltipMaxWidth = 720f,
        };

        Assert.Equal(
            expected,
            HoverTooltipLayoutPolicy.ResolveRichOriginalImGuiMaxWidth(
                config,
                viewportWidth),
            precision: 3);
    }

    /// <summary>
    ///     Ensures short tooltip text keeps using the base width and does not
    ///     pay for measurement callbacks.
    /// </summary>
    [Fact]
    public void ResolveTextureMaxWidth_ShortText_SkipsAdaptiveMeasurement()
    {
        var config = new Config
        {
            HoverTooltipMaxWidth = 720f,
        };
        var measurementCalls = 0;

        var resolvedWidth = HoverTooltipLayoutPolicy.ResolveTextureMaxWidth(
            config,
            1280f,
            "کوتاه",
            _ =>
            {
                measurementCalls++;
                return 100f;
            });

        Assert.Equal(537.6f, resolvedWidth, precision: 3);
        Assert.Equal(0, measurementCalls);
    }

    /// <summary>
    ///     Ensures long tooltip text widens beyond the base width even before
    ///     the very-long measurement fallback kicks in.
    /// </summary>
    [Fact]
    public void ResolveTextureMaxWidth_LongText_WidensWithoutMeasurement()
    {
        var config = new Config
        {
            HoverTooltipMaxWidth = 720f,
        };
        var text = string.Join(
            ' ',
            Enumerable.Repeat("بازرسی", 60));
        var measurementCalls = 0;

        var resolvedWidth = HoverTooltipLayoutPolicy.ResolveTextureMaxWidth(
            config,
            1920f,
            text,
            _ =>
            {
                measurementCalls++;
                return 100f;
            });

        Assert.Equal(936f, resolvedWidth, precision: 3);
        Assert.Equal(0, measurementCalls);
    }

    /// <summary>
    ///     Ensures very long tooltip text can invoke measured candidates and
    ///     choose a width that is narrower than the absolute widest candidate
    ///     while still close to the best measured height.
    /// </summary>
    [Fact]
    public void ResolveTextureMaxWidth_VeryLongText_UsesMeasuredCandidates()
    {
        var config = new Config
        {
            HoverTooltipMaxWidth = 720f,
        };
        var text = string.Join(
            ' ',
            Enumerable.Repeat("گزارش", 120));
        var measurementCalls = 0;

        var resolvedWidth = HoverTooltipLayoutPolicy.ResolveTextureMaxWidth(
            config,
            1920f,
            text,
            width =>
            {
                measurementCalls++;
                return width switch
                {
                    < 721f => 1900f,
                    < 829f => 1350f,
                    < 937f => 1000f,
                    _ => 950f,
                };
            });

        Assert.Equal(936f, resolvedWidth, precision: 3);
        Assert.True(measurementCalls >= 3);
    }

    /// <summary>
    ///     Ensures invalid hover-tooltip scale values are clamped into the
    ///     supported runtime range.
    /// </summary>
    [Theory]
    [InlineData(0.01f, 0.25f)]
    [InlineData(4.5f, 3.0f)]
    public void ResolveTextureFontScale_ClampsInvalidConfigValues(
        float configuredScale,
        float expected)
    {
        var config = new Config
        {
            HoverTooltipFontScale = configuredScale,
        };

        Assert.Equal(
            expected,
            HoverTooltipLayoutPolicy.ResolveTextureFontScale(config));
    }
}
