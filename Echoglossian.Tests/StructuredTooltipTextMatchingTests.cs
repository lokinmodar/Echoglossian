// <copyright file="StructuredTooltipTextMatchingTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the live text-matching helpers used by ActionDetail and
///     ItemDetail native apply.
/// </summary>
public class StructuredTooltipTextMatchingTests
{
    /// <summary>
    ///     Ensures structured-tooltip matching collapses wrapped whitespace and
    ///     strips control-format noise before comparing live and canonical text.
    /// </summary>
    [Fact]
    public void NormalizeStructuredTooltipLookupText_CollapsesWhitespaceAndFormatNoise()
    {
        var input = "Heart\u200E of\r\n  Corundum\t";

        var result = Echoglossian.NormalizeStructuredTooltipLookupText(input);

        Assert.Equal("Heart of Corundum", result);
    }

    /// <summary>
    ///     Ensures wrapped description text still matches its canonical sheet
    ///     description.
    /// </summary>
    [Fact]
    public void ComputeStructuredTooltipTextMatchScore_MatchesWrappedDescription()
    {
        const string visibleText =
            "Increases the chance of obtaining items\nwhile gathering by 50%.";
        const string canonicalText =
            "Increases the chance of obtaining items while gathering by 50%.";

        var result = Echoglossian.ComputeStructuredTooltipTextMatchScore(
            visibleText,
            canonicalText);

        Assert.True(result > 0);
    }

    /// <summary>
    ///     Ensures decorative glyphs around the visible item name do not block
    ///     native name resolution.
    /// </summary>
    [Fact]
    public void ComputeStructuredTooltipTextMatchScore_MatchesDecoratedItemName()
    {
        const string visibleText = "\uE03C Super-Potion";
        const string canonicalText = "Super-Potion";

        var result = Echoglossian.ComputeStructuredTooltipTextMatchScore(
            visibleText,
            canonicalText);

        Assert.True(result > 0);
    }

    /// <summary>
    ///     Ensures native tooltip mutation is deferred until both name and
    ///     description nodes are resolved for description-bearing tooltips.
    /// </summary>
    [Fact]
    public void CanApplyStructuredTooltipNative_RequiresCompleteResolutionForDescriptionTooltips()
    {
        var result = Echoglossian.CanApplyStructuredTooltipNative(
            descriptionExpected: true,
            nameNodeResolved: true,
            nameNodeSupportsPlainTextMutation: true,
            descriptionNodeResolved: false,
            descriptionNodeSupportsPlainTextMutation: false);

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures native tooltip mutation is blocked when the live node is not
    ///     plain-text safe.
    /// </summary>
    [Fact]
    public void CanApplyStructuredTooltipNative_BlocksNonTextOnlyNodes()
    {
        var result = Echoglossian.CanApplyStructuredTooltipNative(
            descriptionExpected: false,
            nameNodeResolved: true,
            nameNodeSupportsPlainTextMutation: false,
            descriptionNodeResolved: false,
            descriptionNodeSupportsPlainTextMutation: false);

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures native tooltip mutation is allowed only when the full
    ///     tooltip surface is safely writable.
    /// </summary>
    [Fact]
    public void CanApplyStructuredTooltipNative_AllowsCompletePlainTextTooltip()
    {
        var result = Echoglossian.CanApplyStructuredTooltipNative(
            descriptionExpected: true,
            nameNodeResolved: true,
            nameNodeSupportsPlainTextMutation: true,
            descriptionNodeResolved: true,
            descriptionNodeSupportsPlainTextMutation: true);

        Assert.True(result);
    }

    /// <summary>
    ///     Ensures node matching prefers the plain-text-safe candidate when
    ///     two live nodes have the same text-match score.
    /// </summary>
    [Fact]
    public void TryFindBestStructuredTooltipTextNodeCandidate_PrefersSafeCandidateOnTiedScore()
    {
        IReadOnlyList<Echoglossian.StructuredTooltipTextNodeCandidate> candidates =
        [
            new Echoglossian.StructuredTooltipTextNodeCandidate(
                (nint)1,
                "Standard Step",
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Standard Step"),
                false),
            new Echoglossian.StructuredTooltipTextNodeCandidate(
                (nint)2,
                "Standard Step",
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Standard Step"),
                true),
        ];

        var found = Echoglossian.TryFindBestStructuredTooltipTextNodeCandidate(
            candidates,
            "Standard Step",
            excludedNodeAddress: 0,
            out var bestCandidate);

        Assert.True(found);
        Assert.Equal((nint)2, bestCandidate.NodeAddress);
        Assert.True(bestCandidate.SupportsPlainTextMutation);
    }
}
