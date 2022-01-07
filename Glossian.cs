// <copyright file="Glossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

using Dalamud.Logging;
using Dalamud.Utility;
using Echoglossian.Properties;
using Newtonsoft.Json.Linq;
using NTextCat;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private const string GTranslateUrl =
      "https://clients5.google.com/translate_a/t?client=dict-chrome-ex";

    private const string UaString =
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36";

    private static readonly RankedLanguageIdentifierFactory Factory = new();

    private static RankedLanguageIdentifier identifier;

    /// <summary>
    /// Detects which language the source text is in.
    /// </summary>
    /// <param name="message">text to have the source language identified.</param>
    /// <returns>Returns the detected language code.</returns>
    public static string LangIdentify(string message)
    {
      // Sanitizer sanitizer = new(ClientLanguage);
      string sanitizedString = sanitizer.Sanitize(message);

#if DEBUG
      PluginLog.LogInformation($"Message in Lang Method: {sanitizedString}");
#endif
      Tuple<NTextCat.LanguageInfo, double> mostCertainLanguage = identifier.Identify(sanitizedString).FirstOrDefault();
#if DEBUG
      PluginLog.LogInformation($"Most Certain language: {mostCertainLanguage?.Item1.Iso639_2T}");
#endif
      return mostCertainLanguage != null
        ? mostCertainLanguage.Item1.Iso639_2T
        : Resources.LangIdentError;
    }

    /// <summary>
    /// Translates the sentences passed to it. Uses Google Translate Free endpoint.
    /// </summary>
    /// <param name="text">Text to be translated.</param>
    /// <returns>Returns the translated text passed in the call parameter.</returns>
    /// <exception cref="Exception">Returns exception in case something goes wrong in the translation steps.</exception>
    public static string Translate(string text)
    {
      string startingEllipsis = string.Empty;
      if (text.IsNullOrEmpty())
      {
        return string.Empty;
      }
#if DEBUG
      PluginLog.LogError("inside translate method");
#endif
      try
      {
        string lang = langDict[languageInt].Code;
        string sanitizedString = sanitizer.Sanitize(text);
        if (sanitizedString.IsNullOrEmpty())
        {
          return string.Empty;
        }

        if (sanitizedString == "...")
        {
          return sanitizedString;
        }

        string parsedText;
        if (sanitizedString.StartsWith("..."))
        {
          startingEllipsis = "...";
          parsedText = sanitizedString.Substring(3);
        }
        else
        {
          parsedText = sanitizedString;
        }

        /*        parsedText = parsedText.Replace("\u2500", "\u002D");
                parsedText = parsedText.Replace("\u0021", "\u0021\u0020");
                parsedText = parsedText.Replace("\u003F", "\u003F\u0020");
                parsedText = parsedText.Replace("\u201C", "\u0022");
                parsedText = parsedText.Replace("\u201D", "\u0022");*/
        parsedText = parsedText.Replace("\u200B", string.Empty);

        string detectedLanguage = LangIdentify(parsedText);
        if (detectedLanguage is "oc" or "an" or "bpy" or "br" or "roa_rup" or "vo" or "war" or "zh_classical")
        {
          detectedLanguage = "en";
        }

#if DEBUG
        PluginLog.LogInformation($"Chosen Translation Engine: {chosenTransEngine}");
        PluginLog.LogInformation($"Chosen Translation LanguageInfo: {lang}");
#endif
        string url = $"{GTranslateUrl}&sl={detectedLanguage}&tl={lang}&q={Uri.EscapeDataString(parsedText)}";
#if DEBUG
        PluginLog.LogInformation($"URL: {url}");
#endif
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.UserAgent = UaString;
        WebResponse requestResult = request.GetResponse();

        StreamReader reader = new StreamReader(requestResult.GetResponseStream() ?? throw new Exception());
        string read = reader.ReadToEnd();

        JObject parsed = JObject.Parse(read);

        List<string> dialogueSentenceList = parsed.SelectTokens("sentences[*].trans").Select(i => (string)i).ToList();

        string finalDialogueText =
          dialogueSentenceList.Aggregate(string.Empty, (current, dialogueSentence) => current + dialogueSentence);

        finalDialogueText = finalDialogueText.Replace("\u200B", string.Empty);

        /*finalDialogueText = finalDialogueText.Replace("\u201C", "\u0022");
finalDialogueText = finalDialogueText.Replace("\u201D", "\u0022");
finalDialogueText = startingEllipsis + finalDialogueText;
finalDialogueText = finalDialogueText.Replace("...", ". . .");*/

        finalDialogueText = !startingEllipsis.IsNullOrEmpty()
         ? startingEllipsis + finalDialogueText
         : finalDialogueText;

        JValue src = (JValue)parsed["src"];
        Debug.Assert(finalDialogueText != null, nameof(finalDialogueText) + " != null");
#if DEBUG
        PluginLog.LogInformation($"FinalTranslatedText: {finalDialogueText}");
#endif
        if (src != null && (src.ToString(CultureInfo.InvariantCulture) == lang || finalDialogueText == text))
        {
          return text;
        }

        return finalDialogueText;
      }
      catch (Exception e)
      {
        PluginLog.Error(e.ToString());
        throw;
      }
    }
  }
}