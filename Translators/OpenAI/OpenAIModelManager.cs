// <copyright file="OpenAIModelManager.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.Capabilities;

namespace Echoglossian.Translators.OpenAI;

public static class OpenAIModelManager
{
  private const string DefaultOpenAiModelsBaseUrl = "https://api.openai.com/v1";
  private const string OfficialProviderStateKey = "OpenAI";
  private static readonly HttpClient HttpClient = new();
  private static readonly Dictionary<string, OpenAiProviderRefreshState> ProviderStates = new(StringComparer.Ordinal);
  private static readonly object SyncLock = new();

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

  private sealed class OpenAiProviderRefreshState
  {
    public List<LlmTextModel> CurrentModelList { get; set; } =
        OpenAITextModelDefaults.PredefinedModels;

    public string? LastRefreshFailureDetail { get; set; }

    public DateTime? LastRefreshObservedAtUtc { get; set; }

    public string? LastRefreshProviderName { get; set; }

    public bool? LastRefreshSucceeded { get; set; }

    public string? LastRefreshUrl { get; set; }
  }

  /// <summary>
  ///     Gets the current official OpenAI live model list.
  /// </summary>
  public static IReadOnlyList<LlmTextModel> CurrentModelList =>
      GetCurrentModelList(OfficialProviderStateKey);

  /// <summary>
  ///     Gets the current live model list for one OpenAI-family provider
  ///     profile.
  /// </summary>
  /// <param name="providerStateKey">The provider-profile state key.</param>
  /// <returns>The retained model list for that provider profile.</returns>
  public static IReadOnlyList<LlmTextModel> GetCurrentModelList(
      string providerStateKey)
  {
    lock (SyncLock)
    {
      return TryGetProviderState(providerStateKey, out OpenAiProviderRefreshState? state) &&
             state != null
          ? state.CurrentModelList
          : OpenAITextModelDefaults.PredefinedModels;
    }
  }

  public static void ResetToDefault()
  {
    ResetToDefault(OfficialProviderStateKey);
  }

  /// <summary>
  ///     Restores the predefined OpenAI-family model catalog for one provider
  ///     profile.
  /// </summary>
  /// <param name="providerStateKey">The provider-profile state key.</param>
  public static void ResetToDefault(string providerStateKey)
  {
    lock (SyncLock)
    {
      GetOrCreateProviderState(providerStateKey).CurrentModelList =
          OpenAITextModelDefaults.PredefinedModels;
    }
  }

  public static async Task<bool> RefreshAsync(
      string apiKey,
      CancellationToken cancellationToken = default)
  {
    return await RefreshAsync(
        apiKey,
        DefaultOpenAiModelsBaseUrl,
        OfficialProviderStateKey,
        cancellationToken);
  }

