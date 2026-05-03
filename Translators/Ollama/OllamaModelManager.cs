// <copyright file="OllamaModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.Ollama;

public static class OllamaModelManager
{
    private static readonly List<LlmTextModel> FallbackModels =
        OllamaTextModelDefaults.PredefinedModels;

    private static readonly List<LlmTextModel> CurrentModels = new();
    private static Dictionary<string, string>? tooltips;

    public static IReadOnlyList<LlmTextModel> CurrentModelList =>
        CurrentModels.Count > 0 ? CurrentModels : FallbackModels;

    public static async Task RefreshAsync(string baseUrl)
    {
        try
        {
            using var client = new HttpClient();
            var url = baseUrl.TrimEnd('/') + "/api/tags";
            var response = await client.GetStringAsync(url);
            var root = JObject.Parse(response);
            var tags = root["models"]?.Select(m => m["name"]?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct()
                .ToList();

            CurrentModels.Clear();
            tooltips = new Dictionary<string, string>();

            if (tags != null)
            {
                foreach (var name in tags)
                {
                    var tier = name?.Split(':')[0] ?? "unknown";
                    var model = new LlmTextModel(
                        name!,
                        $"🦙 {name}",
                        true,
                        false,
                        false,
                        false,
                        name == "llama3",
                        "Ollama",
                        tier);

                    CurrentModels.Add(model);
                    tooltips[name!] = $"🦙 Ollama model: {tier}";
                }
            }
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Warning(
                $"[OllamaModelManager] Failed to fetch model list from Ollama: {ex.Message}");
            ResetToDefault();
        }
    }

    public static void ResetToDefault()
    {
        CurrentModels.Clear();
        tooltips = null;
    }

    public static Dictionary<string, string>? GetTooltips()
    {
        return tooltips;
    }
}