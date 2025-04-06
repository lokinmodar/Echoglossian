// <copyright file="YandexTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Echoglossian.Translators
{
	public partial class YandexTranslator : ITranslator
	{
		private readonly IPluginLog pluginLog;
		private readonly Config config;
		private static readonly HttpClient HttpClient = new();
		private readonly int characterQuotaLimit = 1000000;

		public YandexTranslator(IPluginLog pluginLog, Config config)
		{
			this.pluginLog = pluginLog;
			this.config = config;
		}

		public string Translate(string text, string sourceLanguage, string targetLanguage)
		{
			this.pluginLog.Debug("Inside YandexTranslator Translate (sync)");
			return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result;
		}

		/// <inheritdoc/>
		public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
		{
			this.pluginLog.Debug("Inside YandexTranslator TranslateAsync");

			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			string fixedText = Echoglossian.FixText(text);
			this.pluginLog.Debug($"Fixed Input Text: {fixedText}");

			try
			{
				string result = this.config.UsePaidYandexApi || this.config.UseYandexV2ForFreeApi
						? await this.TranslateWithV2Api(fixedText, sourceLanguage, targetLanguage)
						: await this.TranslateWithLegacyFreeApi(fixedText, sourceLanguage, targetLanguage);

				string cleanedResult = Echoglossian.FixText(result);
				this.pluginLog.Debug($"Final Translated Text: {cleanedResult}");

				return cleanedResult;
			}
			catch (Exception ex)
			{
				this.pluginLog.Warning($"Yandex translation failed: {ex}");
				return string.Empty;
			}
		}

		private async Task<string> TranslateWithLegacyFreeApi(string text, string sourceLang, string targetLang)
		{
			string from = Echoglossian.NormalizeLanguageCode(sourceLang);
			string to = Echoglossian.NormalizeLanguageCode(targetLang);
			string apiKey = this.config.YandexFreeApiKey;

			string requestUrl = $"https://translate.yandex.net/api/v1.5/tr.json/translate?key={apiKey}&text={Uri.EscapeDataString(text)}&lang={from}-{to}";
			this.pluginLog.Debug($"Free API Request URL: {requestUrl}");

			var response = await HttpClient.GetAsync(requestUrl);
			var responseContent = await response.Content.ReadAsStringAsync();
			this.pluginLog.Debug($"Response: {responseContent}");

			var parsed = JObject.Parse(responseContent);
			return parsed["text"]?[0]?.ToString() ?? string.Empty;
		}

		private async Task<string> TranslateWithV2Api(string text, string sourceLang, string targetLang)
		{
			string from = Echoglossian.NormalizeLanguageCode(sourceLang);
			string to = Echoglossian.NormalizeLanguageCode(targetLang);
			string folderId = this.config.YandexFolderId;
			string apiKey = this.config.UsePaidYandexApi ? this.config.YandexPaidApiKey : this.config.YandexFreeApiKey;

			var requestBody = new
			{
				folderId,
				texts = new[] { text },
				sourceLanguageCode = from,
				targetLanguageCode = to,
			};

			var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

			HttpClient.DefaultRequestHeaders.Clear();
			HttpClient.DefaultRequestHeaders.Add("Authorization", $"Api-Key {apiKey}");

			this.pluginLog.Debug($"V2 API request body: {JsonConvert.SerializeObject(requestBody)}");

			var response = await HttpClient.PostAsync("https://translate.api.cloud.yandex.net/translate/v2/translate", content);
			var responseBody = await response.Content.ReadAsStringAsync();
			this.pluginLog.Debug($"V2 API response: {responseBody}");

			if (!response.IsSuccessStatusCode)
			{
				this.HandleApiError(responseBody);
				return string.Empty;
			}

			var parsed = JObject.Parse(responseBody);
			var translation = parsed["translations"]?[0];
			string translatedText = translation?["text"]?.ToString() ?? string.Empty;
			string? detectedLang = translation?["detectedLanguageCode"]?.ToString();

			if (!string.IsNullOrEmpty(detectedLang))
			{
				this.pluginLog.Debug($"Detected Language: {detectedLang}");
			}

			this.TrackApiUsage(text.Length);
			return translatedText;
		}

		private void HandleApiError(string responseBody)
		{
			try
			{
				var error = JObject.Parse(responseBody);
				string message = error["message"]?.ToString() ?? "Unknown error";
				string code = error["code"]?.ToString() ?? "N/A";
				this.pluginLog.Warning($"Yandex API Error [{code}]: {message}");
			}
			catch
			{
				this.pluginLog.Warning($"Unexpected error response: {responseBody}");
			}
		}

		private void TrackApiUsage(int charCount)
		{
			try
			{
				this.config.YandexCharactersTranslated += charCount;

				Echoglossian.PluginInterface.SavePluginConfig(this.config);

				this.pluginLog.Debug($"Characters translated today (stored in config): {this.config.YandexCharactersTranslated}");

				if (this.config.YandexCharactersTranslated > this.characterQuotaLimit)
				{
					this.pluginLog.Warning("Yandex API character quota likely exceeded.");
				}
			}
			catch (Exception ex)
			{
				this.pluginLog.Error($"Failed to track API usage: {ex}");
			}
		}

	}
}
