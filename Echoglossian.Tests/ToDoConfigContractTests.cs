// <copyright file="ToDoConfigContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the dedicated ToDo configuration and Journal-tab UI contract.
/// </summary>
public sealed class ToDoConfigContractTests
{
    /// <summary>
    ///     Ensures the config declares dedicated ToDo settings.
    /// </summary>
    [Fact]
    public void Config_DefinesDedicatedToDoToggleAndDisplayMode()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root.FullName, "Config.cs"));

        Assert.Contains("TranslateToDo", source, StringComparison.Ordinal);
        Assert.Contains("ToDoTranslationDisplayMode", source, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the Journal tab draws a dedicated ToDo section.
    /// </summary>
    [Fact]
    public void JournalTab_DrawsDedicatedToDoSection()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "JournalTab.cs"));

        Assert.Contains("Resources.TranslateToDoToggle", source, StringComparison.Ordinal);
        Assert.Contains("ref config.TranslateToDo", source, StringComparison.Ordinal);
        Assert.Contains(
            "ref config.ToDoTranslationDisplayMode",
            source,
            StringComparison.Ordinal);
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
