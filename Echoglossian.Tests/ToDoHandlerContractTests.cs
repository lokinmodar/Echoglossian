// <copyright file="ToDoHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

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
    ///     Ensures stable PreDraw work is skipped only while the observed
    ///     operation remains applied or otherwise request-suppressed.
    /// </summary>
    [Fact]
    public void ToDoRuntimeRequestState_StablePreDrawRequiresReusableWork()
    {
        var state = new ToDoRuntimeRequestState();
        var operation = new ToDoTranslationOperation(
            "PAYLOAD",
            new ToDoTranslationScope("ja", "en", 1, "7.0"));
        var generation = state.ObserveVisibleOperation(operation);

        Assert.True(state.TryStart(operation, generation));
        Assert.True(InvokeStablePreDrawDecision(
            state,
            operation,
            presentationStable: false,
            nodeSnapshotStable: true));
        Assert.False(InvokeStablePreDrawDecision(
            state,
            operation,
            presentationStable: false,
            nodeSnapshotStable: false));

        Assert.True(state.TryComplete(operation, generation));
        Assert.False(InvokeStablePreDrawDecision(
            state,
            operation,
            presentationStable: false,
            nodeSnapshotStable: true));
        Assert.True(InvokeStablePreDrawDecision(
            state,
            operation,
            presentationStable: true,
            nodeSnapshotStable: true));
    }

    /// <summary>
    ///     Ensures the dense handler path invokes the stable PreDraw shortcut
    ///     and presentation application does not traverse the addon again.
    /// </summary>
    [Fact]
    public void ToDoHandler_PreDrawReusesResolvedNodeSnapshot()
    {
        var process = typeof(ToDoHandler).GetMethod(
            "ProcessVisibleToDo",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var shortcut = typeof(ToDoHandler).GetMethod(
            "TryShortCircuitStablePreDraw",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var apply = typeof(ToDoHandler).GetMethod(
            "ApplyCurrentToDoPresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var resolver = typeof(ToDoTextNodeResolvers).GetMethod(
            "ResolveVisibleTextNodes",
            BindingFlags.Static | BindingFlags.Public);

        Assert.NotNull(process);
        Assert.NotNull(shortcut);
        Assert.NotNull(apply);
        Assert.NotNull(resolver);
        Assert.True(MethodReferences(process, shortcut));
        Assert.True(MethodReferences(process, resolver));
        Assert.False(MethodReferences(apply, resolver));
    }

    /// <summary>
    ///     Ensures stable PreDraw reuse still refreshes hover-tooltip
    ///     lifetime for tooltip presentation.
    /// </summary>
    [Fact]
    public void ToDoHandler_StablePreDrawRefreshesHoverTooltipLifetime()
    {
        var shortcut = typeof(ToDoHandler).GetMethod(
            "TryShortCircuitStablePreDraw",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var touch = typeof(HoverTooltipManager).GetMethod(
            nameof(HoverTooltipManager.TouchByPrefix),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(shortcut);
        Assert.NotNull(touch);
        Assert.True(MethodReferences(shortcut, touch));
    }

    /// <summary>
    ///     Ensures countdown rows with three-digit minute values remain native.
    /// </summary>
    [Theory]
    [InlineData("100:00")]
    [InlineData("123:45")]
    public void ToDoTextNodeResolvers_RecognizesThreeDigitCountdowns(string text)
    {
        var method = typeof(ToDoTextNodeResolvers).GetMethod(
            "IsTimerText",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True(Assert.IsType<bool>(method.Invoke(null, [text])));
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

    /// <summary>
    ///     Invokes the stable PreDraw decision on the real request state.
    /// </summary>
    /// <param name="state">The request state to evaluate.</param>
    /// <param name="operation">The visible operation.</param>
    /// <param name="presentationStable">Whether presentation state is unchanged.</param>
    /// <param name="nodeSnapshotStable">Whether cached visible nodes are unchanged.</param>
    /// <returns><c>true</c> when dense PreDraw work can be skipped.</returns>
    private static bool InvokeStablePreDrawDecision(
        ToDoRuntimeRequestState state,
        ToDoTranslationOperation operation,
        bool presentationStable,
        bool nodeSnapshotStable)
    {
        var method = typeof(ToDoRuntimeRequestState).GetMethod(
            "ShouldShortCircuitStablePreDraw",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(
            state,
            [operation, presentationStable, nodeSnapshotStable]));
    }

    /// <summary>
    ///     Determines whether one compiled method references another member.
    /// </summary>
    /// <param name="method">The method body to inspect.</param>
    /// <param name="referencedMember">The expected referenced member.</param>
    /// <returns><c>true</c> when the metadata token is referenced.</returns>
    private static bool MethodReferences(
        MethodInfo method,
        MemberInfo referencedMember)
    {
        var methodBody = method.GetMethodBody()?.GetILAsByteArray();
        if (methodBody == null)
        {
            return false;
        }

        var referencedToken = BitConverter.GetBytes(
            referencedMember.MetadataToken);
        return methodBody.AsSpan().IndexOf(referencedToken) >= 0;
    }
}
