// <copyright file="LibreTranslateTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Headers;

using Newtonsoft.Json.Linq;

namespace Echoglossian.Translators
{
  public class LibreTranslateTranslator : ITranslator
  {
    private readonly IPluginLog pluginLog;
    private static readonly HttpClient HttpClient = new();

    private const string Endpoint = "https://libretranslate.de/translate";

    public LibreTranslateTranslator(IPluginLog pluginLog)
    {
      this.pluginLog = pluginLog;
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result;
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      try
      {
        string fixedText = Echoglossian.FixText(text);

        var requestBody = new Dictionary<string, string>
                {
                    { "q", fixedText },
                    { "source", sourceLanguage },
                    { "target", targetLanguage },
                    { "format", "text" },
                };

        var content = new FormUrlEncodedContent(requestBody);

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Echoglossian LibreTranslate Client");
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        this.pluginLog.Debug($"Sending LibreTranslate request for: {fixedText}");

        var response = await HttpClient.PostAsync(Endpoint, content);
        var json = await response.Content.ReadAsStringAsync();

        this.pluginLog.Debug($"Response: {json}");

        var parsed = JObject.Parse(json);
        var translated = parsed["translatedText"]?.ToString();

        if (!string.IsNullOrEmpty(translated))
        {
          string clean = Echoglossian.FixText(translated);
          this.pluginLog.Debug($"Translated: {clean}");
          return clean;
        }

        this.pluginLog.Warning("LibreTranslateTranslator: Empty translation result");
        return string.Empty;
      }
      catch (Exception ex)
      {
        this.pluginLog.Warning($"LibreTranslateTranslator failed: {ex.Message}");
        return string.Empty;
      }
    }


  }
}
