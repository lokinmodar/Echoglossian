// <copyright file="DeepSeekModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;
using Echoglossian.Translators.Capabilities;

namespace Echoglossian.Translators.DeepSeek;

/// <summary>
///     Manages the DeepSeek live model list used by the configuration UI.
/// </summary>
public static class DeepSeekModelManager
{
    private static readonly HttpClient HttpClient = new();
    private static readonly object SyncLock = new();

    public static List<LlmTextModel> CurrentModelList { get; private set; } =
        DeepSeekTextModelDefaults.PredefinedModels;

    /// <summary>
    ///     Restores the predefined DeepSeek model catalog.
    /// </summary>
    public static void ResetToDefault()
    {
        lock (SyncLock)
        {
            CurrentModelList = DeepSeekTextModelDefaults.PredefinedModels;
        }
    }

    /// <summary>
    ///     Refreshes the DeepSeek model list from the configured endpoint.
    /// </summary>
    /// <param name="apiKey">The configured DeepSeek API key.</param>
    /// <param name="baseUrl">The configured DeepSeek base URL.</param>
    public static async Task RefreshAsync(string apiKey, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(baseUrl))
        {
            ResetToDefault();
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                BuildModelsEndpoint(baseUrl));
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                ResetToDefault();
                return;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var root = JObject.Parse(json);
            var data = root["data"] as JArray;
            if (data == null)
            {
                ResetToDefault();
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

            if (models.Count > 0)
            {
                ApplyRefreshSuccess(baseUrl, models, DateTime.UtcNow);
                return;
            }

            ResetToDefault();
        }
        catch
        {
            ResetToDefault();
        }
    }

    /// <summary>
    ///     Builds the DeepSeek models endpoint from the configured base URL.
    /// </summary>
    /// <param name="baseUrl">The configured DeepSeek base URL.</param>
    /// <returns>The normalized DeepSeek models endpoint.</returns>
    internal static string BuildModelsEndpoint(string baseUrl)
    {
        return $"{baseUrl.Trim().TrimEnd('/')}/models";
    }

    /// <summary>
    ///     Applies a successful DeepSeek model discovery result and promotes
    ///     any known capability overlays without risking the discovered list.
    /// </summary>
    /// <param name="baseUrl">The configured DeepSeek base URL.</param>
    /// <param name="models">The discovered supported model list.</param>
    /// <param name="observedAtUtc">The UTC discovery observation time.</param>
    internal static void ApplyRefreshSuccess(
        string baseUrl,
        IReadOnlyList<LlmTextModel> models,
        DateTime observedAtUtc)
    {
        lock (SyncLock)
        {
            CurrentModelList = models.ToList();
        }

        LlmCapabilityRefreshPromoter.PromoteDiscoveredModels(
            Echoglossian.TransEngines.DeepSeek,
            "DeepSeek",
            baseUrl,
            models.Select(static model => model.Id).ToArray(),
            observedAtUtc);
    }
}
