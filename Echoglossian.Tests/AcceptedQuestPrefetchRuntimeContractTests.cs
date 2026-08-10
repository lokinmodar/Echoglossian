// <copyright file="AcceptedQuestPrefetchRuntimeContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
/// Guards the accepted-quest prefetch runtime against running the full
/// canonical prefetch pipeline inline on the plugin tick.
/// </summary>
public sealed class AcceptedQuestPrefetchRuntimeContractTests
{
    /// <summary>
    /// Ensures the plugin tick schedules accepted-quest prefetch work instead
    /// of invoking the heavy prefetch routine inline.
    /// </summary>
    [Fact]
    public void TickAcceptedQuestPrefetch_QueuesBackgroundWorkInsteadOfCallingPrefetchInline()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AcceptedQuestPrefetchRuntime.cs"));

        Assert.Contains(
            "this.ScheduleAcceptedQuestPrefetch(",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "this.PrefetchAcceptedQuest(",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures accepted-quest capture starts dialogue metadata generation in a
    /// separately owned operation instead of performing sheet or database work
    /// on the framework tick.
    /// </summary>
    [Fact]
    public void ScheduleAcceptedQuestPrefetch_StartsOwnedDialogueMetadataGeneration()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AcceptedQuestPrefetchRuntime.cs"));

        Assert.Contains(
            "private readonly OwnedAsyncOperationSet acceptedQuestDialogueMetadataOperations",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.acceptedQuestDialogueMetadataOperations.Run(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QuestDialogueMetadataDerivation.ReadDialogueEntries(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QuestDialogueMetadataDerivation.BuildEntries(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "this.UpsertQuestDialogueMetadataBatchAsync(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "workItem.Generation !=\n        Volatile.Read(ref this.acceptedQuestPrefetchGeneration)",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures clearing accepted-quest state cancels metadata work captured by
    /// the prior generation before it can reach the database upsert boundary.
    /// </summary>
    [Fact]
    public void ClearAcceptedQuestPrefetchState_CancelsPriorGenerationMetadataWrites()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AcceptedQuestPrefetchRuntime.cs"));

        Assert.Contains(
            "private CancellationTokenSource acceptedQuestDialogueMetadataGenerationCancellationSource",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Exchange(\n        ref this.acceptedQuestDialogueMetadataGenerationCancellationSource,\n        new CancellationTokenSource())",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "previousGenerationCancellationSource.Cancel();",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "workItem.GenerationCancellationToken);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "cancellationToken.ThrowIfCancellationRequested();\n\n    await this.UpsertQuestDialogueMetadataBatchAsync(",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures a stale accepted-quest generation cannot commit dialogue
    /// metadata after its transaction has begun.
    /// </summary>
    [Fact]
    public void UpsertQuestDialogueMetadataBatchAsync_RevalidatesGenerationAtTransactionCommit()
    {
        var root = FindRepositoryRoot();
        var runtimeSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "Helpers",
            "AcceptedQuestPrefetchRuntime.cs"));
        var persistenceSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "DBHelpers",
            "DbOperations.cs"));

        Assert.Contains(
            "() => workItem.Generation ==\n                Volatile.Read(ref this.acceptedQuestPrefetchGeneration)",
            runtimeSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Func<bool> commitGuard",
            persistenceSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "BeginTransactionAsync(cancellationToken)",
            persistenceSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (!commitGuard())\n    {\n      return;\n    }\n\n    await transaction.CommitAsync(cancellationToken)",
            persistenceSource,
            StringComparison.Ordinal);
    }

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
