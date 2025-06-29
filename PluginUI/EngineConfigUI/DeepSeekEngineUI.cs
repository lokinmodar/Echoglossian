using Echoglossian.Translators.DeepSeek;
using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.EngineConfigUI;
public static class DeepSeekEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForDeepSeekText);

    bool isApiKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.APIKey, ref config.DeepSeekTranslatorApiKey, 300, out isApiKeyInvalid);

    bool isEndpointInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.Endpoint, ref config.DeepSeekBaseUrl, 300, out isEndpointInvalid);

    var models = DeepSeekTextModelDefaults.PredefinedModels;
    int currentIndex = models.FindIndex(m => m.Id == config.DeepSeekModel);
    if (currentIndex == -1) currentIndex = 0;

    string LabelFor(OpenAITextModel model)
    {
      var flags = new List<string>();
      if (model.IsTurbo) flags.Add("Turbo");
      if (model.IsMini) flags.Add("Mini");
      string meta = flags.Count > 0 ? $" ({string.Join(", ", flags)})" : string.Empty;
      return $"{model.DisplayName}{meta}";
    }

    string currentLabel = LabelFor(models[currentIndex]);

    if (ImGui.BeginCombo(Resources.LLMModel, currentLabel))
    {
      for (int i = 0; i < models.Count; i++)
      {
        var model = models[i];
        bool isSelected = i == currentIndex;
        string display = LabelFor(model);

        if (ImGui.Selectable(display, isSelected))
        {
          config.DeepSeekModel = model.Id;
          changed = true;
        }

        if (isSelected)
        {
          ImGui.SetItemDefaultFocus();
        }
      }
      ImGui.EndCombo();
    }

    ImGui.TextColored(new Vector4(1f, 1f, 0.6f, 1f), $"Model ID: {config.DeepSeekModel}");

    PromptEditorUI.Draw(promptManager, PromptType.DeepSeek, PromptTemplateManager.DefaultPrompt, TransEngines.DeepSeek.ToString());

    if (changed)
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
