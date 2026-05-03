// <copyright file="GoogleTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian.Translators;

/// <summary>
///     Provides translation services using Google Translate APIs.
/// </summary>
public class GoogleTranslator : ITranslator
{
    private readonly IPluginLog pluginLog;
    private readonly Config? config;

    /// <summary>
    ///     Shared static HTTP client instance for sending requests.
    /// </summary>
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    ///     Defines the available Google Translate endpoints and required headers/query
    ///     parameters.
    /// </summary>
    private readonly
        Dictionary<int, (string Url, Dictionary<string, string> Headers,
            Dictionary<string, string> QueryParams)> translateEndpoints = new()
        {
            {
                0,
                ("https://translate.google.com/m",
                    new Dictionary<string, string>
                    {
                        {
                            "User-Agent",
                            "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.6998.108 Mobile Safari/537.36"
                        },
                    }, new Dictionary<string, string>())
            },
            {
                1,
                ("https://translate-pa.googleapis.com/v1/translateHtml",
                    new Dictionary<string, string>
                    {
                        {
                            "User-Agent",
                            "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.6998.108 Mobile Safari/537.36"
                        },
                        {
                            "X-Goog-API-Key",
                            "AIzaSyATBXajvzQLTDHEQbcpq0Ihe0vWDHmO520"
                        },
                    }, new Dictionary<string, string>())
            },
            {
                2,
                ("https://dictionaryextension-pa.googleapis.com/v1/dictionaryExtensionData",
                    new Dictionary<string, string>
                    {
                        {
                            "x-referer",
                            "chrome-extension://mgijmajocgfcbeboacabfgobmjgjcoja"
                        },
                    }, new Dictionary<string, string>
                    {
                        { "strategy", "2" },
                        { "key", "AIzaSyA6EEtrDCfBkHV8uU2lgGY-N383ZgAOo7Y" },
                    })
            },
        };

    /// <summary>
    ///     Initializes a new instance of the <see cref="GoogleTranslator" /> class.
    /// </summary>
    /// <param name="pluginLog">Logger for plugin debugging and warnings.</param>
    /// <param name="config">Plugin configuration options.</param>
    public GoogleTranslator(IPluginLog pluginLog, Config? config)
    {
        this.pluginLog = pluginLog;
        this.config = config;
    }

    /// <inheritdoc />
    public string Translate(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        PluginRuntimeLog.Debug(this.pluginLog, "inside GoogleTranslator translate method");

        try
        {
            return this.config?.GoogleTranslateVersion switch
            {
                1 => this.TranslateUsingV1(
                    text,
                    sourceLanguage,
                    targetLanguage),
                2 => this.TranslateUsingV2(text, sourceLanguage, targetLanguage)
                    .GetAwaiter().GetResult() ?? string.Empty,
                _ => this.TranslateUsingV0(text, sourceLanguage, targetLanguage),
            };
        }
        catch (Exception e)
        {
            PluginRuntimeLog.Error(this.pluginLog, e.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string?> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        PluginRuntimeLog.Debug(this.pluginLog, "inside GoogleTranslator translateAsync method");

        try
        {
            return this.config?.GoogleTranslateVersion switch
            {
                1 => await Task.FromResult(
                    this.TranslateUsingV1(
                        text,
                        sourceLanguage,
                        targetLanguage)),
                2 => await this.TranslateUsingV2(
                    text,
                    sourceLanguage,
                    targetLanguage),
                _ => await this.TranslateUsingV0Async(
                    text,
                    sourceLanguage,
                    targetLanguage),
            };
        }
        catch (Exception e)
        {
            PluginRuntimeLog.Error(this.pluginLog, e.ToString());
            throw;
        }
    }

    private string TranslateUsingV0(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        var (url, headers, _) = this.translateEndpoints[0];
        url = BuildV0Url(url, text, sourceLanguage, targetLanguage);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            headers["User-Agent"]);

        var response = HttpClient.Send(request);
        using var reader = new StreamReader(response.Content.ReadAsStream());
        return FormatStreamReader(reader.ReadToEnd());
    }

    private async Task<string?> TranslateUsingV0Async(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        var (url, headers, _) = this.translateEndpoints[0];
        url = BuildV0Url(url, text, sourceLanguage, targetLanguage);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            headers["User-Agent"]);

        var response = await HttpClient.SendAsync(request);
        using var reader =
            new StreamReader(await response.Content.ReadAsStreamAsync());
        return FormatStreamReader(await reader.ReadToEndAsync());
    }

