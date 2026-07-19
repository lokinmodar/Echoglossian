// <copyright file="RecommendListHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies crash-sensitive RecommendList handler contracts.
/// </summary>
public sealed class RecommendListHandlerContractTests
{
    /// <summary>
    ///     Ensures the visible-node update pass does not call itself directly.
    /// </summary>
    [Fact]
    public void UpdateRecommendList_does_not_recurse_directly()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "RecommendListHandler.cs"));

        var updateMethodStart = source.IndexOf(
            "private unsafe void UpdateRecommendList",
            StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf(
            "private unsafe void OnRecommendListEvent",
            StringComparison.Ordinal);

        Assert.True(updateMethodStart >= 0);
        Assert.True(nextMethodStart > updateMethodStart);

        var updateMethod = source[updateMethodStart..nextMethodStart];

        Assert.DoesNotContain(
            "this.UpdateRecommendList(sourceLanguage);",
            updateMethod);
        Assert.Contains(
            "private unsafe void TranslateRecommendListHandler",
            source);
        Assert.Contains(
            "this.UpdateRecommendList(sourceLanguage);",
            source[source.IndexOf(
                "private unsafe void TranslateRecommendListHandler",
                StringComparison.Ordinal)..]);
    }

    /// <summary>
    ///     Finds the repository root from the test output directory.
    /// </summary>
    /// <returns>The repository root directory.</returns>
    private static DirectoryInfo FindRepositoryRoot()
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
}
