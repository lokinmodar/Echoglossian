// <copyright file="OpenAIModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.OpenAI;

public static class OpenAIModelManager
{
  private static readonly HttpClient HttpClient = new();
  private static readonly object SyncLock = new();

  public static List<LlmTextModel> CurrentModelList { get; private set; } = OpenAITextModelDefaults.PredefinedModels;

  public static void ResetToDefault()
  {
    lock (SyncLock)
    {
      CurrentModelList = OpenAITextModelDefaults.PredefinedModels;
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
      var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

      var response = await HttpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
      {
        return;
      }

      string json = await response.Content.ReadAsStringAsync();
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

        // ✅ Filter out non-text models
        if (id.StartsWith("dall-e") || id.StartsWith("whisper") || id.StartsWith("tts") || id.Contains("embedding") || id.Contains("moderation"))
        {
          continue;
        }

        string displayName = id switch
        {
          "gpt-4" => "🧠 GPT-4",
          "gpt-4o" => "👁 GPT-4o",
          "gpt-3.5-turbo" => "⚡ GPT-3.5 Turbo",
          _ => $"🧩 {id}",
        };

        bool isMini = id.Contains("mini");
        bool isTurbo = id.Contains("turbo");
        bool supportsText = true;
        bool supportsVision = id.Contains("gpt-4o");

        models.Add(new LlmTextModel(
          Id: id,
          DisplayName: displayName,
          SupportsText: supportsText,
          SupportsVision: supportsVision,
          IsTurbo: isTurbo,
          IsMini: isMini,
          IsDefault: false,
          EngineName: "OpenAI"));
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
