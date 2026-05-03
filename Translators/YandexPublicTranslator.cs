// <copyright file="YandexPublicTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Net.Http.Json;

namespace Echoglossian.Translators;

public class YandexPublicTranslator : ITranslator, IDisposable
{
    private const string ApiUrl = "https://translate.yandex.net/api/v1/tr.json";
    private const string DefaultUserAgent = "ru.yandex.translate/3.20.2024";
    private readonly Config config;
    private readonly HttpClient httpClient;

    private readonly IPluginLog pluginLog;
    private CachedObject<Guid> cachedUcid;
    private bool disposed;

    public YandexPublicTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.config = config;

        this.httpClient = new HttpClient();

        if (this.httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                DefaultUserAgent);
        }

        this.cachedUcid = new CachedObject<Guid>(
            Guid.NewGuid(),
            TimeSpan.FromSeconds(360));
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.httpClient.Dispose();
            this.disposed = true;
        }
    }

    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return this.TranslateAsync(text, sourceLanguage, targetLanguage)
            .GetAwaiter().GetResult() ?? string.Empty;
    }

    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return await this.TranslateWithFreeApi(
                text,
                sourceLanguage,
                targetLanguage);
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"Yandex translation failed: {ex}");
            return string.Empty;
        }
    }

    private async Task<string> TranslateWithFreeApi(
        string text,
        string fromLang,
        string toLang)
    {
        var langPair = BuildLanguagePair(fromLang, toLang);

        var query = $"?ucid={this.GetOrUpdateUcid():N}&srv=android&format=text";

        var data = new Dictionary<string, string>
        {
            { "text", text },
            { "lang", langPair },
        };

        PluginRuntimeLog.Debug(
            this.pluginLog,
            $"Yandex Free API data: {string.Join(", ", data.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");

        var requestURL = $"{ApiUrl}/translate{query}";

        PluginRuntimeLog.Debug(this.pluginLog, $"Yandex Free API Request URL: {requestURL}");

        using var request = new HttpRequestMessage(HttpMethod.Post, requestURL)
        {
            Content = new FormUrlEncodedContent(data),
        };

        request.Headers.UserAgent.ParseAdd(DefaultUserAgent);

        var response = await this.httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                $"Yandex API returned HTTP {response.StatusCode}");
            return string.Empty;
        }

        var result =
            await response.Content.ReadFromJsonAsync<YandexFreeResult>();

        if (result is null || !result.IsSuccessful)
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                $"Yandex API returned error code {result?.Code}, lang: {result?.Lang}");
            return string.Empty;
        }

        return result.Text[0];
    }

    internal static string BuildLanguagePair(string? fromLang, string toLang)
    {
        return string.IsNullOrEmpty(fromLang)
            ? YandexHotPatch(toLang)
            : $"{YandexHotPatch(fromLang)}-{YandexHotPatch(toLang)}";
    }

    internal static string YandexHotPatch(string lang)
    {
        return lang switch
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
    }

    internal static string ReversePatch(string lang)
    {
        return lang switch
        {
            "pt" => "pt-PT",
            _ => lang,
        };
    }

    private Guid GetOrUpdateUcid()
    {
        if (this.cachedUcid.IsExpired())
        {
            this.cachedUcid = new CachedObject<Guid>(
                Guid.NewGuid(),
                TimeSpan.FromSeconds(360));
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
        private readonly TimeSpan lifetime;
        private DateTime expiresAt;

        public CachedObject(T value, TimeSpan lifetime)
        {
            this.lifetime = lifetime;
            this.Set(value);
        }

        public T Value { get; private set; } = default!;

        public bool IsExpired()
        {
            return DateTime.UtcNow >= this.expiresAt;
        }

        public void Set(T value)
        {
            this.Value = value;
            this.expiresAt = DateTime.UtcNow.Add(this.lifetime);
        }
    }
}
