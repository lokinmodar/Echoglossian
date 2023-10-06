using System;
using System.Threading.Tasks;

using static Echoglossian.Echoglossian;

namespace Echoglossian
{
  public class TranslationService
  {
    private readonly ITranslator translator;

    public TranslationService(Config config)
    {
      TransEngines chosenEngine = (TransEngines)config.ChosenTransEngine;

      switch (chosenEngine)
      {
        case TransEngines.Google:
          this.translator = new GoogleTranslator();
          break;
        case TransEngines.Deepl:
          this.translator = new DeepLTranslator();
          break;
        case TransEngines.Bing:
          break;
        case TransEngines.Yandex:
          break;
        case TransEngines.GTranslate:
          break;
        case TransEngines.Amazon:
          break;
        case TransEngines.Azure:
          break;
        case TransEngines.ChatGPT:
          break;
        case TransEngines.GoogleCloud:
          break;
        case TransEngines.All:
          break;
        // ... add cases for other translation engines
        default:
          throw new NotSupportedException($"Translation engine {chosenEngine} is not supported.");
      }
    }

    public Task<string> Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.translator.Translate(text, sourceLanguage, targetLanguage);
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      return await this.translator.TranslateAsync(text, sourceLanguage, targetLanguage);
    }
  }
}
