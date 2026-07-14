// <copyright file="NativeRuntimeSourceScopeTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.NativeUI.AddonHandlers.Common;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers source-scoped native runtime state and DB-first work identity.
/// </summary>
public class NativeRuntimeSourceScopeTests
{
    /// <summary>
    ///     Ensures identical visible payloads cannot reuse a live DB-first
    ///     translation after the client source changes.
    /// </summary>
    [Fact]
    public void RuntimeState_DifferentSource_DoesNotMatch()
    {
        var payload = CreatePayload("Hello");
        var state = new DbFirstGameWindowRuntimeState(
            "en",
            "payload-key",
            payload,
            CreatePayload("Ola"));

        Assert.True(state.MatchesSource(new SourceClientLanguage("en", "en")));
        Assert.False(state.MatchesSource(new SourceClientLanguage("de", "de")));
        Assert.True(state.ShouldInvalidateFor(
            new SourceClientLanguage("de", "de")));
    }

    /// <summary>
    ///     Ensures an unresolved client source invalidates plugin-owned runtime
    ///     state instead of allowing the prior result to remain active.
    /// </summary>
    [Fact]
    public void RuntimeState_UnknownSource_DoesNotMatch()
    {
        var state = new DbFirstGameWindowRuntimeState(
            "en",
            "payload-key",
            CreatePayload("Hello"),
            CreatePayload("Ola"));

        Assert.False(state.MatchesSource(null));
        Assert.True(state.ShouldInvalidateFor(null));
    }

    /// <summary>
    ///     Ensures dialogue local-cache matching includes canonical source
    ///     identity even when speaker and visible text are identical.
    /// </summary>
    [Fact]
    public void DialogueCache_DifferentSource_DoesNotMatch()
    {
        var matches = NativeRuntimeSourceScope.MatchesDialogueState(
            "en",
            "Alphinaud",
            "Understood.",
            new SourceClientLanguage("de", "de"),
            "Alphinaud",
            "Understood.");

        Assert.False(matches);
    }

    /// <summary>
    ///     Ensures DB-first in-flight and cooldown identities differ by the
    ///     complete operation-captured source scope.
    /// </summary>
    [Fact]
    public void WorkKey_DifferentSource_Differs()
    {
        var englishKey = DbFirstGameWindowWorkKey.Build(
            "ActionMenu",
            new TranslationReuseScope("en", "pt-BR", 0, true),
            "test-version",
            "payload");
        var germanKey = DbFirstGameWindowWorkKey.Build(
            "ActionMenu",
            new TranslationReuseScope("de", "pt-BR", 0, true),
            "test-version",
            "payload");

        Assert.NotEqual(englishKey, germanKey);
    }

    /// <summary>
    ///     Ensures DB fallback accepts another engine only when the established
    ///     reuse policy is engine agnostic.
    /// </summary>
    [Fact]
    public void DbFallback_EnginePolicy_ControlsCompatibleReuse()
    {
        const string originalJson = "payload";
        var candidate = new GameWindow(
            windowAddonName: "ActionMenu",
            originalWindowStrings: originalJson,
            originalWindowStringsLang: "en",
            translatedWindowStrings: "translated",
            translationLang: "pt-BR",
            translationEngine: 1,
            gameVersion: "test-version",
            createdDate: DateTime.UtcNow,
            updatedDate: DateTime.UtcNow);
        var compatibleScope = new TranslationReuseScope(
            "en",
            "pt-BR",
            0,
            false);
        var strictScope = compatibleScope with { RequireMatchingEngine = true };

        Assert.True(DbFirstGameWindowFallbackPolicy.Matches(
            candidate,
            "ActionMenu",
            compatibleScope,
            "test-version",
            classJobId: null,
            originalJson));
        Assert.False(DbFirstGameWindowFallbackPolicy.Matches(
            candidate,
            "ActionMenu",
            strictScope,
            "test-version",
            classJobId: null,
            originalJson));
    }

    /// <summary>
    ///     Ensures a cooldown captured for one source cannot suppress work for
    ///     another source when no resolved runtime state exists.
    /// </summary>
    [Fact]
    public void RetryGate_NoRuntimeSourceChange_DropsPriorSourceCooldown()
    {
        var nowUtc = DateTime.UtcNow;
        var gate = new DbFirstSourceRetryGate();
        var englishOperation = gate.TransitionTo(
            new SourceClientLanguage("en", "en"));

        Assert.True(gate.TrySetRetry(
            englishOperation,
            nowUtc.AddSeconds(30)));
        Assert.True(gate.IsCoolingDown(englishOperation, nowUtc));

        var germanOperation = gate.TransitionTo(
            new SourceClientLanguage("de", "de"));

        Assert.False(gate.IsCoolingDown(germanOperation, nowUtc));
        Assert.NotEqual(englishOperation, germanOperation);
    }

    /// <summary>
    ///     Ensures an unknown source clears the active retry owner and rejects
    ///     work captured before source resolution failed.
    /// </summary>
    [Fact]
    public void RetryGate_UnknownSource_ClearsOwnedState()
    {
        var nowUtc = DateTime.UtcNow;
        var gate = new DbFirstSourceRetryGate();
        var operation = gate.TransitionTo(
            new SourceClientLanguage("en", "en"));
        Assert.True(gate.TrySetRetry(operation, nowUtc.AddSeconds(30)));

        gate.TransitionTo(null);

        Assert.False(gate.HasKnownSource);
        Assert.False(gate.IsCoolingDown(operation, nowUtc));
        Assert.False(gate.TryRunIfCurrent(operation, static () => { }));
    }

