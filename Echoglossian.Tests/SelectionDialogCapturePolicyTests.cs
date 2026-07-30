// <copyright file="SelectionDialogCapturePolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

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
    ///     Ensures the selection-dialog runtime can promote matching visible
    ///     text-node payloads so native apply and tooltip anchors use the live
    ///     nodes instead of detached structured sources.
    /// </summary>
    [Fact]
    public void ShouldPreferTextNodePayload_PrefersMatchingVisibleTexts()
    {
        Assert.True(InvokeShouldPreferTextNodePayload(
            ["Have sanction bestowed.", "Ask about sanction.", "Nothing."],
            ["Have sanction bestowed.", "Ask about sanction.", "Nothing."]));
    }

    /// <summary>
    ///     Ensures text-node promotion stays disabled when the visible node
    ///     shape diverges from the structured payload.
    /// </summary>
    [Fact]
    public void ShouldPreferTextNodePayload_RejectsMismatchedVisibleTexts()
    {
        Assert.False(InvokeShouldPreferTextNodePayload(
            ["What would you like to do?", "Have sanction bestowed."],
            ["Have sanction bestowed.", "Ask about sanction.", "Nothing."]));
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

    /// <summary>
    ///     Invokes the visible-node promotion policy through reflection.
    /// </summary>
    /// <param name="primaryTexts">The primary structured payload texts.</param>
    /// <param name="textNodeTexts">The visible text-node payload texts.</param>
    /// <returns>Whether the visible text nodes should be preferred.</returns>
    private static bool InvokeShouldPreferTextNodePayload(
        IReadOnlyList<string> primaryTexts,
        IReadOnlyList<string> textNodeTexts)
    {
        var policyType = typeof(PluginEntry).Assembly.GetType(
            "Echoglossian.NativeUI.AddonHandlers.SelectionDialogs.SelectionDialogCapturePolicy");
        var method = policyType?.GetMethod(
            "ShouldPreferTextNodePayload",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(policyType);
        Assert.NotNull(method);

        var result = method!.Invoke(
            null,
            [primaryTexts, textNodeTexts]);
        return Assert.IsType<bool>(result);
    }
}
