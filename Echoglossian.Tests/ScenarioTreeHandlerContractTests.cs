// <copyright file="ScenarioTreeHandlerContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies ScenarioTree handler contracts that keep visible quest text from
///     getting stuck when canonical persisted rows are not ready yet.
/// </summary>
public sealed class ScenarioTreeHandlerContractTests
{
    /// <summary>
    ///     Ensures ScenarioTree queues visible text translations instead of only
    ///     waiting for a pre-warmed quest database row.
    /// </summary>
    [Fact]
    public void ScenarioTree_queues_visible_text_when_payload_is_missing()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "ScenarioTreeHandler.cs"));

        Assert.Contains("TryResolveScenarioTreeFallbackTranslation", source);
        Assert.Contains("QueueScenarioTreeTranslation", source);
        Assert.Contains("this.QueueTranslation(", source);
    }

    /// <summary>
    ///     Ensures ScenarioTree reads and applies the visible title text node
    ///     observed by addon probing instead of relying only on AtkValue slots.
    /// </summary>
    [Fact]
    public void ScenarioTree_reads_and_applies_visible_title_text_nodes()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "ScenarioTreeHandler.cs"));

        Assert.Contains("ScenarioTreeQuestTitleNodeId", source);
        Assert.Contains("ResolveScenarioTreeVisibleTitleTextNodes", source);
        Assert.Contains("AddonTextNodeResolvers.ResolveReadableTextNodes", source);
        Assert.Contains("ApplyScenarioTreeTextNodePresentation", source);
        Assert.Contains("scenarioTreeNativeMutationTextNodeKeys", source);
        Assert.Contains("TextNodeKey == textNodeKey", source);
        Assert.Contains("textNodeKey: textNodeAddress", source);
    }

    /// <summary>
    ///     Ensures ScenarioTree restores handler-owned native text before
    ///     discarding the original snapshot at addon cleanup.
    /// </summary>
    [Fact]
    public void ScenarioTree_restores_native_text_before_cleanup_discards_runtime_state()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            "ScenarioTreeHandler.cs"));

        var cleanupStart = source.IndexOf(
            "private unsafe void OnScenarioTreeCleanupEvent",
            StringComparison.Ordinal);
        var restoreCall = source.IndexOf(
            "this.RestoreScenarioTreeOriginals(atkValues);",
            cleanupStart,
            StringComparison.Ordinal);
        var clearCall = source.IndexOf(
            "this.scenarioTreeRuntimeEntries.Clear();",
            cleanupStart,
            StringComparison.Ordinal);

        Assert.True(cleanupStart >= 0);
        Assert.True(restoreCall > cleanupStart);
        Assert.True(clearCall > restoreCall);
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
