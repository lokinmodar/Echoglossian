// <copyright file="OpenRouterTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;

using Echoglossian.Properties;

namespace Echoglossian.Translators
{
  public class OpenRouterTranslator : ITranslator
  {
    private readonly HttpClient httpClient;
    private readonly IPluginLog pluginLog;
    private readonly string model;
    private readonly string apiKey;
    private readonly string openRouterUrl;
    private float temperature;
    private Dictionary<string, string> translationCache = new();

    private readonly string prompt;

    public OpenRouterTranslator(IPluginLog pluginLog, Config config)
    {
      this.pluginLog = pluginLog;
      this.model = config.OpenRouterModel;
      this.temperature = config.OpenRouterTemperature;
      this.apiKey = config.OpenRouterApiKey;
      this.openRouterUrl = config.OpenRouterBaseUrl;
      this.prompt = config.OpenRouterPrompt;

      if (string.IsNullOrWhiteSpace(this.apiKey))
      {
        this.pluginLog.Warning(Resources.APIKeyIsEmptyOrInvalidChatGPTTranslationWillNotBeAvailable);
      }

      this.httpClient = new HttpClient
      {
        BaseAddress = new Uri(this.openRouterUrl),
      };

      this.httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.apiKey}");
      this.httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://your-plugin-site-or-github-url"); // Optional but recommended
      this.httpClient.DefaultRequestHeaders.Add("X-Title", "Echoglossian Plugin");
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).GetAwaiter().GetResult() ?? string.Empty;
    }

    public async Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      if (string.IsNullOrWhiteSpace(this.apiKey))
      {
        return Resources.ChatGPTTranslationUnavailablePleaseCheckYourAPIKey;
      }

      string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
      if (this.translationCache.TryGetValue(cacheKey, out var cachedTranslation))
      {
        return cachedTranslation;
      }

      var request = new
      {
        this.model,
        messages = new[]
        {
          new { role = "user", content = this.prompt },
        },
        this.temperature,
      };

      try
      {
        var response = await this.httpClient.PostAsJsonAsync("chat/completions", request);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<OpenRouterResponse>();

        string result = jsonResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;

        result = result.Trim('"');

        if (!string.IsNullOrEmpty(result))
        {
          this.translationCache[cacheKey] = result;
          return result;
        }
      }
      catch (Exception ex)
      {
        this.pluginLog.Error($"{Resources.TranslationError} {ex.Message}");
        return $"[{Resources.TranslationError} {ex.Message}]";
      }

      return string.Empty;
    }

    private class OpenRouterResponse
    {
      public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
      public Message? Message { get; set; }
    }

    private class Message
    {
      public string? Role { get; set; }

      public string? Content { get; set; }
    }
  }
}
