// <copyright file="TooltipSemanticLineHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers semantic line preservation for the dedicated Tooltip addon.
/// </summary>
public sealed class TooltipSemanticLineHelperTests
{
    /// <summary>
    ///     Ensures Tooltip translation can flatten semantic lines for provider
    ///     translation and rebuild them afterward without losing the original
    ///     line boundaries.
    /// </summary>
    [Fact]
    public void FlattenAndRebuildTranslatedTextNodes_PreservesSemanticLineBreaks()
    {
        var originalTextNodes = new SortedDictionary<string, string>(
            new Dictionary<string, string>
            {
                ["2:0"] = "Reduced Rates\nTeleportation fees are reduced.",
            },
            StringComparer.Ordinal);
        var flattened = TooltipSemanticLineHelper.FlattenTextNodesForTranslation(
            originalTextNodes);

        Assert.Equal("Reduced Rates", flattened["2:0#0"]);
        Assert.Equal(
            "Teleportation fees are reduced.",
            flattened["2:0#1"]);

        var translatedLines = new SortedDictionary<string, string>(
            new Dictionary<string, string>
            {
                ["2:0#0"] = "Tarifas reduzidas",
                ["2:0#1"] = "As taxas de teletransporte foram reduzidas.",
            },
            StringComparer.Ordinal);

        var rebuilt = TooltipSemanticLineHelper.TryRebuildTranslatedTextNodes(
            originalTextNodes,
            translatedLines,
            out var rebuiltTextNodes);

        Assert.True(rebuilt);
        Assert.Equal(
            "Tarifas reduzidas\nAs taxas de teletransporte foram reduzidas.",
            rebuiltTextNodes["2:0"]);
    }

    /// <summary>
    ///     Ensures one resolved Tooltip translation that collapsed semantic
    ///     source lines is rejected so the runtime can queue a corrected
    ///     line-preserving translation.
    /// </summary>
    [Fact]
    public void HasCompatibleSemanticLineStructure_ReturnsFalse_WhenTranslationCollapsesSemanticLines()
    {
        Assert.False(
            TooltipSemanticLineHelper.HasCompatibleSemanticLineStructure(
                "Grit\nEnmity is increased.",
                "GritEnmity é aumentado."));
    }

    /// <summary>
    ///     Ensures ordinary single-line Tooltip text remains acceptable for
    ///     native apply and persistence.
    /// </summary>
    [Fact]
    public void HasCompatibleSemanticLineStructure_ReturnsTrue_ForSingleLineTooltipText()
    {
        Assert.True(
            TooltipSemanticLineHelper.HasCompatibleSemanticLineStructure(
                "Character",
                "Personagem"));
    }
}
