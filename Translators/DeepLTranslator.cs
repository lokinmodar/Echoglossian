// <copyright file="DeepLTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using DeepL;

namespace Echoglossian.Translators;

/// <summary>
///     Provides translation services using the DeepL API or Free API based on the
///     configuration.
/// </summary>
public class DeepLTranslator : ITranslator
{
  private const string FreeEndpoint = "https://www2.deepl.com/jsonrpc";
  private readonly Translator? deeplClient;

  private readonly HttpClient? httpClient;

  private readonly bool isUsingAPIKey;
  private readonly IPluginLog pluginLog;

  private readonly Random? rndId;

  public DeepLTranslator(
      IPluginLog pluginLog,
      bool isUsingAPIKey,
      string translatorKey)
  {
    this.pluginLog = pluginLog;
    this.isUsingAPIKey = isUsingAPIKey;
    if (isUsingAPIKey && !string.IsNullOrEmpty(translatorKey))
    {
      this.deeplClient = new Translator(translatorKey);
      return;
    }

    this.rndId = new Random(Guid.NewGuid().GetHashCode());
    var handler = new HttpClientHandler
    {
      AutomaticDecompression = DecompressionMethods.GZip |
                                 DecompressionMethods.Deflate |
                                 DecompressionMethods.Brotli,
    };
    this.httpClient = new HttpClient(handler);

    // Setting HTTP headers to mimic a request from the DeepL iOS App
    this.httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
    this.httpClient.DefaultRequestHeaders.Add("x-app-os-name", "iOS");
    this.httpClient.DefaultRequestHeaders.Add("x-app-os-version", "16.3.0");
    this.httpClient.DefaultRequestHeaders.Add(
        "Accept-Language",
        "en-US,en;q=0.9");
    this.httpClient.DefaultRequestHeaders.Add(
        "Accept-Encoding",
        "gzip, deflate, br");
    this.httpClient.DefaultRequestHeaders.Add("x-app-device", "iPhone13,2");
    this.httpClient.DefaultRequestHeaders.Add(
        "User-Agent",
        "DeepL-iOS/2.9.1 iOS 16.3.0 (iPhone13,2)");
    this.httpClient.DefaultRequestHeaders.Add("x-app-build", "510265");
    this.httpClient.DefaultRequestHeaders.Add("x-app-version", "2.9.1");
    this.httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
  }

  /// <summary>
  ///     Asynchronously translates the given text from source language to target
  ///     language using the DeepL API or Free API based on the configuration.
  /// </summary>
  /// <param name="text"></param>
  /// <param name="sourceLanguage"></param>
  /// <param name="targetLanguage"></param>
  /// <returns></returns>
  async Task<string?> ITranslator.TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    if (this.isUsingAPIKey)
    {
      return await this.TranslateAsync(
          text,
          sourceLanguage,
          targetLanguage);
    }

