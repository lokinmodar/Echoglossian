// <copyright file="QuestReadableTextMatchTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Reflection;

using Dalamud.Game.Text.SeStringHandling;

using Echoglossian.NativeUI.AddonHandlers.Quest;
using Echoglossian.NativeUI.Helpers;

using Lumina.Text.ReadOnly;

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
    ///     Ensures JournalAccept can recognize the richer visible body node as a
    ///     tooltip target even when setup captured only the short quest-sync
    ///     marker.
    /// </summary>
    [Fact]
    public void ResolveJournalAcceptExpandedVisibleBodyMatch_ReturnsPromotedBody()
    {
        const string setupMessage = "**\uE0BE Quest Sync**";
        const string visibleText =
            "\u0002H\u0004\uFFFD\u0001\uFFFD\u0003\u0002I\u0004\uFFFD\u0001\uFFFD\u0003\uE0BE Quest Sync\u0002I\u0002\u0001\u0003\u0002H\u0002\u0001\u0003\rBerthellemy in Camp Broken Glass is having a devil \u0010\u0001\u0003of a time trying to keep things on track.";
        const string expectedText =
            "Quest Sync Berthellemy in Camp Broken Glass is having a devil of a time trying to keep things on track.";

        Assert.Equal(
            expectedText,
            this.InvokeResolveJournalAcceptExpandedVisibleBodyMatch(
                setupMessage,
                visibleText));
    }

    /// <summary>
    ///     Ensures non-body visible text does not get mistaken for the
    ///     JournalAccept body tooltip target.
    /// </summary>
    [Fact]
    public void ResolveJournalAcceptExpandedVisibleBodyMatch_ReturnsEmptyWhenVisibleTextDoesNotBelongToBody()
    {
        const string setupMessage = "**\uE0BE Quest Sync**";
        const string visibleText = "Beasts of the Imperial Wild";

        Assert.Equal(
            string.Empty,
            this.InvokeResolveJournalAcceptExpandedVisibleBodyMatch(
                setupMessage,
                visibleText));
    }

    /// <summary>
    ///     Ensures JournalAccept prefers the expanded visible body payload even
    ///     when a shorter quest-sync marker node appears earlier in the live
    ///     readable-node scan.
    /// </summary>
    [Fact]
    public void ResolvePreferredJournalAcceptVisibleBody_PrefersExpandedBodyOverDirectSetupMatch()
    {
        const string setupMessage = "**\uE0BE Quest Sync**";
        const string directVisibleText = "\uE0BE Quest Sync";
        const string expandedVisibleText =
            "\u0002H\u0004\uFFFD\u0001\uFFFD\u0003\u0002I\u0004\uFFFD\u0001\uFFFD\u0003\uE0BE Quest Sync\u0002I\u0002\u0001\u0003\u0002H\u0002\u0001\u0003\rBerthellemy in Camp Broken Glass is having a devil \u0010\u0001\u0003of a time trying to keep things on track.";
        const string expectedText =
            "Quest Sync Berthellemy in Camp Broken Glass is having a devil of a time trying to keep things on track.";

        Assert.Equal(
            expectedText,
            this.InvokeResolvePreferredJournalAcceptVisibleBody(
                setupMessage,
                [directVisibleText, expandedVisibleText]));
    }

    /// <summary>
    ///     Ensures JournalResult prefers the canonical stored body when it is
    ///     already available alongside the translated body.
    /// </summary>
    [Fact]
    public void ResolveJournalResultStoredMessage_PrefersCanonicalBody()
    {
        var payload = this.InvokeResolveJournalResultStoredMessage(
            "Complete the task at hand.",
            "Conclua a tarefa em questao.",
            "Popup body",
            "Corpo popup");

        Assert.Equal("Complete the task at hand.", payload[0]);
        Assert.Equal("Conclua a tarefa em questao.", payload[1]);
    }

    /// <summary>
    ///     Ensures JournalResult falls back to popup-scoped body text only when
    ///     the canonical row does not provide a complete translated body.
    /// </summary>
    [Fact]
    public void ResolveJournalResultStoredMessage_FallsBackToPopupBody()
    {
        var payload = this.InvokeResolveJournalResultStoredMessage(
            string.Empty,
            string.Empty,
            "Popup body",
            "Corpo popup");

        Assert.Equal("Popup body", payload[0]);
        Assert.Equal("Corpo popup", payload[1]);
    }

    /// <summary>
    ///     Ensures JournalResult keeps the title-only path when no complete
    ///     stored body payload exists yet.
    /// </summary>
    [Fact]
    public void ResolveJournalResultStoredMessage_ReturnsEmptyWhenNoCompleteBodyExists()
    {
        var payload = this.InvokeResolveJournalResultStoredMessage(
            "Canonical body",
            string.Empty,
            "Popup body",
            string.Empty);

        Assert.Equal(string.Empty, payload[0]);
        Assert.Equal(string.Empty, payload[1]);
    }

    /// <summary>
    ///     Ensures quest-popup native mutation can project translated body text
    ///     onto a captured rich SeString payload without degrading it to plain
    ///     text bytes.
    /// </summary>
    [Fact]
    public void ProjectReadablePayloadBytes_PreservesRichFormattingWhenReplacingBodyText()
    {
        const string originalText = "Quest Sync body";
        const string translatedText = "Corpo do Quest Sync";
        var originalPayload = new SeStringBuilder()
            .AddUiForeground(500)
            .AddText(originalText)
            .AddUiForegroundOff()
            .Build()
            .Encode();

        var projectedPayload = this.InvokeProjectReadablePayloadBytes(
            originalPayload,
            originalText,
            translatedText);

        Assert.NotNull(projectedPayload);

        var projectedSeString = new ReadOnlySeString(projectedPayload);
        Assert.Equal(translatedText, projectedSeString.ExtractText());
        Assert.NotEqual(
            ReadOnlySeString.FromText(translatedText).Data.ToArray(),
            projectedPayload);
    }

    /// <summary>
    ///     Ensures captured setup wrappers do not prevent retaining a rich
    ///     payload whose extracted readable text omits those wrappers.
    /// </summary>
    [Fact]
    public void ProjectReadablePayloadBytes_RetainsRichPayloadWhenCapturedTextHasOuterWrappers()
    {
        const string capturedText = "**A**";
        const string translatedText = "B";
        var originalPayload = new SeStringBuilder()
            .AddUiForeground(500)
            .AddText("A")
            .AddUiForegroundOff()
            .Build()
            .Encode();

        var projectedPayload = this.InvokeProjectReadablePayloadBytes(
            originalPayload,
            capturedText,
            translatedText);

        Assert.NotNull(projectedPayload);
        Assert.Equal(translatedText, new ReadOnlySeString(projectedPayload).ExtractText());
        Assert.NotEqual(
            ReadOnlySeString.FromText(translatedText).Data.ToArray(),
            projectedPayload);
    }

    /// <summary>
    ///     Ensures the shared readable SeString helper can still extract plain
    ///     readable text from one payload that carries formatting macros.
    /// </summary>
    [Fact]
    public void ReadableSeStringPayloadHelper_TryExtractReadablePayloadText_ExtractsFormattedReadableText()
    {
        const string originalText = "Journal Result body";
        var payload = new SeStringBuilder()
            .AddUiForeground(500)
            .AddText(originalText)
            .AddUiForegroundOff()
            .Build()
            .Encode();

        Assert.True(
            ReadableSeStringPayloadHelper.TryExtractReadablePayloadText(
                payload,
                out var readableText));
        Assert.Equal(originalText, readableText);
    }

    /// <summary>
    ///     Ensures the shared readable SeString helper drops one stale payload
    ///     instead of reusing it against unrelated source text.
    /// </summary>
    [Fact]
    public void ReadableSeStringPayloadHelper_RetainMatchingPayload_DropsStalePayload()
    {
        var payload = new SeStringBuilder()
            .AddText("Original title")
            .Build()
            .Encode();

        Assert.Null(
            ReadableSeStringPayloadHelper.RetainMatchingPayload(
                payload,
                "Different title"));
    }

    /// <summary>
    ///     Ensures quest-popup node matching prefers the readable structured
    ///     SeString text when the direct live node string still contains raw
    ///     payload noise.
    /// </summary>
    [Fact]
    public void ResolveReadableTextNodeText_PrefersStructuredOriginalText()
    {
        const string currentText =
            "\u0002H\u0004\uFFFD\u0001\uFFFD\u0003\u0002I\u0004\uFFFD\u0001\uFFFD\u0003\uE0BE Quest Sync\u0002I\u0002\u0001\u0003";
        const string originalText =
            "Quest Sync Berthellemy in Camp Broken Glass is having a devil of a time trying to keep things on track.";
        const string legacyText = currentText;

        Assert.Equal(
            originalText,
            this.InvokeResolveReadableTextNodeText(
                currentText,
                originalText,
                legacyText));
    }

    /// <summary>
    ///     Ensures quest-popup node matching falls back to the direct current
    ///     node text when no structured original payload can be read.
    /// </summary>
    [Fact]
    public void ResolveReadableTextNodeText_FallsBackToCurrentTextWhenStructuredTextIsEmpty()
    {
        const string currentText = "The Yedlihmad Hunt";

        Assert.Equal(
            currentText,
            this.InvokeResolveReadableTextNodeText(
                currentText,
                string.Empty,
                string.Empty));
    }

    /// <summary>
    ///     Ensures quest-popup node matching prefers the richer current node
    ///     text when the structured original payload still points at the short
    ///     setup marker and the live node has already expanded to the full
    ///     readable body.
    /// </summary>
    [Fact]
    public void ResolveReadableTextNodeText_PrefersRicherCurrentTextWhenStructuredTextIsShorter()
    {
        const string currentText =
            "Quest Sync Talon has received word from a comrade in dire straits somewhere in the city.";
        const string originalText = "Quest Sync";

        Assert.Equal(
            currentText,
            this.InvokeResolveReadableTextNodeText(
                currentText,
                originalText,
                currentText));
    }

    /// <summary>
    ///     Ensures the shared readable SeString helper promotes the current
    ///     live payload when the original structured payload is still the short
    ///     setup marker and cannot represent the expanded visible body.
    /// </summary>
    [Fact]
    public void ReadableSeStringPayloadHelper_ResolvePreferredMatchingPayload_PrefersCurrentPayloadWhenOriginalIsShorter()
    {
        const string expectedText =
            "Quest Sync Talon has received word from a comrade in dire straits somewhere in the city.";
        var originalPayload = new SeStringBuilder()
            .AddUiForeground(500)
            .AddText("Quest Sync")
            .AddUiForegroundOff()
            .Build()
            .Encode();
        var currentPayload = new SeStringBuilder()
            .AddUiForeground(500)
            .AddText(expectedText)
            .AddUiForegroundOff()
            .Build()
            .Encode();

        var retainedPayload =
            ReadableSeStringPayloadHelper.ResolvePreferredMatchingPayload(
                originalPayload,
                currentPayload,
                expectedText);

        Assert.NotNull(retainedPayload);
        Assert.Equal(
            expectedText,
            new ReadOnlySeString(retainedPayload).ExtractText());
        Assert.Equal(currentPayload, retainedPayload);
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

    /// <summary>
    ///     Invokes the JournalAccept visible-body matcher through reflection so
    ///     RED can pin node-target detection without widening production
    ///     visibility.
    /// </summary>
    /// <param name="setupMessage">The setup-captured quest body.</param>
    /// <param name="visibleText">The live visible text node payload.</param>
    /// <returns>
    ///     The promoted original body when the visible node belongs to the
    ///     JournalAccept body; otherwise an empty string.
    /// </returns>
    private string InvokeResolveJournalAcceptExpandedVisibleBodyMatch(
        string setupMessage,
        string visibleText)
    {
        var method = typeof(JournalAcceptHandler).GetMethod(
                         "ResolveJournalAcceptExpandedVisibleBodyMatch",
                         BindingFlags.Static | BindingFlags.NonPublic) ??
                     throw new MissingMethodException(
                         typeof(JournalAcceptHandler).FullName,
                         "ResolveJournalAcceptExpandedVisibleBodyMatch");

        return (string)method.Invoke(null, [setupMessage, visibleText])!;
    }

    /// <summary>
    ///     Invokes the JournalAccept preferred visible-body resolver through
    ///     reflection so RED can pin scan-order behavior without widening
    ///     production visibility.
    /// </summary>
    /// <param name="setupMessage">The setup-captured quest body.</param>
    /// <param name="visibleTexts">The readable visible-node texts in scan order.</param>
    /// <returns>
    ///     The preferred promoted body payload for runtime use, or the setup
    ///     body when no richer visible node exists.
    /// </returns>
    private string InvokeResolvePreferredJournalAcceptVisibleBody(
        string setupMessage,
        string[] visibleTexts)
    {
        var method = typeof(JournalAcceptHandler).GetMethod(
                         "ResolvePreferredJournalAcceptVisibleBody",
                         BindingFlags.Static | BindingFlags.NonPublic) ??
                     throw new MissingMethodException(
                         typeof(JournalAcceptHandler).FullName,
                         "ResolvePreferredJournalAcceptVisibleBody");

        return (string)method.Invoke(null, [setupMessage, visibleTexts])!;
    }

    /// <summary>
    ///     Invokes the JournalResult stored-body selector through reflection so
    ///     RED can pin the safe title-only fallback behavior.
    /// </summary>
    /// <param name="canonicalOriginalBody">The canonical original body text.</param>
    /// <param name="canonicalTranslatedBody">The canonical translated body text.</param>
    /// <param name="popupOriginalBody">The popup-scoped original body text.</param>
    /// <param name="popupTranslatedBody">The popup-scoped translated body text.</param>
    /// <returns>
    ///     A two-item payload with original body at index 0 and translated body
    ///     at index 1.
    /// </returns>
    private string[] InvokeResolveJournalResultStoredMessage(
        string canonicalOriginalBody,
        string canonicalTranslatedBody,
        string popupOriginalBody,
        string popupTranslatedBody)
    {
        var method = typeof(JournalResultHandler).GetMethod(
                         "ResolveJournalResultStoredMessage",
                         BindingFlags.Static | BindingFlags.NonPublic) ??
                     throw new MissingMethodException(
                         typeof(JournalResultHandler).FullName,
                         "ResolveJournalResultStoredMessage");

        return (string[])method.Invoke(
            null,
            [
                canonicalOriginalBody,
                canonicalTranslatedBody,
                popupOriginalBody,
                popupTranslatedBody,
            ])!;
    }

    /// <summary>
    ///     Invokes the shared readable-payload projector through reflection so
    ///     RED can pin SeString-preserving native mutation behavior without
    ///     widening production visibility.
    /// </summary>
    /// <param name="originalPayload">The captured original SeString bytes.</param>
    /// <param name="originalText">The readable original text extracted from the payload.</param>
    /// <param name="translatedText">The translated text to project onto the payload.</param>
    /// <returns>The projected payload bytes when payload reuse is possible.</returns>
    private byte[]? InvokeProjectReadablePayloadBytes(
        byte[] originalPayload,
        string originalText,
        string translatedText)
    {
        var method = typeof(QuestAddonHandlerBase).GetMethod(
                         "ProjectReadablePayloadBytes",
                         BindingFlags.Static | BindingFlags.NonPublic) ??
                     throw new MissingMethodException(
                         typeof(QuestAddonHandlerBase).FullName,
                         "ProjectReadablePayloadBytes");

        return (byte[]?)method.Invoke(
            null,
            [originalPayload, originalText, translatedText]);
    }

    /// <summary>
    ///     Invokes the shared readable-text preference helper through
    ///     reflection so RED can pin the structured-SeString-first behavior
    ///     without widening production visibility.
    /// </summary>
    /// <param name="currentText">The direct live node string.</param>
    /// <param name="originalText">The structured original payload text.</param>
    /// <param name="legacyText">The legacy buffer-read fallback.</param>
    /// <returns>The preferred readable text selected by the helper.</returns>
    private string InvokeResolveReadableTextNodeText(
        string currentText,
        string originalText,
        string legacyText)
    {
        var method = typeof(QuestAddonHandlerBase).GetMethod(
                         "ResolveReadableTextNodeText",
                         BindingFlags.Static | BindingFlags.NonPublic) ??
                     throw new MissingMethodException(
                         typeof(QuestAddonHandlerBase).FullName,
                         "ResolveReadableTextNodeText");

        return (string)method.Invoke(
            null,
            [currentText, originalText, legacyText])!;
    }
}
