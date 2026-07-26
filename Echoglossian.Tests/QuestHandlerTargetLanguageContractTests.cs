// <copyright file="QuestHandlerTargetLanguageContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies quest-family consumers project canonical <see cref="QuestPlate" />
///     rows with the normalized configured target language instead of raw
///     provider aliases.
/// </summary>
public sealed class QuestHandlerTargetLanguageContractTests
{
    /// <summary>
    ///     Ensures ToDoList projects canonical quest lookups with the
    ///     normalized configured target language.
    /// </summary>
    [Fact]
    public void ToDoList_uses_normalized_configured_target_language_for_canonical_quest_lookup()
    {
        AssertCanonicalQuestProjectionUsesNormalizedTargetLanguage(
            Path.Combine(
                "NativeUI",
                "AddonHandlers",
                "Quest",
                "ToDoListHandler.cs"));
    }

    /// <summary>
    ///     Ensures ScenarioTree projects canonical quest lookups with the
    ///     normalized configured target language.
    /// </summary>
    [Fact]
    public void ScenarioTree_uses_normalized_configured_target_language_for_canonical_quest_lookup()
    {
        AssertCanonicalQuestProjectionUsesNormalizedTargetLanguage(
            Path.Combine(
                "NativeUI",
                "AddonHandlers",
                "Quest",
                "ScenarioTreeHandler.cs"));
    }

    /// <summary>
    ///     Verifies one quest handler source file keeps canonical quest
    ///     projection on the normalized configured target language.
    /// </summary>
    /// <param name="relativePath">The handler source path relative to repo root.</param>
    private static void AssertCanonicalQuestProjectionUsesNormalizedTargetLanguage(
        string relativePath)
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root.FullName, relativePath));
        var toQuestPlateCall = source.IndexOf(
            "var questPlate = questCanonicalData.ToQuestPlate(",
            StringComparison.Ordinal);

        Assert.True(toQuestPlateCall >= 0);

        var findQuestPlateCall = source.IndexOf(
            "this.FindQuestPlate(questPlate);",
            toQuestPlateCall,
            StringComparison.Ordinal);
        Assert.True(findQuestPlateCall > toQuestPlateCall);

        var snippet = source.Substring(
            toQuestPlateCall,
            findQuestPlateCall - toQuestPlateCall);

        Assert.Contains(
            "RuntimeLanguageHelper.GetConfiguredTargetLanguageCode(this.Config.Lang)",
            snippet);
        Assert.DoesNotContain("LangDict[LanguageInt].Code", snippet);
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
