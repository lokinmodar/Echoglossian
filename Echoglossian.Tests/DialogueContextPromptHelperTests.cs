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
    private static readonly DateTime FixedObservedAtUtc =
        new(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    ///     Ensures a first dialogue turn contributes speaker context to prompts
    ///     and cache identity without requiring prior history.
    /// </summary>
    [Fact]
    public void FirstTurnContext_ShouldIncludeSpeakerInPromptAndCacheKey()
    {
        DialogueTranslationContext alphinaudContext = new(
            "Talk",
            "speaker",
            "Alphinaud",
            []);
        DialogueTranslationContext alisaieContext = new(
            "Talk",
            "speaker",
            "Alisaie",
            []);

        string prompt = DialogueContextPromptHelper.AppendDialogueContext(
            "Translate this line.",
            alphinaudContext,
            static text => text);
        string alphinaudKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Advance.",
            "English",
            "Portuguese",
            alphinaudContext);
        string alisaieKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Advance.",
            "English",
            "Portuguese",
            alisaieContext);

        DialogueContextPromptHelper.HasUsableDialogueContext(alphinaudContext).Should().BeTrue();
        prompt.Should().Contain("Current speaker: Alphinaud");
        alphinaudKey.Should().NotBe(alisaieKey);
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
                CreateTurn("Krile", "Stay alert."),
                CreateTurn("Thancred", "We move now."),
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
    ///     Ensures resolved interlocutor hints contribute ordered prompt metadata
    ///     and distinct cache identity without changing prior-turn history.
    /// </summary>
    [Fact]
    public void ContextWithInterlocutorHints_ShouldProjectPromptAndCacheIdentity()
    {
        DialogueTranslationContext hintedContext = new(
            "Talk",
            "quest-1",
            "Krile",
            [CreateTurn("Thancred", "We move now.")],
            SpeakerRoleHint: "npc",
            SpeakerGenderHint: "female",
            AddresseeHint: "Alphinaud",
            AddresseeRoleHint: "npc",
            AddresseeGenderHint: "male",
            MetadataProvenance: "quest-sheet",
            MetadataConfidenceTier: "exact");
        DialogueTranslationContext emptyHintContext = new(
            "Talk",
            "quest-1",
            "Krile",
            [CreateTurn("Thancred", "We move now.")]);

        string prompt = DialogueContextPromptHelper.AppendDialogueContext(
            "Translate this line.",
            hintedContext,
            static text => text);
        string hintedKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Advance.",
            "English",
            "Portuguese",
            hintedContext);
        string emptyHintKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Advance.",
            "English",
            "Portuguese",
            emptyHintContext);

        prompt.Should().Contain(
            $"Current speaker: Krile{Environment.NewLine}Speaker role: npc{Environment.NewLine}Speaker gender: female{Environment.NewLine}Addressee: Alphinaud{Environment.NewLine}Addressee role: npc{Environment.NewLine}Addressee gender: male{Environment.NewLine}[1] Thancred: We move now.");
        hintedKey.Should().NotBe(emptyHintKey);
        emptyHintKey.Should().NotContain("SpeakerGenderHint");
        emptyHintKey.Should().NotContain("AddresseeHint");
    }

    /// <summary>
    ///     Ensures the cache key is namespaced by session and serialized with
    ///     content instead of delimiter-sensitive concatenation.
    /// </summary>
    [Fact]
    public void BuildDialogueContextCacheKey_ShouldIncludeSessionAndHistory()
    {
        DialogueTranslationContext context = new(
            "BattleTalk",
            "battle-1",
            "Alphinaud",
            [CreateTurn("Alphinaud", "Hold the line.")]);

        string key = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Advance.",
            "English",
            "Portuguese",
            context);

        key.Should().Contain("\"Scope\":\"dialogue\"");
        key.Should().Contain("\"SessionNamespace\":\"BattleTalk\"");
        key.Should().Contain("\"SessionKey\":\"battle-1\"");
        key.Should().Contain("\"Text\":\"Advance.\"");
        key.Should().Contain("\"SpeakerName\":\"Alphinaud\"");
        key.Should().Contain("\"SourceText\":\"Hold the line.\"");
    }

    /// <summary>
    ///     Ensures the cache key remains distinct even when dialogue text
    ///     contains characters that previously acted as delimiters.
    /// </summary>
    [Fact]
    public void BuildDialogueContextCacheKey_ShouldAvoidDelimiterDrivenCollisions()
    {
        DialogueTranslationContext firstContext = new(
            "Talk",
            "alpha|beta",
            "Krile",
            [
                CreateTurn("Alpha:Beta", "One|Two"),
            ]);
        DialogueTranslationContext secondContext = new(
            "Talk|alpha",
            "beta",
            "Krile",
            [
                CreateTurn("Alpha", "Beta:One|Two"),
            ]);

        string firstKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Current|Text",
            "English",
            "Portuguese",
            firstContext);
        string secondKey = DialogueContextPromptHelper.BuildDialogueContextCacheKey(
            "Current|Text",
            "English",
            "Portuguese",
            secondContext);

        firstKey.Should().NotBe(secondKey);
    }

    /// <summary>
    ///     Creates one deterministic prior dialogue turn for cache-key and
    ///     prompt-helper tests.
    /// </summary>
    /// <param name="speakerName">The turn speaker name.</param>
    /// <param name="sourceText">The turn source text.</param>
    /// <returns>A deterministic dialogue translation turn.</returns>
    private static DialogueTranslationTurn CreateTurn(
        string speakerName,
        string sourceText)
    {
        return new DialogueTranslationTurn(
            speakerName,
            sourceText,
            FixedObservedAtUtc);
    }
}
