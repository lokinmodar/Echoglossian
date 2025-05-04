// <copyright file="PluginUITranslationEnginesTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Interface.Utility.Raii;
using Echoglossian.Properties;
using Echoglossian.Translators;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public string[] Engines = [];

    /// <summary>
    /// Draws the translation engines tab in the UI.
    /// </summary>
    public void DrawTranslationEnginesTab()
    {
      using var scrollingChild =
                      ImRaii.Child("TranslatinEngineSettings", new Vector2(-1, -1), false, ImGuiWindowFlags.NoBackground);
      if (scrollingChild)
      {
        try
        {
          ImGui.Checkbox(Resources.TranslateTextsAgain, ref this.configuration.TranslateAlreadyTranslatedTexts);

          this.Engines = this.enginesList
      .Where((_, i) => langDict[languageInt].IsEngineSupported(i))
      .ToArray();

          if (ImGui.Combo(Resources.TranslationEngineChoose, ref chosenTransEngine, this.Engines, this.Engines.Length))
          {
            this.configuration.ChosenTransEngine = langDict[languageInt].SupportedEngines.IndexOf(chosenTransEngine);

            this.SaveConfigValue = true;
            PluginLog.Debug("Chosen translation engine: " + this.configuration.ChosenTransEngine + ", engine name: " + this.Engines[this.configuration.ChosenTransEngine]);

            this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
          }

          this.DrawTranslationEnginesTabContent();
        }
        catch (Exception ex)
        {
          PluginLog.Error(ex, "Could not draw TranslationEngine Settings content");
        }
      }
    }

    /// <summary>
    /// Draws the content of the translation engines tab.
    /// </summary>
    public void DrawTranslationEnginesTabContent()
    {
      ImGui.BeginGroup();
      switch (this.configuration.ChosenTransEngine)
      {
        case 0: // Google
          ImGui.TextWrapped(Resources.SettingsForGTransText);
          ImGui.TextWrapped(Resources.TranslationEngineSettingsNotRequired);
          break;
        case 1: // Deepl
          ImGui.TextWrapped(Resources.SettingsForDeepLTransText);
          ImGui.Spacing();

          var isDeeplTranslatorUsingApiKey = this.configuration.DeeplTranslatorUsingApiKey;
          if (ImGui.Checkbox(Resources.DeepLTransAPIKey, ref isDeeplTranslatorUsingApiKey))
          {
            this.configuration.DeeplTranslatorUsingApiKey = isDeeplTranslatorUsingApiKey;
            this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
          }

          if (this.configuration.DeeplTranslatorUsingApiKey)
          {
            if (ImGui.Button(Resources.DeepLTranslatorAPIKeyLink))
            {
              this.SaveConfigValue = true;
              Process.Start(new ProcessStartInfo
              {
                FileName = "https://www.deepl.com/pro-api",
                UseShellExecute = true,
              });
              this.config = false;
            }

            ImGui.Spacing();

            if (ImGui.InputText(Resources.DeeplTranslatorApiKey, ref this.configuration.DeeplTranslatorApiKey, 100))
            {
              this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
            }
          }

          break;
        case 2: // ChatGPT

          ImGui.TextWrapped(Resources.SettingsForChatGptTransText);
          ImGui.Spacing();

          if (ImGui.Button(Resources.ChatGPTAPIKeyLink))
          {
            this.SaveConfigValue = true;
            Process.Start(new ProcessStartInfo
            {
              FileName = "https://platform.openai.com/settings/profile?tab=api-keys",
              UseShellExecute = true,
            });
            this.config = false;
          }

          ImGui.Spacing();

          if (ImGui.InputText(Resources.ChatGptApiKey, ref this.configuration.ChatGptApiKey, 400))
          {
            this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
          }

          ImGui.Spacing();

          if (ImGui.InputText("Model endpoint", ref this.configuration.ChatGPTBaseUrl, 400))
          {
            this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
          }

          ImGui.Spacing();

          if (ImGui.InputText("LLM Model", ref this.configuration.OpenAILlmModel, 400))
          {
            this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
          }

          ImGui.Spacing();

          float temperature = this.configuration.ChatGptTemperature;
          if (ImGui.SliderFloat("Temperature", ref temperature, 0.1f, 1.0f, "%.1f"))
          {
            this.configuration.ChatGptTemperature = temperature;
          }

          // ✅ Shared prompt editor logic
          var promptType = GetPromptTypeForEngine(this.configuration.ChosenTransEngine);
          if (promptType.HasValue)
          {
            this.DrawPromptEditor(
              this.configuration,
              promptType.Value,
              DefaultPrompt, // <- using your global DefaultPrompt
              this.Engines[this.configuration.ChosenTransEngine] // just a display label
            );
          }

          ImGui.Spacing();
          ImGui.Separator();

          if (ImGui.Button("Apply"))
          {
            this.SaveConfigValue = true;
            this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);
          }

          break;

      }

      ImGui.EndGroup();
    }
  }
}
