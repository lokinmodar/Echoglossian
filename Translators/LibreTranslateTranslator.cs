// <copyright file="LibreTranslateTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Echoglossian.Translators.LibreTranslate;

namespace Echoglossian.Translators;

public class LibreTranslateTranslator : ITranslator
{
    private static readonly HttpClient HttpClient = new();
    private static List<string>? supportedLanguages;
    private readonly string apiKey;
    private readonly string endpoint;
    private readonly IPluginLog pluginLog;

    public LibreTranslateTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.apiKey = config.LibreTranslateApiKey ?? string.Empty;
        this.endpoint = DetermineEndpoint(config);
    }

    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result ?? string.Empty;
    }

    /// <summary>
    ///     Asynchronously translates the specified text from the source language to
    ///     the target language using LibreTranslate.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="sourceLanguage"></param>
    /// <param name="targetLanguage"></param>
    /// <returns></returns>
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        try
        {
            var fixedText = FixText(text);

            var requestBody = new Dictionary<string, string>
            {
                { "q", fixedText },
                { "source", sourceLanguage }, // supports "auto"
                { "target", targetLanguage },
                { "format", "text" },
            };

            if (!string.IsNullOrWhiteSpace(this.apiKey))
            {
                requestBody["api_key"] = this.apiKey;
            }

            var content = new FormUrlEncodedContent(requestBody);

            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Echoglossian LibreTranslate Client");
            HttpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            PluginRuntimeLog.Debug(
                this.pluginLog,
                $"Sending LibreTranslate request to {this.endpoint} for: {fixedText}");

            var response = await HttpClient.PostAsync(this.endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            PluginRuntimeLog.Debug(this.pluginLog, $"Response: {json}");

            var parsed = JObject.Parse(json);
            var translated = parsed["translatedText"]?.ToString();

            if (!string.IsNullOrEmpty(translated))
            {
                var clean = FixText(translated);
                PluginRuntimeLog.Debug(this.pluginLog, $"Translated: {clean}");
                return clean;
            }

            PluginRuntimeLog.Warning(
                this.pluginLog,
                "LibreTranslateTranslator: Empty translation result");
            return string.Empty;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(
                this.pluginLog,
                $"LibreTranslateTranslator failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    ///     Determines the endpoint URL for LibreTranslate based on the configuration.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    internal static string DetermineEndpoint(Config config)
    {
        return config.LibreTranslateInstanceType switch
        {
            LibreTranslateInstanceType.Com =>
                "https://libretranslate.com/translate",
            LibreTranslateInstanceType.De =>
                "https://libretranslate.de/translate",
            LibreTranslateInstanceType.Custom => config.LibreTranslateUrl
                ?.TrimEnd('/') + "/translate",
            _ => "https://libretranslate.de/translate",
        };
    }

    public async Task<List<string>> GetSupportedLanguagesAsync()
    {
        if (supportedLanguages != null)
        {
            return supportedLanguages;
        }

        try
        {
            var langUrl = this.endpoint.Replace("/translate", "/languages");
            var response = await HttpClient.GetAsync(langUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var array = JArray.Parse(json);

            supportedLanguages = array.Select(x => x["code"]?.ToString())
                .Where(x => !string.IsNullOrEmpty(x)).Cast<string>().ToList();

            return supportedLanguages;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Warning(
                this.pluginLog,
                $"Failed to fetch LibreTranslate languages: {ex.Message}");
            return new List<string>
                { "en", "es", "fr", "de", "it", "pt", "auto" };
        }
    }
}
