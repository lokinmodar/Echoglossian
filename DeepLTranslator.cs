using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text;
using System;
using System.Threading.Tasks;

using Dalamud.Logging;
using DeepL;
using Newtonsoft.Json;

namespace Echoglossian
{
  public partial class DeepLTranslator : ITranslator
  {
    private Config configuration = Echoglossian.PluginInterface.GetPluginConfig() as Config;

    public Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      using Translator client = new(this.configuration.DeeplTranslatorApiKey);
      try
      {
        var translation = client.TranslateTextAsync(
          text,
          sourceLanguage,
          targetLanguage)
          .Result;
        PluginLog.LogWarning(translation.DetectedSourceLanguageCode);
        PluginLog.LogWarning(translation.Text);
        return Task.FromResult(translation.Text);
      }
      catch (DeepLException exception)
      {
        PluginLog.LogWarning($"An error occurred: {exception.Message}");
        return Task.FromResult(text);
      }
    }

    Task<string> ITranslator.FreeTranslate(string text, string sourceLanguage, string targetLanguage)
    {
      throw new NotImplementedException();
    }

    Task<string> ITranslator.Translate(string text, string sourceLanguage, string targetLanguage)
    {
      throw new NotImplementedException();
    }

    private const string Endpoint = "https://www2.deepl.com/jsonrpc?method=LMT_handle_jobs";

    /// <summary>
    /// Translates a string using the Free DeepL API.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="sourceLanguage"></param>
    /// <param name="targetLanguage"></param>
    /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
    public async Task<string> FreeTranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      var sentencesList = this.ParseSentences(text);
      long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Current timestamp

      using (HttpClient client = new())
      {
        var requestBody = new
        {
          jsonrpc = "2.0",
          method = "LMT_handle_jobs",
          @params = new
          {
            jobs = new[]
                {
                        new
                        {
                            kind = "default",
                            sentences = sentencesList,
                            preferred_num_beams = 1,
                            quality = "normal",
                        },
                    },
            lang = new
            {
              target_lang = targetLanguage,
              source_lang_user_selected = sourceLanguage,
            },
            priority = -1,
            commonJobParams = new
            {
              regionalVariant = "pt-BR",
              mode = "translate",
              browserType = 1,
            },
            timestamp,
          },
          id = 7990014, // This can also be dynamic if needed
        };

        var response = await client.PostAsync(Endpoint, new StringContent(
            JsonConvert.SerializeObject(requestBody),
            Encoding.UTF8,
            "application/json"));

        if (response.IsSuccessStatusCode)
        {
          return await response.Content.ReadAsStringAsync();
        }
        else
        {
          PluginLog.LogWarning($"An error occurred: {response.Content}");
          return text;
        }
      }
    }

    private List<object> ParseSentences(string input)
    {
      var sentences = MyRegex().Split(input);
      var result = new List<object>();
      int idCounter = 1;

      foreach (var sentence in sentences)
      {
        result.Add(new
        {
          text = sentence.Trim(),
          id = idCounter++,
          prefix = string.Empty,
        });
      }

      return result;
    }

    [GeneratedRegex("(?<=[.!?])\\s+")]
    private static partial Regex MyRegex();
  }
}
