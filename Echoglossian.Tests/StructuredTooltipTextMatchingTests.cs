// <copyright file="StructuredTooltipTextMatchingTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the live text-matching helpers used by ActionDetail and
///     ItemDetail native apply.
/// </summary>
public class StructuredTooltipTextMatchingTests
{
    /// <summary>
    ///     Ensures detail tooltip native mutation stays disabled until the
    ///     required FFXIVClientStructs mappings are available.
    /// </summary>
    [Fact]
    public void GetStructuredTooltipDisplayMode_UsesPluginTooltipsOnly()
    {
        var result = Echoglossian.GetStructuredTooltipDisplayMode();

        Assert.Equal(JournalTranslationDisplayMode.TooltipTranslation, result);
        Assert.False(TranslationDisplayModeHelper.WritesNativeTranslation(result));
    }

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
    ///     Ensures strict live-name gating still accepts decorated item names
    ///     that normalize to the canonical payload text.
    /// </summary>
    [Fact]
    public void IsStructuredTooltipExactTextMatch_MatchesDecoratedItemName()
    {
        const string visibleText = "\uE03C Super-Potion";
        const string canonicalText = "Super-Potion";

        var result = Echoglossian.IsStructuredTooltipExactTextMatch(
            visibleText,
            canonicalText);

        Assert.True(result);
    }

