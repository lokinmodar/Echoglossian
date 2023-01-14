using System.Threading.Tasks;

using Dalamud.Logging;
using DeepL;
using DeepL.Model;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public static async Task DeeplTranslateAsync(string[] arguments)
    {
      using Translator client = new("<authentication key>");
      try
      {
        var translation = client.TranslateTextAsync(
          arguments[0],
          "EN",
          "JP"
          ).Result;
        PluginLog.LogWarning(translation.DetectedSourceLanguageCode);
        PluginLog.LogWarning(translation.Text);
      }
      catch (DeepLException exception)
      {
        PluginLog.LogWarning($"An error occurred: {exception.Message}");
      }
    }
  }
}
