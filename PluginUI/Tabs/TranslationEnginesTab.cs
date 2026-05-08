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
        Dictionary<int, LanguageInfo> langDict,
        Action rebuildTranslationService)
    {
        var changed = false;
        var promptManager = new PromptTemplateManager(config);

        changed |= ImGui.Checkbox(
            Resources.TranslateTextsAgain,
            ref config.TranslateAlreadyTranslatedTexts);

        var supportedEngines =
            langDict.TryGetValue(languageIndex, out var langInfo)
                ? langInfo.SupportedEngines ?? new List<int>()
                : new List<int>();

        var engineOptions = supportedEngines
            .Where(TranslationEngineSelectionMigrationHelper.IsConcreteEngineId)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => new TranslationEngineOption(
                id,
                GetDisplayName((Echoglossian.TransEngines)id)))
            .ToArray();

        if (engineOptions.Length == 0)
        {
            ImGui.Text(Resources.NoSettingsForEngine);
            return changed;
        }

        if (TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
                config,
                config.Version,
                supportedEngines))
        {
            rebuildTranslationService();
            changed = true;
        }

        var selected = Array.FindIndex(
            engineOptions,
            option => option.EngineId == config.ChosenTransEngine);
        if (selected < 0 && engineOptions.Length > 0)
        {
            selected = 0;
        }

        if (ImGui.Combo(
                Resources.TranslationEngineChoose,
                ref selected,
                engineOptions.Select(option => option.Label).ToArray(),
                engineOptions.Length))
        {
            TranslationEngineSelectionMigrationHelper.ApplyExplicitSelection(
                config,
                engineOptions[selected].EngineId);
            TranslationEngineSelectionMigrationHelper.NormalizeAndSyncSelection(
                config,
                config.Version,
                supportedEngines);
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
                    PluginRuntimeLog.Error(
                        $"OllamaEngineUI failed: {ex.Message}, {ex.StackTrace}");
                    ImGui.TextColored(
                        new Vector4(1f, 0.4f, 0.4f, 1f),
                        Resources.ResourceManager.GetString("OllamaEngineUiFailedToRender", Resources.Culture) ??
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
            case Echoglossian.TransEngines.Claude:
                changed |= ClaudeEngineUI.Draw(config, promptManager);
                break;
            default:
                ImGui.Text(Resources.NoSettingsForEngine);
                break;
        }

        ImGui.EndGroup();

        return changed;
    }

    /// <summary>
    ///     Resolves the user-facing display name for one concrete translation
    ///     engine without depending on list ordering.
    /// </summary>
    /// <param name="engine">The concrete engine enum value.</param>
    /// <returns>The display label shown in the combo.</returns>
    private static string GetDisplayName(Echoglossian.TransEngines engine)
    {
        return engine switch
        {
            Echoglossian.TransEngines.Google => "Google",
            Echoglossian.TransEngines.Deepl => "DeepL",
            Echoglossian.TransEngines.ChatGPT => "ChatGPT",
            Echoglossian.TransEngines.YandexCloud => "YandexCloud",
            Echoglossian.TransEngines.GTranslate => "GTranslate",
            Echoglossian.TransEngines.DeepSeek => "DeepSeek",
            Echoglossian.TransEngines.Ollama => "Ollama",
            Echoglossian.TransEngines.LibreTranslate => "LibreTranslate",
            Echoglossian.TransEngines.Microsoft => "Microsoft",
            Echoglossian.TransEngines.Amazon => "Amazon",
            Echoglossian.TransEngines.Gemini => "Gemini",
            Echoglossian.TransEngines.YandexPublic => "YandexPublic",
            Echoglossian.TransEngines.OpenRouter => "OpenRouter",
            Echoglossian.TransEngines.LmStudio => "LmStudio",
            Echoglossian.TransEngines.Claude => "Claude",
            _ => engine.ToString(),
        };
    }

    /// <summary>
    ///     Represents one user-facing translation engine choice in the settings
    ///     combo.
    /// </summary>
    /// <param name="EngineId">The concrete engine id persisted in config.</param>
    /// <param name="Label">The user-facing label.</param>
    private sealed record TranslationEngineOption(int EngineId, string Label);
}
