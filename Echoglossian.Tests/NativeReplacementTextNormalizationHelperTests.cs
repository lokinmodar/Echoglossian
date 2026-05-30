// <copyright file="NativeReplacementTextNormalizationHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Common;
using Echoglossian.NativeUI.Helpers;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers native replacement text normalization for shared DB-first
///     payloads.
/// </summary>
public class NativeReplacementTextNormalizationHelperTests
{
    /// <summary>
    ///     Ensures native replacement normalization applies to every payload
    ///     surface that can be written into the live UI.
    /// </summary>
    [Fact]
    public void NormalizePayload_NormalizesAtkStringArrayAndTextNodeValues()
    {
        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = "café",
            },
            new SortedDictionary<int, string>
            {
                [3] = "ação",
            },
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["7:0"] = "Łódź",
            });

        var normalizedPayload =
            NativeReplacementTextNormalizationHelper.NormalizePayload(
                payload,
                text => text
                    .Replace("é", "e", StringComparison.Ordinal)
                    .Replace("ã", "a", StringComparison.Ordinal)
                    .Replace("ç", "c", StringComparison.Ordinal)
                    .Replace("Ł", "L", StringComparison.Ordinal)
                    .Replace("ó", "o", StringComparison.Ordinal)
                    .Replace("ź", "z", StringComparison.Ordinal));

        Assert.Equal("cafe", normalizedPayload.AtkValues[0]);
        Assert.Equal("acao", normalizedPayload.StringArrayValues[3]);
        Assert.Equal("Lodz", normalizedPayload.TextNodes["7:0"]);
    }

    /// <summary>
    ///     Ensures the helper preserves the incoming payload when the supplied
    ///     normalizer does not change any values.
    /// </summary>
    [Fact]
    public void NormalizePayload_PreservesExistingValuesWhenNoChangesOccur()
    {
        var payload = new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = "plain",
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["7:0"] = "ascii",
            });

        var normalizedPayload =
            NativeReplacementTextNormalizationHelper.NormalizePayload(
                payload,
                static text => text);

        Assert.Equal(payload, normalizedPayload);
    }
}
