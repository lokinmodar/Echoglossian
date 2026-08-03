// <copyright file="PopupBodyHoverGeometryTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;
using System.Runtime.InteropServices;

using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.NativeUI.Helpers;

using FFXIVClientStructs.FFXIV.Component.GUI;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers popup-body hover candidate collection and deterministic geometry selection.
/// </summary>
public sealed class PopupBodyHoverGeometryTests
{
    /// <summary>
    /// Ensures the native candidate collector does not inspect the resolved popup section boundary
    /// or any of its ancestors.
    /// </summary>
    [Fact]
    public void TryVisitSectionBoundedTraversalNode_StopsAtResolvedPopupSectionBoundary()
    {
        HashSet<nint> visitedNodes = [];

        var shouldVisit = PopupBodyHoverGeometryHelper.TryVisitSectionBoundedTraversalNode(
            nodeAddress: (nint)200,
            sectionBoundaryAddress: (nint)200,
            inspectedNodeCount: 0,
            maximumNodeCount: 6,
            visitedNodes);

        Assert.False(shouldVisit);
        Assert.Empty(visitedNodes);
    }

    /// <summary>
    /// Ensures the native candidate collector stops repeated or over-limit traversal nodes before
    /// they can expand a popup-body tooltip outside its live section.
    /// </summary>
    [Fact]
    public void TryVisitSectionBoundedTraversalNode_RejectsCyclesAndTraversalLimit()
    {
        HashSet<nint> visitedNodes = [];

        Assert.True(
            PopupBodyHoverGeometryHelper.TryVisitSectionBoundedTraversalNode(
                nodeAddress: (nint)100,
                sectionBoundaryAddress: (nint)200,
                inspectedNodeCount: 0,
                maximumNodeCount: 2,
                visitedNodes));
        Assert.False(
            PopupBodyHoverGeometryHelper.TryVisitSectionBoundedTraversalNode(
                nodeAddress: (nint)100,
                sectionBoundaryAddress: (nint)200,
                inspectedNodeCount: 1,
                maximumNodeCount: 2,
                visitedNodes));
        Assert.False(
            PopupBodyHoverGeometryHelper.TryVisitSectionBoundedTraversalNode(
                nodeAddress: (nint)300,
                sectionBoundaryAddress: (nint)200,
                inspectedNodeCount: 2,
                maximumNodeCount: 2,
                visitedNodes));
        Assert.Equal([(nint)100], visitedNodes);
    }

    /// <summary>
    /// Ensures a same-parent addon-wide collision collected from the production node graph cannot
    /// outrank the resolved popup body component.
    /// </summary>
    [Fact]
    public unsafe void TryBuildPopupBodyHoverBounds_SameParentGlobalCollisionDoesNotBeatResolvedBodySection()
    {
        AtkTextNode textNode = default;
        AtkResNode siblingParent = default;
        AtkResNode globalCollision = default;
        AtkResNode bodyComponent = default;

        textNode.AtkResNode.Type = NodeType.Text;
        textNode.AtkResNode.NodeFlags = NodeFlags.Visible;
        textNode.AtkResNode.ScreenX = 100f;
        textNode.AtkResNode.ScreenY = 100f;
        textNode.AtkResNode.ScaleX = 1f;
        textNode.AtkResNode.ScaleY = 1f;
        textNode.AtkResNode.Width = 100;
        textNode.AtkResNode.Height = 50;
        textNode.AtkResNode.ParentNode = &bodyComponent;

        bodyComponent.Type = NodeType.Component;
        bodyComponent.NodeFlags = NodeFlags.Visible;
        bodyComponent.ScreenX = 90f;
        bodyComponent.ScreenY = 80f;
        bodyComponent.ScaleX = 1f;
        bodyComponent.ScaleY = 1f;
        bodyComponent.Width = 190;
        bodyComponent.Height = 100;
        bodyComponent.ParentNode = &siblingParent;
        bodyComponent.PrevSiblingNode = &globalCollision;
        bodyComponent.ChildNode = (AtkResNode*)&textNode;

        globalCollision.Type = NodeType.Collision;
        globalCollision.NodeFlags = NodeFlags.Visible;
        globalCollision.ScreenX = 0f;
        globalCollision.ScreenY = 0f;
        globalCollision.ScaleX = 1f;
        globalCollision.ScaleY = 1f;
        globalCollision.Width = 900;
        globalCollision.Height = 600;
        globalCollision.ParentNode = &siblingParent;
        globalCollision.NextSiblingNode = &bodyComponent;

        siblingParent.ChildNode = &globalCollision;

        var originalIsVisibleAddress = AtkResNode.Addresses.IsVisible.Value;
        var originalGetWidthAddress = AtkResNode.Addresses.GetWidth.Value;
        var originalGetHeightAddress = AtkResNode.Addresses.GetHeight.Value;
        try
        {
            AtkResNode.Addresses.IsVisible.Value =
                (nint)(delegate* unmanaged<AtkResNode*, byte>)&IsTestNodeVisible;
            AtkResNode.Addresses.GetWidth.Value =
                (nint)(delegate* unmanaged<AtkResNode*, ushort>)&GetTestNodeWidth;
            AtkResNode.Addresses.GetHeight.Value =
                (nint)(delegate* unmanaged<AtkResNode*, ushort>)&GetTestNodeHeight;

            var result = PopupBodyHoverBoundsProbe.TryBuildBounds(
                &textNode,
                &bodyComponent,
                out var topLeft,
                out var bottomRight);

            Assert.True(result);
            Assert.Equal(new Vector2(82f, 76f), topLeft);
            Assert.Equal(new Vector2(288f, 184f), bottomRight);
        }
        finally
        {
            AtkResNode.Addresses.IsVisible.Value = originalIsVisibleAddress;
            AtkResNode.Addresses.GetWidth.Value = originalGetWidthAddress;
            AtkResNode.Addresses.GetHeight.Value = originalGetHeightAddress;
        }
    }

