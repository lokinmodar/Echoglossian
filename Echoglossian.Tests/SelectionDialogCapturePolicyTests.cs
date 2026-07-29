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
