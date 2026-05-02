// <copyright file="DeepSeekModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.DeepSeek;

public static class DeepSeekModelManager
{
    private static readonly HttpClient HttpClient = new();
    private static readonly object SyncLock = new();

    public static List<LlmTextModel> CurrentModelList { get; private set; } =
        DeepSeekTextModelDefaults.PredefinedModels;

    public static void ResetToDefault()
    {
        lock (SyncLock)
        {
            CurrentModelList = DeepSeekTextModelDefaults.PredefinedModels;
        }
    }

    public static async Task RefreshAsync(string apiKey, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl.TrimEnd('/')}/v1/models");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JObject.Parse(json);
            var data = root["data"] as JArray;
            if (data == null)
            {
                return;
            }

            var models = new List<LlmTextModel>();

            foreach (var item in data)
            {
                var id = item["id"]?.ToString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                // ✅ Filter out non-text models (if ever DeepSeek adds vision/audio)
                if (id.Contains("embedding") || id.Contains("vision") ||
                    id.Contains("tts"))
                {
                    continue;
                }

                var displayName = id switch
                {
                    "deepseek-chat" => "💬 DeepSeek Chat",
                    "deepseek-reasoner" => "🧠 DeepSeek Reasoner",
                    _ => $"🧩 {id}",
                };

                var isMini = id.Contains("mini");
                var isTurbo = id.Contains("turbo") || id.Contains("flash");
                var supportsText = true;
                var supportsVision = false;

                models.Add(
                    new LlmTextModel(
                        id,
                        displayName,
                        supportsText,
                        supportsVision,
                        isTurbo,
                        isMini,
                        false,
                        "DeepSeek"));
            }

            lock (SyncLock)
            {
                if (models.Count > 0)
                {
                    CurrentModelList = models;
                }
            }
        }
        catch
        {
            ResetToDefault();
        }
    }
}