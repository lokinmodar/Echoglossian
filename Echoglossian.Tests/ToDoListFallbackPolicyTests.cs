// <copyright file="ToDoListFallbackPolicyTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers retry and prefetch decisions while the ToDoList relies on persisted
/// fallback data.
/// </summary>
public class ToDoListFallbackPolicyTests
{
    /// <summary>
    /// Ensures the ToDoList stops queueing accepted-quest prefetch work once a
    /// persisted translated title already exists and only the live todo
    /// snapshot is missing.
    /// </summary>
    [Fact]
    public void ShouldRequestAcceptedQuestPrefetchWhenTodoProgressUnavailable_FallbackExists_ReturnsFalse()
    {
        Assert.False(
            ToDoListHandler.ShouldRequestAcceptedQuestPrefetchWhenTodoProgressUnavailable(
                hasFallbackTranslatedTitle: true));
    }

    /// <summary>
    /// Ensures the ToDoList still requests accepted-quest prefetch work when
    /// no persisted fallback title exists yet.
    /// </summary>
    [Fact]
    public void ShouldRequestAcceptedQuestPrefetchWhenTodoProgressUnavailable_NoFallback_ReturnsTrue()
    {
        Assert.True(
            ToDoListHandler.ShouldRequestAcceptedQuestPrefetchWhenTodoProgressUnavailable(
                hasFallbackTranslatedTitle: false));
    }

    /// <summary>
    /// Ensures a title-only fallback can settle immediately when there are no
    /// remaining objective rows that depend on the live todo snapshot.
    /// </summary>
    [Fact]
    public void ShouldKeepRetryingWithoutTodoProgress_FallbackWithoutTrackableObjectives_ReturnsFalse()
    {
        Assert.False(
            ToDoListHandler.ShouldKeepRetryingWithoutTodoProgress(
                hasFallbackTranslatedTitle: true,
                trackableObjectiveCount: 0));
    }

    /// <summary>
    /// Ensures the ToDoList can still poll locally for objective upgrades when
    /// the persisted fallback title is ready but visible objective rows remain
    /// unresolved.
    /// </summary>
    [Fact]
    public void ShouldKeepRetryingWithoutTodoProgress_FallbackWithTrackableObjectives_ReturnsTrue()
    {
        Assert.True(
            ToDoListHandler.ShouldKeepRetryingWithoutTodoProgress(
                hasFallbackTranslatedTitle: true,
                trackableObjectiveCount: 1));
    }

    /// <summary>
    /// Ensures repeated identical ToDoList diagnostic states do not emit a
    /// fresh log line every retry tick.
    /// </summary>
    [Fact]
    public void ShouldEmitToDoQuestDiagnosticState_UnchangedState_ReturnsFalse()
    {
        Assert.False(
            ToDoListHandler.ShouldEmitToDoQuestDiagnosticState(
                previousState: "fallback|1|False|True",
                nextState: "fallback|1|False|True"));
    }

    /// <summary>
    /// Ensures the ToDoList still emits a new diagnostic line once the quest
    /// resolution state actually changes.
    /// </summary>
    [Fact]
    public void ShouldEmitToDoQuestDiagnosticState_ChangedState_ReturnsTrue()
    {
        Assert.True(
            ToDoListHandler.ShouldEmitToDoQuestDiagnosticState(
                previousState: "fallback|1|False|True",
                nextState: "resolved|69760|69760:255|1"));
    }
}
