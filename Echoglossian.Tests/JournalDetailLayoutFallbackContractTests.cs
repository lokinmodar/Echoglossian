// <copyright file="JournalDetailLayoutFallbackContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers layout fallback expectations specific to JournalDetail.
/// </summary>
public class JournalDetailLayoutFallbackContractTests
{
    /// <summary>
    ///     Ensures JournalDetail restores only its own native mutations when
    ///     the visible detail pane is neither the current-quest body nor the
    ///     completed-quest layout.
    /// </summary>
    [Fact]
    public void TranslateJournalDetail_RestoresOwnedStateWhenNoSupportedPaneIsActive()
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
            "private unsafe void TranslateJournalDetail(",
            StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf(
            "private unsafe bool TranslateCompletedQuest(",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(
            methodStart >= 0,
            "JournalDetail should keep its top-level detail refresh inside TranslateJournalDetail.");
        Assert.True(
            nextMethodStart > methodStart,
            "JournalDetail should keep completed-quest handling separate from the top-level detail refresh.");

        var methodBody = source.Substring(methodStart, nextMethodStart - methodStart);

        Assert.Contains(
            "var handledCurrentQuest = this.TranslateJournalBox(",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "var handledCompletedQuest = !handledCurrentQuest &&",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!handledCurrentQuest && !handledCompletedQuest)",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.RestoreJournalDetailOriginals(journalDetail);",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.RemoveHoverTooltipsByPrefix(JournalDetailHoverPrefix);",
            methodBody,
            StringComparison.Ordinal);
    }
}
