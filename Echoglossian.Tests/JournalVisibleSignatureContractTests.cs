// <copyright file="JournalVisibleSignatureContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Covers visible-signature expectations specific to Journal.
/// </summary>
public class JournalVisibleSignatureContractTests
{
    /// <summary>
    ///     Ensures Journal invalidates its visible-list signature by stable
    ///     row position and source title, not by transient text-node pointers
    ///     that can churn across harmless addon refreshes.
    /// </summary>
    [Fact]
    public void TryComputeVisibleJournalSignature_AvoidsTransientNodePointers()
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
            "JournalHandler.cs"));
        var methodStart = source.IndexOf(
            "private unsafe bool TryComputeVisibleJournalSignature(",
            StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf(
            "private unsafe void OnJournalPreDrawEvent(",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(
            methodStart >= 0,
            "Journal should keep visible-list invalidation in TryComputeVisibleJournalSignature.");
        Assert.True(
            nextMethodStart > methodStart,
            "Journal should keep pre-draw retry handling separate from visible-list invalidation.");

        var methodBody = source.Substring(methodStart, nextMethodStart - methodStart);

        Assert.Contains(
            "this.TryGetJournalListOriginalTextForLiveText(",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "hash.Add(i);",
            methodBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "hash.Add(signatureQuestName, StringComparer.Ordinal);",
            methodBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "hash.Add(questNameNodeKey);",
            methodBody,
            StringComparison.Ordinal);
    }
}