    private string TranslateUsingV1(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        PluginRuntimeLog.Debug(
            this.pluginLog,
            "Google Translate V1 endpoint logic is not implemented.");

        // Example payload to send:
        /*
         * [
         *   [
         *     [
         *       "Assuming that Rishushu's associates at the Baert Trading Company are in possession of the same document, I suspect that the next attack on our supply caravan will strike right there─in the Central Shroud, due southwest from the White Wolf Gate."
         *     ],
         *     "auto",
         *     "pt-BR"
         *   ],
         *   "wt_lib"
         * ]
         */

        // Example JSON response:
        /*
         * [
         *   [
         *     "Supondo que os associados de Rishushu na Companhia Comercial Baert estejam de posse do mesmo documento, suspeito que o próximo ataque à nossa caravana de suprimentos ocorrerá bem ali, no Sudário Central, a sudoeste do Portão do Lobo Branco."
         *   ],
         *   [
         *     "en"
         *   ]
         * ]
         */

        throw new NotImplementedException(
            "Google Translate V1 endpoint logic is not implemented.");
    }

    private async Task<string?> TranslateUsingV2(
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        var (url, headers, queryParams) = this.translateEndpoints[2];
        var parsedText = FixText(text);
        var endpoint = BuildV2Url(
            url,
            parsedText,
            targetLanguage,
            queryParams["key"],
            queryParams["strategy"]);

        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var response = await HttpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        // Example JSON response:
        /*
         * {
         *   "status": 200,
         *   "translateResponse": {
         *     "translateText": "Supondo que os associados de Rishushu na Baert Trading Company estejam de posse do mesmo documento, suspeito que o próximo ataque à nossa caravana de suprimentos ocorrerá ali mesmo - no Sudário Central, a sudoeste do Portão do Lobo Branco.",
         *     "detectedSourceLanguage": "en",
         *     "outputLanguage": "pt-BR",
         *     "sourceText": "Assuming that Rishushu's associates at the Baert Trading Company are in possession of the same document, I suspect that the next attack on our supply caravan will strike right there─in the Central Shroud, due southwest from the White Wolf Gate."
         *   }
         * }
         */

        try
        {
            var json = JObject.Parse(content);
            var translatedText =
                json.SelectToken("translateResponse.translateText")?.ToString();

            if (!string.IsNullOrEmpty(translatedText))
            {
                PluginRuntimeLog.Debug(this.pluginLog, "Translated Text: " + translatedText);
                return translatedText;
            }

            PluginRuntimeLog.Error(
                this.pluginLog,
                $"GoogleTranslator returned no translateResponse.translateText for input '{parsedText}'. " +
                $"Status={(int)response.StatusCode} {response.ReasonPhrase}. " +
                $"Content preview: {FormatResponsePreview(content)}");
            return string.Empty;
        }
        catch (JsonException ex)
        {
            PluginRuntimeLog.Error(
                this.pluginLog,
                $"GoogleTranslator JSON parse error for input '{parsedText}': {ex.Message}. " +
                $"Content preview: {FormatResponsePreview(content)}");
            return string.Empty;
        }
    }

    private static string FormatResponsePreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "<empty>";
        }

        var preview = content.ReplaceLineEndings(" ").Trim();
        const int maxLength = 240;
        if (preview.Length <= maxLength)
        {
            return preview;
        }

        return preview[..maxLength] + "...";
    }

    internal static string BuildV0Url(
        string baseUrl,
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return
            $"{baseUrl}?hl=en&sl={sourceLanguage}&tl={targetLanguage}&q={Uri.EscapeDataString(FixText(text))}";
    }

    internal static string BuildV2Url(
        string baseUrl,
        string parsedText,
        string targetLanguage,
        string apiKey,
        string strategy)
    {
        return
            $"{baseUrl}?language={targetLanguage}&key={apiKey}&term={Uri.EscapeDataString(parsedText)}&strategy={strategy}";
    }
}