    /// <summary>
    ///     Ensures strict live-name gating does not treat a shorter similar name
    ///     as an exact match for a different action.
    /// </summary>
    [Fact]
    public void IsStructuredTooltipExactTextMatch_RejectsSubstringNameMatch()
    {
        const string visibleText = "Enhanced En Avant";
        const string canonicalText = "Enhanced En Avant II";

        var result = Echoglossian.IsStructuredTooltipExactTextMatch(
            visibleText,
            canonicalText);

        Assert.False(result);
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
    ///     Ensures native tooltip mutation is blocked when the canonical
    ///     payload cannot cover the visible description.
    /// </summary>
    [Fact]
    public void CanApplyStructuredTooltipNative_BlocksTitleOnlyPayloads()
    {
        var result = Echoglossian.CanApplyStructuredTooltipNative(
            descriptionExpected: false,
            nameNodeResolved: true,
            nameNodeSupportsPlainTextMutation: true,
            descriptionNodeResolved: true,
            descriptionNodeSupportsPlainTextMutation: true);

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
    ///     Ensures native UI mode does not mix plugin-overlay text with an
    ///     unmodified native tooltip when native mutation is unsafe.
    /// </summary>
    [Fact]
    public void ShouldShowStructuredTooltipOverlay_NativeMutationUnavailable_HidesFallback()
    {
        var result = Echoglossian.ShouldShowStructuredTooltipOverlay(
            useOverlayOnly: false,
            useSwapOverlay: false,
            nativeApplySucceeded: false);

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures native tooltip mutation is blocked when a resolved live
    ///     node contains formatting payloads that plain-text writes would lose.
    /// </summary>
    [Fact]
    public void CanApplyStructuredTooltipNative_BlocksNonTextOnlyNodes()
    {
        var result = Echoglossian.CanApplyStructuredTooltipNative(
            descriptionExpected: true,
            nameNodeResolved: true,
            nameNodeSupportsPlainTextMutation: false,
            descriptionNodeResolved: true,
            descriptionNodeSupportsPlainTextMutation: true);

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures successful native UI mode leaves the plugin tooltip hidden.
    /// </summary>
    [Fact]
    public void ShouldShowStructuredTooltipOverlay_NativeMutationSucceeded_HidesFallback()
    {
        var result = Echoglossian.ShouldShowStructuredTooltipOverlay(
            useOverlayOnly: false,
            useSwapOverlay: false,
            nativeApplySucceeded: true);

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures cached tooltip node addresses are reused only while they are
    ///     still present in the current addon tree.
    /// </summary>
    [Fact]
    public void AreStructuredTooltipNodeAddressesCurrent_AcceptsCurrentAddresses()
    {
        var currentNodeAddresses = new HashSet<nint>
        {
            (nint)100,
            (nint)200,
        };

        var result = Echoglossian.AreStructuredTooltipNodeAddressesCurrent(
            currentNodeAddresses,
            nameNodeAddress: (nint)100,
            descriptionNodeAddress: (nint)200);

        Assert.True(result);
    }

    /// <summary>
    ///     Ensures cached tooltip node addresses are discarded when an addon
    ///     refresh removes one of the previously resolved nodes.
    /// </summary>
    [Fact]
    public void AreStructuredTooltipNodeAddressesCurrent_RejectsStaleAddresses()
    {
        var currentNodeAddresses = new HashSet<nint>
        {
            (nint)100,
        };

        var result = Echoglossian.AreStructuredTooltipNodeAddressesCurrent(
            currentNodeAddresses,
            nameNodeAddress: (nint)100,
            descriptionNodeAddress: (nint)200);

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures identical numeric action ids from different source payloads
    ///     do not reuse the same mutable native-tooltip state.
    /// </summary>
    [Fact]
    public void HasStructuredTooltipContentIdentity_RejectsCollidingIdsWithDifferentSourceHashes()
    {
        var result = Echoglossian.HasStructuredTooltipContentIdentity(
            leftContentId: 3,
            leftContentKind: 1,
            leftSourceContentHash: "LIMIT_BREAK",
            rightContentId: 3,
            rightContentKind: 1,
            rightSourceContentHash: "FOLLOW");

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures native-tooltip state remains reusable for the exact same
    ///     canonical source payload.
    /// </summary>
    [Fact]
    public void HasStructuredTooltipContentIdentity_AcceptsMatchingSourceHashes()
    {
        var result = Echoglossian.HasStructuredTooltipContentIdentity(
            leftContentId: 3,
            leftContentKind: 1,
            leftSourceContentHash: "FOLLOW",
            rightContentId: 3,
            rightContentKind: 1,
            rightSourceContentHash: "FOLLOW");

        Assert.True(result);
    }

    /// <summary>
    ///     Ensures restoration never replaces text that the game has already
    ///     repopulated for a different tooltip on a recycled native node.
    /// </summary>
    [Fact]
    public void ShouldRestoreStructuredTooltipNodeText_RejectsRecycledGameText()
    {
        var result = Echoglossian.ShouldRestoreStructuredTooltipNodeText(
            liveText: "Follow",
            translatedText: "Limit Break");

        Assert.False(result);
    }

    /// <summary>
    ///     Ensures restoration remains available while the node still contains
    ///     the translated text written by this runtime.
    /// </summary>
    [Fact]
    public void ShouldRestoreStructuredTooltipNodeText_AcceptsOwnedTranslatedText()
    {
        var result = Echoglossian.ShouldRestoreStructuredTooltipNodeText(
            liveText: "Quebra de limite",
            translatedText: "Quebra de limite");

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
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Standard Step"),
                false),
            new Echoglossian.StructuredTooltipTextNodeCandidate(
                (nint)2,
                "Standard Step",
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Standard Step"),
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

    /// <summary>
    ///     Ensures strict live-name candidate resolution does not accept a
    ///     substring-only match for a different tooltip name.
    /// </summary>
    [Fact]
    public void TryFindBestStructuredTooltipExactTextNodeCandidate_RejectsSubstringOnlyCandidate()
    {
        IReadOnlyList<Echoglossian.StructuredTooltipTextNodeCandidate> candidates =
        [
            new Echoglossian.StructuredTooltipTextNodeCandidate(
                (nint)1,
                "Enhanced En Avant",
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Enhanced En Avant"),
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Enhanced En Avant"),
                true),
        ];

        var found = Echoglossian.TryFindBestStructuredTooltipExactTextNodeCandidate(
            candidates,
            "Enhanced En Avant II",
            excludedNodeAddress: 0,
            out _);

        Assert.False(found);
    }

    /// <summary>
    ///     Ensures native description matching uses the evaluated source text
    ///     when the live node retains SeString formatting that differs from the
    ///     canonical sheet payload.
    /// </summary>
    [Fact]
    public void TryFindBestStructuredTooltipExactTextNodeCandidate_MatchesEvaluatedSourceText()
    {
        const string canonicalDescription =
            "Increases movement speed. Duration: 10s (20s when not in combat).";
        IReadOnlyList<Echoglossian.StructuredTooltipTextNodeCandidate> candidates =
        [
            new Echoglossian.StructuredTooltipTextNodeCandidate(
                (nint)1,
                "Increases movement speed. Duration: <If(Combat,10,20)>s.",
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Increases movement speed. Duration: <If(Combat,10,20)>s."),
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    canonicalDescription),
                true),
        ];

        var found = Echoglossian.TryFindBestStructuredTooltipExactTextNodeCandidate(
            candidates,
            canonicalDescription,
            excludedNodeAddress: 0,
            out var bestCandidate);

        Assert.True(found);
        Assert.Equal((nint)1, bestCandidate.NodeAddress);
    }

    /// <summary>
    ///     Ensures the currently visible exact match wins over a different node
    ///     that can only be explained by its retained source payload.
    /// </summary>
    [Fact]
    public void TryFindBestStructuredTooltipExactTextNodeCandidate_PrefersVisibleTextOverEvaluatedSource()
    {
        const string canonicalDescription = "Delivers an attack with a potency of 300.";
        IReadOnlyList<Echoglossian.StructuredTooltipTextNodeCandidate> candidates =
        [
            new Echoglossian.StructuredTooltipTextNodeCandidate(
                (nint)1,
                "Unrelated current node.",
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    "Unrelated current node."),
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    canonicalDescription),
                true),
            new Echoglossian.StructuredTooltipTextNodeCandidate(
                (nint)2,
                canonicalDescription,
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    canonicalDescription),
                Echoglossian.NormalizeStructuredTooltipLookupText(
                    canonicalDescription),
                true),
        ];

        var found = Echoglossian.TryFindBestStructuredTooltipExactTextNodeCandidate(
            candidates,
            canonicalDescription,
            excludedNodeAddress: 0,
            out var bestCandidate);

        Assert.True(found);
        Assert.Equal((nint)2, bestCandidate.NodeAddress);
    }

    /// <summary>
    ///     Ensures ActionDetail does not reuse a stale agent-backed action id
    ///     while a live item hover is active.
    /// </summary>
    [Fact]
    public void ShouldUseActionDetailAgentFallback_BlocksFallbackDuringItemHover()
    {
        Assert.False(
            Echoglossian.ShouldUseActionDetailAgentFallback(
                hoveredActionId: 0,
                hoveredItemId: 23167));
        Assert.True(
            Echoglossian.ShouldUseActionDetailAgentFallback(
                hoveredActionId: 0,
                hoveredItemId: 0));
    }

    /// <summary>
    ///     Ensures ItemDetail does not reuse a stale agent-backed item id while
    ///     a live action hover is active.
    /// </summary>
    [Fact]
    public void ShouldUseItemDetailAgentFallback_BlocksFallbackDuringActionHover()
    {
        Assert.False(
            Echoglossian.ShouldUseItemDetailAgentFallback(
                hoveredItemId: 0,
                hoveredActionId: 15997));
        Assert.True(
            Echoglossian.ShouldUseItemDetailAgentFallback(
                hoveredItemId: 0,
                hoveredActionId: 0));
    }

    /// <summary>
    ///     Ensures a live action hover suppresses a lingering direct item
    ///     hover before ItemDetail can render a stale overlay.
    /// </summary>
    [Fact]
    public void ShouldSuppressItemDetailDuringActionHover_BlocksLingeringItemState()
    {
        Assert.True(
            Echoglossian.ShouldSuppressItemDetailDuringActionHover(
                hoveredActionId: 20,
                isActionDetailActive: true));
        Assert.False(
            Echoglossian.ShouldSuppressItemDetailDuringActionHover(
                hoveredActionId: 20,
                isActionDetailActive: false));
        Assert.False(
            Echoglossian.ShouldSuppressItemDetailDuringActionHover(
                hoveredActionId: 0,
                isActionDetailActive: true));
    }
}
