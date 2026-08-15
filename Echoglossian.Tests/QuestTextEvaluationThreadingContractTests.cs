// <copyright file="QuestTextEvaluationThreadingContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards quest-sheet text evaluation paths against calling
///     <c>ISeStringEvaluator</c> from worker threads.
/// </summary>
public sealed class QuestTextEvaluationThreadingContractTests
{
    /// <summary>
    ///     Ensures quest progress sheet evaluation consults the framework-thread
    ///     gate before invoking the SeString evaluator.
    /// </summary>
    [Fact]
    public void QuestProgressResolver_EvaluateQuestText_GatesSeStringEvaluatorToFrameworkThread()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "Helpers",
            "QuestProgressResolver.cs"));

        Assert.Contains(
            "Echoglossian.FrameworkInterface?.IsInFrameworkUpdateThread != true",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures dialogue metadata derivation consults the framework-thread
    ///     gate before invoking the SeString evaluator.
    /// </summary>
    [Fact]
    public void QuestDialogueMetadataDerivation_EvaluateQuestText_GatesSeStringEvaluatorToFrameworkThread()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot().FullName,
            "NativeUI",
            "Helpers",
            "QuestDialogueMetadataDerivation.cs"));

        Assert.Contains(
            "Echoglossian.FrameworkInterface?.IsInFrameworkUpdateThread != true",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Locates the repository root for source-level contract assertions.
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
