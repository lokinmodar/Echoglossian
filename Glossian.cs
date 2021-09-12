// <copyright file="Glossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

using Dalamud.Logging;
using Echoglossian.Properties;
using Newtonsoft.Json.Linq;
using NTextCat;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private const string GTranslateUrl =
      "https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=";

    private const string UaString =
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36";

    private static readonly RankedLanguageIdentifierFactory Factory = new();

    private static readonly RankedLanguageIdentifier Identifier =
      Factory.Load(Path.Combine(AssemblyDirectory, "Wiki82.profile.xml"));

    private static string AssemblyDirectory
    {
      get
      {
        var codeBase = typeof(Echoglossian).Assembly.Location;
        Debug.Assert(codeBase != null, nameof(codeBase) + " != null");
        var uri = new UriBuilder(codeBase);
        var path = Uri.UnescapeDataString(uri.Path);
        return Path.GetDirectoryName(path);
      }
    }

    public static string Lang(string message)
    {
      // PluginLog.LogInformation($"message in Lang Method {message}");
      var mostCertainLanguage = Identifier.Identify(message).FirstOrDefault();
      // PluginLog.LogInformation($"most Certain language: {mostCertainLanguage}");
      return mostCertainLanguage != null
        ? mostCertainLanguage.Item1.Iso639_2T
        : Resources.LangIdentError;
    }

    public static string Translate(string text)
    {
      try
      {
        var lang = Codes[languageInt];
        // PluginLog.LogInformation($"LANG: {lang}");

        var url = $"{GTranslateUrl}{lang}&q={text}";

        // PluginLog.LogInformation($"URL: {url}");

        var request = (HttpWebRequest)WebRequest.Create(url);
        request.UserAgent = UaString;
        var requestResult = request.GetResponse();

        var reader = new StreamReader(requestResult.GetResponseStream() ?? throw new Exception());
        var read = reader.ReadToEnd();

        var parsed = JObject.Parse(read);

        var dialogueSentenceList = parsed.SelectTokens("sentences[*].trans").Select(i => (string)i).ToList();

        var finalDialogueText =
          dialogueSentenceList.Aggregate(string.Empty, (current, dialogueSentence) => current + dialogueSentence);

        var src = (JValue)parsed["src"];
        Debug.Assert(finalDialogueText != null, nameof(finalDialogueText) + " != null");
/*        PluginLog.LogInformation($"FinalDialogueText: {finalDialogueText}");
        PluginLog.LogInformation($"SRC: {src}");
        PluginLog.LogInformation($"SRC Cultureinfo: {src?.ToString(CultureInfo.InvariantCulture)}");
        PluginLog.LogInformation($"LANG: {lang}");*/

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