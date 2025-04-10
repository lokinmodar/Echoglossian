using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;

namespace Echoglossian.Translators
{
  public partial class YandexPublicTranslator : ITranslator, IDisposable
  {
    private const string ApiUrl = "https://translate.yandex.net/api/v1/tr.json";
    private const string DefaultUserAgent = "ru.yandex.translate/3.20.2024";

    private readonly IPluginLog pluginLog;
    private readonly Config config;
    private readonly HttpClient httpClient;
    private CachedObject<Guid> cachedUcid;
    private bool disposed;

    public YandexPublicTranslator(IPluginLog pluginLog, Config config)
    {
      this.pluginLog = pluginLog;
      this.config = config;

      this.httpClient = new HttpClient();

      if (this.httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
      {
        this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
      }

      this.cachedUcid = new CachedObject<Guid>(Guid.NewGuid(), TimeSpan.FromSeconds(360));
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).GetAwaiter().GetResult();
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      try
      {
        if (string.IsNullOrWhiteSpace(text))
        {
          return string.Empty;
        }

        return await this.TranslateWithFreeApi(text, sourceLanguage, targetLanguage);
      }
      catch (Exception ex)
      {
        this.pluginLog.Error($"Yandex translation failed: {ex}");
        return string.Empty;
      }
    }

    private async Task<string> TranslateWithFreeApi(string text, string fromLang, string toLang)
    {
      string langPair = string.IsNullOrEmpty(fromLang)
          ? YandexHotPatch(toLang)
          : $"{YandexHotPatch(fromLang)}-{YandexHotPatch(toLang)}";

      string query = $"?ucid={this.GetOrUpdateUcid():N}&srv=android&format=text";

      var data = new Dictionary<string, string>
            {
                { "text", text },
                { "lang", langPair },
            };

      this.pluginLog.Debug($"Yandex Free API data: {string.Join(", ", data.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");

      string requestURL = $"{ApiUrl}/translate{query}";

      this.pluginLog.Debug($"Yandex Free API Request URL: {requestURL}");

      using var request = new HttpRequestMessage(HttpMethod.Post, requestURL)
      {
        Content = new FormUrlEncodedContent(data),
      };

      request.Headers.UserAgent.ParseAdd(DefaultUserAgent);

      var response = await this.httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
      {
        this.pluginLog.Warning($"Yandex API returned HTTP {response.StatusCode}");
        return string.Empty;
      }

      var result = await response.Content.ReadFromJsonAsync<YandexFreeResult>();

      if (result is null || !result.IsSuccessful)
      {
        this.pluginLog.Warning($"Yandex API returned error code {result?.Code}, lang: {result?.Lang}");
        return string.Empty;
      }

      return result.Text[0];
    }

    private static string YandexHotPatch(string lang) => lang switch
    {
      "English" => "en",
      "French" => "fr",
      "Français" => "fr",
      "German" => "de",
      "Deutsch" => "de",
      "Japanese" => "ja",
      "日本語" => "ja",
      "pt-PT" => "pt",
      "pt" => "pt-BR",
      "zh-CN" => "zh",
      _ => lang,
    };

    private static string ReversePatch(string lang) => lang switch
    {
      "pt" => "pt-PT",
      _ => lang,
    };

    private Guid GetOrUpdateUcid()
    {
      if (this.cachedUcid.IsExpired())
      {
        this.cachedUcid = new CachedObject<Guid>(Guid.NewGuid(), TimeSpan.FromSeconds(360));
      }

      return this.cachedUcid.Value;
    }

    private class YandexFreeResult
    {
      public int Code { get; set; }

      public string Lang { get; set; } = string.Empty;

      public string[] Text { get; set; } = Array.Empty<string>();

      public bool IsSuccessful => this.Code == 200;
    }

    public class CachedObject<T>
    {
      public T Value { get; private set; }

      private readonly TimeSpan lifetime;
      private DateTime expiresAt;

      public CachedObject(T value, TimeSpan lifetime)
      {
        this.lifetime = lifetime;
        this.Set(value);
      }

      public bool IsExpired() => DateTime.UtcNow >= this.expiresAt;

      public void Set(T value)
      {
        this.Value = value;
        this.expiresAt = DateTime.UtcNow.Add(this.lifetime);
      }
    }

    public void Dispose()
    {
      if (!this.disposed)
      {
        this.httpClient.Dispose();
        this.disposed = true;
      }
    }
  }
}
