// <copyright file="NativeTextNodeLayoutHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers reusable geometry calculations in the native text-node reflow
///     helper.
/// </summary>
public class NativeTextNodeLayoutHelperTests
{
    /// <summary>
    ///     Ensures the secondary background width is left untouched when the
    ///     anchored node already fits inside the current bounds.
    /// </summary>
    [Fact]
    public void ResolveMinimumSecondaryWidthForAnchoredNode_ReturnsCurrentWidth_WhenAnchoredNodeIsAlreadyCovered()
    {
        var resolvedWidth = NativeTextNodeLayoutHelper.ResolveMinimumSecondaryWidthForAnchoredNode(
            secondaryContainerX: 100,
            currentSecondaryWidth: 220,
            anchoredNodeX: 260,
            anchoredNodeWidth: 24,
            preferredRightPadding: 8);

        Assert.Equal((ushort)220, resolvedWidth);
    }

    /// <summary>
    ///     Ensures the secondary background widens enough to cover an anchored
    ///     timer or spinner node when it would otherwise extend past the
    ///     current right edge.
    /// </summary>
    [Fact]
    public void ResolveMinimumSecondaryWidthForAnchoredNode_GrowsWidth_WhenAnchoredNodeExtendsPastCurrentBounds()
    {
        var resolvedWidth = NativeTextNodeLayoutHelper.ResolveMinimumSecondaryWidthForAnchoredNode(
            secondaryContainerX: 100,
            currentSecondaryWidth: 220,
            anchoredNodeX: 310,
            anchoredNodeWidth: 24,
            preferredRightPadding: 8);

        Assert.Equal((ushort)242, resolvedWidth);
    }

    /// <summary>
    ///     Ensures negative historical padding still results in coverage up to
    ///     the anchored node edge instead of shrinking the background further.
    /// </summary>
    [Fact]
    public void ResolveMinimumSecondaryWidthForAnchoredNode_ClampsNegativePaddingToZero()
    {
        var resolvedWidth = NativeTextNodeLayoutHelper.ResolveMinimumSecondaryWidthForAnchoredNode(
            secondaryContainerX: 100,
            currentSecondaryWidth: 220,
            anchoredNodeX: 330,
            anchoredNodeWidth: 20,
            preferredRightPadding: -12);

        Assert.Equal((ushort)250, resolvedWidth);
    }
}
