using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echoglossian
{
  public class GoogleTranslator : ITranslator
  {
    Task<string> ITranslator.FreeTranslate(string text, string sourceLanguage, string targetLanguage)
    {
      throw new NotImplementedException();
    }

    Task<string> ITranslator.FreeTranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      throw new NotImplementedException();
    }

    Task<string> ITranslator.Translate(string text, string sourceLanguage, string targetLanguage)
    {
      throw new NotImplementedException();
    }

    Task<string> ITranslator.TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      throw new NotImplementedException();
    }
  }
}
