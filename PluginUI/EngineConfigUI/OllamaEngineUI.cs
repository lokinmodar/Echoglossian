// <copyright file="OllamaEngineUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.PluginUI.Components;
using Echoglossian.Translators.Ollama;

namespace Echoglossian.PluginUI.EngineConfigUI;

public static class OllamaEngineUI
{
    public static bool Draw(Config config, PromptTemplateManager promptManager)
    {
        var changed = false;

        ImGui.TextWrapped(Resources.SettingsForOllamaText);
        ImGui.Spacing();

        bool isEndpointInvalid;
        changed |= FieldValidationHelper.ValidatedInputText(
            Resources.APIEndpoint,
            ref config.OllamaUrl,
            300,
            out isEndpointInvalid);

        var previous = config.UseLiveOllamaModelList;
        if (ImGui.Checkbox(
                Resources.FetchLiveModels,
                ref config.UseLiveOllamaModelList))
        {
            changed = true;
            if (config.UseLiveOllamaModelList && !previous)
            {
                _ = Task.Run(() =>
                    OllamaModelManager.RefreshAsync(config.OllamaUrl ?? string.Empty));
            }
            else if (!config.UseLiveOllamaModelList)
            {
                OllamaModelManager.ResetToDefault();
            }
        }

        if (config.UseLiveOllamaModelList &&
            OllamaModelManager.CurrentModelList.Count == 0)
        {
            ImGui.TextColored(
                new Vector4(1f, 0.4f, 0.4f, 1f),
                Resources.CouldNotFetchOllamaModels);
        }

        var models = config.UseLiveOllamaModelList
            ? OllamaModelManager.CurrentModelList
            : OllamaTextModelDefaults.PredefinedModels;

        changed |= ModelDropdownUI.Draw(
            Resources.Model,
            ref config.OllamaModel,
            models,
            "Ollama",
            OllamaModelManager.GetTooltips());

        var temp = config.OllamaTemperature;
        if (ImGui.SliderFloat(Resources.Temperature, ref temp, 0.1f, 1.0f, "%.1f"))
        {
            config.OllamaTemperature = temp;
            changed = true;
        }

        PromptEditorUI.Draw(
            promptManager,
            Echoglossian.PromptType.Ollama,
            PromptTemplateManager.DefaultPrompt,
            Echoglossian.TransEngines.Ollama.ToString());

        if (changed)
        {
            FieldValidationHelper.MarkAllRequiredFieldsTouched(config);
            Echoglossian.SaveConfig(config);
        }

        return changed;
    }
}
