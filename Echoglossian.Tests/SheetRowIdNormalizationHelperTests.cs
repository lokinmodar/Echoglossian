// <copyright file="SheetRowIdNormalizationHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers normalization of unresolved sheet row references before they
///     become canonical identity.
/// </summary>
public class SheetRowIdNormalizationHelperTests
{
    /// <summary>
    ///     Ensures empty sheet sentinels normalize to zero for persistence-safe
    ///     identity.
    /// </summary>
    [Theory]
    [InlineData((uint)0)]
    [InlineData(uint.MaxValue)]
    public void NormalizeOrZero_ReturnsZeroForEmptySentinels(uint rowId)
    {
        Assert.Equal((uint)0, SheetRowIdNormalizationHelper.NormalizeOrZero(rowId));
    }

    /// <summary>
    ///     Ensures scoped identity falls back to the live class/job when the
    ///     sheet reference is unresolved.
    /// </summary>
    [Fact]
    public void NormalizeWithFallback_ReturnsFallbackForInvalidScopedRowId()
    {
        Assert.Equal(
            (uint)38,
            SheetRowIdNormalizationHelper.NormalizeWithFallback(
                uint.MaxValue,
                38));
    }
}
