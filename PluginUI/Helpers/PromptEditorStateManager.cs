// <copyright file="PromptEditorStateManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Helpers;

/// <summary>
///     Manages UI editing state for each translation prompt engine tab.
/// </summary>
public static class PromptEditorStateManager
{
    private static readonly ConcurrentDictionary<string, PromptEditorState>
        States = new();

    /// <summary>
    ///     Gets the editor state for a given tab label.
    /// </summary>
    /// <param name="label">Label identifying the translation engine or tab.</param>
    /// <returns>A mutable reference to the editor state.</returns>
    public static PromptEditorState Get(string label)
    {
        return States.GetOrAdd(label, _ => new PromptEditorState());
    }
}

/// <summary>
///     Stores the editor UI state for a single prompt editing session.
/// </summary>
public class PromptEditorState
{
    public readonly string PreviewSampleText = "My blade is for the Fury.";
    public readonly string PreviewSourceLang = "English";
    public readonly string PreviewTargetLang = "Japanese";
    public string EditedPrompt = string.Empty;
    public string PreviewResult = string.Empty;
    public bool ShowPromptInvalidWarning = false;
}