    return await this.FreeTranslateAsync(
        text,
        sourceLanguage,
        targetLanguage);
  }

  /// <summary>
  ///     Synchronously translates the given text from source language to target
  ///     language using the DeepL API or Free API based on the configuration.
  /// </summary>
  /// <param name="text"></param>
  /// <param name="sourceLanguage"></param>
  /// <param name="targetLanguage"></param>
  /// <returns></returns>
  string? ITranslator.Translate(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    if (this.isUsingAPIKey)
    {
      return this.Translate(text, sourceLanguage, targetLanguage);
    }

    return this.FreeTranslateAsync(text, sourceLanguage, targetLanguage)
        .Result;
  }

  /// <summary>
  ///     Synchronously translates the given text from source language to target
  ///     language using the DeepL API.
  /// </summary>
  /// <param name="text"></param>
  /// <param name="sourceLanguage"></param>
  /// <param name="targetLanguage"></param>
  /// <returns></returns>
  private string? Translate(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    PluginRuntimeLog.Debug(this.pluginLog, "inside DeepLTranslator Translate method");

    try
    {
      var translation = this.deeplClient?.TranslateTextAsync(
          text,
          FormatSourceLanguageCode(sourceLanguage),
          FormatTargetLanguageCode(targetLanguage)).Result;
      PluginRuntimeLog.Debug(this.pluginLog, $"FinalTranslatedText: {translation?.Text}");
      return translation?.Text;
    }
    catch (Exception exception)
    {
      PluginRuntimeLog.Error(
          this.pluginLog,
          $"DeepLTranslator Translate: {exception.Message}");
      return text;
    }
  }

  /// <summary>
  ///     Asynchronously translates the given text from source language to target
  ///     language using the DeepL API.
  /// </summary>
  /// <param name="text"></param>
  /// <param name="sourceLanguage"></param>
  /// <param name="targetLanguage"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException">Invalid Operation Exception.</exception>
  private async Task<string?> TranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    PluginRuntimeLog.Debug(this.pluginLog, "inside DeepLTranslator TranslateAsync method");

    try
    {
      if (this.deeplClient == null)
      {
        throw new InvalidOperationException(
            "DeepL client is not initialized.");
      }

      var translation = await this.deeplClient.TranslateTextAsync(
          text,
          FormatSourceLanguageCode(sourceLanguage),
          FormatTargetLanguageCode(targetLanguage));
      PluginRuntimeLog.Debug(this.pluginLog, $"FinalTranslatedText: {translation.Text}");
      return translation.Text;
    }
    catch (Exception exception)
    {
      PluginRuntimeLog.Error(
          this.pluginLog,
          $"DeepLTranslator TranslateAsync: {exception.Message}");
      return text;
    }
  }

  /// <summary>
  ///     Counts the number of 'i' characters in the translated text.
  /// </summary>
  /// <param name="translateText"></param>
  /// <returns></returns>
  private int GetICount(string translateText)
  {
    return translateText.Count(c => c == 'i');
  }

  /// <summary>
  ///     Generates a timestamp based on the current time and the count of 'i'
  ///     characters in the text.
  /// </summary>
  /// <param name="iCount"></param>
  /// <returns></returns>
  private long GetTimestamp(int iCount)
  {
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    if (iCount == 0)
    {
      return timestamp;
    }

    iCount++;
    return timestamp - (timestamp % iCount) + iCount;
  }

  /// <summary>
  ///     Translates a string using the Free DeepL API.
  /// </summary>
  /// <param name="text">The text to translate.</param>
  /// <param name="sourceLanguage">The source language of the text.</param>
  /// <param name="targetLanguage">The target language for the translation.</param>
  /// <returns>
  ///     A <see cref="Task{TResult}" /> representing the result of the
  ///     asynchronous operation.
  /// </returns>
  private async Task<string> FreeTranslateAsync(
      string text,
      string sourceLanguage,
      string targetLanguage)
  {
    PluginRuntimeLog.Debug(
        this.pluginLog,
        "inside DeepLTranslator FreeTranslateAsync method");

    try
    {
      var timestamp = this.GetTimestamp(this.GetICount(text));
      var id = this.rndId?.Next(11111111, 99999999) ??
               throw new InvalidOperationException(
                   "Random number generator not initialized.");

      var requestBody = new
      {
        jsonrpc = "2.0",
        method = "LMT_handle_texts",
        @params = new
        {
          splitting = "newlines",
          lang = new
          {
            target_lang =
                      FormatFreeTargetLanguageCode(targetLanguage),
            source_lang_user_selected =
                      FormatSourceLanguageCode(sourceLanguage),
          },
          commonJobParams = new
          {
            wasSpoken = false,
            transcribe_as = string.Empty,
          },
          texts = new[]
              {
                        new
                        {
                            text,
                            request_alternatives = 3,
                        },
              },
          timestamp,
        },
        id,
      };

      var requestBodyText = JsonConvert.SerializeObject(requestBody);

      // Adding spaces to the JSON string based on the ID to adhere to DeepL's request formatting rules
      if ((id + 5) % 29 == 0 || (id + 3) % 13 == 0)
      {
        requestBodyText = requestBodyText.Replace(
            "\"method\":\"",
            "\"method\" : \"");
      }
      else
      {
        requestBodyText = requestBodyText.Replace(
            "\"method\":\"",
            "\"method\": \"");
      }

      if (this.httpClient == null)
      {
        throw new InvalidOperationException(
            "DeepL HttpClient is not initialized.");
      }

      var response = await this.httpClient.PostAsync(
          FreeEndpoint,
          new StringContent(
              requestBodyText,
              Encoding.UTF8,
              "application/json"));

      if (response.IsSuccessStatusCode)
      {
        var jsonString = await response.Content.ReadAsStringAsync();
        var deepLResponse =
            JsonConvert.DeserializeObject<DeepLResponse>(jsonString);
        if (deepLResponse?.Result?.Texts != null &&
            deepLResponse.Result.Texts.Length > 0)
        {
          var finalTranslatedText =
              deepLResponse.Result.Texts[0].Text;
          PluginRuntimeLog.Debug(
              this.pluginLog,
              $"FinalTranslatedText: {finalTranslatedText}");
          return finalTranslatedText ?? text;
        }

        PluginRuntimeLog.Warning(
            this.pluginLog,
            "DeepLTranslator FreeTranslateAsync: No translation result found.");
        return text;
      }

      PluginRuntimeLog.Warning(
          this.pluginLog,
          $"DeepLTranslator FreeTranslateAsync error: {response.StatusCode}");
      return text;
    }
    catch (Exception exception)
    {
      PluginRuntimeLog.Error(
          this.pluginLog,
          $"DeepLTranslator FreeTranslateAsync: {exception.Message}");
      return text;
    }
  }

  /// <summary>
  ///     Formats the source language for the DeepL API.
  /// </summary>
  /// <param name="source">Source language.</param>
  /// <returns>Returns the formatted source language.</returns>
  internal static string FormatSourceLanguageCode(string source)
  {
    switch (source)
    {
      case "Japanese":
        return "JA";
      case "English":
        return "EN";
      case "German":
        return "DE";
      case "French":
        return "FR";
      default:
        return "EN";
    }
  }

  /// <summary>
  ///     Formats the target language for the DeepL API.
  /// </summary>
  /// <param name="source">Target language.</param>
  /// <returns>Returns the formatted target language.</returns>
  internal static string FormatTargetLanguageCode(string source)
  {
    switch (source)
    {
      case "en":
        return "EN-US";
      case "no":
        return "NB";
      case "pt":
        return "PT-BR";
      case "zh-CN":
        return "ZH";
      case "pt-PT":
        return "PT-PT";
      case "it":
        return "IT";
      default:
        return source.ToUpper();
    }
  }

  /// <summary>
  ///     Formats the target language for the Free DeepL API.
  /// </summary>
  /// <param name="source">Target language.</param>
  /// <returns>Returns the formatted target language.</returns>
  internal static string FormatFreeTargetLanguageCode(string source)
  {
    switch (source)
    {
      case "en":
        return "EN-US";
      case "no":
        return "NB";
      case "pt":
        return "PT-BR";
      case "zh-CN":
        return "ZH";
      case "pt-PT":
        return "PT-PT";
      case "it":
        return "IT";
      default:
        return source.ToUpper();
    }
  }
}

/// <summary>
///     Represents the response from the DeepL translation service.
/// </summary>
public class DeepLResponse
{
  public string? Id { get; set; }

  public string? Jsonrpc { get; set; }

  public DeepLResult? Result { get; set; }
}

/// <summary>
///     Represents the result of a DeepL translation operation.
/// </summary>
public class DeepLResult
{
  public DeepLTextResult[]? Texts { get; set; }

  public string? Lang { get; set; }
}

/// <summary>
///     Represents a text result from the DeepL translation service.
/// </summary>
public class DeepLTextResult
{
  public string? Text { get; set; }
}
