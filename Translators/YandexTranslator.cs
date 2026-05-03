// <copyright file="YandexTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Yandex Translator class for translating text using Yandex API.
/// </summary>
public class YandexTranslator : ITranslator
{
    private static readonly HttpClient HttpClient = new();
    private readonly int characterQuotaLimit = 1000000;
    private readonly Config config;
    private readonly IPluginLog pluginLog;

    /// <summary>
    ///     Initializes a new instance of the <see cref="YandexTranslator" /> class.
    /// </summary>
    /// <param name="pluginLog">The plugin log.</param>
    /// <param name="config">The configuration settings.</param>
    public YandexTranslator(IPluginLog pluginLog, Config config)
    {
        this.pluginLog = pluginLog;
        this.config = config;
    }

    /// <summary>
    ///     Translates the specified text from the source language to the target
    ///     language.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <returns>The translated text.</returns>
    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        PluginRuntimeLog.Debug(this.pluginLog, "Inside YandexTranslator Translate (sync)");
        return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result ?? string.Empty;
    }

    /// <summary>
    ///     Translates the specified text from the source language to the target
    ///     language asynchronously.
    /// </summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <returns>
    ///     A task that represents the asynchronous translation operation. The
    ///     task result contains the translated text.
    /// </returns>
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        PluginRuntimeLog.Debug(this.pluginLog, "Inside YandexTranslator TranslateAsync");

        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var fixedText = FixText(text);
        PluginRuntimeLog.Debug(this.pluginLog, $"Fixed Input Text: {fixedText}");

        try
        {
            var result =
                this.config.UsePaidYandexApi ||
                this.config.UseYandexV2ForFreeApi
                    ? await this.TranslateWithV2Api(
                        fixedText,
                        sourceLanguage,
                        targetLanguage)
                    : await this.TranslateWithLegacyFreeApi(
                        fixedText,
                        sourceLanguage,
                        targetLanguage);

            var cleanedResult = FixText(result);
            PluginRuntimeLog.Debug(this.pluginLog, $"Final Translated Text: {cleanedResult}");

            return cleanedResult;
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"Yandex translation failed: {ex}");
            return string.Empty;
        }
    }

    private async Task<string> TranslateWithLegacyFreeApi(
        string text,
        string sourceLang,
        string targetLang)
    {
        var from = NormalizeLanguageCode(sourceLang);
        var to = NormalizeLanguageCode(targetLang);
        var apiKey = this.config.YandexFreeApiKey;

        var requestUrl =
            $"https://translate.yandex.net/api/v1.5/tr.json/translate?key={apiKey}&text={Uri.EscapeDataString(text)}&lang={from}-{to}";
        PluginRuntimeLog.Debug(this.pluginLog, $"Free API Request URL: {requestUrl}");

        var response = await HttpClient.GetAsync(requestUrl);
        var responseContent = await response.Content.ReadAsStringAsync();
        PluginRuntimeLog.Debug(this.pluginLog, $"Response: {responseContent}");

        var parsed = JObject.Parse(responseContent);
        return parsed["text"]?[0]?.ToString() ?? string.Empty;
    }

    private async Task<string> TranslateWithV2Api(
        string text,
        string sourceLang,
        string targetLang)
    {
        var from = NormalizeLanguageCode(sourceLang);
        var to = NormalizeLanguageCode(targetLang);
        var folderId = this.config.YandexFolderId;
        var apiKey = this.config.UsePaidYandexApi
            ? this.config.YandexPaidApiKey
            : this.config.YandexFreeApiKey;

        var requestBody = new
        {
            folderId,
            texts = new[] { text },
            sourceLanguageCode = from,
            targetLanguageCode = to,
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(requestBody),
            Encoding.UTF8,
            "application/json");

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add(
            "Authorization",
            $"Api-Key {apiKey}");

        PluginRuntimeLog.Debug(
            this.pluginLog,
            $"V2 API request body: {JsonConvert.SerializeObject(requestBody)}");

        var response = await HttpClient.PostAsync(
            "https://translate.api.cloud.yandex.net/translate/v2/translate",
            content);
        var responseBody = await response.Content.ReadAsStringAsync();
        PluginRuntimeLog.Debug(this.pluginLog, $"V2 API response: {responseBody}");

        if (!response.IsSuccessStatusCode)
        {
            this.HandleApiError(responseBody);
            return string.Empty;
        }

        var parsed = JObject.Parse(responseBody);
        var translation = parsed["translations"]?[0];
        var translatedText = translation?["text"]?.ToString() ?? string.Empty;
        var detectedLang = translation?["detectedLanguageCode"]?.ToString();

        if (!string.IsNullOrEmpty(detectedLang))
        {
            PluginRuntimeLog.Debug(this.pluginLog, $"Detected Language: {detectedLang}");
        }

        this.TrackApiUsage(text.Length);
        return translatedText;
    }

    private void HandleApiError(string responseBody)
    {
        try
        {
            var error = JObject.Parse(responseBody);
            var message = error["message"]?.ToString() ?? "Unknown error";
            var code = error["code"]?.ToString() ?? "N/A";
            PluginRuntimeLog.Warning(this.pluginLog, $"Yandex API Error [{code}]: {message}");
        }
        catch
        {
            PluginRuntimeLog.Error(this.pluginLog, $"Unexpected error response: {responseBody}");
        }
    }

    private void TrackApiUsage(int charCount)
    {
        try
        {
            this.config.YandexCharactersTranslated += charCount;

            PluginInterface.SavePluginConfig(this.config);

            PluginRuntimeLog.Debug(
                this.pluginLog,
                $"Characters translated today (stored in config): {this.config.YandexCharactersTranslated}");

            if (this.config.YandexCharactersTranslated >
                this.characterQuotaLimit)
            {
                PluginRuntimeLog.Warning(
                    this.pluginLog,
                    "Yandex API character quota likely exceeded.");
            }
        }
        catch (Exception ex)
        {
            PluginRuntimeLog.Error(this.pluginLog, $"Failed to track API usage: {ex}");
        }
    }
}
