using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;
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
      return TranslateAsync(text, sourceLanguage, targetLanguage).Result;
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

        pluginLog.Debug($"Sending LibreTranslate request for: {fixedText}");

        var response = await HttpClient.PostAsync(Endpoint, content);
        var json = await response.Content.ReadAsStringAsync();

        pluginLog.Debug($"Response: {json}");

        var parsed = JObject.Parse(json);
        var translated = parsed["translatedText"]?.ToString();

        if (!string.IsNullOrEmpty(translated))
        {
          string clean = Echoglossian.FixText(translated);
          pluginLog.Debug($"Translated: {clean}");
          return clean;
        }

        pluginLog.Warning("LibreTranslateTranslator: Empty translation result");
        return string.Empty;
      }
      catch (Exception ex)
      {
        pluginLog.Warning($"LibreTranslateTranslator failed: {ex.Message}");
        return string.Empty;
      }
    }


  }
}
