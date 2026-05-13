// <copyright file="DialogueContextPromptHelperTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using Echoglossian.Translators.Helpers;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Tests shared runtime-only dialogue-context prompt helpers.
/// </summary>
public class DialogueContextPromptHelperTests
{
    /// <summary>
    ///     Ensures usable context is detected only when prior turns exist.
    /// </summary>
    [Fact]
    public void HasUsableDialogueContext_ShouldReflectPriorTurns()
    {
        DialogueTranslationContext emptyContext = new(
            "Talk",
            "speaker",
            "Krile",
            []);
        DialogueTranslationContext populatedContext = new(
            "Talk",
            "speaker",
            "Krile",
            [new DialogueTranslationTurn("Krile", "Stay alert.")]);

        DialogueContextPromptHelper.HasUsableDialogueContext(emptyContext).Should().BeFalse();
        DialogueContextPromptHelper.HasUsableDialogueContext(populatedContext).Should().BeTrue();
    }

    /// <summary>
    ///     Ensures appended prompt text carries current speaker and prior turns.
    /// </summary>
    [Fact]
    public void AppendDialogueContext_ShouldIncludeSpeakerAndHistory()
    {
        DialogueTranslationContext context = new(
            "Talk",
            "quest-1",
            "Krile",
            [
                new DialogueTranslationTurn("Krile", "Stay alert."),
                new DialogueTranslationTurn("Thancred", "We move now."),
            ]);

        string result = DialogueContextPromptHelper.AppendDialogueContext(
            "Translate this line.",
            context,
            static text => text);

        result.Should().Contain("Translate this line.");
        result.Should().Contain("Current speaker: Krile");
        result.Should().Contain("[1] Krile: Stay alert.");
        result.Should().Contain("[2] Thancred: We move now.");
    }

    /// <summary>
    ///     Ensures the cache key is namespaced by session and history.
    /// </summary>
    [Fact]
    public void BuildDialogueContextCacheKey_ShouldIncludeSessionAndHistory()
    {
        DialogueTranslationContext context = new(
            "BattleTalk",
            "battle-1",
            "Alphinaud",
            [new DialogueTranslationTurn("Alphinaud", "Hold the line.")]);

        string key = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Advance.",
            "English",
            "Portuguese",
            context);

        key.Should().Contain("dialogue|BattleTalk|battle-1|");
        key.Should().Contain("Alphinaud:Hold the line.");
        key.Should().EndWith("|Advance._English_Portuguese");
    }
}
