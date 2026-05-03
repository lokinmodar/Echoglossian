// <copyright file="Glossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

namespace Echoglossian
{
  public partial class Echoglossian
  {
    /*    private static readonly RankedLanguageIdentifierFactory Factory = new();

        private static RankedLanguageIdentifier identifier;

        /// <summary>
        /// Detects which language the source text is in.
        /// </summary>
        /// <param name="message">text to have the source language identified.</param>
        /// <returns>Returns the detected language code.</returns>
        private static string LangIdentify(string message)
        {
          // Sanitizer sanitizer = new(ClientLanguage);
          string sanitizedString = Sanitizer.Sanitize(message);

    #if DEBUG
          PluginRuntimeLog.Debug($"Message in Lang Method: {sanitizedString}");
    #endif
          Tuple<NTextCat.LanguageInfo, double> mostCertainLanguage = identifier.Identify(sanitizedString).FirstOrDefault();
    #if DEBUG
          PluginRuntimeLog.Debug($"Most Certain language: {mostCertainLanguage?.Item1.Iso639_2T}");
    #endif
          return mostCertainLanguage != null
            ? mostCertainLanguage.Item1.Iso639_2T
            : Resources.LangIdentError;
        }*/

    /// <summary>
    /// Translates the sentences passed to it using the selected engine.
    /// </summary>
    /// <param name="text">Text to be translated.</param>
    /// <returns>Returns the translated text passed in the call parameter.</returns>
    /// <exception cref="Exception">Returns exception in case something goes wrong in the translation steps.</exception>
    private string Translate(string text)
    {
      return TranslationService.Translate(text, ClientStateInterface.ClientLanguage.Humanize(), LangDict[LanguageInt].Code);
    }

    /// <summary>
    /// Translates the sentences passed to it using the selected engine.
    /// </summary>
    /// <param name="text">Text to be translated.</param>
    /// <returns>Returns the translated text passed in the call parameter.</returns>
    /// <exception cref="Exception">Returns exception in case something goes wrong in the translation steps.</exception>
    private Task<string> TranslateAsync(string text)
    {
      return TranslationService.TranslateAsync(text, ClientStateInterface.ClientLanguage.Humanize(), LangDict[LanguageInt].Code);
    }
  }
}


