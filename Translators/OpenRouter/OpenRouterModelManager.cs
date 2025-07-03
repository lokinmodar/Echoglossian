// <copyright file="OpenRouterModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.OpenAI;

namespace Echoglossian.Translators.OpenRouter
{
  public static class OpenRouterModelManager
  {
    private static readonly HttpClient HttpClient = new();
    private static readonly object SyncLock = new();

    public static List<OpenAITextModel> CurrentModelList { get; private set; } = OpenRouterTextModelDefaults.PredefinedModels;

    public static void ResetToDefault()
    {
      lock (SyncLock)
      {
        CurrentModelList = OpenRouterTextModelDefaults.PredefinedModels;
      }
    }

    public static async Task RefreshAsync(string apiKey, string baseUrl)
    {
      if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
      {
        return;
      }

      try
      {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
          return;
        }

        string json = await response.Content.ReadAsStringAsync();
        var root = JObject.Parse(json);
        var modelsArray = root["data"] as JArray;
        if (modelsArray == null)
        {
          return;
        }

        var models = new List<OpenAITextModel>();

        foreach (var item in modelsArray)
        {
          var id = item["id"]?.ToString();
          if (string.IsNullOrWhiteSpace(id))
          {
            continue;
          }

          // Filter out non-text models
          if (id.Contains("dall-e") || id.Contains("tts") || id.Contains("embed") || id.Contains("whisper"))
          {
            continue;
          }

          var displayName = $"🛰 {id}";
          bool isTurbo = id.Contains("turbo") || id.Contains("flash");
          bool isMini = id.Contains("mini");
          bool supportsText = true;

          models.Add(new OpenAITextModel(
            Id: id,
            DisplayName: displayName,
            SupportsText: supportsText,
            SupportsVision: false,
            IsTurbo: isTurbo,
            IsMini: isMini,
            IsDefault: false,
            EngineName: "OpenRouter"));
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
}
