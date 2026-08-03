// <copyright file="TooltipTextNormalizationHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers Tooltip-specific normalization needed to stabilize capture and
///     native reflow against wrapped SeString payload noise.
/// </summary>
public sealed class TooltipTextNormalizationHelperTests
{
    /// <summary>
    ///     Ensures raw wrap payload bytes inserted mid-word are removed while a
    ///     semantic title-body carriage return remains one real line break for
    ///     translation and native apply.
    /// </summary>
    [Fact]
    public void NormalizeForCapture_RemovesWrappedPayloadAndKeepsSemanticLineBreaks()
    {
        const string rawText =
            "Reduc\u0002\u0010\u0001\u0003ed \u0002\u0010\u0001\u0003Rates\r" +
            "Telepo\u0002\u0010\u0001\u0003rtation " +
            "\u0002\u0010\u0001\u0003fees " +
            "\u0002\u0010\u0001\u0003are " +
            "\u0002\u0010\u0001\u0003reduce\u0002\u0010\u0001\u0003d.";

        var actual = TooltipTextNormalizationHelper.NormalizeForCapture(rawText);

        Assert.Equal(
            "Reduced Rates\nTeleportation fees are reduced.",
            actual);
    }

    /// <summary>
    ///     Ensures trailing control bytes and raw wrap payloads do not leak
    ///     into persisted Tooltip text or later native comparison.
    /// </summary>
    [Fact]
    public void NormalizeForCapture_RemovesResidualControlsWithoutBreakingWords()
    {
        const string rawText =
            "GritEnmity \u00e9 \u0002\u0010\u0001\u0003aumentado.\u0002";

        var actual = TooltipTextNormalizationHelper.NormalizeForCapture(rawText);

        Assert.Equal("GritEnmity \u00e9 aumentado.", actual);
    }
}
