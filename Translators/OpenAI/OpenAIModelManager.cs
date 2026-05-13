// <copyright file="OpenAIModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators.OpenAI;

public static class OpenAIModelManager
{
  private const string DefaultOpenAiModelsBaseUrl = "https://api.openai.com/v1";
  private static readonly HttpClient HttpClient = new();
  private static readonly object SyncLock = new();
  private static string? lastRefreshFailureDetail;
  private static DateTime? lastRefreshObservedAtUtc;
  private static string? lastRefreshProviderName;
  private static bool? lastRefreshSucceeded;
  private static string? lastRefreshUrl;

  public static List<LlmTextModel> CurrentModelList { get; private set; } = OpenAITextModelDefaults.PredefinedModels;

  /// <summary>
  ///     Describes the last observed live model refresh state for the
  ///     OpenAI-family model manager.
  /// </summary>
  /// <param name="LastRefreshObservedAtUtc">
  ///     The timestamp of the last refresh attempt.
  /// </param>
  /// <param name="LastRefreshSucceeded">
  ///     Whether the last refresh attempt succeeded.
  /// </param>
  /// <param name="LastRefreshProviderName">
  ///     The provider label used by the last refresh attempt.
  /// </param>
  /// <param name="LastRefreshUrl">
  ///     The normalized base URL used by the last refresh attempt.
  /// </param>
  /// <param name="CurrentModelCount">
  ///     The current model count retained by the shared model manager.
  /// </param>
  /// <param name="LastRefreshFailureDetail">
  ///     The last observed refresh failure detail when the most recent attempt
  ///     failed.
  /// </param>
  public readonly record struct OpenAiModelRefreshSnapshot(
      DateTime? LastRefreshObservedAtUtc,
      bool? LastRefreshSucceeded,
      string? LastRefreshProviderName,
      string? LastRefreshUrl,
      int CurrentModelCount,
      string? LastRefreshFailureDetail);

  public static void ResetToDefault()
  {
    lock (SyncLock)
    {
      CurrentModelList = OpenAITextModelDefaults.PredefinedModels;
    }
  }

  public static async Task<bool> RefreshAsync(string apiKey)
  {
    return await RefreshAsync(apiKey, DefaultOpenAiModelsBaseUrl, "OpenAI");
  }

  /// <summary>
  ///     Refreshes the model list from the specified OpenAI-compatible
  ///     provider endpoint.
  /// </summary>
  /// <param name="apiKey">The API key used by the provider.</param>
  /// <param name="baseUrl">The provider base URL.</param>
  /// <param name="providerName">The displayable provider label.</param>
  public static async Task<bool> RefreshAsync(
      string apiKey,
      string baseUrl,
      string providerName)
  {
    var observedAtUtc = DateTime.UtcNow;
    var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      ResetToDefault();
      UpdateRefreshState(
          observedAtUtc,
          providerName,
          normalizedBaseUrl,
          false,
          "Missing API key.");
      return false;
    }

    try
    {
      if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
      {
        ResetToDefault();
        UpdateRefreshState(
            observedAtUtc,
            providerName,
            normalizedBaseUrl,
            false,
            "Missing model endpoint.");
        return false;
      }

      using var request = new HttpRequestMessage(
          HttpMethod.Get,
          $"{normalizedBaseUrl}/models");
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
      request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

      using var response = await HttpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
      {
        ResetToDefault();
        UpdateRefreshState(
            observedAtUtc,
            providerName,
            normalizedBaseUrl,
            false,
            $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        return false;
      }

      string json = await response.Content.ReadAsStringAsync();
      var root = JObject.Parse(json);
      var data = root["data"] as JArray;
      if (data == null)
      {
        ResetToDefault();
        UpdateRefreshState(
            observedAtUtc,
            providerName,
            normalizedBaseUrl,
            false,
            "Provider response did not include a models data array.");
        return false;
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
          EngineName: providerName));
      }

      lock (SyncLock)
      {
        if (models.Count > 0)
        {
          CurrentModelList = models;
          UpdateRefreshState(
              observedAtUtc,
              providerName,
              normalizedBaseUrl,
              true,
              null);
          return true;
        }
      }

      ResetToDefault();
      UpdateRefreshState(
          observedAtUtc,
          providerName,
          normalizedBaseUrl,
          false,
          "Provider returned no supported text models.");
    }
    catch (Exception ex)
    {
      ResetToDefault();
      UpdateRefreshState(
          observedAtUtc,
          providerName,
          normalizedBaseUrl,
          false,
          $"{ex.GetType().Name}: {ex.Message}");
    }

    return false;
  }

  /// <summary>
  ///     Gets the current shared OpenAI-family live model refresh snapshot for
  ///     debugger inspection.
  /// </summary>
  /// <returns>The current live model refresh snapshot.</returns>
  public static OpenAiModelRefreshSnapshot GetRefreshSnapshot()
  {
    lock (SyncLock)
    {
      return new OpenAiModelRefreshSnapshot(
          lastRefreshObservedAtUtc,
          lastRefreshSucceeded,
          lastRefreshProviderName,
          lastRefreshUrl,
          CurrentModelList.Count,
          lastRefreshFailureDetail);
    }
  }

  /// <summary>
  ///     Updates the shared refresh snapshot after one live model-list
  ///     attempt.
  /// </summary>
  /// <param name="observedAtUtc">The observation timestamp.</param>
  /// <param name="providerName">The provider label.</param>
  /// <param name="baseUrl">The provider base URL.</param>
  /// <param name="succeeded">Whether the refresh succeeded.</param>
  /// <param name="failureDetail">The failure detail when the refresh failed.</param>
  private static void UpdateRefreshState(
      DateTime observedAtUtc,
      string? providerName,
      string? baseUrl,
      bool succeeded,
      string? failureDetail)
  {
    lock (SyncLock)
    {
      lastRefreshObservedAtUtc = observedAtUtc;
      lastRefreshSucceeded = succeeded;
      lastRefreshProviderName = string.IsNullOrWhiteSpace(providerName)
          ? null
          : providerName;
      lastRefreshUrl = string.IsNullOrWhiteSpace(baseUrl)
          ? null
          : baseUrl.TrimEnd('/');
      lastRefreshFailureDetail = succeeded || string.IsNullOrWhiteSpace(failureDetail)
          ? null
          : failureDetail;
    }
  }
}
