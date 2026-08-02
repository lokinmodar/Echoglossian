// <copyright file="QuestOverlayOnlyPolicyContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Verifies quest-family handlers pass overlay-only language policy into
///     their shared display-mode helper calls.
/// </summary>
public sealed class QuestOverlayOnlyPolicyContractTests
{
    /// <summary>
    ///     Ensures each targeted quest-family handler explicitly threads the
    ///     global overlay-only flag through helper-driven mode decisions.
    /// </summary>
    /// <param name="fileName">The quest-family handler file to inspect.</param>
    [Theory]
    [InlineData("JournalHandler.cs")]
    [InlineData("JournalDetailHandler.cs")]
    [InlineData("JournalAcceptHandler.cs")]
    [InlineData("JournalResultHandler.cs")]
    [InlineData("ScenarioTreeHandler.cs")]
    [InlineData("ToDoListHandler.cs")]
    [InlineData("RecommendListHandler.cs")]
    [InlineData("AreaMapHandler.cs")]
    [InlineData("MapSurfaceStringArrayHandler.cs")]
    public void Quest_family_handlers_pass_overlay_only_language_to_mode_helper(
        string fileName)
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "NativeUI",
            "AddonHandlers",
            "Quest",
            fileName));

        Assert.Contains("this.Config.OverlayOnlyLanguage", source);
    }

    /// <summary>
    ///     Finds the repository root from the current test directory.
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
