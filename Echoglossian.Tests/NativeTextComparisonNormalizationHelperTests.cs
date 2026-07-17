// <copyright file="NativeTextComparisonNormalizationHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers native text-comparison normalization for wrapped SeString payloads
///     that leak raw line-break bytes into visible node text.
/// </summary>
public class NativeTextComparisonNormalizationHelperTests
{
    /// <summary>
    ///     Ensures raw MiniTalk line-break payload bytes compare equal to the
    ///     same translated line after native wrapping inserts control markers.
    /// </summary>
    [Fact]
    public void NormalizeForComparison_RawSeStringLineBreakPayloadsCollapseForMatching()
    {
        const string wrappedVisibleText =
            "N\u00e3o consigo ganhar \u0002\u0010\u0001\u0003nada... Estou " +
            "\u0002\u0010\u0001\u0003come\u00e7ando a achar que " +
            "\u0002\u0010\u0001\u0003os sorteios s\u00e3o " +
            "\u0002\u0010\u0001\u0003fraudados.";
        const string replacementText =
            "N\u00e3o consigo ganhar nada... Estou come\u00e7ando a achar que os sorteios s\u00e3o fraudados.";

        var normalizedVisible =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                wrappedVisibleText);
        var normalizedReplacement =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                replacementText);

        Assert.Equal(normalizedReplacement, normalizedVisible);
    }

    /// <summary>
    ///     Ensures legacy carriage-return wraps and residual control bytes do
    ///     not create a false source change during native reconciliation.
    /// </summary>
    [Fact]
    public void NormalizeForComparison_CarriageReturnsAndControlBytesDoNotChangeMeaning()
    {
        const string wrappedVisibleText =
            "Rig away! I've won\rthree drawings \u0002\u0010\u0001\u0003straight!\u0002";
        const string replacementText =
            "Rig away! I've won three drawings straight!";

        var normalizedVisible =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                wrappedVisibleText);
        var normalizedReplacement =
            NativeTextComparisonNormalizationHelper.NormalizeForComparison(
                replacementText);

        Assert.Equal(normalizedReplacement, normalizedVisible);
    }
}
