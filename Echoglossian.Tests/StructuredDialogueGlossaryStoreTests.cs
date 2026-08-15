// <copyright file="StructuredDialogueGlossaryStoreTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators;
using FluentAssertions;
using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Covers the shared runtime glossary store introduced for structured
///     dialogue glossary loading.
/// </summary>
public class StructuredDialogueGlossaryStoreTests
{
    /// <summary>
    ///     Ensures the shared store filters loaded entries by language scope.
    /// </summary>
    [Fact]
    public async Task GetEntries_ShouldFilterBySourceAndTargetLanguage()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                filePath,
                """
                [
                  {
                    "source_text": "スフェーン",
                    "target_text": "Sphene",
                    "source_language": "ja-JP",
                    "target_language": "en-US"
                  },
                  {
                    "source_text": "Krile",
                    "target_text": "Krile",
                    "source_language": "en-US",
                    "target_language": "pt-BR"
                  }
                ]
                """);

            StructuredDialogueGlossaryStore.Clear();
            (await StructuredDialogueGlossaryStore.RefreshAsync(
                filePath,
                CancellationToken.None)).Should().BeTrue();

            var japaneseToEnglishEntries =
                StructuredDialogueGlossaryStore.GetEntries("ja-JP", "en-US");
            var englishToPortugueseEntries =
                StructuredDialogueGlossaryStore.GetEntries("en-US", "pt-BR");

            japaneseToEnglishEntries.Should().ContainSingle();
            japaneseToEnglishEntries[0].TargetText.Should().Be("Sphene");
            englishToPortugueseEntries.Should().ContainSingle();
            englishToPortugueseEntries[0].TargetText.Should().Be("Krile");
        }
        finally
        {
            StructuredDialogueGlossaryStore.Clear();
            File.Delete(filePath);
        }
    }

    /// <summary>
    ///     Ensures a newer refresh result stays published even if an older
    ///     in-flight refresh completes afterward.
    /// </summary>
    /// <returns>A task that completes when the competing refreshes settle.</returns>
    [Fact]
    public async Task RefreshAsync_NewerCompletedLoadStaysPublished()
    {
        var firstLoad = new TaskCompletionSource<StructuredDialogueGlossaryLoadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondLoad = new TaskCompletionSource<StructuredDialogueGlossaryLoadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;

        StructuredDialogueGlossaryStore.ResetForTests(
            (_, _) =>
            {
                var invocation = Interlocked.Increment(ref invocationCount);
                return invocation == 1 ? firstLoad.Task : secondLoad.Task;
            });

        try
        {
            var firstRefresh = StructuredDialogueGlossaryStore.RefreshAsync(
                "first.json",
                CancellationToken.None);
            var secondRefresh = StructuredDialogueGlossaryStore.RefreshAsync(
                "second.json",
                CancellationToken.None);

            secondLoad.SetResult(new StructuredDialogueGlossaryLoadResult(
                true,
                [
                    new StructuredDialogueGlossaryEntry(
                        "Krile",
                        "Krile",
                        null,
                        "en-US",
                        "pt-BR"),
                ],
                0,
                null));
            await secondRefresh.WaitAsync(TimeSpan.FromSeconds(1));

            firstLoad.SetResult(new StructuredDialogueGlossaryLoadResult(
                true,
                [
                    new StructuredDialogueGlossaryEntry(
                        "スフェーン",
                        "Sphene",
                        null,
                        "ja-JP",
                        "en-US"),
                ],
                0,
                null));
            await firstRefresh.WaitAsync(TimeSpan.FromSeconds(1));

            var snapshot = StructuredDialogueGlossaryStore.GetSnapshot();
            var entries = StructuredDialogueGlossaryStore.GetEntries("en-US", "pt-BR");

            snapshot.LastLoadPath.Should().Be(Path.GetFullPath("second.json"));
            entries.Should().ContainSingle();
            entries[0].SourceText.Should().Be("Krile");
        }
        finally
        {
            StructuredDialogueGlossaryStore.ResetForTests();
        }
    }

    /// <summary>
    ///     Ensures clearing the store invalidates any older in-flight refresh
    ///     so it cannot repopulate stale glossary rows afterward.
    /// </summary>
    /// <returns>A task that completes when the stale refresh is discarded.</returns>
    [Fact]
    public async Task Clear_RejectsOlderInFlightRefreshResult()
    {
        var pendingLoad = new TaskCompletionSource<StructuredDialogueGlossaryLoadResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        StructuredDialogueGlossaryStore.ResetForTests(
            (_, _) => pendingLoad.Task);

        try
        {
            var refreshTask = StructuredDialogueGlossaryStore.RefreshAsync(
                "stale.json",
                CancellationToken.None);
            StructuredDialogueGlossaryStore.Clear();

            pendingLoad.SetResult(new StructuredDialogueGlossaryLoadResult(
                true,
                [
                    new StructuredDialogueGlossaryEntry(
                        "スフェーン",
                        "Sphene",
                        null,
                        "ja-JP",
                        "en-US"),
                ],
                0,
                null));
            await refreshTask.WaitAsync(TimeSpan.FromSeconds(1));

            var snapshot = StructuredDialogueGlossaryStore.GetSnapshot();

            snapshot.EntryCount.Should().Be(0);
            snapshot.LastLoadPath.Should().BeNull();
            StructuredDialogueGlossaryStore.GetEntries("ja-JP", "en-US")
                .Should()
                .BeEmpty();
        }
        finally
        {
            StructuredDialogueGlossaryStore.ResetForTests();
        }
    }
}
