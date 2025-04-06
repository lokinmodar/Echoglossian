using System;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;
using GTranslate;
using GTranslate.Translators;

namespace Echoglossian.Translators
{
	public class GTranslateTranslator : ITranslator
	{
		private readonly IPluginLog pluginLog;
		private readonly Config config;
		private readonly AggregateTranslator translator;

		public GTranslateTranslator(IPluginLog pluginLog, Config config)
		{
			this.pluginLog = pluginLog;
			this.config = config;
			translator = new AggregateTranslator(); // Switch to GoogleTranslator() if you want to force only Google
		}

		public string Translate(string text, string sourceLanguage, string targetLanguage)
		{
			pluginLog.Debug("GTranslate sync translate requested.");
			return TranslateAsync(text, sourceLanguage, targetLanguage).Result;
		}

		public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			string fixedText = Echoglossian.FixText(text);
			pluginLog.Debug($"GTranslate input: {fixedText}");

			try
			{
				var result = await translator.TranslateAsync(fixedText, sourceLanguage, targetLanguage);
				string cleaned = Echoglossian.FixText(result.Translation);
				pluginLog.Debug($"GTranslate result: {cleaned}");
				return cleaned;
			}
			catch (Exception ex)
			{
				pluginLog.Warning($"GTranslate error: {ex}");
				return string.Empty;
			}
		}

	}
}
