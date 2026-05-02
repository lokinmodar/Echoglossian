// <copyright file="ClaudeModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.Claude;

/// <summary>
///     Manages the optional live Claude model catalog used by the engine configuration UI.
/// </summary>
public static class ClaudeModelManager
{
    private const string AnthropicVersion = "2023-06-01";
    private static readonly HttpClient HttpClient = new();
    private static readonly object SyncLock = new();

    /// <summary>
    ///     Gets the current Claude model list used by the UI.
    /// </summary>
    public static List<LlmTextModel> CurrentModelList { get; private set; } =
        ClaudeTextModelDefaults.PredefinedModels;

    /// <summary>
    ///     Restores the committed fallback model list.
    /// </summary>
    public static void ResetToDefault()
    {
        lock (SyncLock)
        {
            CurrentModelList = ClaudeTextModelDefaults.PredefinedModels;
        }
    }

    /// <summary>
    ///     Refreshes the Claude model list from Anthropic's official models endpoint.
    /// </summary>
    /// <param name="apiKey">The Anthropic API key.</param>
    /// <param name="baseUrl">The Anthropic API base URL.</param>
    /// <returns>A task that completes when refresh finishes.</returns>
    public static async Task RefreshAsync(string apiKey, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                $"{baseUrl.TrimEnd('/')}/v1/models");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            JObject root = JObject.Parse(json);
            JArray? data = root["data"] as JArray;
            if (data is null)
            {
                return;
            }

            List<LlmTextModel> models = new();
            foreach (JToken item in data)
            {
                string? id = item["id"]?.ToString();
                if (string.IsNullOrWhiteSpace(id) ||
                    !id.StartsWith("claude-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                models.Add(new LlmTextModel(
                    id,
                    GetDisplayName(id),
                    true,
                    false,
                    id.Contains("haiku", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("sonnet", StringComparison.OrdinalIgnoreCase),
                    id.Contains("haiku", StringComparison.OrdinalIgnoreCase),
                    false,
                    "Claude"));
            }

            if (models.Count == 0)
            {
                return;
            }

            List<LlmTextModel> ordered = models
                .DistinctBy(static model => model.Id)
                .OrderBy(static model => model.Id, StringComparer.Ordinal)
                .ToList();

            int defaultIndex = ordered.FindIndex(static model =>
                string.Equals(model.Id, "claude-sonnet-4-20250514", StringComparison.Ordinal));
            if (defaultIndex < 0)
            {
                defaultIndex = ordered.FindIndex(static model =>
                    model.Id.Contains("sonnet", StringComparison.OrdinalIgnoreCase));
            }

            if (defaultIndex < 0)
            {
                defaultIndex = 0;
            }

            LlmTextModel defaultModel = ordered[defaultIndex];
            ordered[defaultIndex] = defaultModel with { IsDefault = true };

            lock (SyncLock)
            {
                CurrentModelList = ordered;
            }
        }
        catch
        {
            ResetToDefault();
        }
    }

    private static string GetDisplayName(string id)
    {
        if (id.Contains("haiku", StringComparison.OrdinalIgnoreCase))
        {
            return $"⚡ {id}";
        }

        if (id.Contains("sonnet", StringComparison.OrdinalIgnoreCase))
        {
            return $"🟢 {id}";
        }

        if (id.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            return $"🧠 {id}";
        }

        return $"🧩 {id}";
    }
}
