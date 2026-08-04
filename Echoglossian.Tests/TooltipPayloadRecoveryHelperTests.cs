// <copyright file="TooltipPayloadRecoveryHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers canonical original recovery for the dedicated Tooltip addon.
/// </summary>
public sealed class TooltipPayloadRecoveryHelperTests
{
    /// <summary>
    ///     Ensures one live Tooltip payload that already shows translated text
    ///     with wrap-induced whitespace churn still recovers the canonical
    ///     original payload.
    /// </summary>
    [Fact]
    public void TryRecoverOriginalPayload_RecoversOriginal_WhenLiveTooltipWrapsTranslatedText()
    {
        var original = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "The Heat of Battle\nEXP earned through battle is increased.",
            });
        var translated = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "O Calor da Batalha aumenta o EXP ganho em batalha.",
            });
        var liveTranslated = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "O Calor da Batalha aumenta o EXP ganho\nem batalha.",
            });

        var resolved = TooltipPayloadRecoveryHelper.TryRecoverOriginalPayload(
            liveTranslated,
            new[]
            {
                new DbFirstPayloadRecoveryCandidate(original, translated),
            },
            out var recoveredOriginal);

        Assert.True(resolved);
        Assert.Equal(original, recoveredOriginal);
    }

    /// <summary>
    ///     Ensures translated-slot evidence survives semantic normalization so
    ///     the handler can suppress bad retranslation even when exact recovery
    ///     is not possible yet.
    /// </summary>
    [Fact]
    public void HasTranslatedSlotEvidence_ReturnsTrue_WhenLiveTooltipCollapsesWhitespace()
    {
        var liveTranslated = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "Tarifas reduzidasAs taxas de\nteletransporte foram reduzidas.",
            });
        var original = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "Reduced Rates\nTeleportation fees are reduced.",
            });
        var translated = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "Tarifas reduzidas As taxas de teletransporte foram reduzidas.",
            });

        Assert.True(
            TooltipPayloadRecoveryHelper.HasTranslatedSlotEvidence(
                liveTranslated,
                new[]
                {
                    new DbFirstPayloadRecoveryCandidate(original, translated),
                }));
    }

    /// <summary>
    ///     Ensures runtime-captured Tooltip text is rewritten back to the
    ///     canonical translated payload when the live node only differs by
    ///     wrap-introduced whitespace churn.
    /// </summary>
    [Fact]
    public void CanonicalizeLiveTextNodes_ReusesCanonicalTranslatedText_WhenLiveNodeWrapsAppliedText()
    {
        var liveNodes = new SortedDictionary<string, string>(
            new Dictionary<string, string>
            {
                ["2:0"] = "Céu\nlimpo",
            },
            StringComparer.Ordinal);
        var originalNodes = new SortedDictionary<string, string>(
            new Dictionary<string, string>
            {
                ["2:0"] = "Clear Skies",
            },
            StringComparer.Ordinal);
        var translatedNodes = new SortedDictionary<string, string>(
            new Dictionary<string, string>
            {
                ["2:0"] = "Céu limpo",
            },
            StringComparer.Ordinal);

        var canonicalized = TooltipPayloadRecoveryHelper.CanonicalizeLiveTextNodes(
            liveNodes,
            originalNodes,
            translatedNodes);

        Assert.Equal("Céu limpo", canonicalized["2:0"]);
    }

    /// <summary>
    ///     Ensures recovery candidates whose original and translated payloads
    ///     are only whitespace-mutated copies are rejected so poisoned rows do
    ///     not compete with canonical Tooltip originals.
    /// </summary>
    [Fact]
    public void HasSemanticallyDistinctPayloads_ReturnsFalse_ForWhitespaceOnlyPoisonedRows()
    {
        var original = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "Mente\nObscura",
            });
        var translated = CreatePayload(
            new Dictionary<string, string>
            {
                ["2:0"] = "MenteObscura",
            });

        Assert.False(
            TooltipPayloadRecoveryHelper.HasSemanticallyDistinctPayloads(
                original,
                translated));
    }

    /// <summary>
    ///     Creates one Tooltip-style text-node payload for tests.
    /// </summary>
    /// <param name="textNodes">The text-node values.</param>
    /// <returns>The payload.</returns>
    private static DbFirstGameWindowPayload CreatePayload(
        IDictionary<string, string> textNodes)
    {
        return new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>(),
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(
                textNodes,
                StringComparer.Ordinal));
    }
}
