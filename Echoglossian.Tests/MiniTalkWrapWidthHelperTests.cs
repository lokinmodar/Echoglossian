// <copyright file="MiniTalkWrapWidthHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the MiniTalk-specific wrap-width policy used before native bubble
/// reflow is applied.
/// </summary>
public sealed class MiniTalkWrapWidthHelperTests
{
    /// <summary>
    /// Ensures no extra width is added when the shared helper already resolved a
    /// wider visual container.
    /// </summary>
    [Fact]
    public void ResolveAdditionalWrapWidth_WiderPreferredWrap_ReturnsZero()
    {
        var result = MiniTalkWrapWidthHelper.ResolveAdditionalWrapWidth(
            currentTextWidth: 160,
            preferredWrapWidth: 240,
            leftPadding: 12,
            rightPadding: 24);

        Assert.Equal((ushort)0, result);
    }

    /// <summary>
    /// Ensures the helper still contributes the larger side padding when the
    /// shared wrap-width resolution did not already widen the node.
    /// </summary>
    [Fact]
    public void ResolveAdditionalWrapWidth_UsesLargestPaddingWhenNoWiderContainerExists()
    {
        var result = MiniTalkWrapWidthHelper.ResolveAdditionalWrapWidth(
            currentTextWidth: 180,
            preferredWrapWidth: 180,
            leftPadding: 12,
            rightPadding: 28);

        Assert.Equal((ushort)28, result);
    }
}