    /// <summary>
    ///     Ensures an async completion captured before a source transition
    ///     cannot mutate the new source's refresh state.
    /// </summary>
    [Fact]
    public void RetryGate_StaleAsyncCompletion_IsRejected()
    {
        var gate = new DbFirstSourceRetryGate();
        var staleOperation = gate.TransitionTo(
            new SourceClientLanguage("en", "en"));
        gate.TransitionTo(new SourceClientLanguage("de", "de"));
        var refreshRequested = false;

        var accepted = gate.TryRunIfCurrent(
            staleOperation,
            () => refreshRequested = true);

        Assert.False(accepted);
        Assert.False(refreshRequested);
    }

    /// <summary>
    ///     Ensures restoration does not overwrite a game repaint that replaced
    ///     the exact text previously written by the plugin.
    /// </summary>
    [Fact]
    public void NativeMutation_GameRepaintBeforeRestore_IsPreserved()
    {
        var liveText = "New game-owned source";
        var writeCount = 0;

        var restored = NativeMutationOwnership.TryRestore(
            liveText,
            "Plugin replacement",
            "Old game source",
            restoredText =>
            {
                writeCount++;
                liveText = restoredText;
            });

        Assert.False(restored);
        Assert.Equal(0, writeCount);
        Assert.Equal("New game-owned source", liveText);
    }

    /// <summary>
    ///     Ensures an empty replacement cannot claim ownership of an untouched
    ///     empty native field.
    /// </summary>
    [Fact]
    public void NativeMutation_EmptyReplacement_DoesNotRestore()
    {
        var writeCount = 0;

        var restored = NativeMutationOwnership.TryRestore(
            string.Empty,
            string.Empty,
            "Old game source",
            _ => writeCount++);

        Assert.False(restored);
        Assert.Equal(0, writeCount);
    }

    /// <summary>
    ///     Ensures DB-first restoration uses the replacement recorded for the
    ///     same stable node key rather than another node's equal translation.
    /// </summary>
    [Fact]
    public void RuntimeState_TextNodeGameRepaint_PreservesDifferentStableNode()
    {
        var originalPayload = CreateTextNodePayload(
            ("2:0", "Original first"),
            ("2:1", "Original second"));
        var translatedPayload = CreateTextNodePayload(
            ("2:0", "Shared translation"),
            ("2:1", "Second translation"));
        var state = new DbFirstGameWindowRuntimeState(
            "en",
            "payload-key",
            originalPayload,
            translatedPayload);
        var restoredText = string.Empty;

        var restored = state.TryRestoreTextNode(
            "2:1",
            "Shared translation",
            text => restoredText = text);

        Assert.False(restored);
        Assert.Equal(string.Empty, restoredText);
    }

    /// <summary>
    ///     Ensures an effectively visible node skipped from capture still
    ///     consumes its duplicate ordinal before the next matching node.
    /// </summary>
    [Fact]
    public void TextNodeOrdinals_FilteredVisibleNode_ConsumesDuplicateOrdinal()
    {
        var ordinalsByNodeId = new Dictionary<uint, int>();

        var filteredNodeKey = DbFirstTextNodeKeyAllocator.ConsumeVisibleNode(
            ordinalsByNodeId,
            7);
        var capturedNodeKey = DbFirstTextNodeKeyAllocator.ConsumeVisibleNode(
            ordinalsByNodeId,
            7);

        Assert.Equal("7:0", filteredNodeKey);
        Assert.Equal("7:1", capturedNodeKey);
    }

    /// <summary>
    ///     Ensures a source transition does not publish its new source while
    ///     handler-owned invalidation is still running.
    /// </summary>
    [Fact]
    public async Task SourcePublicationLifecycle_BlockedInvalidation_HidesNewSource()
    {
        using var invalidationEntered = new ManualResetEventSlim();
        using var releaseInvalidation = new ManualResetEventSlim();
        var lifecycle = new SourcePublicationLifecycle();
        var englishSource = new SourceClientLanguage("en", "en");
        var germanSource = new SourceClientLanguage("de", "de");
        var englishOperation = lifecycle.TransitionTo(englishSource, static () => { });
        var transitionTask = Task.Run(
            () => lifecycle.TransitionTo(
                germanSource,
                () =>
                {
                    invalidationEntered.Set();
                    releaseInvalidation.Wait();
                }));

        try
        {
            Assert.True(await Task.Run(
                () => invalidationEntered.Wait(TimeSpan.FromSeconds(5))));
            Assert.Equal(default, lifecycle.Capture(germanSource));
        }
        finally
        {
            releaseInvalidation.Set();
        }

        var germanOperation = await transitionTask;
        var published = false;

        Assert.Equal(germanOperation, lifecycle.Capture(germanSource));
        Assert.False(lifecycle.TryPublish(
            englishOperation,
            () => published = true));
        Assert.False(published);
    }

    /// <summary>
    ///     Creates a minimal DB-first payload for native-free runtime tests.
    /// </summary>
    /// <param name="text">The payload text.</param>
    /// <returns>The payload.</returns>
    private static DbFirstGameWindowPayload CreatePayload(string text)
    {
        return new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>
            {
                [0] = text,
            },
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Creates a text-node-only payload using stable node keys.
    /// </summary>
    /// <param name="values">The stable keys and text values.</param>
    /// <returns>The requested payload.</returns>
    private static DbFirstGameWindowPayload CreateTextNodePayload(
        params (string Key, string Text)[] values)
    {
        return new DbFirstGameWindowPayload(
            new SortedDictionary<int, string>(),
            new SortedDictionary<int, string>(),
            new SortedDictionary<string, string>(
                values.ToDictionary(value => value.Key, value => value.Text),
                StringComparer.Ordinal));
    }
}
