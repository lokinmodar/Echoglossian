using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Echoglossian;

public class TranslationHandler
{
  private readonly ConcurrentDictionary<string, string> translations = new();

  public async Task HandleTextEntered(string text, DbContext dbContext)
  {
    // Check if the translated text already exists in the database
    var translatedTextFromDb = await dbContext.Set<Translation>()
      .Where(t => t.OriginalText == text)
      .Select(t => t.TranslatedText)
      .FirstOrDefaultAsync();

    if (translatedTextFromDb != null)
    {
      // The translated text already exists in the database, show it in the UI
      this.ShowTranslatedText(text, translatedTextFromDb);
    }
    else
    {
      // Add the original text to the ConcurrentDictionary with the value set to "translating..."
      this.translations.TryAdd(text, "translating...");

      // Update the UI to show "translating..."
      this.ShowTranslating(text);

      // Call the TranslateAsync method on a separate thread
      var translatedText = await Task.Run(() => this.TranslateAsync(text));

      // Update the value of the key in the ConcurrentDictionary with the translated text
      this.translations.TryUpdate(text, translatedText, "translating...");

      // Store the translated text in the database
      var translation = new Translation
      { OriginalText = text, TranslatedText = translatedText };
      dbContext.Set<Translation>().Add(translation);
      await dbContext.SaveChangesAsync();

      // Update the UI to show the translated text
      this.ShowTranslatedText(text, translatedText);
    }
  }

  private async Task<string> TranslateAsync(string text)
  {
    // Call the translation API and get the translated text
    //string translatedText = await YourTranslationAPICall(text);

    // Return the translated text
    return "a"; //translatedText;
  }

  private void ShowTranslating(string text)
  {
    // Update the UI to show "translating..."
  }

  private void ShowTranslatedText(string originalText, string translatedText)
  {
    // Update the UI to show the translated text
  }
}

public class Translation
{
  public int Id { get; set; }
  public string OriginalText { get; set; }
  public string TranslatedText { get; set; }
}