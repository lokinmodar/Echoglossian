// <copyright file="PromptEditorUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Components;

/// <summary>
///     Provides UI functionality for editing prompts.
/// </summary>
public static class PromptEditorUI
{
  /// <summary>
  ///     Draws the editable prompt box for a specific engine.
  /// </summary>
  /// <param name="templateManager">
  ///     The manager responsible for handling prompt
  ///     templates.
  /// </param>
  /// <param name="type">The type of the prompt being edited.</param>
  /// <param name="defaultPrompt">
  ///     The default prompt text to use if no custom prompt
  ///     is set.
  /// </param>
  /// <param name="label">The label used to identify the prompt editor instance.</param>
  public static void Draw(
      PromptTemplateManager templateManager,
      Echoglossian.PromptType type,
      string defaultPrompt,
      string label)
  {
    var state = PromptEditorStateManager.Get(label);

    if (string.IsNullOrWhiteSpace(state.EditedPrompt))
    {
      state.EditedPrompt = templateManager.GetPromptOrDefault(type) ??
                           defaultPrompt;
    }

    ImGui.Text(Resources.AITranslatorPromptCustomization);
    ImGui.Separator();
    ImGui.TextWrapped(
        Resources
            .CustomizeThePromptUsedForTranslationAllOfTheFollowingPlaceholdersAreRequired);

    ImGui.BulletText("{text}");
    ImGui.BulletText("{sourceLanguage}");
    ImGui.BulletText("{targetLanguage}");

    ImGui.Columns(2, default, true);
    ImGui.TextWrapped(Resources.Editor);
    ImGui.PushItemWidth(-1);

    if (ImGui.InputTextMultiline(
            $"##{Resources.PromptInput}_{label}",
            ref state.EditedPrompt,
            8000,
            new Vector2(-1, 200)))
    {
      state.ShowPromptInvalidWarning =
          !templateManager.IsPromptValid(state.EditedPrompt);
    }

    ImGui.PopItemWidth();

    if (state.ShowPromptInvalidWarning)
    {
      ImGui.PushStyleColor(
          ImGuiCol.Text,
          new Vector4(1f, 0.5f, 0.5f, 1f));
      ImGui.Text(Resources.MissingOneOrMoreRequiredPlaceholders);
      ImGui.PopStyleColor();
    }

    if (ImGui.Button($"{Resources.Save}##{label}"))
    {
      if (templateManager.IsPromptValid(state.EditedPrompt))
      {
        templateManager.SetPrompt(type, state.EditedPrompt);
        state.ShowPromptInvalidWarning = false;
      }
      else
      {
        state.ShowPromptInvalidWarning = true;
      }
    }

    ImGui.SameLine();

    if (ImGui.Button($"{Resources.ResetToDefault}##{label}"))
    {
      state.EditedPrompt = defaultPrompt;
      templateManager.SetPrompt(type, null);
      state.ShowPromptInvalidWarning = false;
    }

    ImGui.NextColumn();
    ImGui.TextWrapped(Resources.LivePreviewWithSampleInput);

    state.PreviewResult = templateManager.ApplyPromptVariables(
        state.EditedPrompt,
        state.PreviewSampleText,
        state.PreviewSourceLang,
        state.PreviewTargetLang);

    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1f, 0.5f, 1f));
    ImGui.InputTextMultiline(
        $"##{Resources.Preview}_{label}",
        ref state.PreviewResult,
        10000,
        new Vector2(-1, 200),
        ImGuiInputTextFlags.ReadOnly);
    ImGui.PopStyleColor();

    if (ImGui.Button($"{Resources.CopyPreview}##{label}"))
    {
      ImGui.SetClipboardText(state.PreviewResult);
    }

    ImGui.Columns(1);
  }
}