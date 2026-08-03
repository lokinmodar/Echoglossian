// <copyright file="JournalDetailObjectiveMatchingTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers JournalDetail objective reconciliation against canonical TODO rows.
/// </summary>
public class JournalDetailObjectiveMatchingTests
{
    /// <summary>
    /// Ensures one visible JournalDetail objective still resolves to the
    /// canonical TODO row when the native UI inserts hard line breaks while
    /// wrapping the text.
    /// </summary>
    [Fact]
    public void TryResolveObjectiveRowKeyByVisibleText_WrappedObjective_ReturnsMatchingTodoRow()
    {
        var method = typeof(JournalDetailHandler).GetMethod(
            "TryResolveObjectiveRowKeyByVisibleText",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(method);

        object?[] parameters =
        [
            this.CreateCanonicalData(),
            "Wait at the designated location, then follow the boy thief \rwithout being seen.",
            null,
        ];

        var resolved = (bool)(method!.Invoke(null, parameters) ?? false);

        Assert.True(resolved);
        Assert.Equal("_TODO_01", Assert.IsType<string>(parameters[2]));
    }

    /// <summary>
    /// Creates canonical data that reproduces the active Yedlihmad Hunt TODO
    /// rows involved in the JournalDetail mismatch.
    /// </summary>
    /// <returns>The canonical quest data fixture.</returns>
    private QuestCanonicalData CreateCanonicalData()
    {
        var snapshot = new QuestProgressSnapshot(
            QuestId: 70028,
            QuestSequence: 2,
            QuestName: "\ue0be The Yedlihmad Hunt",
            QuestSheetName: "quest/044/AktKza204_04492",
            QuestSteps:
            [
                new QuestProgressEntry(
                    default,
                    default,
                    "_TODO_00",
                    "Wait at the designated location."),
                new QuestProgressEntry(
                    default,
                    default,
                    "_TODO_01",
                    "Wait at the designated location, then follow the boy thief without being seen."),
            ],
            QuestSeqTexts: [],
            QuestSystemTexts: [],
            ContentHash: "test-hash");

        return QuestCanonicalData.Create(snapshot, "test-version");
    }
}
