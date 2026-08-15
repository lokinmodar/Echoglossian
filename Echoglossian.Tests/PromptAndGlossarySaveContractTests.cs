// <copyright file="PromptAndGlossarySaveContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Echoglossian.PluginUI;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the non-destructive prompt and glossary save contract for issue
///     252.
/// </summary>
public sealed class PromptAndGlossarySaveContractTests
{
    /// <summary>
    ///     Ensures prompt and glossary saves preserve the existing
    ///     retranslation toggle instead of silently flipping it.
    /// </summary>
    [Fact]
    public void SaveConfig_PromptAndGlossaryChanges_PreserveTranslateAlreadyTranslatedTexts()
    {
        var config = new Config
        {
            TranslateAlreadyTranslatedTexts = true,
            ChatGptPrompt = "before",
            EnableDialogueGlossaryInjection = true,
            DialogueGlossaryFilePath = "before.json",
        };
        Config? captured = null;

        using var scope = PluginConfigSaveScope.Push(
            saved => captured = saved.CreatePersistenceSnapshot());

        config.ChatGptPrompt = "after";
        config.DialogueGlossaryFilePath = "after.json";
        Echoglossian.SaveConfig(config);

        Assert.True(config.TranslateAlreadyTranslatedTexts);
        Assert.NotNull(captured);
        Assert.True(captured!.TranslateAlreadyTranslatedTexts);
        Assert.Equal("after", captured.ChatGptPrompt);
        Assert.Equal("after.json", captured.DialogueGlossaryFilePath);
    }

    /// <summary>
    ///     Ensures the prompt and glossary settings surfaces explicitly explain
    ///     that saves only affect new requests and point operators to the
    ///     existing retranslation action.
    /// </summary>
    [Fact]
    public void TranslationSettingsUi_ExplainsNewRequestOnlyRefreshBehavior()
    {
        var root = FindRepositoryRoot();
        var promptSource = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Components",
            "PromptEditorUI.cs"));
        var glossarySource = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Tabs",
            "TranslationEnginesTab.cs"));
        var resources = File.ReadAllText(Path.Combine(
            root.FullName,
            "Properties",
            "Resources.resx"));

        Assert.Contains(
            "Resources.TranslationSettingsNewRequestsOnlyNotice",
            promptSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Resources.TranslatorDebuggerRetranslateVisibleDialogueAndPersist",
            promptSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Resources.TranslationSettingsNewRequestsOnlyNotice",
            glossarySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Resources.TranslatorDebuggerRetranslateVisibleDialogueAndPersist",
            glossarySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "<data name=\"TranslationSettingsNewRequestsOnlyNotice\"",
            resources,
            StringComparison.Ordinal);
        Assert.Contains(
            "Changes here affect new requests only.",
            resources,
            StringComparison.Ordinal);
        Assert.Contains(
            "Existing stored translations stay in the database.",
            resources,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Ensures the shared configuration save path used by prompt and
    ///     glossary edits remains config-only and contains no database row
    ///     deletion behavior.
    /// </summary>
    [Fact]
    public void SaveConfig_Method_RemainsFreeOfDatabaseDeletion()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "GeneralHelpers",
            "Utils.cs"));
        var saveConfigStart = source.IndexOf(
            "public static void SaveConfig(Config config)",
            StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf(
            "private void QueueConfigurationSave(Config snapshot)",
            saveConfigStart,
            StringComparison.Ordinal);

        Assert.True(saveConfigStart >= 0, "Expected SaveConfig to exist.");
        Assert.True(nextMethodStart > saveConfigStart, "Expected the next method boundary.");

        var saveConfigSource = source.Substring(
            saveConfigStart,
            nextMethodStart - saveConfigStart);

        Assert.DoesNotContain(
            "EchoglossianDbContext",
            saveConfigSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".Database",
            saveConfigSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RemoveRange(",
            saveConfigSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Delete(",
            saveConfigSource,
            StringComparison.Ordinal);
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
