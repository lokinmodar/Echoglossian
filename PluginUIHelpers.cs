using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private string editedPrompt = string.Empty;
    private string previewResult = string.Empty;
    private bool showPromptInvalidWarning = false;

    private readonly string previewSampleText = "My blade is for the Fury.";
    private readonly string previewSourceLang = "English";
    private readonly string previewTargetLang = "Japanese";

    /// <summary>
    /// Draws the AI Translator prompt tab in the UI.
    /// </summary>
    private void DrawPromptEditor(Config config, PromptType type, string defaultPrompt, string label)
    {
      ImGui.BeginGroup();
      ImGui.Text("AI Translator Prompt Customization");
      ImGui.Separator();
      ImGui.TextWrapped("Customize the prompt used for translation. All of the following placeholders are required:");


      ImGui.BulletText("{text}");
      ImGui.BulletText("{sourceLanguage}");
      ImGui.BulletText("{targetLanguage}");

      if (string.IsNullOrWhiteSpace(this.editedPrompt))
      {
        this.editedPrompt = GetPrompt(config, type) ?? defaultPrompt;
      }

      ImGui.Spacing();

      if (ImGui.InputTextMultiline($"##PromptInput_{label}", ref this.editedPrompt, 8000, new System.Numerics.Vector2(-1, 200)))
      {
        this.showPromptInvalidWarning = !IsPromptValid(this.editedPrompt);
      }

      if (this.showPromptInvalidWarning)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.5f, 0.5f, 1f));
        ImGui.Text("⚠ Missing one or more required placeholders!");
        ImGui.PopStyleColor();
      }

      if (ImGui.Button($"Save##{label}"))
      {
        if (IsPromptValid(this.editedPrompt))
        {
          SetPrompt(config, type, this.editedPrompt);
          this.showPromptInvalidWarning = false;
        }
        else
        {
          this.showPromptInvalidWarning = true;
        }
      }

      ImGui.SameLine();

      if (ImGui.Button($"Reset to Default##{label}"))
      {
        this.editedPrompt = defaultPrompt;
        SetPrompt(config, type, null);
        this.showPromptInvalidWarning = false;
      }

      ImGui.Separator();
      ImGui.TextWrapped("Live preview with sample input:");
      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1f, 0.5f, 1f));
      ImGui.SetNextItemWidth(-1);
      this.previewResult = ApplyPromptVariables(this.editedPrompt, this.previewSampleText, this.previewSourceLang, this.previewTargetLang);
      ImGui.InputTextMultiline($"##Preview_{label}", ref this.previewResult, 10000, new System.Numerics.Vector2(-1, 200), ImGuiInputTextFlags.ReadOnly);
      ImGui.PopStyleColor();

      if (ImGui.Button($"Copy Preview##{label}"))
      {
        ImGui.SetClipboardText(this.previewResult);
      }

      ImGui.EndGroup();
    }
  }
}
