using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.EngineConfigUI;
public static class ChatGPTEngineUI
{
  public static bool Draw(Config config, PromptTemplateManager promptManager)
  {
    bool changed = false;

    ImGui.TextWrapped(Resources.SettingsForChatGptTransText);
    ImGui.Spacing();

    if (ImGui.Button(Resources.ChatGPTAPIKeyLink))
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = "https://platform.openai.com/settings/profile?tab=api-keys",
        UseShellExecute = true,
      });
    }

    bool isApiKeyInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.ChatGptApiKey, ref config.ChatGptApiKey, 4000, out isApiKeyInvalid);

    bool isBaseUrlInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.ModelEndpoint, ref config.ChatGPTBaseUrl, 400, out isBaseUrlInvalid);

    // Live model list toggle
    bool prevLive = config.UseOpenAILiveModelList;
    if (ImGui.Checkbox("Fetch models from OpenAI live", ref config.UseOpenAILiveModelList))
    {
      changed = true;

      if (config.UseOpenAILiveModelList && !prevLive)
      {
        _ = Task.Run(() => OpenAIModelManager.RefreshAsync(config.ChatGptApiKey));
      }
      else if (!config.UseOpenAILiveModelList)
      {
        OpenAIModelManager.ResetToDefault();
      }
    }

    // Model dropdown using current model list
    var models = OpenAIModelManager.CurrentModelList;
    int currentIndex = models.ToList().FindIndex(m => m.Id == config.OpenAILlmModel);
    if (currentIndex == -1)
    {
      currentIndex = 0;
    }

    string LabelFor(OpenAITextModel model)
    {
      var flags = new List<string>();
      if (model.IsTurbo)
      {
        flags.Add("Turbo");
      }

      if (model.IsMini)
      {
        flags.Add("Mini");
      }

      if (model.SupportsVision)
      {
        flags.Add("Vision");
      }

      string meta = flags.Count > 0 ? $" ({string.Join(", ", flags)})" : string.Empty;
      return $"{model.DisplayName}{meta}";
    }

    string GetTier(OpenAITextModel model)
    {
      if (model.Id.StartsWith("gpt-4o"))
      {
        return "GPT-4o";
      }

      if (model.Id.StartsWith("gpt-4"))
      {
        return "GPT-4";
      }

      if (model.Id.StartsWith("gpt-3.5"))
      {
        return "GPT-3.5";
      }

      if (model.Id.StartsWith("chatgpt-"))
      {
        return "ChatGPT";
      }

      if (model.Id.StartsWith("o1-"))
      {
        return "O1";
      }

      return "Other";
    }

    void PushColor(OpenAITextModel model)
    {
      if (model.IsTurbo)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.75f, 1f, 1f)); // blue
      }
      else if (model.IsMini)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 1f, 0.7f, 1f));   // green
      }
      else if (model.SupportsVision)
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.6f, 1f)); // orange
      }
      else
      {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));     // white
      }
    }

    string currentLabel = LabelFor(models[currentIndex]);

    if (ImGui.BeginCombo(Resources.LLMModel, currentLabel))
    {
      string? lastGroup = null;

      for (int i = 0; i < models.Count; i++)
      {
        var model = models[i];
        string tier = GetTier(model);

        if (tier != lastGroup)
        {
          ImGui.Separator();
          ImGui.TextDisabled(tier);
          lastGroup = tier;
        }

        bool isSelected = i == currentIndex;
        string display = LabelFor(model);

        PushColor(model);
        if (ImGui.Selectable(display, isSelected))
        {
          config.OpenAILlmModel = model.Id;
          changed = true;
        }

        ImGui.PopStyleColor();

        if (isSelected)
        {
          ImGui.SetItemDefaultFocus();
        }
      }

      ImGui.EndCombo();
    }

    // Display model ID
    ImGui.TextColored(new Vector4(1f, 1f, 0.6f, 1f), $"Model ID: {config.OpenAILlmModel}");

    float temp = config.ChatGptTemperature;
    if (ImGui.SliderFloat(Resources.Temperature, ref temp, 0.1f, 1.0f, "%.1f"))
    {
      config.ChatGptTemperature = temp;
      changed = true;
    }

    PromptEditorUI.Draw(promptManager, PromptType.ChatGPT, PromptTemplateManager.DefaultPrompt, TransEngines.ChatGPT.ToString());

    if (changed)
    {
      FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
      SaveConfig(config);
    }

    return changed;
  }
}