  /// <summary>
  ///     Refreshes the model list from the specified OpenAI-compatible
  ///     provider endpoint.
  /// </summary>
  /// <param name="apiKey">The API key used by the provider.</param>
  /// <param name="baseUrl">The provider base URL.</param>
  /// <param name="providerName">The displayable provider label.</param>
  /// <param name="cancellationToken">The token that cancels live discovery.</param>
  public static async Task<bool> RefreshAsync(
      string apiKey,
      string baseUrl,
      string providerName,
      CancellationToken cancellationToken = default)
  {
    var observedAtUtc = DateTime.UtcNow;
    var providerStateKey = ResolveProviderStateKey(providerName);
    var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      ResetToDefault(providerStateKey);
      UpdateRefreshState(
          providerStateKey,
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
        ResetToDefault(providerStateKey);
        UpdateRefreshState(
            providerStateKey,
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

      using var response = await HttpClient.SendAsync(request, cancellationToken);
      if (!response.IsSuccessStatusCode)
      {
        ResetToDefault(providerStateKey);
        UpdateRefreshState(
            providerStateKey,
            observedAtUtc,
            providerName,
            normalizedBaseUrl,
            false,
            $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        return false;
      }

      string json = await response.Content.ReadAsStringAsync(cancellationToken);
      var root = JObject.Parse(json);
      var data = root["data"] as JArray;
      if (data == null)
      {
        ResetToDefault(providerStateKey);
        UpdateRefreshState(
            providerStateKey,
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

      if (models.Count > 0)
      {
        cancellationToken.ThrowIfCancellationRequested();
        ApplyRefreshSuccess(
            providerStateKey,
            observedAtUtc,
            providerName,
            normalizedBaseUrl,
            models);
        return true;
      }

      ResetToDefault(providerStateKey);
      UpdateRefreshState(
          providerStateKey,
          observedAtUtc,
          providerName,
          normalizedBaseUrl,
          false,
          "Provider returned no supported text models.");
    }
    catch (OperationCanceledException)
      when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      ResetToDefault(providerStateKey);
      UpdateRefreshState(
          providerStateKey,
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
    return GetRefreshSnapshot(OfficialProviderStateKey);
  }

  /// <summary>
  ///     Gets the current OpenAI-family live model refresh snapshot for one
  ///     provider profile.
  /// </summary>
  /// <param name="providerStateKey">The provider-profile state key.</param>
  /// <returns>The current live model refresh snapshot.</returns>
  public static OpenAiModelRefreshSnapshot GetRefreshSnapshot(
      string providerStateKey)
  {
    lock (SyncLock)
    {
      if (!TryGetProviderState(providerStateKey, out OpenAiProviderRefreshState? state) ||
          state == null)
      {
        return new OpenAiModelRefreshSnapshot(
            null,
            null,
            null,
            null,
            0,
            null);
      }

      return new OpenAiModelRefreshSnapshot(
          state.LastRefreshObservedAtUtc,
          state.LastRefreshSucceeded,
          state.LastRefreshProviderName,
          state.LastRefreshUrl,
          state.CurrentModelList.Count,
          state.LastRefreshFailureDetail);
    }
  }

  /// <summary>
  ///     Applies a successful live-model refresh result for one provider
  ///     profile.
  /// </summary>
  /// <param name="providerStateKey">The provider-profile state key.</param>
  /// <param name="observedAtUtc">The observation timestamp.</param>
  /// <param name="providerName">The provider label.</param>
  /// <param name="baseUrl">The normalized base URL.</param>
  /// <param name="models">The supported model list.</param>
  internal static void ApplyRefreshSuccess(
      string providerStateKey,
      DateTime observedAtUtc,
      string providerName,
      string baseUrl,
      IReadOnlyList<LlmTextModel> models)
  {
    lock (SyncLock)
    {
      var state = GetOrCreateProviderState(providerStateKey);
      state.CurrentModelList = models.ToList();
      state.LastRefreshObservedAtUtc = observedAtUtc;
      state.LastRefreshSucceeded = true;
      state.LastRefreshProviderName = string.IsNullOrWhiteSpace(providerName)
          ? null
          : providerName;
      state.LastRefreshUrl = string.IsNullOrWhiteSpace(baseUrl)
          ? null
          : baseUrl.TrimEnd('/');
      state.LastRefreshFailureDetail = null;
    }

    LlmCapabilityRefreshPromoter.PromoteDiscoveredModels(
        Echoglossian.TransEngines.ChatGPT,
        providerName,
        baseUrl,
        models.Select(static model => model.Id).ToArray(),
        observedAtUtc);
  }

  /// <summary>
  ///     Clears retained provider refresh state for unit tests.
  /// </summary>
  internal static void ResetAllForTesting()
  {
    lock (SyncLock)
    {
      ProviderStates.Clear();
    }
  }

  /// <summary>
  ///     Updates the shared refresh snapshot after one live model-list
  ///     attempt.
  /// </summary>
  /// <param name="providerStateKey">The provider-profile state key.</param>
  /// <param name="observedAtUtc">The observation timestamp.</param>
  /// <param name="providerName">The provider label.</param>
  /// <param name="baseUrl">The provider base URL.</param>
  /// <param name="succeeded">Whether the refresh succeeded.</param>
  /// <param name="failureDetail">The failure detail when the refresh failed.</param>
  private static void UpdateRefreshState(
      string providerStateKey,
      DateTime observedAtUtc,
      string? providerName,
      string? baseUrl,
      bool succeeded,
      string? failureDetail)
  {
    lock (SyncLock)
    {
      var state = GetOrCreateProviderState(providerStateKey);
      state.LastRefreshObservedAtUtc = observedAtUtc;
      state.LastRefreshSucceeded = succeeded;
      state.LastRefreshProviderName = string.IsNullOrWhiteSpace(providerName)
          ? null
          : providerName;
      state.LastRefreshUrl = string.IsNullOrWhiteSpace(baseUrl)
          ? null
          : baseUrl.TrimEnd('/');
      state.LastRefreshFailureDetail = succeeded || string.IsNullOrWhiteSpace(failureDetail)
          ? null
          : failureDetail;
    }
  }

  /// <summary>
  ///     Resolves the retained state key for one OpenAI-family provider
  ///     profile.
  /// </summary>
  /// <param name="providerName">The displayable provider label.</param>
  /// <returns>The stable provider state key.</returns>
  private static string ResolveProviderStateKey(string? providerName)
  {
    return string.IsNullOrWhiteSpace(providerName)
        ? OfficialProviderStateKey
        : providerName.Trim();
  }

  /// <summary>
  ///     Gets or creates the retained refresh state for one provider profile.
  /// </summary>
  /// <param name="providerStateKey">The provider-profile state key.</param>
  /// <returns>The retained provider refresh state.</returns>
  private static OpenAiProviderRefreshState GetOrCreateProviderState(
      string providerStateKey)
  {
    var normalizedKey = ResolveProviderStateKey(providerStateKey);
    if (!ProviderStates.TryGetValue(normalizedKey, out OpenAiProviderRefreshState? state))
    {
      state = new OpenAiProviderRefreshState();
      ProviderStates[normalizedKey] = state;
    }

    return state;
  }

  /// <summary>
  ///     Tries to get retained refresh state for one provider profile without
  ///     creating a new entry.
  /// </summary>
  /// <param name="providerStateKey">The provider-profile state key.</param>
  /// <param name="state">The retained provider state when present.</param>
  /// <returns><see langword="true" /> when a retained state exists.</returns>
  private static bool TryGetProviderState(
      string providerStateKey,
      out OpenAiProviderRefreshState? state)
  {
    return ProviderStates.TryGetValue(
        ResolveProviderStateKey(providerStateKey),
        out state);
  }
}
