// <copyright file="PopupBodyHoverGeometryHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.NativeUI.Helpers;

/// <summary>
/// Selects the best pure geometry candidate for popup-body hover handling.
/// </summary>
internal static class PopupBodyHoverGeometryHelper
{
    private const float MaterialWidthThreshold = 12f;
    private const float MaterialHeightThreshold = 18f;

    /// <summary>
    /// Determines whether a native popup-body traversal node can be inspected within its bounded
    /// live section.
    /// </summary>
    /// <param name="nodeAddress">The address of the node to inspect.</param>
    /// <param name="sectionBoundaryAddress">
    /// The address of the resolved popup section boundary, or <see cref="nint.Zero" /> when no
    /// boundary applies.
    /// </param>
    /// <param name="inspectedNodeCount">The number of nodes already inspected.</param>
    /// <param name="maximumNodeCount">The maximum number of nodes that may be inspected.</param>
    /// <param name="visitedNodes">The addresses already visited by the traversal.</param>
    /// <returns>
    /// <see langword="true" /> when the node may be inspected; otherwise,
    /// <see langword="false" />.
    /// </returns>
    public static bool TryVisitSectionBoundedTraversalNode(
        nint nodeAddress,
        nint sectionBoundaryAddress,
        int inspectedNodeCount,
        int maximumNodeCount,
        ISet<nint> visitedNodes)
    {
        return nodeAddress != nint.Zero
            && nodeAddress != sectionBoundaryAddress
            && inspectedNodeCount < maximumNodeCount
            && visitedNodes.Add(nodeAddress);
    }

    /// <summary>
    /// Selects the best valid popup-body candidate by deterministic geometry ranking.
    /// </summary>
    /// <param name="textWidth">The width of the body text rectangle.</param>
    /// <param name="textHeight">The height of the body text rectangle.</param>
    /// <param name="candidates">The candidate snapshots to evaluate.</param>
    /// <returns>The zero-based selected candidate index, or <c>-1</c> when none is valid.</returns>
    public static int SelectCandidateIndex(
        float textWidth,
        float textHeight,
        IReadOnlyList<PopupBodyHoverCandidate> candidates)
    {
        if (textWidth <= 0 || textHeight <= 0 || candidates.Count == 0)
        {
            return -1;
        }

        var selectedIndex = -1;
        var selectedRank = int.MaxValue;
        var selectedArea = float.MaxValue;
        var selectedDistance = int.MaxValue;

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsValidCandidate(textWidth, textHeight, candidate))
            {
                continue;
            }

            var rank = GetCandidateRank(candidate);
            var area = candidate.Width * candidate.Height;
            if (rank < selectedRank
                || (rank == selectedRank && area < selectedArea)
                || (rank == selectedRank && area == selectedArea && candidate.DistanceFromText < selectedDistance))
            {
                selectedIndex = index;
                selectedRank = rank;
                selectedArea = area;
                selectedDistance = candidate.DistanceFromText;
            }
        }

        return selectedIndex;
    }

    /// <summary>
    /// Determines whether a candidate satisfies the popup-body geometry requirements.
    /// </summary>
    /// <param name="textWidth">The width of the body text rectangle.</param>
    /// <param name="textHeight">The height of the body text rectangle.</param>
    /// <param name="candidate">The candidate snapshot to validate.</param>
    /// <returns><see langword="true"/> when the candidate is valid.</returns>
    private static bool IsValidCandidate(
        float textWidth,
        float textHeight,
        PopupBodyHoverCandidate candidate)
    {
        return candidate.Width > 0
            && candidate.Height > 0
            && candidate.IsVisible
            && candidate.ContainsText
            && candidate.Width >= textWidth
            && candidate.Height >= textHeight
            && (candidate.Width >= textWidth + MaterialWidthThreshold
                || candidate.Height >= textHeight + MaterialHeightThreshold);
    }

    /// <summary>
    /// Gets the deterministic precedence rank for a valid candidate.
    /// </summary>
    /// <param name="candidate">The candidate snapshot to rank.</param>
    /// <returns>The candidate precedence rank.</returns>
    private static int GetCandidateRank(PopupBodyHoverCandidate candidate)
    {
        if (candidate.IsCollision)
        {
            return 0;
        }

        if (candidate.IsComponent)
        {
            return 1;
        }

        return 2;
    }
}

/// <summary>
/// Describes a native-node-derived popup-body hover candidate without retaining native pointers.
/// </summary>
internal readonly record struct PopupBodyHoverCandidate(
    float Width,
    float Height,
    bool IsVisible,
    bool ContainsText,
    bool IsCollision,
    bool IsComponent,
    int DistanceFromText);
