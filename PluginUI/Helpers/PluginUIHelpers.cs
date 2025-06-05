using Echoglossian.Properties;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    /// <summary>
    /// Draws the AI Translator prompt tab in the UI.
    /// </summary>
    public static void DrawPromptEditor(Config config, PromptType type, string defaultPrompt, string label)
    {
      ImGui.BeginGroup();
      ImGui.Text(Resources.AITranslatorPromptCustomization);
      ImGui.Separator();
      ImGui.TextWrapped(Resources.CustomizeThePromptUsedForTranslationAllOfTheFollowingPlaceholdersAreRequired);

      ImGui.BulletText("{text}");
      ImGui.BulletText("{sourceLanguage}");
      ImGui.BulletText("{targetLanguage}");

      ref var state = ref PromptEditorState.Get(label);

      if (string.IsNullOrWhiteSpace(state.EditedPrompt))
      {
        state.EditedPrompt = GetPrompt(config, type) ?? defaultPrompt;
      }

      ImGui.Spacing();
      ImGui.Columns(2, null, true);

      ImGui.TextWrapped(Resources.Editor);
      ImGui.PushItemWidth(-1);

      if (ImGui.InputTextMultiline($"##{Resources.PromptInput}_{label}", ref state.EditedPrompt, 8000, new Vector2(-1, 200)))
      {
        state.ShowPromptInvalidWarning = !IsPromptValid(state.EditedPrompt);
      }

      ImGui.PopItemWidth();

      if (state.ShowPromptInvalidWarning)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.5f, 0.5f, 1f));
        ImGui.Text(Resources.MissingOneOrMoreRequiredPlaceholders);
        ImGui.PopStyleColor();
      }

      if (ImGui.Button($"{Resources.Save}##{label}"))
      {
        if (IsPromptValid(state.EditedPrompt))
        {
          SetPrompt(config, type, state.EditedPrompt);
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
        SetPrompt(config, type, null);
        state.ShowPromptInvalidWarning = false;
      }

      ImGui.NextColumn();
      ImGui.TextWrapped(Resources.LivePreviewWithSampleInput);

      state.PreviewResult = ApplyPromptVariables(
        state.EditedPrompt,
        state.PreviewSampleText,
        state.PreviewSourceLang,
        state.PreviewTargetLang
      );

      ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1f, 0.5f, 1f));
      ImGui.InputTextMultiline($"##{Resources.Preview}_{label}", ref state.PreviewResult, 10000, new Vector2(-1, 200), ImGuiInputTextFlags.ReadOnly);
      ImGui.PopStyleColor();

      if (ImGui.Button($"{Resources.CopyPreview}##{label}"))
      {
        ImGui.SetClipboardText(state.PreviewResult);
      }

      ImGui.Columns(1);
      ImGui.EndGroup();
    }

    private static class PromptEditorState
    {
      private static readonly Dictionary<string, State> states = new();

      public static ref State Get(string label)
      {
        if (!states.TryGetValue(label, out var s))
        {
          s = new State();
          states[label] = s;
        }

        return ref states[label];
      }

      public class State
      {
        public string EditedPrompt = string.Empty;
        public string PreviewResult = string.Empty;
        public bool ShowPromptInvalidWarning = false;
        public readonly string PreviewSampleText = "My blade is for the Fury.";
        public readonly string PreviewSourceLang = "English";
        public readonly string PreviewTargetLang = "Japanese";
      }
    }
  }
}
