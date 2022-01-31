using System.Threading.Tasks;
using Dalamud.Logging;
using DeepL;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public static async Task DeeplTranslateAsync(string[] arguments)
    {
      using DeepLClient client = new("<authentication key>");
      try
      {
        var translation = await client.TranslateAsync(
          "This is a test sentence.",
          Language.German);
        PluginLog.LogWarning(translation.DetectedSourceLanguage);
        PluginLog.LogWarning(translation.Text);
      }
      catch (DeepLException exception)
      {
        PluginLog.LogWarning($"An error occurred: {exception.Message}");
      }
    }
  }
}