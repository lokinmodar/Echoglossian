// <copyright file="JournalDetailCurrentQuestContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers contract expectations specific to the active JournalDetail quest pane.
/// </summary>
public class JournalDetailCurrentQuestContractTests
{
    /// <summary>
    ///     Ensures the current-quest JournalDetail pane only enters the active
    ///     quest translation path after the visible title is gated through the
    ///     accepted-quest runtime.
    /// </summary>
    [Fact]
    public void TranslateJournalBox_GatesCurrentQuestPaneToAcceptedQuestStateBeforeDbLookup()
    {
        static DirectoryInfo FindRepositoryRoot()
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
                {
                    return current;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root.");
        }

        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "JournalDetailHandler.cs"));
        var translateJournalBoxStart = source.IndexOf(
            "private unsafe bool TranslateJournalBox(",
            StringComparison.Ordinal);
        var acceptedGate = source.IndexOf(
            "QuestProgressResolver.TryResolveAcceptedQuestId",
            translateJournalBoxStart,
            StringComparison.Ordinal);
        var dbLookup = source.IndexOf(
            "var foundQuestPlate = this.FindQuestPlate(questPlate);",
            translateJournalBoxStart,
            StringComparison.Ordinal);

        Assert.True(
            translateJournalBoxStart >= 0,
            "JournalDetail current-quest translation path should remain in TranslateJournalBox.");
        Assert.True(
            dbLookup > translateJournalBoxStart,
            "JournalDetail current-quest translation path should keep its canonical DB lookup inside TranslateJournalBox.");
        Assert.True(
            acceptedGate > translateJournalBoxStart && acceptedGate < dbLookup,
            "JournalDetail current-quest translation must gate the visible title through accepted-quest state before reusing DB rows or scheduling prefetch.");
    }

    /// <summary>
    ///     Ensures direct JournalDetail lifecycle refreshes still resolve the
    ///     current source language and translate immediately instead of routing
    ///     through an extra visible-signature invalidation layer.
    /// </summary>
    [Fact]
    public void OnJournalDetailEvent_TranslatesDirectlyWithoutVisibleSignatureGate()
    {
        static DirectoryInfo FindRepositoryRoot()
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Echoglossian.sln")))
                {
                    return current;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root.");
        }

        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "JournalDetailHandler.cs"));
        var methodStart = source.IndexOf(
            "private unsafe void OnJournalDetailEvent(",
            StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf(
            "private unsafe void OnJournalDetailPreDrawEvent(",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(
            methodStart >= 0,
            "JournalDetail lifecycle refresh should remain in OnJournalDetailEvent.");
        Assert.True(
            nextMethodStart > methodStart,
            "JournalDetail pre-draw retry path should still follow OnJournalDetailEvent.");

        var methodBody = source.Substring(methodStart, nextMethodStart - methodStart);

        Assert.Contains(
            "RuntimeLanguageHelper.TryResolveCurrentSourceLanguage",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.TranslateJournalDetail(sourceLanguage);",
            methodBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryComputeVisibleJournalDetailSignature",
            methodBody,
            StringComparison.Ordinal);
    }
}
