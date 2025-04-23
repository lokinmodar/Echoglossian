using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Echoglossian.Properties;
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
      ImGui.Text(Resources.AITranslatorPromptCustomization);
      ImGui.Separator();
      ImGui.TextWrapped(Resources.CustomizeThePromptUsedForTranslationAllOfTheFollowingPlaceholdersAreRequired);

      ImGui.BulletText("{text}");
      ImGui.BulletText("{sourceLanguage}");
      ImGui.BulletText("{targetLanguage}");

      if (string.IsNullOrWhiteSpace(this.editedPrompt))
      {
        this.editedPrompt = GetPrompt(config, type) ?? defaultPrompt;
      }

      ImGui.Spacing();

      ImGui.Columns(2, null, true); // 2 columns with border

      // Column 1: Prompt Editor
      ImGui.TextWrapped(Resources.Editor);
      ImGui.PushItemWidth(-1);
      if (ImGui.InputTextMultiline($"##{Resources.PromptInput}_{label}", ref this.editedPrompt, 8000, new Vector2(-1, 200)))
      {
        this.showPromptInvalidWarning = !IsPromptValid(this.editedPrompt);
      }
      ImGui.PopItemWidth();

      if (this.showPromptInvalidWarning)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.5f, 0.5f, 1f));
        ImGui.Text(Resources.MissingOneOrMoreRequiredPlaceholders);
        ImGui.PopStyleColor();
      }

      if (ImGui.Button($"{Resources.Save}##{label}"))
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

      if (ImGui.Button($"{Resources.ResetToDefault}##{label}"))
      {
        this.editedPrompt = defaultPrompt;
        SetPrompt(config, type, null);
        this.showPromptInvalidWarning = false;
      }

      ImGui.NextColumn();

      // Column 2: Preview
      ImGui.TextWrapped(Resources.LivePreviewWithSampleInput);

      this.previewResult = ApplyPromptVariables(
        this.editedPrompt,
        this.previewSampleText,
        this.previewSourceLang,
        this.previewTargetLang
      );

      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1f, 0.5f, 1f));
      ImGui.InputTextMultiline($"##{Resources.Preview}_{label}", ref this.previewResult, 10000, new Vector2(-1, 200), ImGuiInputTextFlags.ReadOnly);
      ImGui.PopStyleColor();

      if (ImGui.Button($"{Resources.CopyPreview}##{label}"))
      {
        ImGui.SetClipboardText(this.previewResult);
      }

      ImGui.Columns(1); // Reset to 1 column

      ImGui.EndGroup();
    }

  }
}
