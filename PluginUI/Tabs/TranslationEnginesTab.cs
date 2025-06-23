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
  /// <summary>
  /// Draws the translation engine settings UI, allowing users to select and configure translation engines.
  /// </summary>
  /// <param name="config">The configuration object containing translation settings.</param>
  /// <param name="languageIndex">The index of the selected language in the language list.</param>
  /// <param name="languageList">The list of available languages.</param>
  /// <param name="availableEngines">The list of available translation engines.</param>
  /// <param name="langDict">Dictionary mapping language indices to their information, including supported engines.</param>
  /// <param name="rebuildTranslationService">The action to rebuild the translation service when settings change.</param>
  /// <returns>True if any settings were changed; otherwise, false.</returns>
  public static bool Draw(
  Config config,
  int languageIndex,
  List<string> languageList,
  List<string> availableEngines,
  Dictionary<int, LanguageInfo> langDict,
  Action rebuildTranslationService)
  {
    bool changed = false;

    using var scrollingChild = ImRaii.Child("TranslatinEngineSettings", new Vector2(-1, -1), false, ImGuiWindowFlags.NoBackground);

    if (!scrollingChild)
    {
      return false;
    }

    ImGui.Checkbox(Resources.TranslateTextsAgain, ref config.TranslateAlreadyTranslatedTexts);

    var supportedEngines = langDict.TryGetValue(languageIndex, out var langInfo)
      ? langInfo.SupportedEngines
      : new List<int>();

    var filteredEngines = availableEngines
      .Where((_, i) => supportedEngines.Contains(i))
      .ToArray();

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
