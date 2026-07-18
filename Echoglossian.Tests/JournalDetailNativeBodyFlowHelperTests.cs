// <copyright file="JournalDetailNativeBodyFlowHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers deterministic JournalDetail-native reflow calculations.
/// </summary>
public class JournalDetailNativeBodyFlowHelperTests
{
    /// <summary>
    /// Ensures unchanged translated heights keep the original block positions
    /// and container size.
    /// </summary>
    [Fact]
    public void CalculateVerticalLayoutPlan_PreservesOriginalLayout_WhenHeightsUnchanged()
    {
        var blocks = new[]
        {
            CreateBlock((nint)0x101, 0, 10, (nint)0x201),
            CreateBlock((nint)0x102, 14, 8, (nint)0x202),
        };
        var desiredWrapperHeights = new Dictionary<nint, ushort>
        {
            [(nint)0x201] = 10,
            [(nint)0x202] = 8,
        };
        var containers = new[]
        {
            new JournalDetailNativeBodyFlowContainerSnapshot(
                (nint)0x301,
                4,
                40,
                6),
        };

        var plan = JournalDetailNativeBodyFlowHelper.CalculateVerticalLayoutPlan(
            blocks,
            desiredWrapperHeights,
            containers);

        Assert.Collection(
            plan.BlockPlans,
            first =>
            {
                Assert.Equal((nint)0x101, first.WrapperNodeAddress);
                Assert.Equal(0, first.WrapperY);
                Assert.Equal((ushort)10, first.WrapperHeight);
            },
            second =>
            {
                Assert.Equal((nint)0x102, second.WrapperNodeAddress);
                Assert.Equal(14, second.WrapperY);
                Assert.Equal((ushort)8, second.WrapperHeight);
            });
        Assert.Collection(
            plan.ContainerPlans,
            container => Assert.Equal((ushort)40, container.Height));
    }

    /// <summary>
    /// Ensures later body blocks move by the cumulative growth or shrinkage of
    /// earlier blocks.
    /// </summary>
    [Fact]
    public void CalculateVerticalLayoutPlan_AppliesCumulativeOffsets_ForMixedDeltas()
    {
        var blocks = new[]
        {
            CreateBlock((nint)0x101, 0, 10, (nint)0x201),
            CreateBlock((nint)0x102, 12, 20, (nint)0x202),
            CreateBlock((nint)0x103, 36, 15, (nint)0x203),
        };
        var desiredWrapperHeights = new Dictionary<nint, ushort>
        {
            [(nint)0x201] = 15,
            [(nint)0x202] = 20,
            [(nint)0x203] = 12,
        };

        var plan = JournalDetailNativeBodyFlowHelper.CalculateVerticalLayoutPlan(
            blocks,
            desiredWrapperHeights,
            Array.Empty<JournalDetailNativeBodyFlowContainerSnapshot>());

        Assert.Collection(
            plan.BlockPlans,
            first =>
            {
                Assert.Equal(0, first.WrapperY);
                Assert.Equal((ushort)15, first.WrapperHeight);
            },
            second =>
            {
                Assert.Equal(17, second.WrapperY);
                Assert.Equal((ushort)20, second.WrapperHeight);
            },
            third =>
            {
                Assert.Equal(41, third.WrapperY);
                Assert.Equal((ushort)12, third.WrapperHeight);
            });
    }

    /// <summary>
    /// Ensures the internal body or scroll container grows from the final
    /// translated block plus its original bottom padding.
    /// </summary>
    [Fact]
    public void CalculateVerticalLayoutPlan_GrowsContainer_FromFinalBlockAndBottomPadding()
    {
        var blocks = new[]
        {
            CreateBlock((nint)0x101, 0, 10, (nint)0x201),
            CreateBlock((nint)0x102, 20, 10, (nint)0x202),
        };
        var desiredWrapperHeights = new Dictionary<nint, ushort>
        {
            [(nint)0x201] = 25,
            [(nint)0x202] = 18,
        };
        var containers = new[]
        {
            new JournalDetailNativeBodyFlowContainerSnapshot(
                (nint)0x301,
                4,
                40,
                7),
        };

        var plan = JournalDetailNativeBodyFlowHelper.CalculateVerticalLayoutPlan(
            blocks,
            desiredWrapperHeights,
            containers);

        Assert.Collection(
            plan.ContainerPlans,
            container => Assert.Equal((ushort)64, container.Height));
    }

    private static JournalDetailNativeBodyFlowBlockSnapshot CreateBlock(
        nint wrapperNodeAddress,
        short wrapperY,
        ushort wrapperHeight,
        nint textNodeAddress)
    {
        return new JournalDetailNativeBodyFlowBlockSnapshot(
            wrapperNodeAddress,
            0,
            wrapperY,
            100,
            wrapperHeight,
            textNodeAddress,
            0,
            0,
            90,
            wrapperHeight,
            default,
            12,
            0);
    }
}
