// <copyright file="ModelDropdownUI.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.PluginUI.Components;

public static class ModelDropdownUI
{
    public static bool Draw(
        string label,
        ref string modelId,
        IReadOnlyList<LlmTextModel> models,
        string engine,
        Dictionary<string, string>? tooltips = null,
        Func<LlmTextModel, int>? sortOverride = null)
    {
        LlmTextModel? selected;
        return DrawInternal(
            label,
            models,
            engine,
            tooltips,
            sortOverride,
            ref modelId,
            out selected);
    }

    public static bool Draw(
        string label,
        IReadOnlyList<LlmTextModel> models,
        string engine,
        out LlmTextModel? selectedModel,
        Dictionary<string, string>? tooltips = null,
        Func<LlmTextModel, int>? sortOverride = null,
        string? initialModelId = null)
    {
        var selectedId = initialModelId ??
                         models.FirstOrDefault()?.Id ?? string.Empty;
        return DrawInternal(
            label,
            models,
            engine,
            tooltips,
            sortOverride,
            ref selectedId,
            out selectedModel);
    }

    private static bool DrawInternal(
        string label,
        IReadOnlyList<LlmTextModel> models,
        string engine,
        Dictionary<string, string>? tooltips,
        Func<LlmTextModel, int>? sortOverride,
        ref string modelId,
        out LlmTextModel? selectedModel)
    {
        selectedModel = null;

        if (models == null || models.Count == 0)
        {
            ImGui.TextColored(
                new Vector4(1f, 0.6f, 0.6f, 1f),
                "No models available.");
            return false;
        }

        var sorter = sortOverride ?? BuildTooltipSort(tooltips);
        var sortedModels = sorter != null
            ? models.OrderBy(sorter).ToList()
            : models.ToList();

        var selectedModelId = modelId;
        var currentIndex = sortedModels.FindIndex(m => m.Id == selectedModelId);
        if (currentIndex == -1)
        {
            currentIndex = 0;
        }

        string LabelFor(LlmTextModel model)
        {
            var tag = model.IsDefault ? " [default]" : string.Empty;
            return $"{model.DisplayName}{tag}";
        }

        string GetEngineTierGroup(LlmTextModel model)
        {
            var tier = model.Id switch
            {
                var id when id.StartsWith("gpt-4o") => "GPT-4o",
                var id when id.StartsWith("gpt-4") => "GPT-4",
                var id when id.StartsWith("gpt-3.5") => "GPT-3.5",
                var id when id.StartsWith("gemini-1.5") => "Gemini 1.5",
                var id when id.StartsWith("gemini-pro") => "Gemini Pro",
                var id when id.StartsWith("deepseek-chat") => "Chat",
                var id when id.StartsWith("deepseek-reasoner") => "Reasoner",
                var id when id.StartsWith("claude-opus") => "Opus",
                var id when id.StartsWith("claude-sonnet") => "Sonnet",
                var id when id.StartsWith("claude-3-7-sonnet") => "Sonnet",
                var id when id.StartsWith("claude-3-5-haiku") => "Haiku",
                var id when id.StartsWith("o1-") => "O1",
                _ => "Other",
            };

            return $"{model.EngineName} / {tier}";
        }

        Vector4 GetColor(string engineName)
        {
            return engineName.ToLowerInvariant() switch
            {
                "openai" => new Vector4(0.5f, 0.75f, 1f, 1f),
                "gemini" => new Vector4(0.85f, 0.6f, 1f, 1f),
                "deepseek" => new Vector4(0.6f, 1f, 0.6f, 1f),
                "ollama" => new Vector4(1f, 1f, 0.6f, 1f),
                "claude" => new Vector4(1f, 0.78f, 0.55f, 1f),
                _ => new Vector4(1f, 1f, 1f, 1f),
            };
        }

        var currentLabel = LabelFor(sortedModels[currentIndex]);
        var changed = false;

        if (ImGui.BeginCombo(label, currentLabel))
        {
            try
            {
                string? lastGroup = null;

                for (var i = 0; i < sortedModels.Count; i++)
                {
                    var model = sortedModels[i];
                    var group = GetEngineTierGroup(model);

                    if (group != lastGroup)
                    {
                        ImGui.Separator();
                        ImGui.TextDisabled(group);
                        lastGroup = group;
                    }

                    var isSelected = i == currentIndex;
                    var labelText = LabelFor(model);

                    ImGui.PushStyleColor(
                        ImGuiCol.Text,
                        GetColor(model.EngineName));

                    if (ImGui.Selectable(labelText, isSelected))
                    {
                        modelId = model.Id;
                        selectedModel = model;
                        changed = true;
                    }

                    if (ImGui.IsItemHovered() &&
                        tooltips?.TryGetValue(model.Id, out var tip) == true)
                    {
                        ImGui.SetTooltip(tip);
                    }

                    ImGui.PopStyleColor();

                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
            }
            finally
            {
                ImGui.EndCombo(); // ✅ Always close!
            }
        }

        ImGui.TextColored(
            new Vector4(1f, 1f, 0.6f, 1f),
            $"Model ID: {modelId}");
        return changed;
    }

    private static Func<LlmTextModel, int>? BuildTooltipSort(
        Dictionary<string, string>? tooltips)
    {
        if (tooltips == null)
        {
            return null;
        }

        return model =>
        {
            if (!tooltips.TryGetValue(model.Id, out var tip))
            {
                return 999;
            }

            if (tip.Contains("Fast"))
            {
                return 0;
            }

            if (tip.Contains("cheap") || tip.Contains("Budget"))
            {
                return 1;
            }

            if (tip.Contains("standard"))
            {
                return 2;
            }

            if (tip.Contains("accurate") || tip.Contains("long"))
            {
                return 3;
            }

            return 999;
        };
    }
}