    /// <summary>
    /// Ensures a valid body collision wins over a text-sized component and addon ancestor.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_CollisionWinsOverComponentAndAncestor()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(112, 68, true, true, false, true, true, 1),
            new PopupBodyHoverCandidate(260, 140, true, true, true, false, true, 20),
            new PopupBodyHoverCandidate(900, 600, true, true, false, false, true, 2),
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
            new PopupBodyHoverCandidate(260, 140, false, true, true, false, true, 1),
            new PopupBodyHoverCandidate(0, 140, true, true, true, false, true, 1),
            new PopupBodyHoverCandidate(260, 0, true, true, true, false, true, 1),
            new PopupBodyHoverCandidate(260, 140, true, false, true, false, true, 1),
            new PopupBodyHoverCandidate(99, 140, true, true, true, false, true, 1),
            new PopupBodyHoverCandidate(111, 50, true, true, true, false, true, 1),
            new PopupBodyHoverCandidate(111, 67, true, true, true, false, true, 1),
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
            new PopupBodyHoverCandidate(260, 140, true, false, true, false, true, 1),
            new PopupBodyHoverCandidate(170, 90, true, true, false, true, true, 4),
            new PopupBodyHoverCandidate(190, 100, true, true, false, true, true, 2),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Ensures an addon-wide collision cannot outrank a valid body component.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_GlobalCollisionDoesNotBeatBodyComponent()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(900, 600, true, true, true, false, false, 1),
            new PopupBodyHoverCandidate(190, 100, true, true, false, true, true, 2),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(1, result);
    }

    /// <summary>
    /// Ensures an otherwise practical candidate must represent a collision or component.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_RejectsUnclassifiedCandidate()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(190, 100, true, true, false, false, true, 1),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Ensures material width alone is sufficient when the candidate is not smaller in height.
    /// </summary>
    [Fact]
    public void SelectCandidateIndex_AcceptsMaterialWidthWithoutMaterialHeight()
    {
        var candidates = new[]
        {
            new PopupBodyHoverCandidate(112, 50, true, true, true, false, true, 1),
        };

        var result = PopupBodyHoverGeometryHelper.SelectCandidateIndex(100, 50, candidates);

        Assert.Equal(0, result);
    }

    /// <summary>
    /// Reports native-node visibility from the fixture's real node flags.
    /// </summary>
    /// <param name="node">The fixture node to inspect.</param>
    /// <returns><c>1</c> when the node has the visible flag; otherwise, <c>0</c>.</returns>
    [UnmanagedCallersOnly]
    private static unsafe byte IsTestNodeVisible(AtkResNode* node)
    {
        return node != null && (node->NodeFlags & NodeFlags.Visible) != 0
            ? (byte)1
            : (byte)0;
    }

    /// <summary>
    /// Gets the fixture node width used by the production bounds collector.
    /// </summary>
    /// <param name="node">The fixture node to inspect.</param>
    /// <returns>The fixture node width.</returns>
    [UnmanagedCallersOnly]
    private static unsafe ushort GetTestNodeWidth(AtkResNode* node)
    {
        return node == null ? (ushort)0 : node->Width;
    }

    /// <summary>
    /// Gets the fixture node height used by the production bounds collector.
    /// </summary>
    /// <param name="node">The fixture node to inspect.</param>
    /// <returns>The fixture node height.</returns>
    [UnmanagedCallersOnly]
    private static unsafe ushort GetTestNodeHeight(AtkResNode* node)
    {
        return node == null ? (ushort)0 : node->Height;
    }

    /// <summary>
    /// Exposes the production popup-body bounds collector to native-node regression fixtures.
    /// </summary>
    private sealed class PopupBodyHoverBoundsProbe : QuestAddonHandlerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PopupBodyHoverBoundsProbe" /> class.
        /// </summary>
        /// <param name="dependencies">The shared quest-handler dependencies.</param>
        private PopupBodyHoverBoundsProbe(QuestAddonHandlerDependencies dependencies)
            : base(dependencies)
        {
        }

        /// <summary>
        /// Builds popup-body bounds through the production native candidate collector.
        /// </summary>
        /// <param name="textNode">The live popup body text node.</param>
        /// <param name="preferredHoverNode">The resolved popup body section node.</param>
        /// <param name="topLeft">The resolved top-left hover coordinate.</param>
        /// <param name="bottomRight">The resolved bottom-right hover coordinate.</param>
        /// <returns>
        /// <see langword="true" /> when production classification selects a candidate; otherwise,
        /// <see langword="false" />.
        /// </returns>
        public static unsafe bool TryBuildBounds(
            AtkTextNode* textNode,
            AtkResNode* preferredHoverNode,
            out Vector2 topLeft,
            out Vector2 bottomRight)
        {
            return TryBuildPopupBodyHoverBounds(
                textNode,
                preferredHoverNode,
                out topLeft,
                out bottomRight);
        }
    }
}
