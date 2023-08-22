using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echoglossian
{
  public interface ITranslator
  {
    Task<string> FreeTranslate(string text, string sourceLanguage, string targetLanguage);

    Task<string> FreeTranslateAsync(string text, string sourceLanguage, string targetLanguage);

    Task<string> Translate(string text, string sourceLanguage, string targetLanguage);

    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
  }
}
