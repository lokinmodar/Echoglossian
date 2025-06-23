// <copyright file="MicrosoftTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Headers;

using Echoglossian.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Echoglossian.Translators
{
  public class MicrosoftTranslator : ITranslator
  {
    private readonly HttpClient? httpClient;
    private readonly IPluginLog pluginLog;
    private readonly Dictionary<string, string> translationCache = new Dictionary<string, string>();
    private readonly string apiKey;
    private readonly string region;
    private readonly string endpoint;
    private readonly int maxRetries = 3;
    private readonly TimeSpan initialBackoff = TimeSpan.FromSeconds(1);

    public MicrosoftTranslator(IPluginLog pluginLog, Config config)
    {
      // Read the API key, region, and endpoint from the configuration.
      // (For free tier usage, simply use the free-key from your Azure Cognitive Services subscription.)
      this.apiKey = config.MicrosoftTranslatorApiKey ?? string.Empty;
      this.region = config.MicrosoftTranslatorRegion ?? string.Empty;
      // Default endpoint for Microsoft Translator
      this.endpoint = config.MicrosoftTranslatorEndpoint ?? "https://api.cognitive.microsofttranslator.com";

      this.pluginLog = pluginLog;

      if (string.IsNullOrWhiteSpace(this.apiKey))
      {
        this.pluginLog.Warning(Resources.APIKeyIsEmptyOrInvalidMicrosoftTranslationWillNotBeAvailable);
        this.httpClient = null;
      }
      else
      {
        try
        {
          // Log a debug message with partial key and region info for troubleshooting.
          pluginLog.Debug($"MicrosoftTranslator: key {this.apiKey.Substring(0, 5)}***{this.apiKey.Substring(this.apiKey.Length - 5)}, region: {this.region}, endpoint: {this.endpoint}");

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
          this.pluginLog.Error($"Failed to initialize Microsoft Translator HTTP client: {ex.Message}");
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

      // Preprocess the incoming text.
      string fixedInputText = Echoglossian.FixText(text);

      // Build the request URL with query parameters.
      string requestUrl = $"{this.endpoint}/translate?api-version=3.0&from={sourceLanguage}&to={targetLanguage}";

      // Prepare the request body. Microsoft Translator expects an array of objects with a "Text" property.
      var requestBody = new[]
      {
                new { Text = fixedInputText },
      };

      var jsonContent = JsonConvert.SerializeObject(requestBody);
      var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

      // Attempt the translation with retries (exponential backoff).
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
              this.pluginLog.Warning($"Microsoft Translator API request failed with status code {response.StatusCode}. Retrying in {backoff.TotalSeconds} seconds...");
              await Task.Delay(backoff);
              continue; // Retry
            }
            else
            {
              this.pluginLog.Error($"Microsoft Translator API request failed after {this.maxRetries} retries with status code {response.StatusCode}.");
              return $"[{Resources.TranslationError} Microsoft Translator API request failed with status code {response.StatusCode}]";
            }
          }

          var responseString = await response.Content.ReadAsStringAsync();

          // The response is an array. Example response:
          // [
          //   {
          //     "translations": [
          //       {
          //         "text": "Bonjour!",
          //         "to": "fr"
          //       }
          //     ]
          //   }
          // ]
          var responseArray = JArray.Parse(responseString);
          if (responseArray.Count > 0)
          {
            var translations = responseArray[0]["translations"];
            if (translations != null && translations.HasValues)
            {
              string translatedText = translations[0]?["text"]?.ToString().Trim();
              if (!string.IsNullOrEmpty(translatedText))
              {
                translatedText = Echoglossian.FixText(translatedText.Trim('"'));
                this.translationCache[cacheKey] = translatedText;
                return translatedText;
              }
            }
          }

          this.pluginLog.Error("Microsoft Translator API returned an empty or invalid translated text.");
          return $"[{Resources.TranslationError} Microsoft Translator API returned an empty or invalid translated text.]";
        }
        catch (HttpRequestException httpEx)
        {
          if (retry < this.maxRetries)
          {
            var backoff = this.initialBackoff * Math.Pow(2, retry);
            this.pluginLog.Warning($"HTTP Error: {httpEx.Message}. Retrying in {backoff.TotalSeconds} seconds...");
            await Task.Delay(backoff);
            continue;
          }
          else
          {
            this.pluginLog.Error($"{Resources.TranslationError} HTTP Error: {httpEx.Message}");
            return $"[{Resources.TranslationError} HTTP Error: {httpEx.Message}]";
          }
        }
        catch (JsonException jsonEx)
        {
          this.pluginLog.Error($"{Resources.TranslationError} JSON Error: {jsonEx.Message}");
          return $"[{Resources.TranslationError} JSON Error: {jsonEx.Message}]";
        }
        catch (Exception ex)
        {
          this.pluginLog.Error($"{Resources.TranslationError} {ex.Message}");
          return $"[{Resources.TranslationError} {ex.Message}]";
        }
      }

      return string.Empty;
    }
  }
}
