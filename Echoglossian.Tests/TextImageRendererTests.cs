// <copyright file="TextImageRendererTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.ImageGeneration;

using System.Drawing;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers image-backed text rasterization behavior.
/// </summary>
public class TextImageRendererTests
{
    /// <summary>
    /// Ensures texture-backed LTR text uses normal direction and near
    /// alignment instead of inheriting the RTL rasterization format.
    /// </summary>
    [Fact]
    public void CreateStringFormat_LtrText_UsesLtrNearAlignment()
    {
        using var format = TextImageRenderer.CreateStringFormat(
            rightToLeft: false);

        Assert.Equal(StringAlignment.Near, format.Alignment);
        Assert.Equal(
            StringFormatFlags.NoWrap,
            format.FormatFlags &
            (StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap));
    }

    /// <summary>
    /// Ensures the existing RTL format remains right-to-left and far aligned.
    /// </summary>
    [Fact]
    public void CreateStringFormat_RtlText_UsesRtlFarAlignment()
    {
        using var format = TextImageRenderer.CreateStringFormat(
            rightToLeft: true);

        Assert.Equal(StringAlignment.Far, format.Alignment);
        Assert.Equal(
            StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap,
            format.FormatFlags &
            (StringFormatFlags.DirectionRightToLeft | StringFormatFlags.NoWrap));
    }

    /// <summary>
    ///     Ensures reducing the configured line-height scale produces a more
    ///     compact multiline texture at the same font size and width.
    /// </summary>
    [Fact]
    public void RenderShapedText_ReducedLineHeightScale_ProducesShorterBitmap()
    {
        const string text =
            "اگر اشتباه‌های بازرسی بریزارد درست باشد.\n" +
            "او همچنین توضیح می‌دهد که شمشیرهای برنجی.\n" +
            "گزارش بازرس: به کاستا دل سول رسیدم.";

        using TextImageRenderer defaultRenderer =
            new("missing-font.ttf", 24f, FontStyle.Regular, 1.0f);
        using TextImageRenderer compactRenderer =
            new("missing-font.ttf", 24f, FontStyle.Regular, 0.9f);
        using Bitmap defaultBitmap = defaultRenderer.RenderShapedText(
            text,
            Color.White,
            Color.Transparent,
            480);
        using Bitmap compactBitmap = compactRenderer.RenderShapedText(
            text,
            Color.White,
            Color.Transparent,
            480);

        Assert.True(compactBitmap.Height < defaultBitmap.Height);
    }

    /// <summary>
    ///     Ensures wider measurements for the same long tooltip text reduce
    ///     the total measured height.
    /// </summary>
    [Fact]
    public void MeasureShapedText_WiderMaxWidth_ProducesShorterMeasurement()
    {
        const string text =
            "اگر اشتباه‌های بازرسی بریزارد درست باشد، پس دزد خیالی با جا زدن خود به عنوان یکی از شمشیرهای برنجی " +
            "زیر نظر استاد گوگورومو برای محافظت از ضیافت استخدام شده بود.";

        using TextImageRenderer renderer =
            new("missing-font.ttf", 24f, FontStyle.Regular, 0.9f);
        var narrowMeasurement = renderer.MeasureShapedText(text, 420);
        var wideMeasurement = renderer.MeasureShapedText(text, 760);

        Assert.True(wideMeasurement.Height < narrowMeasurement.Height);
        Assert.True(wideMeasurement.Width > narrowMeasurement.Width);
    }

    /// <summary>
    /// Ensures an unbroken word is split within the raster dimension limit so
    /// it can still be rendered without allocating an oversized bitmap.
    /// </summary>
    [Fact]
    public void RenderShapedText_UnbrokenWordBeyondRasterLimit_RemainsRenderable()
    {
        using TextImageRenderer renderer =
            new("missing-font.ttf", 24f, FontStyle.Regular, 1.0f);
        using Bitmap bitmap = renderer.RenderShapedText(
            new string('W', 600),
            Color.White,
            Color.Transparent,
            int.MaxValue);

        Assert.InRange(bitmap.Width, 1, 2048);
        Assert.InRange(bitmap.Height, 1, 2048);
    }

    /// <summary>
    /// Ensures a raster layout exceeding the approved area limit is rejected
    /// while it is measured, before all lines and a bitmap are allocated.
    /// </summary>
    [Fact]
    public void RenderShapedText_LayoutExceedsRasterArea_ThrowsBeforeAllocation()
    {
        var text = string.Join(
            "\n",
            Enumerable.Repeat(new string('W', 200), 67));
        using TextImageRenderer renderer =
            new("missing-font.ttf", 10f, FontStyle.Regular, 1.0f);
        Assert.Throws<InvalidOperationException>(() =>
            renderer.MeasureShapedText(text, 2048));
    }

    /// <summary>
    ///     Ensures pathological line counts stop layout construction as soon
    ///     as they can no longer fit in the bounded raster height.
    /// </summary>
    [Fact]
    public void CreateTextLayout_TooManyLines_StopsBeforeCompleteLayout()
    {
        var text = string.Join(
            "\n",
            Enumerable.Repeat("line", 2_500));
        using TextImageRenderer renderer =
            new("missing-font.ttf", 10f, FontStyle.Regular, 1.0f);

        Assert.Throws<InvalidOperationException>(() =>
            renderer.CreateTextLayout(text, 480));
    }

    /// <summary>
    /// Ensures a validated text layout can be reused for rasterization without
    /// rebuilding its measurements before the target bitmap is allocated.
    /// </summary>
    [Fact]
    public void RenderTextLayout_PrecomputedLayout_RendersMeasuredText()
    {
        using TextImageRenderer renderer =
            new("missing-font.ttf", 24f, FontStyle.Regular, 1.0f);
        var layout = renderer.CreateTextLayout("precomputed layout", 480);
        using Bitmap bitmap = renderer.RenderTextLayout(
            layout,
            Color.White,
            Color.Transparent);

        Assert.InRange(bitmap.Width, 1, 2048);
        Assert.InRange(bitmap.Height, 1, 2048);
    }
}
