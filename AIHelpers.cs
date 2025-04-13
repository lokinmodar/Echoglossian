using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DeepL.Model;

using static System.Net.Mime.MediaTypeNames;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    /// <summary>
    /// The default prompt for the translation engine.
    /// </summary>
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

    /// <summary>
    /// The required placeholders for the prompt.
    /// </summary>
    private static readonly string[] RequiredPlaceholders =
        {
          "{text}",
          "{sourceLanguage}",
          "{targetLanguage}",
        };

    /// <summary>
    /// Checks if the prompt is valid by ensuring it contains all required placeholders.
    /// </summary>
    /// <param name="prompt">The API engine text propmt.</param>
    /// <returns>Returns 'true' if the promnpt is valid.</returns>
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
    /// <summary>
    /// Applies the prompt variables to the template.
    /// </summary>
    /// <param name="template"></param>
    /// <param name="text"></param>
    /// <param name="sourceLang"></param>
    /// <param name="targetLang"></param>
    /// <returns></returns>
    public static string ApplyPromptVariables(string template, string text, string sourceLang, string targetLang)
    {
      return template
          .Replace("{text}", text)
          .Replace("{sourceLanguage}", sourceLang)
          .Replace("{targetLanguage}", targetLang);
    }
    /// <summary>
    /// Gets the prompt for the specified type.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string? GetPrompt(Config config, PromptType type) =>
        type switch
        {
          PromptType.DeepSeek => config.DeepSeekPrompt,
          PromptType.Gemini => config.GeminiPrompt,
          PromptType.OpenRouter => config.OpenRouterPrompt,
          PromptType.Microsoft => config.MicrosoftTranslatorPrompt,
          PromptType.Amazon => config.AmazonPrompt,
          PromptType.ChatGPT => config.ChatGptPrompt,
          PromptType.YandexCloud => config.YandexCloudPrompt,
          _ => null,
        };
    /// <summary>
    /// Gets the prompt type for the specified engine index.
    /// </summary>
    /// <param name="engineIndex"></param>
    /// <returns></returns>
    public static PromptType? GetPromptTypeForEngine(int engineIndex)
    {
      return engineIndex switch
      {
        2 => PromptType.ChatGPT,
        3 => PromptType.DeepSeek,
        4 => PromptType.Gemini,
        5 => PromptType.OpenRouter,
        6 => PromptType.Microsoft,
        7 => PromptType.Amazon,
        8 => PromptType.YandexCloud,
        _ => null
      };
    }
    /// <summary>
    /// Sets the prompt for the specified type.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="type"></param>
    /// <param name="prompt"></param>
    public static void SetPrompt(Config config, PromptType type, string? prompt)
    {
      switch (type)
      {
        case PromptType.DeepSeek: config.DeepSeekPrompt = prompt; break;
        case PromptType.Gemini: config.GeminiPrompt = prompt; break;
        case PromptType.OpenRouter: config.OpenRouterPrompt = prompt; break;
        case PromptType.Microsoft: config.MicrosoftTranslatorPrompt = prompt; break;
        case PromptType.Amazon: config.AmazonPrompt = prompt; break;
        case PromptType.ChatGPT: config.ChatGptPrompt = prompt; break;
        case PromptType.YandexCloud: config.YandexCloudPrompt = prompt; break;
      }
    }
  }
}
