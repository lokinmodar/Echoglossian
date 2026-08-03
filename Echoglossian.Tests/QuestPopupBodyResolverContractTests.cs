// <copyright file="QuestPopupBodyResolverContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies shared popup-body resolver contracts for quest popup handlers.
/// </summary>
public sealed class QuestPopupBodyResolverContractTests
{
    /// <summary>
    ///     Ensures the shared popup body text-node resolver searches both sides
    ///     of the heading so JournalAccept and JournalResult do not depend on a
    ///     single sibling ordering.
    /// </summary>
    [Fact]
    public void PopupSectionBodyTextResolver_SearchesBothHeadingSiblingDirections()
    {
        var methodSource = ReadQuestAddonHandlerBaseMethodSource(
            "TryFindPopupSectionBodyTextNodeByHeadingTextId");

        Assert.Contains("PrevSiblingNode", methodSource, StringComparison.Ordinal);
        Assert.Contains("NextSiblingNode", methodSource, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the shared popup body hover-node resolver mirrors the same
    ///     bidirectional heading search used by the body text-node resolver.
    /// </summary>
    [Fact]
    public void PopupSectionBodyHoverResolver_SearchesBothHeadingSiblingDirections()
    {
        var methodSource = ReadQuestAddonHandlerBaseMethodSource(
            "TryFindPopupSectionBodyHoverNodeByHeadingTextId");

        Assert.Contains("PrevSiblingNode", methodSource, StringComparison.Ordinal);
        Assert.Contains("NextSiblingNode", methodSource, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Reads one method body from <c>QuestAddonHandlerBase.cs</c>.
    /// </summary>
    /// <param name="methodName">The method name to extract.</param>
    /// <returns>The method source text.</returns>
    private static string ReadQuestAddonHandlerBaseMethodSource(string methodName)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "QuestAddonHandlerBase.cs"));
        var signature = $"bool {methodName}(";
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Unable to locate method '{methodName}'.");

        var nextSummary = source.IndexOf(
            "  /// <summary>",
            start + signature.Length,
            StringComparison.Ordinal);
        Assert.True(nextSummary > start, $"Unable to locate the end of method '{methodName}'.");
        return source[start..nextSummary];
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
