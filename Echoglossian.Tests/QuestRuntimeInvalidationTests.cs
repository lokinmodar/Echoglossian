// <copyright file="QuestRuntimeInvalidationTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

using PluginEntry = Echoglossian.Echoglossian;

namespace Echoglossian.Tests;

/// <summary>
/// Covers quest-family runtime invalidation keys that control reapplication
/// after quest progress or canonical source content changes.
/// </summary>
public class QuestRuntimeInvalidationTests
{
    /// <summary>
    /// Ensures JournalDetail local caches are invalidated when the canonical
    /// quest text changes without a quest id or sequence change.
    /// </summary>
    [Fact]
    public void JournalDetailScopeKey_ContentHashChanges_ChangesScope()
    {
        var method = typeof(JournalDetailHandler).GetMethod(
            "BuildJournalDetailScopeKey",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var firstScope = InvokeJournalDetailScopeKey(
            method!,
            CreateQuestProgressSnapshot("hash-a"));
        var secondScope = InvokeJournalDetailScopeKey(
            method!,
            CreateQuestProgressSnapshot("hash-b"));

        Assert.Contains("hash-a", firstScope, StringComparison.Ordinal);
        Assert.NotEqual(firstScope, secondScope);
    }

    /// <summary>
    /// Ensures the accepted-quest prefetch signature is sensitive to live TODO
    /// state, so objective advances retrigger background canonical prewarm.
    /// </summary>
    [Fact]
    public void AcceptedQuestSignature_TodoStateChanges_ChangesSignature()
    {
        var method = typeof(PluginEntry)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(static candidate =>
                candidate.Name == "BuildAcceptedQuestSignature" &&
                candidate.GetParameters().Length == 3);

        Assert.NotNull(method);

        var firstSignature = InvokeAcceptedQuestSignature(
            method!,
            static _ => "67011:4:0:1:2");
        var secondSignature = InvokeAcceptedQuestSignature(
            method!,
            static _ => "67011:4:1:1:2");

        Assert.NotEqual(firstSignature, secondSignature);
    }

    private static string InvokeJournalDetailScopeKey(
        MethodInfo method,
        global::Echoglossian.QuestProgressSnapshot snapshot)
    {
        return (string)(method.Invoke(
            null,
            [snapshot, "Test Quest", "Test quest message."]) ??
            string.Empty);
    }

    private static string InvokeAcceptedQuestSignature(
        MethodInfo method,
        Func<uint, string?> todoSignatureResolver)
    {
        return (string)(method.Invoke(
            null,
            [
                new uint[] { 67011U },
                new Func<uint, byte>(static _ => 4),
                todoSignatureResolver,
            ]) ??
            string.Empty);
    }

    private static global::Echoglossian.QuestProgressSnapshot CreateQuestProgressSnapshot(
        string contentHash)
    {
        return new global::Echoglossian.QuestProgressSnapshot(
            QuestId: 67011U,
            QuestSequence: 4,
            QuestName: "Test Quest",
            QuestSheetName: "quest/014/HeaVnz025_01475",
            QuestSteps: Array.Empty<global::Echoglossian.QuestProgressEntry>(),
            QuestSeqTexts: Array.Empty<global::Echoglossian.QuestProgressEntry>(),
            QuestSystemTexts: Array.Empty<global::Echoglossian.QuestProgressEntry>(),
            ContentHash: contentHash);
    }
}
