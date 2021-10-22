using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public class Language
    {
      private string Code;
      private string LanguageName;
      private string FontName;
      private string ExclusiveCharsToAdd;
      private int[] SupportedEngines;

      public Language(string code, string languageName, string fontName, string exclusiveCharsToAdd,
        int[] supportedEngines)
      {
        this.Code = code ?? throw new ArgumentNullException(nameof(code));
        this.LanguageName = languageName ?? throw new ArgumentNullException(nameof(languageName));
        this.FontName = fontName ?? throw new ArgumentNullException(nameof(fontName));
        this.ExclusiveCharsToAdd = exclusiveCharsToAdd ?? throw new ArgumentNullException(nameof(exclusiveCharsToAdd));
        this.SupportedEngines = supportedEngines ?? throw new ArgumentNullException(nameof(supportedEngines));
      }
    }

    public Dictionary<int, Language> LanguagesDictionary = new();
  }
}
