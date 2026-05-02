// <copyright file="PluginUITranslationEnginesTab.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.PluginUI.Tabs;

/// <summary>
///     Renders the Translation Engines tab, supporting engine selection and
///     per-engine configuration.
/// </summary>
public static class TranslationEnginesTab
{
    /// <summary>
    ///     Draws the translation engine settings UI, allowing users to select and
    ///     configure translation engines.
    /// </summary>
    /// <param name="config">The configuration object containing translation settings.</param>
    /// <param name="languageIndex">
    ///     The index of the selected language in the language
    ///     list.
    /// </param>
    /// <param name="languageList">The list of available languages.</param>
    /// <param name="availableEngines">The list of available translation engines.</param>
    /// <param name="langDict">
    ///     Dictionary mapping language indices to their
    ///     information, including supported engines.
    /// </param>
    /// <param name="rebuildTranslationService">
    ///     The action to rebuild the translation
    ///     service when settings change.
    /// </param>
    /// <returns>True if any settings were changed; otherwise, false.</returns>
    public static bool Draw(
        Config config,
        int languageIndex,
        List<string> languageList,
        List<string> availableEngines,
        Dictionary<int, LanguageInfo> langDict,
        Action rebuildTranslationService)
    {
        var changed = false;
        var promptManager = new PromptTemplateManager(config);

        using var scrollingChild = ImRaii.Child(
            "TranslationEngineSettings",
            new Vector2(-1, -100),
            false,
            ImGuiWindowFlags.NoBackground);

        if (!scrollingChild)
        {
            return false;
        }

        ImGui.Checkbox(
            Resources.TranslateTextsAgain,
            ref config.TranslateAlreadyTranslatedTexts);

        var supportedEngines =
            langDict.TryGetValue(languageIndex, out var langInfo)
                ? langInfo.SupportedEngines ?? new List<int>()
                : new List<int>();

        var filteredEngines = availableEngines
            .Where((_, i) => supportedEngines.Contains(i)).ToArray();

        var selected = supportedEngines.IndexOf(config.ChosenTransEngine);
        if (selected < 0 && supportedEngines.Count > 0)
        {
            selected = 0;
        }

        if (ImGui.Combo(
                Resources.TranslationEngineChoose,
                ref selected,
                filteredEngines,
                filteredEngines.Length))
        {
            config.ChosenTransEngine = supportedEngines[selected];
            rebuildTranslationService();
            changed = true;
        }

        ImGui.Separator();
        ImGui.BeginGroup();

        var engine = (Echoglossian.TransEngines)config.ChosenTransEngine;

        switch (engine)
        {
            case Echoglossian.TransEngines.Google:
                changed |= GoogleEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.Deepl:
                changed |= DeepLEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.ChatGPT:
                changed |= ChatGPTEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.YandexCloud:
                changed |= YandexCloudEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.GTranslate:
                changed |= GTranslateEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.DeepSeek:
                changed |= DeepSeekEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Ollama:
                try
                {
                    changed |= OllamaEngineUI.Draw(config, promptManager);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(
                        $"OllamaEngineUI failed: {ex.Message}, {ex.StackTrace}");
                    ImGui.TextColored(
                        new Vector4(1f, 0.4f, 0.4f, 1f),
                        "Ollama engine UI failed to render.");
                }

                break;
            case Echoglossian.TransEngines.LibreTranslate:
                changed |= LibreTranslateEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Microsoft:
                changed |= MicrosoftEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Amazon:
                changed |= AmazonEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.Gemini:
                changed |= GeminiEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.YandexPublic:
                changed |= YandexPublicEngineUI.Draw(config);
                break;
            case Echoglossian.TransEngines.OpenRouter:
                changed |= OpenRouterEngineUI.Draw(config, promptManager);
                break;
            case Echoglossian.TransEngines.LmStudio:
                changed |= LmStudioEngineUI.Draw(config, promptManager);
                break;
            default:
                ImGui.Text(Resources.NoSettingsForEngine);
                break;
        }

        ImGui.EndGroup();

        return changed;
    }
}
