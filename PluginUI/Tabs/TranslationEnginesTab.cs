// <copyright file="PluginUITranslationEnginesTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>


namespace Echoglossian.PluginUI.Tabs;

/// <summary>
/// Renders the Translation Engines tab, supporting engine selection and per-engine configuration.
/// </summary>
public static class TranslationEnginesTab
{
  public static bool Draw(Config config, int languageIndex, List<string> languageList, List<string> availableEngines, Action rebuildTranslationService)
  {
    bool changed = false;

    using var scrollingChild = ImRaii.Child("TranslatinEngineSettings", new Vector2(-1, -1), false, ImGuiWindowFlags.NoBackground);
    if (!scrollingChild)
    {
      return false;
    }

    ImGui.Checkbox(Resources.TranslateTextsAgain, ref config.TranslateAlreadyTranslatedTexts);

    var supportedEngines = config.GetLanguageInfo(languageIndex)?.SupportedEngines ?? new();
    var filteredEngines = availableEngines.Where((_, i) => supportedEngines.Contains(i)).ToArray();
    var selected = supportedEngines.IndexOf(config.ChosenTransEngine);

    if (ImGui.Combo(Resources.TranslationEngineChoose, ref selected, filteredEngines, filteredEngines.Length))
    {
      config.ChosenTransEngine = supportedEngines[selected];
      rebuildTranslationService();
      changed = true;
    }

    ImGui.Separator();
    ImGui.BeginGroup();

    var engine = (TransEngines)config.ChosenTransEngine;
    switch (engine)
    {
      case TransEngines.Google:
        changed |= GoogleEngineUI.Draw(config);
        break;
      case TransEngines.Deepl:
        changed |= DeepLEngineUI.Draw(config);
        break;
      case TransEngines.ChatGPT:
        changed |= ChatGPTEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.YandexCloud:
        changed |= YandexCloudEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.GTranslate:
        changed |= GTranslateEngineUI.Draw(config);
        break;
      case TransEngines.DeepSeek:
        changed |= DeepSeekEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.OpenLlama:
        changed |= OpenLlamaEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.LibreTranslate:
        changed |= LibreTranslateEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.Microsoft:
        changed |= MicrosoftEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.Amazon:
        changed |= AmazonEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.Gemini:
        changed |= GeminiEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      case TransEngines.YandexPublic:
        changed |= YandexPublicEngineUI.Draw(config);
        break;
      case TransEngines.OpenRouter:
        changed |= OpenRouterEngineUI.Draw(config, new PromptTemplateManager(config));
        break;
      default:
        ImGui.Text(Resources.NoSettingsForEngine);
        break;
    }

    ImGui.EndGroup();
    return changed;
  }
}
