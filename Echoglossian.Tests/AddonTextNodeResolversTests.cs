// <copyright file="AddonTextNodeResolversTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Toasts;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers traversal guards used by shared native addon text-node resolvers.
/// </summary>
public class AddonTextNodeResolversTests
{
    /// <summary>
    ///     Ensures one resolver walk does not recurse through the same native
    ///     node address more than once when it is reachable from both a node
    ///     list and a sibling chain.
    /// </summary>
    [Fact]
    public void TryVisitNodeAddress_RejectsRepeatedStructuralNodes()
    {
        var visitedNodes = new HashSet<nint>();

        Assert.True(AddonTextNodeResolvers.TryVisitNodeAddress(
            visitedNodes,
            (nint)0x100));
        Assert.False(AddonTextNodeResolvers.TryVisitNodeAddress(
            visitedNodes,
            (nint)0x100));
        Assert.True(AddonTextNodeResolvers.TryVisitNodeAddress(
            visitedNodes,
            (nint)0x200));
    }
}
