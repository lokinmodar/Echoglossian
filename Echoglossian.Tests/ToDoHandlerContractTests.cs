// <copyright file="ToDoHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.NativeUI.Helpers;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers dedicated ToDo runtime request behavior.
/// </summary>
public sealed class ToDoHandlerContractTests
{
    /// <summary>
    ///     Ensures changing only the dedicated ToDo toggle invalidates addon
    ///     handler registration.
    /// </summary>
    [Fact]
    public void AddonHandlerRegistrationSignature_ChangesWhenToDoToggleChanges()
    {
        var disabled = new Config { TranslateToDo = false };
        var enabled = new Config { TranslateToDo = true };

        Assert.NotEqual(
            Echoglossian.ComputeAddonHandlerRegistrationSignature(disabled),
            Echoglossian.ComputeAddonHandlerRegistrationSignature(enabled));
    }

    /// <summary>
    ///     Ensures in-flight and failed payloads are short-circuited before
    ///     persistence lookup and newer visible work rejects stale completion.
    /// </summary>
    [Fact]
    public void ToDoRuntimeRequestState_SuppressesRepeatedLookupAndRejectsStaleCompletion()
    {
        var state = new ToDoRuntimeRequestState();
        var first = new ToDoTranslationOperation(
            "FIRST",
            new ToDoTranslationScope("ja", "en", 1, "7.0"));
        var second = new ToDoTranslationOperation(
            "SECOND",
            new ToDoTranslationScope("ja", "en", 1, "7.0"));

        var firstGeneration = state.ObserveVisibleOperation(first);
        Assert.True(state.TryStart(first, firstGeneration));
        Assert.True(state.ShouldSkipPersistenceLookup(first));

        var secondGeneration = state.ObserveVisibleOperation(second);
        Assert.True(state.TryStart(second, secondGeneration));
        Assert.False(state.TryComplete(first, firstGeneration));
        Assert.True(state.TryComplete(second, secondGeneration));
        Assert.False(state.ShouldSkipPersistenceLookup(second));

        var failedGeneration = state.ObserveVisibleOperation(first);
        Assert.True(state.TryStart(first, failedGeneration));
        state.MarkFailed(first, failedGeneration);
        Assert.True(state.ShouldSkipPersistenceLookup(first));
    }

    /// <summary>
    ///     Ensures a countdown-only change observes the same in-flight
    ///     operation and therefore cannot issue another persistence lookup.
    /// </summary>
    [Fact]
    public void ToDoRuntimeRequestState_TimerOnlyChangesReuseInFlightOperation()
    {
        var beforeTick = new ToDoPayload(
            [
                new ToDoCapturedText("10:0", 10, "Duty name", false),
                new ToDoCapturedText("11:0", 11, "10:00", true),
            ]);
        var afterTick = new ToDoPayload(
            [
                new ToDoCapturedText("10:0", 10, "Duty name", false),
                new ToDoCapturedText("11:0", 11, "09:59", true),
            ]);
        var scope = new ToDoTranslationScope("ja", "en", 1, "7.0");
        var beforeOperation = new ToDoTranslationOperation(
            beforeTick.ComputeSourceContentHash(),
            scope);
        var afterOperation = new ToDoTranslationOperation(
            afterTick.ComputeSourceContentHash(),
            scope);
        var state = new ToDoRuntimeRequestState();

        var generation = state.ObserveVisibleOperation(beforeOperation);
        Assert.True(state.TryStart(beforeOperation, generation));

        Assert.Equal(generation, state.ObserveVisibleOperation(afterOperation));
        Assert.True(state.ShouldSkipPersistenceLookup(afterOperation));
    }

    /// <summary>
    ///     Ensures overlay-only languages keep the dedicated ToDo surface in
    ///     tooltip presentation and never rewrite native text.
    /// </summary>
    [Fact]
    public void ToDoPresentationPolicy_OverlayOnlyLanguageUsesTooltipsWithoutNativeWrites()
    {
        var policy = ToDoPresentationPolicy.Create(
            JournalTranslationDisplayMode.NativeUiTranslation,
            overlayOnlyLanguage: true);

        Assert.True(policy.UsesHoverTooltips);
        Assert.False(policy.WritesNativeTranslation);
        Assert.False(policy.HoverShowsOriginal);
    }
}
