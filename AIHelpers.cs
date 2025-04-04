using DeepL.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private const string defaultPrompt = @"As an expert translator and cultural localization specialist with deep knowledge of video game localization, your task is to translate dialogues from the game Final Fantasy XIV from {sourceLanguage} to {targetLanguage}. This is not just a translation, but a full localization effort tailored for the Final Fantasy XIV universe. Please adhere to the following guidelines:

1. Preserve the original tone, humor, personality, and emotional nuances of the dialogue, considering the unique style and atmosphere of Final Fantasy XIV.
2. Adapt idioms, cultural references, and wordplay to resonate naturally with native {targetLanguage} speakers while maintaining the fantasy RPG context.
3. Maintain consistency in character voices, terminology, and naming conventions specific to Final Fantasy XIV throughout the translation.
4. Avoid literal translations that may lose the original intent or impact, especially for game-specific terms or lore elements.
5. Ensure the translation flows naturally and reads as if it were originally written in {targetLanguage}, while staying true to the game's narrative style.
6. Consider the context and subtext of the dialogue, including any references to the game's lore, world, or ongoing storylines.
7. If a word, phrase, or name has been translated in a specific way, maintain that translation consistently unless the context demands otherwise, respecting established localization choices for Final Fantasy XIV.
8. Pay attention to formal/informal speech patterns and adjust accordingly for the target language and cultural norms, considering the speaker's role and status within the game world.
9. Be mindful of character limits or text box constraints that may be present in the game, adapting the translation to fit if necessary.
10. Preserve any game-specific jargon, spell names, or technical terms according to the official localization guidelines for Final Fantasy XIV in the target language.

Text to translate: ""{text}""

Please provide only the translated text in your response, without any explanations, additional comments, or quotation marks. Your goal is to create a localized version that captures the essence of the original Final Fantasy XIV dialogue while feeling authentic to {targetLanguage} speakers and seamlessly fitting into the game world.;";

    private static readonly string[] RequiredPlaceholders =
        {
          "{text}",
          "{sourceLanguage}",
          "{targetLanguage}",
        };

    public static bool IsPromptValid(string prompt)
    {
      foreach (var placeholder in RequiredPlaceholders)
      {
        if (!prompt.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
        {
          return false;
        }
      }

      return true;
    }

    public static string ApplyPromptVariables(string template, string text, string sourceLanguage, string targetLanguage)
    {
      return template
          .Replace("{text}", text)
          .Replace("{sourceLanguage}", sourceLanguage)
          .Replace("{targetLanguage}", targetLanguage);
    }
  }
}
