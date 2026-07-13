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
}
