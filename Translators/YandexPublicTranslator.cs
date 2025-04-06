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
  public class YandexPublicTranslator : ITranslator
  {
    private readonly IPluginLog pluginLog;
    private static readonly HttpClient HttpClient = new();

    public YandexPublicTranslator(IPluginLog pluginLog)
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
        string langPair = $"{sourceLanguage}-{targetLanguage}";
        string url = "https://translate.yandex.net/api/v1/tr.json/translate";

        var content = new FormUrlEncodedContent(new[]
        {
          new KeyValuePair<string, string>("id", this.GenerateRequestId()),
          new KeyValuePair<string, string>("srv", "tr-text"),
          new KeyValuePair<string, string>("lang", langPair),
          new KeyValuePair<string, string>("reason", "paste"),
          new KeyValuePair<string, string>("format", "text"),
          new KeyValuePair<string, string>("text", fixedText),
        });

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        this.pluginLog.Debug($"Sending public Yandex request for: {fixedText}");

        var response = await HttpClient.PostAsync(url, content);
        var json = await response.Content.ReadAsStringAsync();

        this.pluginLog.Debug($"Response: {json}");

        var parsed = JObject.Parse(json);
        var translated = parsed["text"]?[0]?.ToString();

        if (!string.IsNullOrEmpty(translated))
        {
          string clean = Echoglossian.FixText(translated);
          this.pluginLog.Debug($"Translated: {clean}");
          return clean;
        }

        this.pluginLog.Warning("YandexPublicTranslator: Empty translation result");
        return string.Empty;
      }
      catch (Exception ex)
      {
        this.pluginLog.Warning($"YandexPublicTranslator failed: {ex.Message}");
        return string.Empty;
      }
    }

    private string GenerateRequestId()
    {
      long unixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
      return $"{unixTime}-0-0";
    }
  }
}
