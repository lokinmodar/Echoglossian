// <copyright file="SelectionDialogCapturePolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.SelectionDialogs;

using PluginEntry = Echoglossian.Echoglossian;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared capture-order contract for generic selection-dialog
///     runtimes.
/// </summary>
public sealed class SelectionDialogCapturePolicyTests
{
    /// <summary>
    ///     Ensures ATK values remain the preferred capture source when all
    ///     three capture paths are simultaneously available.
    /// </summary>
    [Fact]
    public void ResolveBestSource_PrefersAtkValuesBeforeOtherCapturePaths()
    {
        var result = InvokeResolveBestSource(
            hasAtkValueText: true,
            hasStringArrayText: true,
            hasReadableTextNodes: true);

        Assert.Equal("AtkValues", result);
    }

    /// <summary>
    ///     Ensures StringArrayData remains the second capture path before the
    ///     runtime falls all the way back to text-node scraping.
    /// </summary>
    [Fact]
    public void ResolveBestSource_FallsBackToStringArraysBeforeTextNodes()
    {
        var result = InvokeResolveBestSource(
            hasAtkValueText: false,
            hasStringArrayText: true,
            hasReadableTextNodes: true);

        Assert.Equal("StringArrayData", result);
    }

    /// <summary>
    ///     Ensures the visible text-node payload can override the structured
    ///     source when both payloads expose the same ordered texts.
    /// </summary>
    [Fact]
    public void ShouldPreferTextNodePayload_WhenVisibleTextsMatchStructuredSource()
    {
        var structuredPayload = SelectionDialogPayload.FromAtkValues(
            [10, 11],
            ["What would you like to do?", "Nothing"]);
        var textNodePayload = SelectionDialogPayload.FromTextNodes(
            [1, 2],
            ["What would you like to do?", "Nothing"]);

        Assert.True(
            SelectionDialogCapturePolicy.ShouldPreferTextNodePayload(
                structuredPayload,
                textNodePayload));
    }

    /// <summary>
    ///     Ensures unrelated text-node scraping cannot replace the structured
    ///     payload when the visible text diverges.
    /// </summary>
    [Fact]
    public void ShouldPreferTextNodePayload_RejectsMismatchedVisibleTexts()
    {
        var structuredPayload = SelectionDialogPayload.FromStringArrayData(
            7,
            [0, 1],
            ["What would you like to do?", "Nothing"]);
        var textNodePayload = SelectionDialogPayload.FromTextNodes(
            [1, 2],
            ["Question", "Something else"]);

        Assert.False(
            SelectionDialogCapturePolicy.ShouldPreferTextNodePayload(
                structuredPayload,
                textNodePayload));
    }

    /// <summary>
    ///     Invokes the shared capture policy through reflection so the tests
    ///     fail cleanly before the infrastructure exists.
    /// </summary>
    /// <param name="hasAtkValueText">Whether ATK values contain text.</param>
    /// <param name="hasStringArrayText">Whether StringArrayData contains text.</param>
    /// <param name="hasReadableTextNodes">Whether text nodes contain text.</param>
    /// <returns>The resolved source kind name.</returns>
    private static string InvokeResolveBestSource(
        bool hasAtkValueText,
        bool hasStringArrayText,
        bool hasReadableTextNodes)
    {
        var policyType = typeof(PluginEntry).Assembly.GetType(
            "Echoglossian.NativeUI.AddonHandlers.SelectionDialogs.SelectionDialogCapturePolicy");
        var method = policyType?.GetMethod(
            "ResolveBestSource",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(policyType);
        Assert.NotNull(method);

        var result = method!.Invoke(
            null,
            [hasAtkValueText, hasStringArrayText, hasReadableTextNodes]);
        Assert.NotNull(result);
        return result!.ToString() ?? string.Empty;
    }
}
