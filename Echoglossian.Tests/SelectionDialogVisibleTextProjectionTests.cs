// <copyright file="SelectionDialogVisibleTextProjectionTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers projection of visible selection-dialog text nodes onto the
///     authoritative ordered payload used for persistence and translation.
/// </summary>
public sealed class SelectionDialogVisibleTextProjectionTests
{
    /// <summary>
    ///     Ensures visible text nodes can align to a structured payload even
    ///     when the native addon exposes extra non-visible strings between the
    ///     displayed entries.
    /// </summary>
    [Fact]
    public void MatchVisibleTexts_ProjectsVisibleSubsetInOrder()
    {
        var matches = InvokeMatchVisibleTexts(
            ["Enter the barracks?", "unused hidden prompt", "Yes", "No"],
            ["Enter the barracks?", "Yes", "No"]);

        Assert.Collection(
            matches,
            match =>
            {
                Assert.Equal(0, match.VisibleIndex);
                Assert.Equal(0, match.SourceIndex);
            },
            match =>
            {
                Assert.Equal(1, match.VisibleIndex);
                Assert.Equal(2, match.SourceIndex);
            },
            match =>
            {
                Assert.Equal(2, match.VisibleIndex);
                Assert.Equal(3, match.SourceIndex);
            });
    }

    /// <summary>
    ///     Ensures duplicate visible labels remain stable by consuming source
    ///     entries in order instead of collapsing repeated text into one map
    ///     key.
    /// </summary>
    [Fact]
    public void MatchVisibleTexts_PreservesDuplicateRowsInOrder()
    {
        var matches = InvokeMatchVisibleTexts(
            ["???", "???", "???"],
            ["???", "???"]);

        Assert.Collection(
            matches,
            match =>
            {
                Assert.Equal(0, match.VisibleIndex);
                Assert.Equal(0, match.SourceIndex);
            },
            match =>
            {
                Assert.Equal(1, match.VisibleIndex);
                Assert.Equal(1, match.SourceIndex);
            });
    }

    /// <summary>
    ///     Ensures projection follows the current on-screen text order when
    ///     the addon tree exposes the prompt node after the option nodes.
    /// </summary>
    [Fact]
    public void MatchVisibleTexts_UsesVisualOrderWhenTraversalOrderPlacesTitleLast()
    {
        var matches = InvokeMatchVisibleTextCandidates(
            ["What would you like to do?", "Option A", "Option B", "Nothing."],
            [
                (0, "Option A", 50, 10),
                (1, "Option B", 72, 10),
                (2, "Nothing.", 94, 10),
                (3, "What would you like to do?", 21, 22),
            ]);

        Assert.Collection(
            matches,
            match =>
            {
                Assert.Equal(3, match.VisibleIndex);
                Assert.Equal(0, match.SourceIndex);
            },
            match =>
            {
                Assert.Equal(0, match.VisibleIndex);
                Assert.Equal(1, match.SourceIndex);
            },
            match =>
            {
                Assert.Equal(1, match.VisibleIndex);
                Assert.Equal(2, match.SourceIndex);
            },
            match =>
            {
                Assert.Equal(2, match.VisibleIndex);
                Assert.Equal(3, match.SourceIndex);
            });
    }

    private static IReadOnlyList<(int VisibleIndex, int SourceIndex)>
        InvokeMatchVisibleTexts(
            IReadOnlyList<string> sourceTexts,
            IReadOnlyList<string> visibleTexts)
    {
        var helperType = typeof(Echoglossian).Assembly.GetType(
            "Echoglossian.NativeUI.AddonHandlers.SelectionDialogs.SelectionDialogVisibleTextProjection");
        var method = helperType?.GetMethod(
            "MatchVisibleTexts",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [typeof(IReadOnlyList<string>), typeof(IReadOnlyList<string>)],
            modifiers: null);

        Assert.NotNull(helperType);
        Assert.NotNull(method);

        var matches = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            method!.Invoke(null, [sourceTexts, visibleTexts]));
        var results = new List<(int VisibleIndex, int SourceIndex)>();
        foreach (var match in matches)
        {
            Assert.NotNull(match);
            var matchType = match!.GetType();
            results.Add((
                (int)(matchType.GetProperty("VisibleIndex")?.GetValue(match) ?? -1),
                (int)(matchType.GetProperty("SourceIndex")?.GetValue(match) ?? -1)));
        }

        return results;
    }

    private static IReadOnlyList<(int VisibleIndex, int SourceIndex)>
        InvokeMatchVisibleTextCandidates(
            IReadOnlyList<string> sourceTexts,
            IReadOnlyList<(int VisibleIndex, string Text, int ScreenY, int ScreenX)>
                visibleTexts)
    {
        var helperType = typeof(Echoglossian).Assembly.GetType(
            "Echoglossian.NativeUI.AddonHandlers.SelectionDialogs.SelectionDialogVisibleTextProjection");
        var candidateType = typeof(Echoglossian).Assembly.GetType(
            "Echoglossian.NativeUI.AddonHandlers.SelectionDialogs.SelectionDialogVisibleTextProjection+SelectionDialogVisibleTextCandidate");
        var method = helperType?.GetMethod(
            "MatchVisibleTexts",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [typeof(IReadOnlyList<string>), typeof(IReadOnlyList<>).MakeGenericType(candidateType!)],
            modifiers: null);

        Assert.NotNull(helperType);
        Assert.NotNull(candidateType);
        Assert.NotNull(method);

        var candidates = (System.Collections.IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(candidateType!))!;
        foreach (var visibleText in visibleTexts)
        {
            candidates.Add(Activator.CreateInstance(
                candidateType!,
                visibleText.VisibleIndex,
                visibleText.Text,
                visibleText.ScreenY,
                visibleText.ScreenX)!);
        }

        var matches = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            method!.Invoke(null, [sourceTexts, candidates]));
        var results = new List<(int VisibleIndex, int SourceIndex)>();
        foreach (var match in matches)
        {
            Assert.NotNull(match);
            var matchType = match!.GetType();
            results.Add((
                (int)(matchType.GetProperty("VisibleIndex")?.GetValue(match) ?? -1),
                (int)(matchType.GetProperty("SourceIndex")?.GetValue(match) ?? -1)));
        }

        return results;
    }
}
