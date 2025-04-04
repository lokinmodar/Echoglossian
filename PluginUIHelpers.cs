using ImGuiNET;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private string editedAiTranslatorPrompt = string.Empty;
    private bool showPromptInvalidWarning = false;

    private readonly string previewSampleText = "My blade is for the Fury.";
    private readonly string previewSourceLang = "English";
    private readonly string previewTargetLang = "Japanese";
    private string previewResult = string.Empty;

    /// <summary>
    /// Draws the AI Translator prompt tab in the UI.
    /// </summary>
    private void DrawAiTranslatorPromptTab()
    {
      ImGui.BeginGroup();
      ImGui.Text("AI Translator Prompt Customization");
      ImGui.Separator();
      ImGui.TextWrapped("Customize the prompt used for translation. All of the following placeholders are required:");

      ImGui.BulletText("{text}");
      ImGui.BulletText("{sourceLanguage}");
      ImGui.BulletText("{targetLanguage}");

      if (string.IsNullOrWhiteSpace(this.editedAiTranslatorPrompt))
      {
        this.editedAiTranslatorPrompt = this.configuration.AiTranslatorPrompt ?? defaultPrompt;
      }

      ImGui.Spacing();

      if (ImGui.InputTextMultiline("##AiTranslatorPromptInput", ref this.editedAiTranslatorPrompt, 10000, new System.Numerics.Vector2(-1, 160)))
      {
        this.showPromptInvalidWarning = !IsPromptValid(this.editedAiTranslatorPrompt);
      }

      if (this.showPromptInvalidWarning)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.5f, 0.5f, 1f));
        ImGui.Text("⚠ Missing one or more required placeholders!");
        ImGui.PopStyleColor();
      }

      if (ImGui.Button("Save Prompt"))
      {
        if (IsPromptValid(this.editedAiTranslatorPrompt))
        {
          this.configuration.AiTranslatorPrompt = this.editedAiTranslatorPrompt;
          this.showPromptInvalidWarning = false;
        }
        else
        {
          this.showPromptInvalidWarning = true;
        }
      }

      ImGui.SameLine();

      if (ImGui.Button("Reset to Default"))
      {
        this.editedAiTranslatorPrompt = defaultPrompt;
        this.configuration.AiTranslatorPrompt = string.Empty;
        this.showPromptInvalidWarning = false;
      }

      ImGui.Separator();
      ImGui.TextWrapped("Live preview of the prompt with sample variables:");

      this.previewResult = ApplyPromptVariables(
          this.editedAiTranslatorPrompt,
          this.previewSampleText,
          this.previewSourceLang,
          this.previewTargetLang
      );

      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1f, 0.5f, 1f));
      ImGui.SetNextItemWidth(-1);
      ImGui.InputTextMultiline("##PreviewPrompt", ref this.previewResult, 10000, new System.Numerics.Vector2(-1, 160), ImGuiInputTextFlags.ReadOnly);
      ImGui.PopStyleColor();

      if (ImGui.Button("Copy Preview to Clipboard"))
      {
        ImGui.SetClipboardText(this.previewResult);
      }

      ImGui.EndGroup();
    }

  }
}
