// <copyright file="QuestReadableTextMatchTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Echoglossian.NativeUI.AddonHandlers.Quest;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards readable-text matching for quest popup handlers that must
///     resolve live native nodes from formatted SeString payloads.
/// </summary>
public sealed class QuestReadableTextMatchTests
{
    /// <summary>
    ///     Ensures payload matching can still identify JournalAccept body text
    ///     when the visible native node contains formatting payload bytes and a
    ///     quest-sync icon ahead of the readable message.
    /// </summary>
    [Fact]
    public void TextNodePayloadMatches_StripsJournalAcceptFormattingNoise()
    {
        const string visibleText =
            "\u0002H\u0004\uFFFD\u0001\uFFFD\u0003\u0002I\u0004\uFFFD\u0001\uFFFD\u0003\uE0BE Quest Sync\u0002I\u0002\u0001\u0003\u0002H\u0002\u0001\u0003\rBerthellemy in Camp Broken Glass is having a devil \u0010\u0001\u0003of a time trying to keep things on track.";
        const string expectedText =
            "Berthellemy in Camp Broken Glass is having a devil of a time trying to keep things on track.";

        Assert.True(this.InvokeTextNodePayloadMatches(visibleText, expectedText));
    }

    /// <summary>
    ///     Ensures JournalAccept hover tooltips promote the richer visible body
    ///     text when setup only captures the short quest-sync marker.
    /// </summary>
    [Fact]
    public void ResolveJournalAcceptOriginalHoverBody_PromotesExpandedVisibleMessage()
    {
        const string setupMessage = "**\uE0BE Quest Sync**";
        const string visibleText =
            "\u0002H\u0004\uFFFD\u0001\uFFFD\u0003\u0002I\u0004\uFFFD\u0001\uFFFD\u0003\uE0BE Quest Sync\u0002I\u0002\u0001\u0003\u0002H\u0002\u0001\u0003\rBerthellemy in Camp Broken Glass is having a devil \u0010\u0001\u0003of a time trying to keep things on track.";
        const string expectedText =
            "Quest Sync Berthellemy in Camp Broken Glass is having a devil of a time trying to keep things on track.";

        Assert.Equal(
            expectedText,
            this.InvokeResolveJournalAcceptOriginalHoverBody(
                setupMessage,
                visibleText));
    }

    /// <summary>
    ///     Ensures JournalAccept hover tooltips keep the setup payload when the
    ///     visible node does not expose anything richer.
    /// </summary>
    [Fact]
    public void ResolveJournalAcceptOriginalHoverBody_KeepsSetupMessageWhenVisibleTextIsNotRicher()
    {
        const string setupMessage = "**\uE0BE Quest Sync**";

        Assert.Equal(
            setupMessage,
            this.InvokeResolveJournalAcceptOriginalHoverBody(
                setupMessage,
                setupMessage));
    }

    /// <summary>
    ///     Invokes the shared quest-popup readable-text matcher through
    ///     reflection so RED can pin behavior without widening production
    ///     visibility.
    /// </summary>
    /// <param name="visibleText">The visible node text.</param>
    /// <param name="expectedText">The expected payload text.</param>
    /// <returns>True when the matcher considers both texts equivalent.</returns>
    private bool InvokeTextNodePayloadMatches(
        string visibleText,
        string expectedText)
    {
        var method = typeof(QuestAddonHandlerBase).GetMethod(
                         "TextNodePayloadMatches",
                         BindingFlags.Static | BindingFlags.NonPublic) ??
                     throw new MissingMethodException(
                         typeof(QuestAddonHandlerBase).FullName,
                         "TextNodePayloadMatches");

        return (bool)method.Invoke(null, [visibleText, expectedText])!;
    }

    /// <summary>
    ///     Invokes the JournalAccept hover-body promotion helper through
    ///     reflection so RED can pin tooltip behavior without widening
    ///     production visibility.
    /// </summary>
    /// <param name="setupMessage">The setup-captured quest body.</param>
    /// <param name="visibleText">The live visible body node text.</param>
    /// <returns>The preferred original hover body text.</returns>
    private string InvokeResolveJournalAcceptOriginalHoverBody(
        string setupMessage,
        string visibleText)
    {
        var method = typeof(JournalAcceptHandler).GetMethod(
                         "ResolveJournalAcceptOriginalHoverBody",
                         BindingFlags.Static | BindingFlags.NonPublic) ??
                     throw new MissingMethodException(
                         typeof(JournalAcceptHandler).FullName,
                         "ResolveJournalAcceptOriginalHoverBody");

        return (string)method.Invoke(null, [setupMessage, visibleText])!;
    }
}
