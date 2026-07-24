// <copyright file="ToDoListObjectiveMatchingTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers the canonical objective matching used by the ToDoList runtime.
/// </summary>
public class ToDoListObjectiveMatchingTests
{
    /// <summary>
    /// Ensures a visible later quest objective resolves to its own TODO row
    /// instead of the first row in the canonical quest sheet.
    /// </summary>
    [Fact]
    public void TryResolveObjectiveRowKeyByText_LaterVisibleObjective_ReturnsMatchingTodoRow()
    {
        var canonicalData = this.CreateCanonicalData();

        var rowKeys = canonicalData.EnumerateObjectiveRowKeysByText(
            "Sneak with Radovan at Spineless Basin.").ToArray();

        Assert.Equal(["_TODO_01"], rowKeys);
    }

    /// <summary>
    /// Ensures a previously swapped objective is restored to its original
    /// source text before resolving the canonical TODO row.
    /// </summary>
    [Fact]
    public void TryResolveObjectiveRowKeyByText_SwappedVisibleObjective_UsesRecoveredOriginalText()
    {
        var canonicalData = this.CreateCanonicalData();
        var originalObjectiveText = QuestAddonOriginalTextHelper.ResolveOriginalVisibleText(
            "Esgueire-se com Radovan na Bacia Sem Espinha.",
            "Sneak with Radovan at Spineless Basin.",
            "Esgueire-se com Radovan na Bacia Sem Espinha.");

        var rowKeys = canonicalData.EnumerateObjectiveRowKeysByText(
            originalObjectiveText).ToArray();

        Assert.Equal(["_TODO_01"], rowKeys);
    }

    /// <summary>
    /// Ensures the ToDoList does not infer a TODO row from order when the live
    /// objective is not part of the canonical quest payload.
    /// </summary>
    [Fact]
    public void TryResolveObjectiveRowKeyByText_UnknownVisibleObjective_DoesNotMatchAnotherTodoRow()
    {
        var canonicalData = this.CreateCanonicalData();

        var rowKeys = canonicalData.EnumerateObjectiveRowKeysByText(
            "Speak with Alphinaud.").ToArray();

        Assert.Empty(rowKeys);
    }

    /// <summary>
    /// Creates canonical data that reproduces the objective order persisted
    /// for the affected quest.
    /// </summary>
    /// <returns>The canonical quest data fixture.</returns>
    private QuestCanonicalData CreateCanonicalData()
    {
        var snapshot = new QuestProgressSnapshot(
            QuestId: 68799,
            QuestSequence: 0,
            QuestName: "For Better or Worse",
            QuestSheetName: "quest/032/LucKbb111_03263",
            QuestSteps:
            [
                new QuestProgressEntry(
                    default,
                    default,
                    "_TODO_00",
                    "Speak with Oriel at the Gate of Thal."),
                new QuestProgressEntry(
                    default,
                    default,
                    "_TODO_01",
                    "Sneak with Radovan at Spineless Basin."),
            ],
            QuestSeqTexts: [],
            QuestSystemTexts: [],
            ContentHash: "test-hash");

        return QuestCanonicalData.Create(snapshot, "test-version");
    }
}
