// <copyright file="PopupBodyHoverGeometryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers deterministic popup-body hover candidate selection without native allocations.
/// </summary>
public sealed class PopupBodyHoverGeometryTests
{
    /// <summary>
    /// Ensures a valid body collision wins over a text-sized component and addon ancestor.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_CollisionWinsOverComponentAndAncestor()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(112, 68, true, true, false, true, 1),
            new PopupBodyHoverCandidate(260, 140, true, true, true, false, 20),
            new PopupBodyHoverCandidate(900, 600, true, true, false, false, 2),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Ensures invalid popup-body candidates are rejected.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_RejectsInvalidCandidates()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(260, 140, false, true, true, false, 1),
            new PopupBodyHoverCandidate(0, 140, true, true, true, false, 1),
            new PopupBodyHoverCandidate(260, 0, true, true, true, false, 1),
            new PopupBodyHoverCandidate(260, 140, true, false, true, false, 1),
            new PopupBodyHoverCandidate(99, 140, true, true, true, false, 1),
            new PopupBodyHoverCandidate(111, 50, true, true, true, false, 1),
            new PopupBodyHoverCandidate(111, 67, true, true, true, false, 1),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Ensures a valid component is selected when no valid collision exists.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_UsesComponentFallbackWithoutValidCollision()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(260, 140, true, false, true, false, 1),
            new PopupBodyHoverCandidate(170, 90, true, true, false, true, 4),
            new PopupBodyHoverCandidate(190, 100, true, true, false, true, 2),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Ensures material width alone is sufficient when the candidate is not smaller in height.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_AcceptsMaterialWidthWithoutMaterialHeight()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(112, 50, true, true, true, false, 1),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(0, result);
    }
}
