// <copyright file="OllamaTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;

namespace Echoglossian.Translators;

public class OllamaTranslator : ITranslator
{
  private readonly HttpClient httpClient;
  private readonly IPluginLog pluginLog;
  private readonly Dictionary<string, string> translationCache = new();

  private readonly string endpoint;
  private readonly string model;
  private readonly float temperature;

  public OllamaTranslator(IPluginLog pluginLog, Config config)
  {
    this.pluginLog = pluginLog;
    this.endpoint = config.OllamaUrl?.TrimEnd('/') ?? "http://localhost:11434";
    this.model = config.OllamaModel ?? "llama3";
    this.temperature = config.OllamaTemperature;

    this.httpClient = new HttpClient
    {
      BaseAddress = new Uri(this.endpoint),
    };
    this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
  }

  public string Translate(string text, string sourceLanguage, string targetLanguage)
  {
    return this.TranslateAsync(text, sourceLanguage, targetLanguage).GetAwaiter().GetResult() ?? string.Empty;
  }

  public async Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
  {
    string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
    if (this.translationCache.TryGetValue(cacheKey, out var cached))
    {
      return cached;
    }

    string fixedText = Echoglossian.FixText(text);
    string prompt = $"Translate the following Final Fantasy XIV dialogue from {sourceLanguage} to {targetLanguage}. Keep it localized and immersive:\n\n\"{fixedText}\"";

    var request = new
    {
      model = this.model,
      prompt = prompt,
      temperature = this.temperature,
      stream = false,
    };

    try
    {
      var response = await this.httpClient.PostAsJsonAsync("/api/generate", request);
      response.EnsureSuccessStatusCode();

      var json = await response.Content.ReadAsStringAsync();
      var parsed = JObject.Parse(json);
      string? output = parsed["response"]?.ToString()?.Trim();

      if (!string.IsNullOrWhiteSpace(output))
      {
        string cleaned = Echoglossian.FixText(output.Trim('"'));
        this.translationCache[cacheKey] = cleaned;
        return cleaned;
      }

      this.pluginLog.Warning("OllamaTranslator: No output returned.");
      return $"[{Resources.TranslationError} No translation received from Ollama]";
    }
    catch (Exception ex)
    {
      this.pluginLog.Error($"OllamaTranslator failed: {ex.Message}");
      return $"[{Resources.TranslationError} Ollama error: {ex.Message}]";
    }
  }
}
