// <copyright file="ChatGptEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Numerics;

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators;
using Echoglossian.Translators.OpenAI;
using ImGuiNET;

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
    changed |= FieldValidationHelper.ValidatedInputText(Resources.ChatGptApiKey, ref config.ChatGptApiKey, 400, out isApiKeyInvalid);

    bool isBaseUrlInvalid;
    changed |= FieldValidationHelper.ValidatedInputText(Resources.ModelEndpoint, ref config.ChatGPTBaseUrl, 400, out isBaseUrlInvalid);

    // Live model fetch toggle
    bool prev = config.UseLiveOpenAIModelList;
    if (ImGui.Checkbox("Fetch OpenAI model list live", ref config.UseLiveOpenAIModelList))
    {
      changed = true;
      if (config.UseLiveOpenAIModelList && !prev)
      {
        _ = Task.Run(() => OpenAIModelManager.RefreshAsync(config.ChatGptApiKey));
      }
      else if (!config.UseLiveOpenAIModelList)
      {
        OpenAIModelManager.ResetToDefault();
      }
    }

    var tooltips = new Dictionary<string, string>
    {
      ["gpt-3.5-turbo"] = "⚡ Fast and affordable (4k tokens)",
      ["gpt-3.5-turbo-16k"] = "⚡ 16k token context",
      ["gpt-4"] = "🧠 More capable but slower and costly",
      ["gpt-4-turbo"] = "🟢 Faster and cheaper GPT-4 variant",
      ["gpt-4o"] = "👁 Multimodal and real-time model",
      ["gpt-4o-mini"] = "⚡ GPT-4o Mini — fast and compact",
    };

    var models = config.UseLiveOpenAIModelList
      ? OpenAIModelManager.CurrentModelList
      : OpenAITextModelDefaults.PredefinedModels;

    changed |= ModelDropdownUI.Draw(
      Resources.LLMModel,
      ref config.OpenAILlmModel,
      models,
      engine: "OpenAI",
      tooltips: tooltips);

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
