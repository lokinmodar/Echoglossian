// <copyright file="LmStudioModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

using Newtonsoft.Json.Linq;

namespace Echoglossian.Translators.LmStudio;

/// <summary>
/// Manages dynamic model fetching for LM Studio.
/// </summary>
public static class LmStudioModelManager
{
  private static readonly HttpClient client = new();
  private static List<OpenAITextModel> modelList = LmStudioTextModelDefaults.PredefinedModels;

  /// <summary>
  /// Gets the current list of LM Studio models.
  /// </summary>
  public static List<OpenAITextModel> CurrentModelList => modelList;

  /// <summary>
  /// Refreshes the LM Studio model list from the live API.
  /// </summary>
  /// <param name="baseUrl">Base API URL.</param>
  /// <param name="apiKey">Optional API key.</param>
  /// <returns>Awaitable task.</returns>
  public static async Task RefreshAsync(string baseUrl, string? apiKey = null)
  {
    try
    {
      var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/models");
      if (!string.IsNullOrWhiteSpace(apiKey))
      {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
      }

      var response = await client.SendAsync(request);
      response.EnsureSuccessStatusCode();

      string json = await response.Content.ReadAsStringAsync();
      var parsed = JObject.Parse(json);
      var data = parsed["data"]?.ToObject<List<JObject>>() ?? [];

      var models = new List<OpenAITextModel>();
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
          models.Add(new OpenAITextModel(
            Id: id,
            DisplayName: $"🧠 {id}",
            SupportsText: true,
            SupportsVision: false,
            IsTurbo: false,
            IsMini: false,
            IsDefault: id.ToLower().Contains("llama3"),
            EngineName: "LmStudio"
          ));
        }
      }

      if (models.Any())
      {
        modelList = models;
      }
    }
    catch (Exception ex)
    {
      PluginLog.Warning($"[LmStudioModelManager] Failed to fetch models: {ex.Message}");
      modelList = LmStudioTextModelDefaults.PredefinedModels;
    }
  }
}
