// <copyright file="AreaMapHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies AreaMap handler contracts that keep quest tracker text from
///     getting stuck after a background translation completes.
/// </summary>
public sealed class AreaMapHandlerContractTests
{
    /// <summary>
    ///     Ensures the draw path can promote pending translations and apply the
    ///     selected display mode instead of refreshing hover targets only.
    /// </summary>
    [Fact]
    public void AreaMap_predraw_promotes_pending_translations()
    {
        var source = ReadAreaMapHandlerSource();

        Assert.Contains("OnAreaMapPreDrawEvent", source);
        Assert.Contains("TryRefreshAreaMapPendingTranslation", source);
        Assert.Contains("ApplyAreaMapPresentation", source);
        Assert.Contains("QueueAreaMapTranslation", source);
    }

    /// <summary>
    ///     Ensures AreaMap reads quest text from the visible map text nodes
    ///     observed by addon probing instead of a stale AtkValue index.
    /// </summary>
    [Fact]
    public void AreaMap_reads_visible_quest_text_nodes()
    {
        var source = ReadAreaMapHandlerSource();

        Assert.Contains("ResolveAreaMapQuestTextNodes", source);
        Assert.Contains("AddonTextNodeResolvers.ResolveReadableTextNodes", source);
        Assert.Contains("AreaMapLevelQuestTextPattern", source);
        Assert.DoesNotContain("AreaMapQuestValueIndex", source);
    }

    /// <summary>
    ///     Reads the AreaMap handler source.
    /// </summary>
    /// <returns>The handler source text.</returns>
    private static string ReadAreaMapHandlerSource()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "AreaMapHandler.cs"));
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
