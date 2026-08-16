// <copyright file="LmStudioModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;
using Echoglossian.Translators.Capabilities;

namespace Echoglossian.Translators.LmStudio;

/// <summary>
///     Manages dynamic model fetching for LM Studio.
/// </summary>
public static class LmStudioModelManager
{
    private static readonly HttpClient client = new();

    /// <summary>
    ///     Gets the current list of LM Studio models.
    /// </summary>
    public static List<LlmTextModel> CurrentModelList { get; private set; } =
        LmStudioTextModelDefaults.PredefinedModels;

    /// <summary>
    ///     Restores the committed fallback model list.
    /// </summary>
    public static void ResetToDefault()
    {
        CurrentModelList = LmStudioTextModelDefaults.PredefinedModels;
    }

    /// <summary>
    ///     Refreshes the LM Studio model list from the live API.
    /// </summary>
    /// <param name="baseUrl">Base API URL.</param>
    /// <param name="apiKey">Optional API key.</param>
    /// <param name="cancellationToken">The token that cancels live discovery.</param>
    /// <returns>Awaitable task.</returns>
    public static async Task RefreshAsync(
        string baseUrl,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl.TrimEnd('/')}/models");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JObject.Parse(json);
            var data = parsed["data"]?.ToObject<List<JObject>>() ?? [];

            var models = new List<LlmTextModel>();
            foreach (var item in data)
            {
                var idToken = item["id"];
                if (idToken == null)
                {
                    continue;
                }

                var id = idToken.ToString();

                if (!id.Contains("vision")) // skip vision models
                {
                    models.Add(
                        new LlmTextModel(
                            id,
                            $"🧠 {id}",
                            true,
                            false,
                            false,
                            false,
                            id.ToLower().Contains("llama3"),
                            "LmStudio"));
                }
            }

            if (models.Any())
            {
                cancellationToken.ThrowIfCancellationRequested();
                CurrentModelList = models;
                LlmCapabilityRefreshPromoter.PromoteDiscoveredModels(
                    Echoglossian.TransEngines.LmStudio,
                    "LmStudio",
                    baseUrl,
                    models.Select(static model => model.Id).ToArray(),
                    DateTime.UtcNow);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Warning(
                $"[LmStudioModelManager] Failed to fetch models: {ex.Message}");
            ResetToDefault();
        }
    }
}
