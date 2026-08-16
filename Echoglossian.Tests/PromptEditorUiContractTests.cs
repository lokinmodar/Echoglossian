// <copyright file="PromptEditorUiContractTests.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.IO;

using Xunit;

namespace Echoglossian.Tests;

/// <summary>
///     Guards the prompt-editor save and reset wiring used by the LLM engine
///     configuration panels.
/// </summary>
public sealed class PromptEditorUiContractTests
{
    /// <summary>
    ///     Ensures the prompt editor now reports changes only for successful
    ///     Save and Reset actions while leaving idle or invalid-save draws
    ///     unchanged.
    /// </summary>
    [Fact]
    public void PromptEditorUi_Draw_ReturnsChangedStateOnlyForSuccessfulSaveOrReset()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root.FullName,
            "PluginUI",
            "Components",
            "PromptEditorUI.cs"));

        Assert.Contains("public static bool Draw(", source, StringComparison.Ordinal);
        Assert.Contains("var changed = false;", source, StringComparison.Ordinal);
        Assert.Contains("return changed;", source, StringComparison.Ordinal);

        var saveButton = source.IndexOf(
            "if (ImGui.Button($\"{Resources.Save}##{label}\"))",
            StringComparison.Ordinal);
        var validSetPrompt = source.IndexOf(
            "templateManager.SetPrompt(type, state.EditedPrompt);",
            saveButton,
            StringComparison.Ordinal);
        var saveChanged = source.IndexOf(
            "changed = true;",
            validSetPrompt,
            StringComparison.Ordinal);
        var invalidSaveWarning = source.IndexOf(
            "state.ShowPromptInvalidWarning = true;",
            saveButton,
            StringComparison.Ordinal);

        Assert.True(saveButton >= 0, "Expected the Save button block to exist.");
        Assert.True(validSetPrompt > saveButton, "Expected Save to update the prompt.");
        Assert.True(
            saveChanged > validSetPrompt && saveChanged < invalidSaveWarning,
            "Expected only a valid Save path to report a change.");

        var resetButton = source.IndexOf(
            "if (ImGui.Button($\"{Resources.ResetToDefault}##{label}\"))",
            StringComparison.Ordinal);
        var resetChanged = source.IndexOf(
            "changed = true;",
            invalidSaveWarning,
            StringComparison.Ordinal);

        Assert.True(resetButton > invalidSaveWarning, "Expected Reset to appear after Save.");
        Assert.True(
            resetChanged > resetButton,
            "Expected Reset to report a change.");
    }

    /// <summary>
    ///     Ensures every engine panel that embeds the prompt editor aggregates
    ///     its result into the shared changed flag and no longer saves config
    ///     directly from the panel.
    /// </summary>
    [Fact]
    public void LlmEnginePanels_AggregatePromptEditorChanges_AndAvoidDirectPanelSaves()
    {
        var root = FindRepositoryRoot();
        var engineFiles = new[]
        {
            "ChatGptEngineUI.cs",
            "ClaudeEngineUI.cs",
            "DeepSeekEngineUI.cs",
            "GeminiEngineUI.cs",
            "LmStudioEngineUI.cs",
            "OllamaEngineUI.cs",
            "OpenRouterEngineUI.cs",
            "YandexCloudEngineUI.cs",
        };

        foreach (var fileName in engineFiles)
        {
            var source = File.ReadAllText(Path.Combine(
                root.FullName,
                "PluginUI",
                "EngineConfigUI",
                fileName));

            Assert.Contains(
                "changed |= PromptEditorUI.Draw(",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Echoglossian.SaveConfig(config);",
                source,
                StringComparison.Ordinal);
        }
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
