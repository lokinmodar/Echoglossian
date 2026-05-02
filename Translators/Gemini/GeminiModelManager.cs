// <copyright file="GeminiModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.Gemini;

public static class GeminiModelManager
{
    private static readonly HttpClient HttpClient = new();
    private static readonly object SyncLock = new();

    public static List<LlmTextModel> CurrentModelList { get; private set; } =
        GeminiTextModelDefaults.PredefinedModels;

    public static void ResetToDefault()
    {
        lock (SyncLock)
        {
            CurrentModelList = GeminiTextModelDefaults.PredefinedModels;
        }
    }

    public static async Task RefreshAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        try
        {
            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JObject.Parse(json);
            var modelsToken = root["models"];

            if (modelsToken is not JArray modelsArray)
            {
                return;
            }

            var models = new List<LlmTextModel>();

            foreach (var item in modelsArray)
            {
                var id = item["name"]?.ToString()?.Split('/').Last();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                // ✅ Optional filter: Skip if not text-capable
                var supportedInterfaces =
                    item["supportedGenerationMethods"] as JArray;
                if (supportedInterfaces == null ||
                    !supportedInterfaces.Any(m =>
                        m?.ToString() == "generateContent"))
                {
                    continue;
                }

                var displayName = id switch
                {
                    "gemini-pro" => "🔷 Gemini Pro",
                    "gemini-1.5-pro" => "🟢 Gemini 1.5 Pro",
                    "gemini-1.5-flash" => "⚡ Gemini 1.5 Flash",
                    _ => $"🧩 {id}",
                };

                var isMini = id.Contains("flash");
                var isTurbo = id.Contains("flash") || id.Contains("pro");
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
                        "Gemini"));
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
            ResetToDefault(); // fallback on error
        }
    }
}