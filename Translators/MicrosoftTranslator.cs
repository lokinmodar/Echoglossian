// <copyright file="MicrosoftTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>
using Echoglossian.Translators.Helpers;

namespace Echoglossian.Translators;

public class MicrosoftTranslator : ITranslator
{
  private readonly HttpClient? httpClient;
  private readonly IPluginLog pluginLog;
  private readonly ConcurrentTranslationRequestCache translationCache = new();
  private readonly string apiKey;
  private readonly string region;
  private readonly string endpoint;
  private readonly int maxRetries = 3;
  private readonly TimeSpan initialBackoff = TimeSpan.FromSeconds(1);

  public MicrosoftTranslator(IPluginLog pluginLog, Config config)
  {
    this.apiKey = config.MicrosoftTranslatorApiKey ?? string.Empty;
    this.region = config.MicrosoftTranslatorRegion ?? string.Empty;
    this.endpoint = config.MicrosoftTranslatorEndpoint ?? "https://api.cognitive.microsofttranslator.com";
    this.pluginLog = pluginLog;

    if (string.IsNullOrWhiteSpace(this.apiKey))
    {
      PluginRuntimeLog.Warning(this.pluginLog, Resources.APIKeyIsEmptyOrInvalidMicrosoftTranslationWillNotBeAvailable);
      this.httpClient = null;
    }
    else
    {
      try
      {
        PluginRuntimeLog.Debug(pluginLog, $"MicrosoftTranslator: key {this.apiKey[..5]}***{this.apiKey[^5..]}, region: {this.region}, endpoint: {this.endpoint}");

        this.httpClient = new HttpClient();
        this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        this.httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", this.apiKey);
        if (!string.IsNullOrWhiteSpace(this.region))
        {
          this.httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", this.region);
        }
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error(this.pluginLog, $"Failed to initialize Microsoft Translator HTTP client: {ex.Message}");
        this.httpClient = null;
      }
    }
  }

  public string Translate(string text, string sourceLanguage, string targetLanguage)
  {
    return this.TranslateAsync(text, sourceLanguage, targetLanguage).GetAwaiter().GetResult() ?? string.Empty;
  }

  public async Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
  {
    if (this.httpClient == null)
    {
      return Resources.MicrosoftTranslationUnavailablePleaseCheckYourAPIKey;
    }

    string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
    if (this.translationCache.TryGetValue(cacheKey, out string? cachedTranslation))
    {
      return cachedTranslation;
    }

    return await this.translationCache.GetOrAddAsync(
        cacheKey,
        () => this.TranslateCoreAsync(
            text,
            sourceLanguage,
            targetLanguage,
            cacheKey)).ConfigureAwait(false);
  }

  /// <summary>
  ///     Performs the actual Microsoft Translator request for one cache key.
  /// </summary>
  /// <param name="text">The text to translate.</param>
  /// <param name="sourceLanguage">The source language of the text.</param>
  /// <param name="targetLanguage">The target language for the translation.</param>
  /// <param name="cacheKey">The normalized cache key for this request.</param>
  /// <returns>The translated text or an error placeholder.</returns>
  private async Task<string?> TranslateCoreAsync(
      string text,
      string sourceLanguage,
      string targetLanguage,
      string cacheKey)
  {
    string fixedInputText = Echoglossian.FixText(text);

    string requestUrl = BuildRequestUrl(
        this.endpoint,
        sourceLanguage,
        targetLanguage);

    var requestBody = new[]
    {
      new { Text = fixedInputText },
    };

    var jsonContent = JsonConvert.SerializeObject(requestBody);
    var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

    for (int retry = 0; retry <= this.maxRetries; retry++)
    {
      try
      {
        var response = await this.httpClient.PostAsync(requestUrl, httpContent);

        if (!response.IsSuccessStatusCode)
        {
          if (retry < this.maxRetries)
          {
            var backoff = this.initialBackoff * Math.Pow(2, retry);
            PluginRuntimeLog.Warning(this.pluginLog, $"Microsoft Translator API request failed with status code {response.StatusCode}. Retrying in {backoff.TotalSeconds} seconds...");
            await Task.Delay(backoff);
            continue;
          }

          PluginRuntimeLog.Error(this.pluginLog, $"Microsoft Translator API request failed after {this.maxRetries} retries with status code {response.StatusCode}.");
          return $"[{Resources.TranslationError} Microsoft Translator API request failed with status code {response.StatusCode}]";
        }

        var responseString = await response.Content.ReadAsStringAsync();
        var responseArray = JArray.Parse(responseString);
        var translations = responseArray[0]?["translations"];
        string? translatedText = translations?[0]?["text"]?.ToString()?.Trim();

        if (!string.IsNullOrEmpty(translatedText))
        {
          translatedText = Echoglossian.FixText(translatedText.Trim('"'));
          if (TranslationResultGuard.IsPersistableTranslation(translatedText))
          {
            this.translationCache.Remember(cacheKey, translatedText);
          }

          return translatedText;
        }

        PluginRuntimeLog.Error(this.pluginLog, "Microsoft Translator API returned an empty or invalid translated text.");
        return $"[{Resources.TranslationError} Microsoft Translator API returned an empty or invalid translated text.]";
      }
      catch (HttpRequestException httpEx)
      {
        if (retry < this.maxRetries)
        {
          var backoff = this.initialBackoff * Math.Pow(2, retry);
          PluginRuntimeLog.Warning(this.pluginLog, $"HTTP Error: {httpEx.Message}. Retrying in {backoff.TotalSeconds} seconds...");
          await Task.Delay(backoff);
          continue;
        }

        PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} HTTP Error: {httpEx.Message}");
        return $"[{Resources.TranslationError} HTTP Error: {httpEx.Message}]";
      }
      catch (JsonException jsonEx)
      {
        PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} JSON Error: {jsonEx.Message}");
        return $"[{Resources.TranslationError} JSON Error: {jsonEx.Message}]";
      }
      catch (Exception ex)
      {
        PluginRuntimeLog.Error(this.pluginLog, $"{Resources.TranslationError} {ex.Message}");
        return $"[{Resources.TranslationError} {ex.Message}]";
      }
    }

    return string.Empty;
  }

  internal static string BuildRequestUrl(
      string endpoint,
      string sourceLanguage,
      string targetLanguage)
  {
    return $"{endpoint}/translate?api-version=3.0" +
           $"&from={sourceLanguage}&to={targetLanguage}" +
           "&category=general&profanityAction=NoAction&textType=plain";
  }
}
